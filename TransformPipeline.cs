using System.Windows;
using texAi.Ui;

namespace texAi;

/// <summary>
/// Owns the capture -> transform -> replace pipeline. Every exit path either
/// restores the user's original clipboard or leaves their selected text
/// untouched; there is no path that overwrites the selection with an empty or
/// failed result.
/// </summary>
internal static class TransformPipeline
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PasteSettleDelay = TimeSpan.FromMilliseconds(250);

    public static async Task<TransformOutcome> RunAsync(HotkeyAction action)
    {
        // Read this before the indicator appears. The indicator is WS_EX_NOACTIVATE
        // so it should not take focus, but the window we blame for a failed copy
        // has to be the one the user was actually typing in.
        IntPtr target = NativeMethods.GetForegroundWindow();

        ProgressIndicatorWindow indicator = ProgressIndicatorWindow.Instance;
        indicator.ShowBeside(CaretLocator.GetAnchor());

        TransformOutcome outcome = await CaptureTransformReplaceAsync(action, target);

        if (outcome.Ok)
        {
            indicator.Succeed();
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
        IDataObject? original = ClipboardHelper.TryGetDataObject();

        if (!ClipboardHelper.TryClear())
        {
            return TransformOutcome.Failure(FailureKind.ClipboardLocked, "could not clear the clipboard to capture the selection");
        }

        // Waits for the user to let go of the hotkey chord before injecting,
        // so the target app sees a bare Ctrl+C. See InputSimulator.
        await InputSimulator.SendCtrlCAsync();

        string? selected = await ClipboardHelper.WaitForTextAsync(CaptureTimeout);
        if (string.IsNullOrWhiteSpace(selected))
        {
            ClipboardHelper.TryRestore(original);
            return ClassifyEmptyCapture(target);
        }

        TransformOutcome result = await OllamaClient.TransformAsync(action, selected);
        if (!result.Ok)
        {
            ClipboardHelper.TryRestore(original);
            return result;
        }

        if (!ClipboardHelper.TrySetText(result.Text!))
        {
            ClipboardHelper.TryRestore(original);
            return TransformOutcome.Failure(FailureKind.ClipboardLocked, "could not put the result on the clipboard to paste it");
        }

        await InputSimulator.SendCtrlVAsync();

        // Let the target app read the clipboard before it is taken away again.
        await Task.Delay(PasteSettleDelay);
        ClipboardHelper.TryRestore(original);

        HistoryStore.Add(action, selected, result.Text!);
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
}
