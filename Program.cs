using System.Threading;
using System.Windows.Forms;

namespace texAi;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, name: "texAi.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        // HotkeyWindow is a NativeWindow, not a Control, so it won't auto-install
        // this the way a Form would. Needed so awaits in TextTransformer resume
        // on this thread, since clipboard and SendInput calls must run on the STA
        // thread that owns the hotkey window.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        _ = new HotkeyWindow();
        using var trayIcon = new TrayIcon();

        // Pay the model's load-and-warm cost now, in the background, rather than
        // charging it to whichever hotkey the user presses first.
        _ = OllamaClient.WarmAsync();

        Application.Run();
    }
}
