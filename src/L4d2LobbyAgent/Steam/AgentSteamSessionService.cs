using L4d2Matchmaking.Contracts;

public interface IAgentSteamSessionService
{
    Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken);
    Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken);
    Task<AgentOperationSnapshot?> GetAsync(Guid operationId, CancellationToken cancellationToken);
    Task<bool> StopAsync(Guid operationId, CancellationToken cancellationToken);
    Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken);
    Task<LobbySnapshot> QueryLobbyAsync(ulong lobbyId, bool includeMembers, CancellationToken cancellationToken);
}

public sealed class AgentSteamSessionService(ISteamSessionActor actor) : IAgentSteamSessionService
{
    public Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken) =>
        actor.ObserveHealthAsync(cancellationToken);

    public Task<AgentOperationStartResult> StartAsync(
        AgentOperationRequest request,
        CancellationToken cancellationToken) =>
        actor.StartAsync(request, cancellationToken);

    public Task<AgentOperationSnapshot?> GetAsync(Guid operationId, CancellationToken cancellationToken) =>
        actor.GetOperationAsync(operationId, cancellationToken);

    public async Task<bool> StopAsync(Guid operationId, CancellationToken cancellationToken)
    {
        if (await actor.GetOperationAsync(operationId, cancellationToken).ConfigureAwait(false) is null)
            return false;

        await actor.StopAsync(operationId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken) =>
        actor.ReadLobbyAsync(lobbyId, cancellationToken);

    public Task<LobbySnapshot> QueryLobbyAsync(ulong lobbyId, bool includeMembers, CancellationToken cancellationToken) =>
        actor.QueryLobbyAsync(lobbyId, includeMembers, cancellationToken);
}
