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

    public static async Task RunAsync(HotkeyAction action)
    {
        IDataObject? original = TryGetClipboardData();

        if (!TryClearClipboard())
        {
            return;
        }

        InputSimulator.SendCtrlC();

        string? selected = await WaitForClipboardTextAsync(CaptureTimeout);
        if (string.IsNullOrWhiteSpace(selected))
        {
            TryRestoreClipboard(original);
            return;
        }

        string? result = await OllamaClient.TransformAsync(action, selected);
        if (string.IsNullOrWhiteSpace(result))
        {
            TryRestoreClipboard(original);
            return;
        }

        if (!TrySetClipboardText(result))
        {
            TryRestoreClipboard(original);
            return;
        }

        InputSimulator.SendCtrlV();
        await Task.Delay(250);
        TryRestoreClipboard(original);
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
