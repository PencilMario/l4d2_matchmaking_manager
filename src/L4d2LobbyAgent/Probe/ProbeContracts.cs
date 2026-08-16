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
    ProbeChecks Checks);
