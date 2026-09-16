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

        Application.Run(new HotkeyForm());
    }
}
