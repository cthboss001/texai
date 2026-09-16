namespace texAi;

internal sealed record ErrorEntry(
    DateTime Timestamp,
    HotkeyAction Action,
    FailureKind Kind,
    string Description,
    string? Detail,
    string Model);

/// <summary>
/// A bounded in-memory record of what went wrong, so "it just showed a red dot"
/// can become "Ollama wasn't running at 14:52". Deliberately holds no user text:
/// only the action, the failure kind, and the model involved. Nothing is written
/// to disk, matching the rest of the app's no-persistence-of-content rule.
/// </summary>
internal static class ErrorLog
{
    private const int Capacity = 200;

    private static readonly object Gate = new();
    private static readonly Queue<ErrorEntry> Entries = new(Capacity);

    /// <summary>Raised on the logging thread, which is not necessarily the UI thread.</summary>
    public static event Action<ErrorEntry>? EntryAdded;

    public static void Record(HotkeyAction action, TransformOutcome outcome, string model)
    {
        if (outcome.Ok)
        {
            return;
        }

        var entry = new ErrorEntry(
            DateTime.Now,
            action,
            outcome.Kind,
            outcome.Describe(),
            outcome.Detail,
            model);

        lock (Gate)
        {
            if (Entries.Count == Capacity)
            {
                Entries.Dequeue();
            }

            Entries.Enqueue(entry);
        }

        EntryAdded?.Invoke(entry);
    }

    /// <summary>Newest first.</summary>
    public static IReadOnlyList<ErrorEntry> Snapshot()
    {
        lock (Gate)
        {
            return Entries.Reverse().ToList();
        }
    }

    public static ErrorEntry? Latest()
    {
        lock (Gate)
        {
            return Entries.Count == 0 ? null : Entries.Last();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
        }
    }
}
