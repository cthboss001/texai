using System.Collections.ObjectModel;
using System.Windows;

namespace texAi;

internal sealed record HistoryEntry(DateTime Timestamp, HotkeyAction Action, string Before, string After);

/// <summary>
/// The current session's rewrites, newest first, capped at 50.
///
/// In memory only, and never serialised. texAi's whole claim is that what you
/// select stays on your machine, and a history file sitting in AppData would
/// quietly undo that: it would outlive the session, land in backups, and be
/// readable by anything running as you. The cost is that closing texAi loses
/// the list, which is the right side of that trade.
/// </summary>
internal static class HistoryStore
{
    private const int Capacity = 50;

    public static ObservableCollection<HistoryEntry> Entries { get; } = [];

    public static void Add(HotkeyAction action, string before, string after)
    {
        var entry = new HistoryEntry(DateTime.Now, action, before, after);

        // The pipeline runs on the Dispatcher thread today, but a background
        // caller mutating a bound ObservableCollection throws, so normalise here
        // rather than relying on that staying true.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Entries.Insert(0, entry);

            while (Entries.Count > Capacity)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        });
    }

    public static HistoryEntry? Latest(HotkeyAction action) =>
        Entries.FirstOrDefault(e => e.Action == action);

    public static void Clear() => Application.Current?.Dispatcher.Invoke(Entries.Clear);
}
