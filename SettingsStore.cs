using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace texAi;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to %AppData%\texAi\settings.json.
///
/// Writes go to a temp file and then File.Replace, so a crash or a pulled power
/// cable during a save leaves either the old settings or the new ones, never a
/// half-written file that throws on next launch and looks like the app is broken.
/// </summary>
internal static class SettingsStore
{
    // The relaxed encoder, because this file is meant to be opened and edited.
    // The default one escapes the plus sign to its unicode form, which renders
    // every hotkey as an unreadable run of backslash-u sequences. That escaping
    // only earns its keep when the JSON is being embedded in HTML; this is a
    // local file read by one program.
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static AppSettings? _current;

    public static string FolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "texAi");

    public static string FilePath { get; } = Path.Combine(FolderPath, "settings.json");

    /// <summary>Raised after a successful save, so hotkeys and the tray can pick the change up.</summary>
    public static event Action<AppSettings>? Changed;

    public static AppSettings Current => _current ??= Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));

                if (loaded is not null)
                {
                    return _current = loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable settings file must not stop texAi starting.
            // Defaults are always usable, and the failure is worth showing.
            ErrorLog.Record(FailureKind.SettingsUnavailable, "Settings could not be read, so defaults are in use", ex.Message);
        }

        return _current = new AppSettings();
    }

    public static bool Save()
    {
        AppSettings settings = Current;

        try
        {
            Directory.CreateDirectory(FolderPath);

            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Format));

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temp, FilePath);
            }

            Changed?.Invoke(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Record(FailureKind.SettingsUnavailable, "Settings could not be saved", ex.Message);
            return false;
        }
    }
}
