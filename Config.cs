using System.Windows.Input;

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

    /// <summary>
    /// How long Ollama should hold the model in VRAM after a request. It unloads
    /// after about 5 minutes by default, and the reload plus one-time CUDA kernel
    /// compile can outlast the request timeout, which is why a hotkey pressed
    /// after a break used to fail for no visible reason. "30m" covers a working
    /// session; -1 would pin roughly 5GB forever, which on a 6GB card is not a
    /// trade worth making.
    /// </summary>
    public const string KeepAlive = "30m";

    /// <summary>
    /// Ctrl+Alt, not the original Ctrl+Shift. A global hotkey outranks the
    /// focused app, so Ctrl+Shift+T took "reopen closed tab" away from every
    /// browser, Ctrl+Shift+R took hard reload, and Ctrl+Shift+F took
    /// find-in-files in VS Code, system-wide. Ctrl+Alt collides with far less.
    /// </summary>
    public static IReadOnlyDictionary<HotkeyAction, HotkeyBinding> DefaultHotkeys { get; } =
        new Dictionary<HotkeyAction, HotkeyBinding>
        {
            [HotkeyAction.Grammar] = new(ModifierKeys.Control | ModifierKeys.Alt, Key.G),
            [HotkeyAction.Translate] = new(ModifierKeys.Control | ModifierKeys.Alt, Key.T),
            [HotkeyAction.Rewrite] = new(ModifierKeys.Control | ModifierKeys.Alt, Key.R),
            [HotkeyAction.Tone] = new(ModifierKeys.Control | ModifierKeys.Alt, Key.F),
        };

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
