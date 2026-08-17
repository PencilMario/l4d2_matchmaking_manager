namespace L4d2MatchmakingCore.Servers;

public sealed record TargetServerObservationResponse(
    Guid TargetServerId,
    string Status,
    string? ServerName,
    int? PlayerCount,
    int? MaxPlayers,
    DateTimeOffset? ObservedAt);
