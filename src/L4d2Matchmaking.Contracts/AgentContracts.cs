namespace L4d2Matchmaking.Contracts;

public enum AgentLobbyMode
{
    Standard,
    Reserved,
}

public sealed record AgentOperationRequest(
    Guid OperationId,
    AgentLobbyMode Mode,
    string Ipv4Address,
    ushort Port,
    string? RconPassword = null);

public sealed record LobbyMemberSnapshot(string SteamId, string? PersonaName);

public sealed record LobbySnapshot(
    string LobbyId,
    string OwnerSteamId,
    IReadOnlyList<LobbyMemberSnapshot> Members,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset ObservedAt);

public sealed record AgentOperationSnapshot(
    Guid OperationId,
    string State,
    LobbySnapshot? Lobby,
    string? Failure,
    DateTimeOffset ObservedAt);

public sealed record AgentOperationStartResult(
    AgentOperationSnapshot Operation,
    bool AlreadyExists);

public sealed record AgentHealthSnapshot(
    bool Ready,
    string? Failure,
    DateTimeOffset ObservedAt);
