using System.Drawing;
using System.Runtime.InteropServices;

namespace texAi;

/// <summary>
/// Best-effort screen position for the progress indicator to anchor next to.
/// There's no universal cross-process "get caret position" API on Windows;
/// GetGUIThreadInfo exposes it for apps that report a real caret (most native
/// edit controls do), and the mouse cursor is the fallback for the rest.
/// </summary>
internal static class CaretLocator
{
    public static Point GetAnchorPoint()
    {
        try
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                uint threadId = NativeMethods.GetWindowThreadProcessId(foreground, out _);
                var info = new NativeMethods.GUITHREADINFO
                {
                    cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>(),
                };

                if (NativeMethods.GetGUIThreadInfo(threadId, ref info) && info.hwndCaret != IntPtr.Zero)
                {
                    var topLeft = new NativeMethods.POINT { X = info.rcCaret.Left, Y = info.rcCaret.Top };
                    if (NativeMethods.ClientToScreen(info.hwndCaret, ref topLeft))
                    {
                        return new Point(topLeft.X, topLeft.Y);
                    }
                }
            }
        }
        catch
        {
            // Fall through to cursor position.
        }

        return NativeMethods.GetCursorPos(out NativeMethods.POINT cursor)
            ? new Point(cursor.X, cursor.Y)
            : new Point(0, 0);
    }
}
