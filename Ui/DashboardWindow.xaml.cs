using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace texAi.Ui;

internal partial class DashboardWindow : Window
{
    private static DashboardWindow? _instance;

    private readonly DashboardViewModel _model = new();

    private Button? _capturing;
    private HotkeyAction _capturingAction;

    private DashboardWindow()
    {
        InitializeComponent();
        DataContext = _model;
    }

    public static void ShowOrActivate()
    {
        _instance ??= new DashboardWindow();

        _instance.Show();

        if (_instance.WindowState == WindowState.Minimized)
        {
            _instance.WindowState = WindowState.Normal;
        }

        _instance.Activate();
        _ = _instance._model.RefreshAsync();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Without this the title bar stays light while everything below it is
        // dark, which looks like a rendering bug rather than a theme.
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int dark = 1;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
    }

    /// <summary>
    /// Hides rather than closes. texAi has no main window, so a real close would
    /// destroy the only view of the session's history, which cannot be rebuilt
    /// because it is never persisted.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        EndCapture();
        Hide();
    }

    // ---- hotkey capture ---------------------------------------------------

    private void OnCaptureHotkey(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        EndCapture();

        _capturing = button;
        _capturingAction = Enum.Parse<HotkeyAction>((string)button.Tag);

        // The chord being replaced is registered globally, so Windows would
        // deliver it to the hotkey sink instead of to this window. Suspending
        // lets the user press the same chord they are editing.
        App.Hotkeys?.Suspend();

        BindingOperations.ClearBinding(button, ContentControl.ContentProperty);
        button.Content = "Press a chord, Esc to cancel";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_capturing is null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        // Alt-anything arrives as Key.System with the real key in SystemKey.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndCapture();
            return;
        }

        if (IsModifier(key))
        {
            // Still mid-chord; wait for the key the modifiers apply to.
            return;
        }

        var binding = new HotkeyBinding(Keyboard.Modifiers, key);

        if (!binding.IsValid)
        {
            // A bare letter would be swallowed system-wide.
            return;
        }

        _model.Rebind(_capturingAction, binding);
        EndCapture();
    }

    private static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin
        or Key.System;

    private void EndCapture()
    {
        if (_capturing is null)
        {
            return;
        }

        BindingOperations.SetBinding(
            _capturing,
            ContentControl.ContentProperty,
            new Binding($"Hotkey{_capturing.Tag}"));

        _capturing = null;
        App.Hotkeys?.Resume();
    }

    // ---- small handlers ---------------------------------------------------

    private void OnCompareModelChecked(object sender, RoutedEventArgs e)
    {
        if (((CheckBox)sender).Tag is string name && !_model.CompareSelection.Contains(name))
        {
            _model.CompareSelection.Add(name);
        }
    }

    private void OnCompareModelUnchecked(object sender, RoutedEventArgs e)
    {
        if (((CheckBox)sender).Tag is string name)
        {
            _model.CompareSelection.Remove(name);
        }
    }

    private void OnGoToModels(object sender, RoutedEventArgs e) =>
        _model.Section = DashboardSection.Models;

    private void OnOpenOllamaSite(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://ollama.com/download") { UseShellExecute = true });
}
