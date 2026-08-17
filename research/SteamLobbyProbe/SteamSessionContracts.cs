using L4d2Matchmaking.Contracts;

public interface ISteamSessionActor : IAsyncDisposable
{
    Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken);
    Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken);
    Task<AgentOperationSnapshot?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken);
    Task StopAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken);
    Task<LobbySnapshot> QueryLobbyAsync(ulong lobbyId, bool includeMembers, CancellationToken cancellationToken);
}

internal interface ICampaignSelector
{
    CampaignProfile Select();
}

internal interface ISteamNativeRuntime : IDisposable
{
    AgentHealthSnapshot ObserveHealth();
    LobbySnapshot CreateLobby(AgentOperationRequest request, CampaignProfile profile);
    LobbySnapshot ReadLobby(ulong lobbyId);
    NativeLobbyJoinResult JoinLobby(ulong lobbyId);
    ulong GetCurrentSteamId();
    bool IsCurrentUserLobbyMember(ulong lobbyId, ulong steamId);
    void LeaveLobby(ulong lobbyId);
    void PumpCallbacks();
}

internal enum NativeLobbyJoinResult
{
    Success,
    Denied,
    Timeout,
}

internal sealed class RandomCampaignSelector : ICampaignSelector
{
    public CampaignProfile Select() => CampaignProfile.SelectRandom();
}
