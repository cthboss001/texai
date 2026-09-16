using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace texAi;

/// <summary>
/// Sends synthetic Ctrl+C / Ctrl+V via SendInput so the currently focused
/// app (whatever it is) receives real key events, not a WM_COPY message.
///
/// The hard part is not the send, it is the wait before it. A global hotkey
/// fires on key-down, so when we run the user is still physically holding the
/// chord. Injecting Ctrl+C on top of a held Shift makes the target app see
/// Ctrl+Shift+C, which in Firefox and Chrome is the DevTools element picker;
/// on top of a held Win it is Win+Ctrl+C, which toggles Windows colour filters.
/// Neither copies anything, so the whole transform then fails on empty input.
/// </summary>
internal static class InputSimulator
{
    private const int PollIntervalMs = 15;
    private const int ReleaseTimeoutMs = 400;

    /// <summary>
    /// Reserved virtual key that no application handles. Injecting it while a
    /// Win key is held makes the shell treat that Win press as part of a chord,
    /// so the synthetic Win key-up below does not pop the Start menu. This is
    /// AutoHotkey's "menu mask key" trick (A_MenuMaskKey, vkE8).
    /// </summary>
    private const ushort VK_NONAME = 0xFC;

    private static readonly ushort[] ChordModifiers =
    [
        NativeMethods.VK_CONTROL,
        NativeMethods.VK_SHIFT,
        NativeMethods.VK_MENU,
        NativeMethods.VK_LWIN,
        NativeMethods.VK_RWIN,
    ];

    public static Task SendCtrlCAsync() => SendCtrlComboAsync((ushort)Keys.C);

    public static Task SendCtrlVAsync() => SendCtrlComboAsync((ushort)Keys.V);

    private static async Task SendCtrlComboAsync(ushort vk)
    {
        await WaitForChordReleaseAsync();

        Send(
            KeyEvent(NativeMethods.VK_CONTROL, keyUp: false),
            KeyEvent(vk, keyUp: false),
            KeyEvent(vk, keyUp: true),
            KeyEvent(NativeMethods.VK_CONTROL, keyUp: true));
    }

    /// <summary>
    /// Polls until no modifier is physically down, then returns. On timeout it
    /// forces key-ups for whatever is still held and returns anyway: aborting
    /// here would look exactly like the hotkey doing nothing, which is the
    /// failure mode this whole change exists to remove.
    /// </summary>
    private static async Task WaitForChordReleaseAsync()
    {
        for (int waited = 0; waited < ReleaseTimeoutMs; waited += PollIntervalMs)
        {
            if (!AnyModifierDown())
            {
                return;
            }

            await Task.Delay(PollIntervalMs);
        }

        ForceReleaseHeldModifiers();
    }

    private static bool AnyModifierDown() => ChordModifiers.Any(IsDown);

    private static bool IsDown(ushort vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static void ForceReleaseHeldModifiers()
    {
        var events = new List<NativeMethods.INPUT>();

        foreach (ushort vk in ChordModifiers)
        {
            if (!IsDown(vk))
            {
                continue;
            }

            if (vk is NativeMethods.VK_LWIN or NativeMethods.VK_RWIN)
            {
                events.Add(KeyEvent(VK_NONAME, keyUp: false));
                events.Add(KeyEvent(VK_NONAME, keyUp: true));
            }

            events.Add(KeyEvent(vk, keyUp: true));
        }

        if (events.Count > 0)
        {
            Send([.. events]);
        }
    }

    private static void Send(params NativeMethods.INPUT[] inputs) =>
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

    private static NativeMethods.INPUT KeyEvent(ushort vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = NativeMethods.InjectedTag,
            },
        },
    };
}
