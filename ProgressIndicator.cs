using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace texAi;

/// <summary>
/// A tiny borderless circle shown next to the caret while a transformation is
/// in flight: a spinning arc while waiting, a checkmark on success, solid red
/// on failure. Always self-closes a moment after reaching an end state.
/// WS_EX_NOACTIVATE keeps it from ever taking focus, so it can't interrupt
/// typing or steal the window Ctrl+V needs to land in.
/// </summary>
internal sealed class ProgressIndicator : Form
{
    private const int Diameter = 20;
    private const int SpinIntervalMs = 30;
    private const int SpinStepDegrees = 12;
    private const int SweepDegrees = 100;
    private const int HoldAfterEndMs = 550;

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private enum State { Spinning, Success, Error }

    private readonly System.Windows.Forms.Timer _spinTimer;
    private int _spinAngle;
    private State _state = State.Spinning;

    public ProgressIndicator()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(Diameter, Diameter);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        TopMost = true;

        using (var path = new GraphicsPath())
        {
            path.AddEllipse(0, 0, Diameter, Diameter);
            Region = new Region(path);
        }

        _spinTimer = new System.Windows.Forms.Timer { Interval = SpinIntervalMs };
        _spinTimer.Tick += (_, _) =>
        {
            _spinAngle = (_spinAngle + SpinStepDegrees) % 360;
            Invalidate();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ShowNear(Point anchor)
    {
        Rectangle area = Screen.FromPoint(anchor).WorkingArea;
        int x = Math.Clamp(anchor.X + 14, area.Left, area.Right - Diameter);
        int y = Math.Clamp(anchor.Y - Diameter - 8, area.Top, area.Bottom - Diameter);
        Location = new Point(x, y);

        Show();
        _spinTimer.Start();
    }

    public void Complete() => EndWith(State.Success);

    public void Fail() => EndWith(State.Error);

    private void EndWith(State endState)
    {
        if (IsDisposed)
        {
            return;
        }

        _spinTimer.Stop();
        _state = endState;
        Invalidate();

        var closeTimer = new System.Windows.Forms.Timer { Interval = HoldAfterEndMs };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            closeTimer.Dispose();
            Close();
        };
        closeTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill = _state == State.Error
            ? Color.FromArgb(235, 196, 44, 44)
            : Color.FromArgb(230, 32, 32, 36);

        using (var bg = new SolidBrush(fill))
        {
            g.FillEllipse(bg, 0, 0, Diameter - 1, Diameter - 1);
        }

        if (_state == State.Success)
        {
            using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var p1 = new PointF(Diameter * 0.26f, Diameter * 0.52f);
            var p2 = new PointF(Diameter * 0.43f, Diameter * 0.70f);
            var p3 = new PointF(Diameter * 0.76f, Diameter * 0.30f);
            g.DrawLines(pen, new[] { p1, p2, p3 });
        }
        else if (_state == State.Spinning)
        {
            using var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, 3, 3, Diameter - 7, Diameter - 7, _spinAngle, SweepDegrees);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _spinTimer.Stop();
        _spinTimer.Dispose();
        base.OnFormClosed(e);
    }
}
