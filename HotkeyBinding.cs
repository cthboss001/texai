using System.Text;
using System.Windows.Input;

namespace texAi;

/// <summary>
/// One global hotkey, stored the way WPF reports it from a KeyDown event so the
/// rebinding UI can capture a chord without translating anything, and translated
/// to Win32 only at RegisterHotKey time.
/// </summary>
internal sealed record HotkeyBinding(ModifierKeys Modifiers, Key Key)
{
    /// <summary>
    /// Win32's MOD_* values happen to equal WPF's ModifierKeys values, but this
    /// maps them by name rather than casting: one silent divergence would break
    /// every hotkey at once and look like the app simply not starting.
    /// </summary>
    public uint Win32Modifiers
    {
        get
        {
            uint flags = NativeMethods.MOD_NOREPEAT;

            if (Modifiers.HasFlag(ModifierKeys.Control)) flags |= NativeMethods.MOD_CONTROL;
            if (Modifiers.HasFlag(ModifierKeys.Shift)) flags |= NativeMethods.MOD_SHIFT;
            if (Modifiers.HasFlag(ModifierKeys.Alt)) flags |= NativeMethods.MOD_ALT;
            if (Modifiers.HasFlag(ModifierKeys.Windows)) flags |= NativeMethods.MOD_WIN;

            return flags;
        }
    }

    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>A chord with no modifier would swallow a bare letter system-wide.</summary>
    public bool IsValid => Modifiers != ModifierKeys.None && Key != Key.None;

    public override string ToString()
    {
        var text = new StringBuilder();

        if (Modifiers.HasFlag(ModifierKeys.Control)) text.Append("Ctrl+");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) text.Append("Alt+");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) text.Append("Shift+");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) text.Append("Win+");

        return text.Append(Key).ToString();
    }

    public static bool TryParse(string? value, out HotkeyBinding binding)
    {
        binding = new HotkeyBinding(ModifierKeys.None, Key.None);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key key = Key.None;

        foreach (string part in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "win" or "windows": modifiers |= ModifierKeys.Windows; break;
                default:
                    if (!Enum.TryParse(part, ignoreCase: true, out key))
                    {
                        return false;
                    }

                    break;
            }
        }

        var parsed = new HotkeyBinding(modifiers, key);
        if (!parsed.IsValid)
        {
            return false;
        }

        binding = parsed;
        return true;
    }
}
