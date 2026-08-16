using L4d2Matchmaking.Contracts;

public interface ISteamSessionActor : IAsyncDisposable
{
    Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken);
    Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken);
    Task<AgentOperationSnapshot?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken);
    Task StopAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken);
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
    void LeaveLobby(ulong lobbyId);
    void PumpCallbacks();
}

internal sealed class RandomCampaignSelector : ICampaignSelector
{
    public CampaignProfile Select() => CampaignProfile.SelectRandom();
}
