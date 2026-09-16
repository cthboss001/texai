using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace texAi;

/// <summary>
/// The only persistent, always-visible part of texAi: a system tray dot
/// showing whether Ollama and the configured model are currently reachable
/// (green/red), polled periodically since Ollama can be started or stopped
/// independently of texAi. Also the one place to exit cleanly, so stopping
/// it no longer requires Task Manager.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _iconOk;
    private readonly Icon _iconError;
    private readonly Icon _iconChecking;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly ToolStripMenuItem _statusMenuItem;

    public TrayIcon()
    {
        _iconOk = CreateDotIcon(Color.FromArgb(52, 168, 83));
        _iconError = CreateDotIcon(Color.FromArgb(196, 44, 44));
        _iconChecking = CreateDotIcon(Color.FromArgb(150, 150, 150));

        _statusMenuItem = new ToolStripMenuItem("Checking...") { Enabled = false };
        var exitMenuItem = new ToolStripMenuItem("Exit texAi");
        exitMenuItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
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

        _pollTimer = new System.Windows.Forms.Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _pollTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _pollTimer.Start();

        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        bool healthy = await OllamaClient.CheckHealthAsync();

        _notifyIcon.Icon = healthy ? _iconOk : _iconError;
        _notifyIcon.Text = healthy
            ? $"texAi - connected ({Config.ModelName})"
            : "texAi - Ollama not reachable";
        _statusMenuItem.Text = healthy
            ? $"Connected - {Config.ModelName}"
            : "Ollama not reachable";
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        Application.Exit();
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
        _pollTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _iconOk.Dispose();
        _iconError.Dispose();
        _iconChecking.Dispose();
    }
}
