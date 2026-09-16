namespace texAi;

/// <summary>
/// Everything that can go visibly wrong, whether or not a transform was
/// involved. Shared with <see cref="ErrorLog"/> so the dashboard has one list
/// to show rather than two.
/// </summary>
internal enum FailureKind
{
    None,

    /// <summary>RegisterHotKey was refused, almost always by another app owning the chord.</summary>
    HotkeyUnavailable,

    /// <summary>Synthetic Ctrl+C produced nothing and the target app is responsive.</summary>
    NoTextSelected,

    /// <summary>Another process held the clipboard open past every retry.</summary>
    ClipboardLocked,

    /// <summary>Ctrl+C produced nothing because the foreground window is hung.</summary>
    TargetAppNotResponding,

    OllamaUnreachable,
    ModelNotInstalled,
    HttpError,
    Timeout,
    EmptyResponse,
}

/// <summary>
/// What a transform attempt actually did. Replaces the old "string? or null"
/// return, where Ollama being down, the model being missing, a timeout, and
/// an empty selection were all indistinguishable to the caller and therefore
/// to the user, who only ever saw one red dot.
/// </summary>
/// <param name="Text">The rewritten text. Non-null exactly when <paramref name="Kind"/> is None.</param>
/// <param name="Detail">Human-readable specifics. Never contains the user's text.</param>
internal sealed record TransformOutcome(
    FailureKind Kind,
    string? Text = null,
    string? Detail = null,
    int? HttpStatus = null)
{
    public bool Ok => Kind == FailureKind.None;

    public static TransformOutcome Success(string text) => new(FailureKind.None, text);

    public static TransformOutcome Failure(FailureKind kind, string? detail = null, int? httpStatus = null) =>
        new(kind, Detail: detail, HttpStatus: httpStatus);

    /// <summary>One short line for the tray tooltip and the dashboard error list.</summary>
    public string Describe() => Kind switch
    {
        FailureKind.None => "OK",
        FailureKind.HotkeyUnavailable => $"Another app already owns that hotkey{Suffix()}",
        FailureKind.NoTextSelected => "Nothing was selected",
        FailureKind.ClipboardLocked => "Clipboard is locked by another app",
        FailureKind.TargetAppNotResponding => "The app you're in stopped responding",
        FailureKind.OllamaUnreachable => "Ollama isn't running",
        FailureKind.ModelNotInstalled => $"Model not installed{Suffix()}",
        FailureKind.HttpError => $"Ollama returned {HttpStatus?.ToString() ?? "an error"}{Suffix()}",
        FailureKind.Timeout => "Ollama took too long to answer",
        FailureKind.EmptyResponse => "The model returned nothing",
        _ => "Unknown failure",
    };

    private string Suffix() => string.IsNullOrWhiteSpace(Detail) ? string.Empty : $": {Detail}";
}
