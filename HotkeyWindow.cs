using System.Windows.Forms;

namespace texAi;

/// <summary>
/// A plain NativeWindow, not a Form: its handle is created unconditionally in the
/// constructor via CreateHandle, so it exists purely to receive WM_HOTKEY messages
/// and is never shown, never has a taskbar entry, and never steals focus. (An earlier
/// version used a Form with SetVisibleCore forced to false to hide it, but that also
/// suppressed handle creation entirely, so RegisterHotKey silently never ran.)
/// </summary>
internal sealed class HotkeyWindow : NativeWindow
{
    private const int WM_HOTKEY = 0x0312;

    private const int HotkeyIdGrammar = 1;
    private const int HotkeyIdTranslate = 2;
    private const int HotkeyIdRewrite = 3;
    private const int HotkeyIdTone = 4;

    private bool _busy;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());

        const uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        NativeMethods.RegisterHotKey(Handle, HotkeyIdGrammar, modifiers, (uint)Keys.G);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdTranslate, modifiers, (uint)Keys.T);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdRewrite, modifiers, (uint)Keys.R);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdTone, modifiers, (uint)Keys.F);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            OnHotkeyPressed(m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    private async void OnHotkeyPressed(int id)
    {
        // One transformation at a time; a second hotkey press while busy is dropped.
        if (_busy)
        {
            return;
        }

        HotkeyAction? action = id switch
        {
            HotkeyIdGrammar => HotkeyAction.Grammar,
            HotkeyIdTranslate => HotkeyAction.Translate,
            HotkeyIdRewrite => HotkeyAction.Rewrite,
            HotkeyIdTone => HotkeyAction.Tone,
            _ => null,
        };

        if (action is null)
        {
            return;
        }

        _busy = true;
        try
        {
            await TextTransformer.RunAsync(action.Value);
        }
        finally
        {
            _busy = false;
        }
    }
}
