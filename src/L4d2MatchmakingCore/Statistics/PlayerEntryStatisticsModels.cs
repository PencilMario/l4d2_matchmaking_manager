namespace L4d2MatchmakingCore.Statistics;

public sealed record PlayerEntryStatisticsQueryOptions(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string Granularity = "auto",
    string LobbyType = "all",
    string TargetMode = "all",
    Guid? TargetServerId = null,
    Guid? AgentId = null);

public sealed record PlayerEntryTrendBucket(
    DateTimeOffset BucketStartUtc,
    string Label,
    int Count);

public sealed record PlayerEntryDailyPatternBucket(
    int Hour,
    string Label,
    int Count);

public sealed record PlayerEntryAgentStatistics(
    Guid AgentId,
    string AgentName,
    IReadOnlyList<string> NameSnapshots,
    int Entries,
    double EntriesPerHour,
    DateTimeOffset? LastEntryAtUtc);

public sealed record PlayerEntryDownloadRegionStatistics(
    string? Key,
    string Label,
    int Entries,
    double EntriesPerHour);

public sealed record PlayerEntryStatisticsResult(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string TimeZone,
    string Granularity,
    int TotalEntries,
    double EntriesPerHour,
    IReadOnlyList<PlayerEntryTrendBucket> Trend,
    IReadOnlyList<PlayerEntryDailyPatternBucket> DailyPattern,
    IReadOnlyList<PlayerEntryAgentStatistics> Agents,
    IReadOnlyList<PlayerEntryDownloadRegionStatistics> DownloadRegions);
