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
    string? RconPassword = null)
{
    public string? GameMode { get; init; }
}

public sealed record LobbyMemberSnapshot(string SteamId, string? PersonaName, string? AvatarUrl = null);

public static class LobbyMemberDataStatus
{
    public const string Complete = "complete";
    public const string MetadataOnlyNoQueryAgent = "metadata_only_no_query_agent";
    public const string MetadataOnlyJoinDenied = "metadata_only_join_denied";
    public const string MetadataOnlyJoinTimeout = "metadata_only_join_timeout";
    public const string MetadataOnlyAgentStateChanged = "metadata_only_agent_state_changed";
}

public sealed record LobbySnapshot(
    string LobbyId,
    string OwnerSteamId,
    IReadOnlyList<LobbyMemberSnapshot> Members,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset ObservedAt,
    string MemberDataStatus = LobbyMemberDataStatus.Complete);

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
    DateTimeOffset ObservedAt,
    string? CurrentDownloadRegion = null);
