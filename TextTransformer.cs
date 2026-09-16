using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace texAi;

/// <summary>
/// Owns the capture -> transform -> replace pipeline. Every exit path
/// either restores the user's original clipboard or leaves their
/// selected text untouched; there is no path that overwrites the
/// selection with an empty or failed result.
/// </summary>
internal static class TextTransformer
{
    private const int ClipboardRetries = 3;
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(1200);

    public static async Task<TransformOutcome> RunAsync(HotkeyAction action)
    {
        // Read this before the indicator appears. The indicator is WS_EX_NOACTIVATE
        // so it should not take focus, but the window we blame for a failed copy
        // has to be the one the user was actually typing in.
        IntPtr target = NativeMethods.GetForegroundWindow();

        var indicator = new ProgressIndicator();
        indicator.ShowNear(CaretLocator.GetAnchorPoint());

        TransformOutcome outcome = await CaptureTransformReplaceAsync(action, target);

        if (outcome.Ok)
        {
            indicator.Complete();
        }
        else
        {
            indicator.Fail();
            ErrorLog.Record(action, outcome, Config.ModelName);
        }

        return outcome;
    }

    private static async Task<TransformOutcome> CaptureTransformReplaceAsync(HotkeyAction action, IntPtr target)
    {
        IDataObject? original = TryGetClipboardData();

        if (!TryClearClipboard())
        {
            return TransformOutcome.Failure(FailureKind.ClipboardLocked, "could not clear the clipboard to capture the selection");
        }

        // Waits for the user to let go of the hotkey chord before injecting,
        // so the target app sees a bare Ctrl+C. See InputSimulator.
        await InputSimulator.SendCtrlCAsync();

        string? selected = await WaitForClipboardTextAsync(CaptureTimeout);
        if (string.IsNullOrWhiteSpace(selected))
        {
            TryRestoreClipboard(original);
            return ClassifyEmptyCapture(target);
        }

        TransformOutcome result = await OllamaClient.TransformAsync(action, selected);
        if (!result.Ok)
        {
            TryRestoreClipboard(original);
            return result;
        }

        if (!TrySetClipboardText(result.Text!))
        {
            TryRestoreClipboard(original);
            return TransformOutcome.Failure(FailureKind.ClipboardLocked, "could not put the result on the clipboard to paste it");
        }

        await InputSimulator.SendCtrlVAsync();
        await Task.Delay(250);
        TryRestoreClipboard(original);
        return result;
    }

    /// <summary>
    /// Nothing arrived on the clipboard. Usually that means nothing was selected,
    /// but a wedged app also swallows Ctrl+C, and telling the user to select some
    /// text when the real problem is that Word is frozen is worse than saying
    /// nothing at all.
    /// </summary>
    private static TransformOutcome ClassifyEmptyCapture(IntPtr target)
    {
        bool hung = target != IntPtr.Zero && NativeMethods.IsHungAppWindow(target);

        return hung
            ? TransformOutcome.Failure(FailureKind.TargetAppNotResponding)
            : TransformOutcome.Failure(FailureKind.NoTextSelected);
    }

    private static async Task<string?> WaitForClipboardTextAsync(TimeSpan timeout)
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
                // Clipboard momentarily locked by another process; keep polling.
            }

            await Task.Delay(40);
        }

        return null;
    }

    private static IDataObject? TryGetClipboardData()
    {
        for (int i = 0; i < ClipboardRetries; i++)
        {
            try
            {
                return Clipboard.GetDataObject();
            }
            catch (ExternalException)
            {
                Thread.Sleep(ClipboardRetryDelay);
            }
        }

        return null;
    }

    private static bool TryClearClipboard()
    {
        for (int i = 0; i < ClipboardRetries; i++)
        {
            try
            {
                Clipboard.Clear();
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(ClipboardRetryDelay);
            }
        }

        return false;
    }

    private static bool TrySetClipboardText(string text)
    {
        for (int i = 0; i < ClipboardRetries; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(ClipboardRetryDelay);
            }
        }

        return false;
    }

    private static void TryRestoreClipboard(IDataObject? data)
    {
        if (data is null)
        {
            return;
        }

        for (int i = 0; i < ClipboardRetries; i++)
        {
            try
            {
                Clipboard.SetDataObject(data, true);
                return;
            }
            catch (ExternalException)
            {
                Thread.Sleep(ClipboardRetryDelay);
            }
        }
    }
}
