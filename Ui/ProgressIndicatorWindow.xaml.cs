using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace texAi.Ui;

/// <summary>
/// The 26px disc that appears next to the caret while a rewrite is in flight.
///
/// One long-lived instance, hidden between runs rather than created and closed
/// each time: creating a layered WPF window costs a few frames, which is exactly
/// the moment the user is waiting to see something happen.
/// </summary>
internal partial class ProgressIndicatorWindow : Window
{
    /// <summary>Distance from the caret to the edge of the disc, in physical pixels.</summary>
    private const int GapFromCaret = 10;

    private const double CheckDashLength = 8.2;
    private const double CrossDashLength = 6.5;

    private static readonly Lazy<ProgressIndicatorWindow> Lazily = new(() => new ProgressIndicatorWindow());

    public static ProgressIndicatorWindow Instance => Lazily.Value;

    private readonly Storyboard _enter;
    private readonly Storyboard _spin;
    private readonly Storyboard _success;
    private readonly Storyboard _error;

    private IntPtr _handle;
    private bool _running;

    private ProgressIndicatorWindow()
    {
        InitializeComponent();

        _enter = (Storyboard)Resources["EnterStoryboard"];
        _spin = (Storyboard)Resources["SpinStoryboard"];
        _success = (Storyboard)Resources["SuccessStoryboard"];
        _error = (Storyboard)Resources["ErrorStoryboard"];

        _success.Completed += (_, _) => Finish();
        _error.Completed += (_, _) => Finish();
    }

    /// <summary>
    /// Creates the hwnd up front so the extended styles are in place and the
    /// first ShowBeside has nothing left to do but position and show.
    /// </summary>
    public void Prepare() => new WindowInteropHelper(this).EnsureHandle();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        // NOACTIVATE so it can never take focus away from the window Ctrl+V has
        // to land in, TOOLWINDOW so it stays out of Alt-Tab, and TRANSPARENT so
        // a click meant for the text underneath passes straight through it.
        IntPtr style = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE);
        IntPtr updated = style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, updated);
    }

    public void ShowBeside(CaretAnchor anchor)
    {
        ResetVisuals();
        MoveTo(anchor);

        Show();
        _running = true;

        _enter.Begin(this, isControllable: true);
        _spin.Begin(this, isControllable: true);
    }

    public void Succeed()
    {
        if (!_running)
        {
            return;
        }

        // The spin is deliberately left running: the arc fades out over 120ms
        // while still turning, so there is no frame where it snaps back to zero.
        _success.Begin(this, isControllable: true);
    }

    public void Fail()
    {
        if (!_running)
        {
            return;
        }

        _error.Begin(this, isControllable: true);
    }

    private void Finish()
    {
        _running = false;
        Hide();
    }

    /// <summary>
    /// Positions by hwnd rather than Left/Top. The caret rect is in physical
    /// pixels and WPF's Left/Top are device-independent units; on a scaled
    /// monitor under PerMonitorV2 those differ by the scale factor, which puts
    /// the disc in the wrong place by a quarter of the screen at 200%.
    /// </summary>
    private void MoveTo(CaretAnchor anchor)
    {
        double scale = _handle == IntPtr.Zero ? 1.0 : NativeMethods.GetDpiForWindow(_handle) / 96.0;
        int sizePx = (int)Math.Round(Width * scale);

        int left = anchor.X + GapFromCaret;
        int top = anchor.Top + (anchor.LineHeight / 2) - (sizePx / 2);

        NativeMethods.RECT work = WorkAreaContaining(anchor.X, anchor.Top);

        // Flip to the caret's left rather than clamping when it would run off
        // the right edge, so typing at the end of a line does not park the disc
        // on top of the text.
        if (left + sizePx > work.Right)
        {
            left = anchor.X - GapFromCaret - sizePx;
        }

        left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - sizePx));
        top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - sizePx));

        NativeMethods.SetWindowPos(
            _handle,
            IntPtr.Zero,
            left,
            top,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private static NativeMethods.RECT WorkAreaContaining(int x, int y)
    {
        var point = new NativeMethods.POINT { X = x, Y = y };
        IntPtr monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };

        return NativeMethods.GetMonitorInfo(monitor, ref info)
            ? info.rcWork
            : new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
    }

    /// <summary>
    /// Storyboards hold their final value after completing, which would leave a
    /// checkmark from the last run visible on the next one. Removing each clock
    /// hands the properties back before they are set.
    /// </summary>
    private void ResetVisuals()
    {
        _enter.Remove(this);
        _spin.Remove(this);
        _success.Remove(this);
        _error.Remove(this);

        Root.Opacity = 1;
        RootScale.ScaleX = 1;
        RootScale.ScaleY = 1;
        RootShift.X = 0;

        Arc.Opacity = 1;
        ArcRotate.Angle = 0;
        ErrorRing.Opacity = 0;

        Check.Opacity = 0;
        Check.StrokeDashOffset = CheckDashLength;
        Cross.Opacity = 0;
        Cross.StrokeDashOffset = CrossDashLength;
    }
}
