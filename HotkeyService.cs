using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace texAi;

/// <summary>
/// Owns the four global hotkeys. Built on HwndSource rather than a WinForms
/// NativeWindow so the hwnd belongs to the WPF Dispatcher thread: awaits in the
/// transform pipeline then resume on the same STA thread that the clipboard and
/// SendInput calls require, with no manual SynchronizationContext to install.
///
/// The window is parented to HWND_MESSAGE, so it is message-only: never drawn,
/// never in the taskbar, impossible to Alt-Tab to or accidentally show.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, HotkeyAction> _byId = [];
    private bool _busy;
    private bool _disposed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("texAi.HotkeySink")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            Width = 0,
            Height = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(OnMessage);
    }

    /// <summary>
    /// Replaces the whole set atomically from the caller's point of view: every
    /// previous registration is dropped first, so rebinding one key cannot leave
    /// a stale chord registered. A refusal is logged rather than swallowed,
    /// because a silently unregistered hotkey is indistinguishable from the app
    /// being dead, which is exactly the confusion this release is removing.
    /// </summary>
    public void Apply(IReadOnlyDictionary<HotkeyAction, HotkeyBinding> bindings)
    {
        UnregisterAll();

        foreach ((HotkeyAction action, HotkeyBinding binding) in bindings)
        {
            if (!binding.IsValid)
            {
                ErrorLog.Record(FailureKind.HotkeyUnavailable, $"{action} has no usable hotkey", binding.ToString());
                continue;
            }

            int id = (int)action + 1;

            if (NativeMethods.RegisterHotKey(_source.Handle, id, binding.Win32Modifiers, binding.VirtualKey))
            {
                _byId[id] = action;
                continue;
            }

            string reason = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            ErrorLog.Record(FailureKind.HotkeyUnavailable, $"{binding} could not be registered for {action}", reason);
        }
    }

    /// <summary>
    /// Drops every registration so the chords reach the focused window instead.
    /// Used while the dashboard is capturing a new binding: otherwise Windows
    /// would route the chord being edited straight back here.
    /// </summary>
    public void Suspend() => UnregisterAll();

    public void Resume() => Apply(SettingsStore.Current.Bindings);

    private IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _byId.TryGetValue(wParam.ToInt32(), out HotkeyAction action))
        {
            handled = true;
            Dispatch(action);
        }

        return IntPtr.Zero;
    }

    private async void Dispatch(HotkeyAction action)
    {
        // One transformation at a time; a second hotkey press while busy is dropped.
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            _ = await TransformPipeline.RunAsync(action);
        }
        finally
        {
            _busy = false;
        }
    }

    private void UnregisterAll()
    {
        foreach (int id in _byId.Keys)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        }

        _byId.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }
}
