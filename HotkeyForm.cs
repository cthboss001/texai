using System.Windows.Forms;

namespace texAi;

/// <summary>
/// A Form is the easiest way to get a message loop + WindowsFormsSynchronizationContext
/// (required so clipboard/SendInput calls after an await land back on the STA thread),
/// but SetVisibleCore is overridden so this window is never actually shown, has no
/// taskbar entry, and never steals focus.
/// </summary>
internal sealed class HotkeyForm : Form
{
    private const int WM_HOTKEY = 0x0312;

    private const int HotkeyIdGrammar = 1;
    private const int HotkeyIdTranslate = 2;
    private const int HotkeyIdRewrite = 3;
    private const int HotkeyIdTone = 4;

    private bool _busy;

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        const uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        NativeMethods.RegisterHotKey(Handle, HotkeyIdGrammar, modifiers, (uint)Keys.G);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdTranslate, modifiers, (uint)Keys.T);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdRewrite, modifiers, (uint)Keys.R);
        NativeMethods.RegisterHotKey(Handle, HotkeyIdTone, modifiers, (uint)Keys.F);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdGrammar);
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdTranslate);
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdRewrite);
        NativeMethods.UnregisterHotKey(Handle, HotkeyIdTone);

        base.OnHandleDestroyed(e);
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
