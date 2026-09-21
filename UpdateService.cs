using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;

namespace texAi;

/// <summary>
/// Fully silent, no-prompt updates against GitHub Releases on cthboss001/texai.
///
/// Split into two halves on purpose. <see cref="CheckAndStageAsync"/> only ever
/// downloads: it runs from a background timer while texAi is doing real work, and
/// must never interrupt an in-flight rewrite or force-kill the process a user is
/// mid-selection in. <see cref="TryApplyPendingUpdate"/> only ever runs once, at
/// the very start of the next launch, before the tray icon or hotkeys exist to be
/// interrupted. That launch only happens because the user closed texAi themselves
/// (it lives in the tray, so "next restart" is a real gap, not immediate), which
/// is what makes the install silent without ever feeling like it hijacked a
/// session.
/// </summary>
internal static class UpdateService
{
    private const string Owner = "cthboss001";
    private const string Repo = "texai";
    private const string AssetSuffix = "Setup.exe";

    // Five minutes so the first check never competes with OllamaClient.WarmAsync
    // for bandwidth right at startup; twelve hours after that is frequent enough
    // that a release reaches a running install within a day, and nowhere close to
    // GitHub's 60-requests-per-hour unauthenticated limit for one client.
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>Separate client with no timeout, for the same reason ModelService keeps one for pulls: an installer download runs far longer than 30s on a slow link.</summary>
    private static readonly HttpClient Downloads = new()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    private static readonly string UpdateFolder = Path.Combine(SettingsStore.FolderPath, "updates");
    private static readonly string PendingInstallerPath = Path.Combine(UpdateFolder, "texAi-Setup.exe");

    private static DispatcherTimer? _timer;

    static UpdateService()
    {
        var userAgent = new ProductInfoHeaderValue("texAi-UpdateCheck", CurrentVersionString);
        Http.DefaultRequestHeaders.UserAgent.Add(userAgent);
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        Downloads.DefaultRequestHeaders.UserAgent.Add(userAgent);
    }

    /// <summary>Starts the background poll. Call once, after <see cref="TryApplyPendingUpdate"/> has confirmed there is nothing to apply.</summary>
    public static void Start()
    {
        _timer = new DispatcherTimer { Interval = InitialDelay };
        _timer.Tick += async (_, _) =>
        {
            _timer!.Interval = CheckInterval;
            await CheckAndStageAsync();
        };
        _timer.Start();
    }

    /// <summary>
    /// If a previous background check already staged an installer, runs it now.
    /// Returns true when an update was launched, which the caller must treat as
    /// "shut down immediately": the relaunch comes from the installer's own
    /// postinstall entry, not from this process continuing.
    /// </summary>
    public static bool TryApplyPendingUpdate()
    {
        if (!File.Exists(PendingInstallerPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(PendingInstallerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            // A half-downloaded or corrupt installer must not block startup, and
            // must not be retried forever: delete it so the next background
            // check downloads a fresh copy instead of tripping on this one again.
            TryDelete(PendingInstallerPath);
            return false;
        }
    }

    /// <summary>Checks the latest release and downloads it if newer. Never applies it: see the type-level comment for why.</summary>
    public static async Task CheckAndStageAsync()
    {
        try
        {
            if (File.Exists(PendingInstallerPath))
            {
                return;
            }

            using HttpResponseMessage response =
                await Http.GetAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement root = doc.RootElement;

            string? tag = root.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() : null;
            if (tag is null || !IsNewer(tag))
            {
                return;
            }

            string? assetUrl = FindInstallerAssetUrl(root);
            if (assetUrl is not null)
            {
                await DownloadAsync(assetUrl);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // Opportunistic, same as OllamaClient.WarmAsync: a flaky GitHub call
            // is not worth surfacing as an app error, and the next scheduled
            // check simply tries again in twelve hours.
        }
    }

    private static string? FindInstallerAssetUrl(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets))
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
            if (name is not null && name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() : null;
            }
        }

        return null;
    }

    private static async Task DownloadAsync(string url)
    {
        Directory.CreateDirectory(UpdateFolder);
        string temp = PendingInstallerPath + ".tmp";

        using (HttpResponseMessage response = await Downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            await using (FileStream file = File.Create(temp))
            {
                await response.Content.CopyToAsync(file);
            }
        }

        if (!LooksLikeAnExe(temp))
        {
            TryDelete(temp);
            return;
        }

        // Written under a temp name and renamed into place only once complete,
        // the same crash-safety shape as SettingsStore.Save: TryApplyPendingUpdate
        // can only ever see a whole installer, never a half-downloaded one.
        File.Move(temp, PendingInstallerPath, overwrite: true);
    }

    /// <summary>Cheap sanity check against GitHub serving an HTML error page with a 200 status: real Windows executables start with the "MZ" header.</summary>
    private static bool LooksLikeAnExe(string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);
            if (file.Length < 1024 * 1024)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[2];
            return file.Read(header) == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool IsNewer(string tag) =>
        TryParseVersion(tag, out Version? remote) &&
        TryParseVersion(CurrentVersionString, out Version? current) &&
        remote! > current!;

    // The SDK appends "+<git commit sha>" to AssemblyInformationalVersion by
    // default (confirmed by building: ProductVersion came out as
    // "2.0.1+0a5de01..."), which Version.TryParse rejects outright. Split at
    // '+' first, same as trimming semver build metadata.
    private static bool TryParseVersion(string tag, out Version? version) =>
        Version.TryParse(tag.TrimStart('v', 'V').Split('+')[0], out version);

    // AssemblyInformationalVersion, not AssemblyVersion: <Version>2.0.1</Version>
    // in texAi.csproj produces an informational version starting with "2.0.1",
    // which is the same three-part shape as installer\texai.iss's MyAppVersion
    // and a release tag like "v2.0.1". AssemblyVersion pads a fourth ".0", and
    // comparing a three-part Version against a four-part one always ranks the
    // shorter one lower even when they mean the same release.
    private static string CurrentVersionString =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
