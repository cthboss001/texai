using System.Runtime.InteropServices;

namespace texAi;

/// <summary>
/// Where the caret is, in physical screen pixels, plus how tall its line is.
/// Physical, not device-independent: it comes straight from Win32 and is handed
/// straight to SetWindowPos, so it is never converted.
/// </summary>
/// <param name="FromCaret">False when this is the mouse-cursor fallback, which has no line to centre against.</param>
internal readonly record struct CaretAnchor(int X, int Top, int LineHeight, bool FromCaret);

/// <summary>
/// Best-effort screen position for the progress indicator to anchor next to.
/// There is no universal cross-process "get caret position" API on Windows;
/// GetGUIThreadInfo exposes it for apps that report a real caret (most native
/// edit controls do), and the mouse cursor is the fallback for the rest.
/// </summary>
internal static class CaretLocator
{
    private const int FallbackLineHeight = 20;

    // Chrome and some Electron apps report a degenerate or absurd rcCaret rather
    // than none at all. Anything outside this range is treated as no line info.
    private const int MinPlausibleLineHeight = 2;
    private const int MaxPlausibleLineHeight = 200;

    public static CaretAnchor GetAnchor()
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
                    var bottomLeft = new NativeMethods.POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };

                    // Both corners go through ClientToScreen separately: the
                    // window may be scrolled or on a differently scaled monitor,
                    // so subtracting client-space values would be wrong.
                    if (NativeMethods.ClientToScreen(info.hwndCaret, ref topLeft) &&
                        NativeMethods.ClientToScreen(info.hwndCaret, ref bottomLeft))
                    {
                        int height = bottomLeft.Y - topLeft.Y;
                        bool plausible = height is >= MinPlausibleLineHeight and <= MaxPlausibleLineHeight;

                        return new CaretAnchor(
                            topLeft.X,
                            topLeft.Y,
                            plausible ? height : FallbackLineHeight,
                            FromCaret: true);
                    }
                }
            }
        }
        catch
        {
            // Fall through to cursor position.
        }

        return NativeMethods.GetCursorPos(out NativeMethods.POINT cursor)
            ? new CaretAnchor(cursor.X, cursor.Y, FallbackLineHeight, FromCaret: false)
            : new CaretAnchor(0, 0, FallbackLineHeight, FromCaret: false);
    }
}
