using System.Runtime.InteropServices;
using System.Windows;

namespace texAi;

/// <summary>
/// Every clipboard call in texAi, in one place and retried.
///
/// The Win32 clipboard is a single global resource that any process can hold
/// open; OLE surfaces that contention as an ExternalException, and clipboard
/// managers, sync agents and remote desktop clients make that routine rather
/// than rare.
///
/// The retry budget used to be three attempts 30ms apart, so texAi gave up
/// after 90ms and reported "clipboard is locked". That is far too impatient:
/// a single rewrite failed this way during an otherwise clean run. Attempts
/// now back off over roughly a second, which is long enough to outlast a
/// clipboard manager's own read and still short enough that a genuinely stuck
/// clipboard does not leave the user watching a spinner.
/// </summary>
internal static class ClipboardHelper
{
    private static readonly int[] BackoffMs = [0, 25, 50, 90, 150, 220, 300, 400];
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// Copies the clipboard's contents out, format by format, rather than
    /// keeping the object Clipboard.GetDataObject hands back.
    ///
    /// That object is a live wrapper over whatever currently owns the clipboard.
    /// The pipeline clears the clipboard a moment later to capture the
    /// selection, which kills it, so restoring it afterwards put back nothing
    /// and left the user's clipboard empty. Reading every format up front costs
    /// a copy but is the only way to still have the data once it is gone.
    /// </summary>
    public static IDataObject? TryCapture() => Retry<IDataObject?>(() =>
    {
        IDataObject? live = Clipboard.GetDataObject();
        if (live is null)
        {
            return null;
        }

        var snapshot = new DataObject();
        int kept = 0;

        foreach (string format in live.GetFormats(autoConvert: false))
        {
            try
            {
                object? data = live.GetData(format, autoConvert: false);
                if (data is not null)
                {
                    snapshot.SetData(format, data);
                    kept++;
                }
            }
            catch (Exception ex) when (ex is ExternalException or NotSupportedException or OutOfMemoryException)
            {
                // Delay-rendered formats whose owner will not produce them on
                // demand. Skipping one is better than losing the rest.
            }
        }

        return kept > 0 ? snapshot : null;
    });

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
        foreach (int delay in BackoffMs)
        {
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }

            try
            {
                return operation();
            }
            catch (ExternalException)
            {
                // Someone else has the clipboard open. Wait longer and retry.
            }
        }

        return default;
    }
}
