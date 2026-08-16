namespace L4d2LobbyAgent.Probe;

public sealed class ProbeStatusService(IProbeCommandRunner runner, ISteamDesktopDetector desktopDetector)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ProbeStatusResponse> GetAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var commandResult = await runner.RunAsync(cancellationToken);
            var desktopRunning = desktopDetector.IsRunning();
            var failure = desktopRunning ? commandResult.Failure : "steam_desktop_unavailable";
            var ready = desktopRunning && commandResult.Ready;

            return new ProbeStatusResponse(
                ready,
                failure,
                DateTimeOffset.UtcNow,
                new ProbeChecks(
                    desktopRunning ? "ok" : "failed",
                    GetSteamApiInitCheck(commandResult),
                    commandResult.AppId,
                    commandResult.Ready ? "ok" : "unknown",
                    commandResult.Ready ? "ok" : "unknown",
                    commandResult.Ready ? "ok" : "failed",
                    commandResult.LobbyCount));
        }
        finally
        {
            gate.Release();
        }
    }

    private static string GetSteamApiInitCheck(ProbeCommandResult commandResult)
    {
        if (commandResult.Ready)
            return "ok";

        return commandResult.Failure switch
        {
            "steam_api_load_failed" or "steam_api_init_failed" => "failed",
            "appid_mismatch" or "steam_not_logged_on" or "lobby_list_request_failed" or "lobby_list_callback_failed" or "lobby_list_timeout" => "ok",
            _ => "unknown"
        };
    }
}
