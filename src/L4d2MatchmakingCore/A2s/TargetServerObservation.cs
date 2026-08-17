namespace L4d2MatchmakingCore.A2s;

public sealed record TargetServerObservation(
    Guid TargetServerId,
    string Status,
    string? ServerName,
    int? PlayerCount,
    int? MaxPlayers,
    DateTimeOffset? ObservedAt);
