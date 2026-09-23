using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace texAi;

/// <summary>
/// Loads WPF's native DLLs before WPF does, so an install that is missing one
/// says which, instead of vanishing.
///
/// 2.0.2 was published single-file without IncludeNativeLibrariesForSelfExtract,
/// which leaves these DLLs loose in the publish folder, and the installer only
/// copied texAi.exe. Every launch then died inside WPF's first window procedure
/// with DllNotFoundException ("Dll was not found.", 16 times in the event log
/// from 21 to 23 Sep 2026). An exception there crosses a native frame, which
/// .NET treats as fatal, so there was no dialog and no tray icon: it looked
/// exactly like the app never having been launched.
///
/// On a complete install this costs nothing. WPF's own loads later find the
/// modules already mapped.
/// </summary>
internal static class WpfNativeLibraries
{
    // vcruntime140_cor3 first because the other four import it, so a missing
    // runtime DLL is reported as itself rather than as whichever came first.
    private static readonly string[] Names =
    [
        "vcruntime140_cor3.dll",
        "PresentationNative_cor3.dll",
        "wpfgfx_cor3.dll",
        "PenImc_cor3.dll",
        "D3DCompiler_47_cor3.dll",
    ];

    /// <summary>
    /// Returns false, after telling the user, when one of them cannot be loaded.
    /// The caller must then exit without touching any WPF type.
    /// </summary>
    public static bool Load()
    {
        foreach (string name in Names)
        {
            if (LoadLibraryEx(Path.Combine(AppContext.BaseDirectory, name), IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH) != IntPtr.Zero)
            {
                continue;
            }

            int error = Marshal.GetLastPInvokeError();

            // user32 directly: WPF's MessageBox is the thing that cannot load.
            MessageBoxW(
                IntPtr.Zero,
                $"texAi could not load {name}, one of the files it needs to show anything.\n\n" +
                $"{new Win32Exception(error).Message} (error {error})\n\n" +
                "Reinstalling texAi from github.com/cthboss001/texai/releases puts it back.",
                "texAi could not start",
                MB_OK | MB_ICONERROR | MB_SETFOREGROUND);

            return false;
        }

        return true;
    }

    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_SETFOREGROUND = 0x00010000;

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
