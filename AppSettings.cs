using System.Text.Json.Serialization;

namespace texAi;

/// <summary>
/// Everything the user can change, and the only thing texAi writes to disk.
///
/// Note what is absent: any rewritten text. Preferences persist because losing
/// them every launch would be useless; content does not, because persisting it
/// would quietly break the promise the app is built on. See <see cref="HistoryStore"/>.
///
/// Stored as strings rather than enums so a hand-edited settings.json stays
/// readable and a bad value degrades to the default instead of failing to parse.
/// </summary>
internal sealed class AppSettings
{
    public string Model { get; set; } = Config.DefaultModel;

    public string Tone { get; set; } = Config.DefaultTone;

    public string KeepAlive { get; set; } = Config.DefaultKeepAlive;

    /// <summary>Keyed by <see cref="HotkeyAction"/> name, valued like "Ctrl+Alt+G".</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = DefaultHotkeyStrings();

    /// <summary>False until the user has been through onboarding once.</summary>
    public bool OnboardingDone { get; set; }

    [JsonIgnore]
    public IReadOnlyDictionary<HotkeyAction, HotkeyBinding> Bindings
    {
        get
        {
            var resolved = new Dictionary<HotkeyAction, HotkeyBinding>();

            foreach (HotkeyAction action in Enum.GetValues<HotkeyAction>())
            {
                resolved[action] =
                    Hotkeys.TryGetValue(action.ToString(), out string? text) &&
                    HotkeyBinding.TryParse(text, out HotkeyBinding parsed)
                        ? parsed
                        : Config.DefaultHotkeys[action];
            }

            return resolved;
        }
    }

    public void SetBinding(HotkeyAction action, HotkeyBinding binding) =>
        Hotkeys[action.ToString()] = binding.ToString();

    private static Dictionary<string, string> DefaultHotkeyStrings() =>
        Config.DefaultHotkeys.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString());
}
