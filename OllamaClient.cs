using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace texAi;

/// <summary>
/// Talks to the local Ollama /api/generate endpoint only. Nothing here
/// touches the network beyond 127.0.0.1.
/// </summary>
internal static class OllamaClient
{
    // 60s, not 30s: even with keep_alive set, the very first request after
    // Ollama itself starts has to load weights from disk and compile CUDA
    // kernels. A tighter timeout would misfire on exactly the case that
    // matters most, the first rewrite of the day.
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private const int DetailMaxChars = 200;

    /// <summary>
    /// True if Ollama is reachable and the configured model is pulled.
    /// Used for the tray icon's status, not the transform pipeline.
    /// </summary>
    public static async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var response = await Http.GetAsync($"{Config.OllamaEndpoint}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("models", out JsonElement models))
            {
                return false;
            }

            foreach (JsonElement model in models.EnumerateArray())
            {
                if (model.TryGetProperty("name", out JsonElement nameProp) &&
                    string.Equals(nameProp.GetString(), Config.ModelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads the model into VRAM without generating anything. Ollama treats a
    /// generate call with an empty prompt as a preload, so one of these at
    /// startup absorbs the multi-second cold start instead of the user's first
    /// hotkey press absorbing it. Failure is ignored: Ollama may simply not be
    /// running yet, and the tray icon already reports that.
    /// </summary>
    public static async Task WarmAsync()
    {
        try
        {
            using var content = JsonBody(new
            {
                model = Config.ModelName,
                prompt = string.Empty,
                keep_alive = Config.KeepAlive,
            });

            using HttpResponseMessage response = await Http.PostAsync($"{Config.OllamaEndpoint}/api/generate", content);
            _ = response;
        }
        catch
        {
            // Nothing to do and nothing to report: this is opportunistic.
        }
    }

    public static async Task<TransformOutcome> TransformAsync(HotkeyAction action, string input)
    {
        string instruction = action switch
        {
            HotkeyAction.Grammar => Config.GrammarPrompt,
            HotkeyAction.Translate => Config.TranslatePrompt,
            HotkeyAction.Rewrite => Config.RewritePrompt,
            HotkeyAction.Tone => Config.TonePrompt(Config.DefaultTone),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        var payload = new
        {
            model = Config.ModelName,
            prompt = $"{instruction}\n\n{input}",
            stream = false,
            keep_alive = Config.KeepAlive,
        };

        try
        {
            using StringContent content = JsonBody(payload);
            using HttpResponseMessage response = await Http.PostAsync($"{Config.OllamaEndpoint}/api/generate", content);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ClassifyHttpFailure(response.StatusCode, body);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
            {
                return TransformOutcome.Failure(FailureKind.EmptyResponse, "no 'response' field in the reply");
            }

            string? text = responseElement.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? TransformOutcome.Failure(FailureKind.EmptyResponse)
                : TransformOutcome.Success(text);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            return TransformOutcome.Failure(FailureKind.OllamaUnreachable, $"{Config.OllamaEndpoint} refused the connection");
        }
        catch (HttpRequestException ex)
        {
            return TransformOutcome.Failure(FailureKind.OllamaUnreachable, Truncate(ex.Message));
        }
        catch (TaskCanceledException)
        {
            // HttpClient surfaces its own timeout as a cancellation. Nothing
            // else cancels this request, so there is no ambiguity to resolve.
            return TransformOutcome.Failure(FailureKind.Timeout, $"no reply within {Http.Timeout.TotalSeconds:0}s");
        }
        catch (JsonException ex)
        {
            return TransformOutcome.Failure(FailureKind.HttpError, $"unreadable reply: {Truncate(ex.Message)}");
        }
    }

    /// <summary>
    /// Ollama answers a missing model with 404 and a body along the lines of
    /// "model 'x' not found, try pulling it first", which is worth separating
    /// from a genuine bad request: one is fixed by a download, the other is a bug.
    /// </summary>
    private static TransformOutcome ClassifyHttpFailure(HttpStatusCode status, string body)
    {
        int code = (int)status;

        if (status == HttpStatusCode.NotFound &&
            body.Contains("pulling", StringComparison.OrdinalIgnoreCase))
        {
            return TransformOutcome.Failure(FailureKind.ModelNotInstalled, Config.ModelName, code);
        }

        return TransformOutcome.Failure(FailureKind.HttpError, Truncate(ExtractError(body)), code);
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out JsonElement error))
            {
                return error.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Not JSON; the raw body is the best detail available.
        }

        return body;
    }

    private static string Truncate(string value) =>
        value.Length <= DetailMaxChars ? value : value[..DetailMaxChars] + "...";

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
