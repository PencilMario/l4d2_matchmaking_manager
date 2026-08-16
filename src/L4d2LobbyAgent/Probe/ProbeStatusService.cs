using L4d2Matchmaking.Contracts;

namespace L4d2LobbyAgent.Probe;

public sealed class ProbeStatusService(IAgentSteamSessionService sessionService, ISteamDesktopDetector desktopDetector)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ProbeStatusResponse> GetAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var health = await sessionService.ObserveHealthAsync(cancellationToken);
            var desktopRunning = desktopDetector.IsRunning();
            var failure = desktopRunning ? health.Failure : "steam_desktop_unavailable";
            var ready = desktopRunning && health.Ready;

            return new ProbeStatusResponse(
                ready,
                failure,
                DateTimeOffset.UtcNow,
                new ProbeChecks(
                    desktopRunning ? "ok" : "failed",
                    GetSteamApiInitCheck(health),
                    GetAppId(health),
                    health.Ready ? "ok" : "unknown",
                    GetLoggedOnCheck(health),
                    health.Ready ? "ok" : "failed",
                    null));
        }
        finally
        {
            gate.Release();
        }
    }

    private static string GetSteamApiInitCheck(AgentHealthSnapshot health)
    {
        if (health.Ready)
            return "ok";

        return health.Failure switch
        {
            "steam_api_load_failed" or "steam_api_init_failed" => "failed",
            "appid_mismatch" or "steam_not_logged_on" or "lobby_list_request_failed" or "lobby_list_callback_failed" or "lobby_list_timeout" => "ok",
            _ => "unknown"
        };
    }

    private static uint? GetAppId(AgentHealthSnapshot health) =>
        health.Failure is "steam_api_load_failed" or "steam_api_init_failed" or "appid_mismatch"
            ? null
            : 550;

    private static string GetLoggedOnCheck(AgentHealthSnapshot health) => health.Failure switch
    {
        "steam_not_logged_on" => "failed",
        null when health.Ready => "ok",
        _ => "unknown",
    };
}
