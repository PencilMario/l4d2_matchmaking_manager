namespace L4d2MatchmakingCore.Servers;

public sealed record TargetServerAddress(string Host, ushort Port)
{
    public string Endpoint => $"{Host}:{Port}";
}

public sealed record CreateTargetServerRequest(
    string Endpoint,
    bool RequiresReservation,
    int? Priority,
    int? MaxConcurrentWarmups,
    int? AttemptWindowSeconds,
    int? PlayerTarget,
    bool? Enabled);

public sealed record UpdateTargetServerRequest(
    string Endpoint,
    bool RequiresReservation,
    int? Priority,
    int? MaxConcurrentWarmups,
    int? AttemptWindowSeconds,
    int? PlayerTarget,
    bool? Enabled);

public sealed record TargetServerResponse(
    Guid Id,
    string Endpoint,
    bool RequiresReservation,
    int Priority,
    int MaxConcurrentWarmups,
    int AttemptWindowSeconds,
    int PlayerTarget,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
