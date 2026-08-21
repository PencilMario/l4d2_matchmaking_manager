namespace L4d2LobbyAgent.Probe;

public interface IProbeCommandRunner
{
    Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken);
}

public interface IProbeProcessLauncher
{
    Task<ProbeProcessOutput> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public interface ISteamDesktopDetector
{
    bool IsRunning();
}

public interface ISteamDownloadRegionReader
{
    string? Read();
}

public interface ICefGuardStatusReader
{
    DateTimeOffset? ReadAppliedAt();
}

public sealed class FileCefGuardStatusReader(string path) : ICefGuardStatusReader
{
    public static FileCefGuardStatusReader FromEnvironment()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("USER_HOME")
            ?? Environment.GetEnvironmentVariable("HOME")
            ?? "/home/default";
        return new FileCefGuardStatusReader(Environment.GetEnvironmentVariable("STEAM_CEF_GUARD_APPLIED_MARKER")
            ?? Path.Combine(homeDirectory, ".steam", "steam", "config", "l4d2-cef-guard-applied"));
    }

    public DateTimeOffset? ReadAppliedAt()
    {
        try
        {
            var value = File.ReadAllText(path).Trim();
            if (!long.TryParse(value, out var unixTimestamp))
                return null;

            return unixTimestamp >= 100_000_000_000L || unixTimestamp <= -100_000_000_000L
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp)
                : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

public sealed class SteamDownloadRegionReader : ISteamDownloadRegionReader
{
    private static readonly System.Text.RegularExpressions.Regex RegionEntry = new(
        "^\\s*\\\"DownloadRegion\\\"\\s+\\\"(?<region>[^\\\"]*)\\\"",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly string statePath;
    private readonly string legacyConfigPath;

    public SteamDownloadRegionReader(string statePath, string legacyConfigPath)
    {
        this.statePath = statePath;
        this.legacyConfigPath = legacyConfigPath;
    }

    public static SteamDownloadRegionReader FromEnvironment()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("USER_HOME")
            ?? Environment.GetEnvironmentVariable("HOME")
            ?? "/home/default";
        var configDirectory = Path.Combine(homeDirectory, ".steam", "steam", "config");
        return new(
            Environment.GetEnvironmentVariable("STEAM_DOWNLOAD_REGION_STATUS_FILE")
                ?? Path.Combine(configDirectory, "steam-download-region"),
            Path.Combine(configDirectory, "config.vdf"));
    }

    public string? Read()
    {
        var state = ReadStateFile();
        return state ?? ReadLegacyConfig();
    }

    private string? ReadStateFile()
    {
        try
        {
            var value = File.ReadAllText(statePath).Trim();
            return int.TryParse(value, out var regionId) && regionId >= 0
                ? regionId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private string? ReadLegacyConfig()
    {
        try
        {
            foreach (var line in File.ReadLines(legacyConfigPath))
            {
                var match = RegionEntry.Match(line);
                if (match.Success)
                    return string.IsNullOrWhiteSpace(match.Groups["region"].Value)
                        ? null
                        : match.Groups["region"].Value;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }
}

public sealed record ProbeCommandResult(
    bool Ready,
    string? Failure,
    uint? AppId,
    uint? LobbyCount)
{
    public static ProbeCommandResult Success(uint appId, uint lobbyCount) =>
        new(true, null, appId, lobbyCount);
}

public sealed record ProbeProcessOutput(int ExitCode, string StandardOutput, bool TimedOut = false);

public sealed record ProbeCommandOptions(
    string ExecutablePath,
    string SteamApiLibraryPath,
    TimeSpan Timeout)
{
    public static ProbeCommandOptions FromEnvironment()
    {
        var timeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("PROBE_TIMEOUT_SECONDS"), out var configuredSeconds)
            ? Math.Clamp(configuredSeconds, 1, 60)
            : 10;
        return new ProbeCommandOptions(
            Environment.GetEnvironmentVariable("STEAM_LOBBY_PROBE_PATH") ?? string.Empty,
            Environment.GetEnvironmentVariable("STEAM_API_LIBRARY_PATH") ?? string.Empty,
            TimeSpan.FromSeconds(timeoutSeconds));
    }
}

public sealed record ProbeChecks(
    string SteamDesktop,
    string SteamApiInit,
    uint? AppId,
    string LoggedOn,
    string ManualDispatch,
    string LobbyListCallback,
    uint? LobbyCount);

public sealed record ProbeStatusResponse(
    bool Ready,
    string? Failure,
    DateTimeOffset ObservedAt,
    ProbeChecks Checks,
    string? CurrentDownloadRegion = null,
    DateTimeOffset? CefGuardAppliedAt = null);
