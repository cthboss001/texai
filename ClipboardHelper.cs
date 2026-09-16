using System.Runtime.InteropServices;
using System.Windows;

namespace texAi;

/// <summary>
/// Every clipboard call in texAi, in one place and retried.
///
/// The Win32 clipboard is a single global resource that any process can hold
/// open; OLE surfaces that contention as an ExternalException, and clipboard
/// managers make it routine rather than rare. Each operation gets three quick
/// attempts before it is treated as a real failure.
/// </summary>
internal static class ClipboardHelper
{
    private const int Retries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    public static IDataObject? TryGetDataObject() => Retry(Clipboard.GetDataObject);

    public static bool TryClear() => Retry(() => { Clipboard.Clear(); return true; });

    public static bool TrySetText(string text) =>
        Retry(() => { Clipboard.SetText(text); return true; });

    /// <summary>
    /// Puts the user's own clipboard back. Flush hands the data to the OS so it
    /// survives texAi exiting; without it, the restored clipboard would go empty
    /// the moment the process ends, which is a nasty thing to do to someone who
    /// had something important copied.
    /// </summary>
    public static void TryRestore(IDataObject? data)
    {
        if (data is null)
        {
            return;
        }

        bool restored = Retry(() =>
        {
            Clipboard.SetDataObject(data, copy: true);
            return true;
        });

        if (restored)
        {
            Retry(() => { Clipboard.Flush(); return true; });
        }
    }

    /// <summary>
    /// Waits for a paste target to actually produce text after a synthetic
    /// Ctrl+C. There is no event for "the other app finished copying", so this
    /// polls until something lands or the deadline passes.
    /// </summary>
    public static async Task<string?> WaitForTextAsync(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            catch (ExternalException)
            {
                // Momentarily locked by another process; keep polling.
            }

            await Task.Delay(PollInterval);
        }

        return null;
    }

    private static T? Retry<T>(Func<T> operation)
    {
        for (int attempt = 0; attempt < Retries; attempt++)
        {
            try
            {
                return operation();
            }
            catch (ExternalException)
            {
                Thread.Sleep(RetryDelay);
            }
        }

        return default;
    }
}
