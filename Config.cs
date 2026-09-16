namespace texAi;

/// <summary>
/// All tunables live here. Nothing else in the app should hardcode
/// the endpoint, model, tone, or prompt text.
/// </summary>
internal static class Config
{
    public const string OllamaEndpoint = "http://127.0.0.1:11434";

    public const string ModelName = "qwen2.5:7b";

    public const string DefaultTone = "Professional";

    public const string GrammarPrompt =
        "Fix grammar, spelling, punctuation, and unnatural phrasing. " +
        "Preserve the original meaning. Return only the corrected text.";

    public const string TranslatePrompt =
        "Translate the text into English. Preserve meaning, formatting, names, " +
        "numbers, and technical terms. Return only the translation.";

    public const string RewritePrompt =
        "Rewrite the text to be natural, clear, and fluent. Preserve the original " +
        "meaning. Do not add information. Return only the rewritten text.";

    public static string TonePrompt(string tone) =>
        $"Rewrite the text in a {tone.ToLowerInvariant()} tone. " +
        "Preserve the original meaning. Return only the rewritten text.";
}
