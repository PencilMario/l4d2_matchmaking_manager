using System.Diagnostics;

public interface ISteamDesktopController
{
    Task RestartAsync(CancellationToken cancellationToken);
}

public sealed class SupervisorSteamDesktopController : ISteamDesktopController
{
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo("supervisorctl", "restart steam")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("steam_supervisor_start_failed");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException("steam_supervisor_restart_failed");
    }
}
