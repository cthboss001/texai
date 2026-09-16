using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace texAi;

/// <param name="SizeBytes">On-disk size as Ollama reports it.</param>
internal sealed record InstalledModel(string Name, long SizeBytes, string ParameterSize, string Quantization)
{
    public double SizeGb => SizeBytes / 1024d / 1024d / 1024d;

    /// <summary>
    /// Flagged against a 6GB card, using measurements rather than an estimate.
    /// On an RTX 2060 6GB at a 4096 context, qwen2.5:7b is 4.4GB on disk but
    /// loads at 5.1GB and runs 18% on the CPU; aya-expanse:8b is 4.7GB on disk,
    /// loads at 6.6GB and runs 36% on the CPU. The KV cache is what does it, and
    /// it is not counted in the download size. Offloading starts around 4.5GB,
    /// so 5.0 is where it becomes bad enough to warn about.
    /// </summary>
    public bool TightFit => SizeGb > 5.0;

    /// <summary>A property, not a method: WPF bindings cannot call methods.</summary>
    public string Description => $"{SizeGb:0.0} GB, {ParameterSize}, {Quantization}";
}

/// <param name="Total">Zero until Ollama knows the size of the layer it is fetching.</param>
internal sealed record PullProgress(string Status, long Completed, long Total)
{
    public double? Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : null;
}

internal sealed record CompareResult(string Model, string? Output, string? Failure, TimeSpan Elapsed);

/// <summary>
/// Everything about models that is not a rewrite: what is installed, pulling a
/// new one, deleting one, and running the same prompt through several to compare.
/// </summary>
internal static class ModelService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// Separate client with no timeout. Pulls are multi-gigabyte downloads that
    /// routinely run for ten minutes; the shared 60-second client would abort one
    /// partway through and report it as a timeout.
    /// </summary>
    private static readonly HttpClient Downloads = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// <summary>Null means Ollama could not be reached at all, which is different from an empty list.</summary>
    public static async Task<IReadOnlyList<InstalledModel>?> ListAsync()
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync($"{Config.OllamaEndpoint}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("models", out JsonElement models))
            {
                return [];
            }

            var result = new List<InstalledModel>();

            foreach (JsonElement model in models.EnumerateArray())
            {
                string name = model.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "?" : "?";
                long size = model.TryGetProperty("size", out JsonElement s) ? s.GetInt64() : 0;

                string parameters = "?";
                string quantization = "?";

                if (model.TryGetProperty("details", out JsonElement details))
                {
                    parameters = details.TryGetProperty("parameter_size", out JsonElement p) ? p.GetString() ?? "?" : "?";
                    quantization = details.TryGetProperty("quantization_level", out JsonElement q) ? q.GetString() ?? "?" : "?";
                }

                result.Add(new InstalledModel(name, size, parameters, quantization));
            }

            return result.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Streams /api/pull, which answers with one JSON object per line rather than
    /// a single document, so the response has to be read as it arrives.
    /// </summary>
    public static async Task<string?> PullAsync(string name, IProgress<PullProgress> progress, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.OllamaEndpoint}/api/pull")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { model = name, stream = true }),
                    Encoding.UTF8,
                    "application/json"),
            };

            using HttpResponseMessage response = await Downloads.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                return $"Ollama returned {(int)response.StatusCode}: {body}";
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? lastError = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("error", out JsonElement error))
                    {
                        lastError = error.GetString();
                        continue;
                    }

                    progress.Report(new PullProgress(
                        root.TryGetProperty("status", out JsonElement status) ? status.GetString() ?? "working" : "working",
                        root.TryGetProperty("completed", out JsonElement completed) ? completed.GetInt64() : 0,
                        root.TryGetProperty("total", out JsonElement total) ? total.GetInt64() : 0));
                }
                catch (JsonException)
                {
                    // One malformed line in a stream of thousands is not worth
                    // abandoning a multi-gigabyte download over.
                }
            }

            return lastError;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// DELETE with a body, which HttpClient.DeleteAsync has no overload for, so
    /// the request has to be built by hand.
    /// </summary>
    public static async Task<string?> DeleteAsync(string name)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{Config.OllamaEndpoint}/api/delete")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { model = name }),
                    Encoding.UTF8,
                    "application/json"),
            };

            using HttpResponseMessage response = await Http.SendAsync(request);

            return response.IsSuccessStatusCode
                ? null
                : $"Ollama returned {(int)response.StatusCode}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Runs the same prompt through each model in turn, not in parallel: two 5GB
    /// models loaded at once will not fit in 6GB of VRAM, and racing them would
    /// measure the swapping rather than the models.
    /// </summary>
    public static async Task<IReadOnlyList<CompareResult>> CompareAsync(
        IEnumerable<string> models,
        HotkeyAction action,
        string sample,
        CancellationToken cancellationToken)
    {
        string prompt = $"{Config.PromptFor(action, SettingsStore.Current.Tone)}\n\n{sample}";
        var results = new List<CompareResult>();

        foreach (string model in models)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clock = Stopwatch.StartNew();
            TransformOutcome outcome = await OllamaClient.GenerateAsync(model, prompt, cancellationToken);
            clock.Stop();

            results.Add(new CompareResult(
                model,
                outcome.Ok ? outcome.Text : null,
                outcome.Ok ? null : outcome.Describe(),
                clock.Elapsed));
        }

        return results;
    }
}
