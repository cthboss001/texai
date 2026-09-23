using System.Windows.Threading;

namespace texAi;

/// <summary>
/// Lets a second launch of texAi ask the one already running to show its
/// dashboard. texAi lives in the tray, so clicking its icon while it runs used to
/// hit the single-instance mutex and exit with nothing on screen, which looks
/// exactly like the app failing to open.
/// </summary>
internal static class ActivationSignal
{
    private const string Name = "texAi.ShowDashboard";

    private static EventWaitHandle? _signal;

    /// <summary>
    /// Called by the first instance as soon as it owns the mutex, before its
    /// slow startup work. A launch that lands in that window then sets an event
    /// that already exists, and <see cref="Listen"/> picks it up once the
    /// dashboard can be shown, instead of the request being lost.
    /// </summary>
    public static void Create() =>
        _signal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, Name);

    public static void Listen(Dispatcher dispatcher, Action onSignal)
    {
        if (_signal is null)
        {
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(
            _signal,
            (_, _) => dispatcher.BeginInvoke(onSignal),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public static void Send()
    {
        if (!EventWaitHandle.TryOpenExisting(Name, out EventWaitHandle? signal))
        {
            return;
        }

        // Windows only lets a process take the foreground if it was the last to
        // receive input. That is this one, just launched by a click, not the tray
        // instance, so the permission has to be handed across or the dashboard
        // opens behind whatever the user was looking at.
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

        using (signal)
        {
            signal.Set();
        }
    }
}
