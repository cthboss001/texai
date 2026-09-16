using System.Windows;
using texAi.Ui;

namespace texAi;

/// <summary>
/// Written in code rather than as App.xaml on purpose: an ApplicationDefinition
/// generates its own Main, which collides with Program.Main and the single
/// instance mutex that lives there.
/// </summary>
internal sealed class App : Application
{
    private TrayIcon? _tray;

    /// <summary>Exposed so the dashboard can suspend the chords while rebinding one.</summary>
    public static HotkeyService? Hotkeys { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Not the constructor: the pack:// URI scheme is registered as part of
        // Application's own initialisation, so resolving a component URI any
        // earlier is a race with it.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/texAi;component/Themes/Palette.xaml", UriKind.Absolute),
        });

        // Must come after the theme is merged: the indicator's XAML resolves its
        // colours with StaticResource at load time.
        ProgressIndicatorWindow.Instance.Prepare();

        Hotkeys = new HotkeyService();
        Hotkeys.Apply(SettingsStore.Current.Bindings);

        // Rebinding a hotkey in the dashboard saves, which fires this, which
        // re-registers the whole set. Nothing else has to know about the change.
        SettingsStore.Changed += settings => Hotkeys?.Apply(settings.Bindings);

        _tray = new TrayIcon();

        // Pay the model's load-and-warm cost now, in the background, rather than
        // charging it to whichever hotkey the user presses first.
        _ = OllamaClient.WarmAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        Hotkeys?.Dispose();
        base.OnExit(e);
    }
}
