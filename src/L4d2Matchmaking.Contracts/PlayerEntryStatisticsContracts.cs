namespace L4d2Matchmaking.Contracts;

public static class PlayerEntryStatisticsContract
{
    public const int MaxBatchSize = 100;

    public const string LobbyTypeStandard = "standard";
    public const string LobbyTypeReserved = "reserved";

    public const string GameModeCoop = "coop";
    public const string GameModeVersus = "versus";

    public static string NormalizeGameMode(string? gameMode)
    {
        return string.Equals(gameMode?.Trim(), GameModeCoop, StringComparison.OrdinalIgnoreCase)
            ? GameModeCoop
            : GameModeVersus;
    }

    public static string? NormalizeDownloadRegion(string? downloadRegion)
    {
        var normalized = downloadRegion?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return normalized.ToLowerInvariant() switch
        {
            "cng" or "shanghai" or "47" => "47",
            "hongkong" or "33" => "33",
            "qingdao" or "168" => "168",
            "tokyo" or "32" => "32",
            _ => normalized.ToLowerInvariant(),
        };
    }
}

public sealed record EntryStatisticsContext(
    Guid AgentId,
    string AgentNameSnapshot,
    string? ConfiguredDownloadRegionSnapshot,
    Guid TargetServerId,
    string TargetServerEndpointSnapshot,
    string? TargetServerNameSnapshot,
    string TargetModeSnapshot);

public sealed record PlayerEntryEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OperationId,
    string LobbyId,
    string LobbyType,
    Guid AgentId,
    string AgentNameSnapshot,
    string? DownloadRegionSnapshot,
    Guid TargetServerId,
    string TargetServerEndpointSnapshot,
    string? TargetServerNameSnapshot,
    string TargetModeSnapshot);

public sealed record PlayerEntryEventBatchRequest(IReadOnlyList<PlayerEntryEvent> Events);

public sealed record PlayerEntryEventBatchResponse(int Accepted, int Duplicates);

public interface IPlayerEntryEventSink
{
    bool TryEnqueue(PlayerEntryEvent entryEvent);
}
