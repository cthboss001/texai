using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace texAi;

/// <summary>
/// Talks to the local Ollama /api/generate endpoint only. Nothing here
/// touches the network beyond 127.0.0.1.
/// </summary>
internal static class OllamaClient
{
    // 60s, not 30s: Ollama unloads an idle model after a few minutes, and
    // reloading it plus a one-time CUDA kernel compile can take 30-40s on
    // the first request back. A tighter timeout would misfire as a silent
    // failure on exactly the case that matters most: coming back to it.
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    public static async Task<string?> TransformAsync(HotkeyAction action, string input)
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
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{Config.OllamaEndpoint}/api/generate", content);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("response", out JsonElement responseElement))
            {
                return null;
            }

            return responseElement.GetString()?.Trim();
        }
        catch
        {
            // Ollama not running, model missing, timeout, malformed JSON, etc.
            // Fail silently: the caller treats null as "do nothing".
            return null;
        }
    }
}
