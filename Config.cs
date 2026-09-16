using System.Windows.Input;

namespace texAi;

/// <summary>
/// Compile-time constants only: the endpoint, the prompts, and the values a
/// fresh install starts from. What the app actually uses at runtime lives in
/// <see cref="SettingsStore.Current"/>, because model, tone and hotkeys are all
/// things the user changes without rebuilding.
/// </summary>
internal static class Config
{
    public const string OllamaEndpoint = "http://127.0.0.1:11434";

    public const string DefaultModel = "qwen2.5:7b";

    public const string DefaultTone = "Professional";

    /// <summary>
    /// How long Ollama should hold the model in VRAM after a request. It unloads
    /// after about 5 minutes by default, and the reload plus one-time CUDA kernel
    /// compile can outlast the request timeout, which is why a hotkey pressed
    /// after a break used to fail for no visible reason. "30m" covers a working
    /// session; -1 would pin roughly 5GB forever, which on a 6GB card is not a
    /// trade worth making.
    /// </summary>
    public const string DefaultKeepAlive = "30m";

    public static readonly string[] Tones =
    [
        "Professional",
        "Friendly",
        "Concise",
        "Formal",
        "Casual",
    ];

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

    /// <summary>
    /// A short suggested list, because Ollama exposes no API for browsing its
    /// remote library: there is /api/tags for what is installed and /api/pull for
    /// a name you already know, and nothing in between. The dashboard pairs this
    /// with a free-text box so any model can still be pulled by name.
    ///
    /// Sizes are the Q4 download, and the warning threshold assumes the 6GB card
    /// this was built against.
    /// </summary>
    public static readonly RecommendedModel[] RecommendedModels =
    [
        new("qwen2.5:7b", "Balanced. The long-standing default here.", 4.7),
        new("aya-expanse:8b", "Cohere's multilingual model; the best bet for Bangla.", 5.1),
        new("gemma2:9b", "Strongest English rewriting, tightest fit in 6GB.", 5.4),
        new("llama3.1:8b", "General purpose fallback.", 4.9),
        new("qwen2.5:3b", "Fast and small, for machines without a usable GPU.", 1.9),
    ];

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

    public static string PromptFor(HotkeyAction action, string tone) => action switch
    {
        HotkeyAction.Grammar => GrammarPrompt,
        HotkeyAction.Translate => TranslatePrompt,
        HotkeyAction.Rewrite => RewritePrompt,
        HotkeyAction.Tone => TonePrompt(tone),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}

/// <param name="SizeGb">Approximate Q4 download size.</param>
internal sealed record RecommendedModel(string Name, string Summary, double SizeGb);
