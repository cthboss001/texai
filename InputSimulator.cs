using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace texAi;

/// <summary>
/// Sends synthetic Ctrl+C / Ctrl+V via SendInput so the currently focused
/// app (whatever it is) receives real key events, not a WM_COPY message.
/// </summary>
internal static class InputSimulator
{
    public static void SendCtrlC() => SendCtrlCombo((ushort)Keys.C);

    public static void SendCtrlV() => SendCtrlCombo((ushort)Keys.V);

    private static void SendCtrlCombo(ushort vk)
    {
        var inputs = new[]
        {
            KeyEvent(NativeMethods.VK_CONTROL, keyUp: false),
            KeyEvent(vk, keyUp: false),
            KeyEvent(vk, keyUp: true),
            KeyEvent(NativeMethods.VK_CONTROL, keyUp: true),
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

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
                dwExtraInfo = IntPtr.Zero,
            },
        },
    };
}
