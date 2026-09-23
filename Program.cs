using System.Threading;
using System.Windows;

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

        // Before `new App()`: the Application constructor creates the
        // dispatcher's hidden window, and that is already the first WPF call
        // into PresentationNative_cor3.dll.
        if (!WpfNativeLibraries.Load())
        {
            return;
        }

        // OnExplicitShutdown because texAi has no main window: with the default
        // OnLastWindowClose, hiding the indicator after a rewrite would quit the
        // whole app.
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Run();
    }
}
