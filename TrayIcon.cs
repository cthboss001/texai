using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Threading;

namespace texAi;

/// <summary>
/// The only persistent, always-visible part of texAi: a system tray dot showing
/// whether Ollama and the active model are currently reachable, polled because
/// Ollama can be started or stopped independently of texAi. Also the one place
/// to exit cleanly.
///
/// Still WinForms: WPF has no tray API of its own, and NotifyIcon plus a
/// GDI-drawn icon is a smaller dependency than pulling in a package for it.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _iconOk;
    private readonly Icon _iconError;
    private readonly Icon _iconChecking;
    private readonly DispatcherTimer _pollTimer;
    private readonly ToolStripMenuItem _statusMenuItem;

    public TrayIcon()
    {
        // Matches Themes/Palette.xaml: Success #5FA463, Error #C75B41, muted text.
        _iconOk = CreateDotIcon(Color.FromArgb(0x5F, 0xA4, 0x63));
        _iconError = CreateDotIcon(Color.FromArgb(0xC7, 0x5B, 0x41));
        _iconChecking = CreateDotIcon(Color.FromArgb(0x6F, 0x67, 0x58));

        _statusMenuItem = new ToolStripMenuItem("Checking...") { Enabled = false };

        var openMenuItem = new ToolStripMenuItem("Open dashboard") { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        openMenuItem.Click += (_, _) => Ui.DashboardWindow.ShowOrActivate();

        var exitMenuItem = new ToolStripMenuItem("Exit texAi");
        exitMenuItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add(openMenuItem);
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _iconChecking,
            Text = "texAi - checking...",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => Ui.DashboardWindow.ShowOrActivate();

        _pollTimer = new DispatcherTimer { Interval = PollInterval };
        _pollTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _pollTimer.Start();

        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        bool healthy = await OllamaClient.CheckHealthAsync();
        string model = SettingsStore.Current.Model;

        _notifyIcon.Icon = healthy ? _iconOk : _iconError;

        // NotifyIcon.Text is capped at 63 characters by the shell, and silently
        // throws above it, so the last failure is trimmed rather than appended
        // in full.
        string status = healthy ? $"connected, {model}" : LastProblem();
        _notifyIcon.Text = Trim($"texAi - {status}", 63);

        _statusMenuItem.Text = healthy ? $"Connected - {model}" : LastProblem();
    }

    private static string LastProblem() =>
        ErrorLog.Latest()?.Description ?? "Ollama not reachable";

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..(max - 3)] + "...";

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon CreateDotIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 4, 4, 24, 24);
        }

        // Icon.FromHandle wraps the HICON without owning it, so it must be
        // cloned (which does own its data) and the original handle destroyed,
        // or every status change leaks a GDI icon handle.
        IntPtr hIcon = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        NativeMethods.DestroyIcon(hIcon);
        return icon;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _iconOk.Dispose();
        _iconError.Dispose();
        _iconChecking.Dispose();
    }
}
