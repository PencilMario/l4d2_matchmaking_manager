using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Statistics;

public sealed class PlayerEntryStatisticsQuery(
    MatchmakingDbContext dbContext,
    Func<DateTimeOffset>? utcNow = null)
{
    public const string TimeZoneId = "Asia/Shanghai";
    private static readonly TimeZoneInfo ShanghaiTimeZone = FindShanghaiTimeZone();
    private static readonly string[] Granularities = ["auto", "hour", "day", "week"];
    private static readonly string[] LobbyTypes = ["all", "standard", "reserved"];
    private static readonly string[] TargetModes = ["all", "coop", "versus"];

    public async Task<PlayerEntryStatisticsResult> QueryAsync(
        PlayerEntryStatisticsQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var now = (utcNow?.Invoke() ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var (fromUtc, toUtc) = NormalizeRange(options, now);
        var granularity = NormalizeGranularity(options.Granularity, fromUtc, toUtc);
        ValidateBucketCount(fromUtc, toUtc, granularity);
        var lobbyType = NormalizeDimension(options.LobbyType, LobbyTypes, "lobby_type_invalid");
        var targetMode = NormalizeDimension(options.TargetMode, TargetModes, "target_mode_invalid");

        var query = dbContext.PlayerEntryEvents
            .AsNoTracking()
            .Where(entry => entry.OccurredAtUtc >= fromUtc && entry.OccurredAtUtc < toUtc);
        if (lobbyType != "all")
            query = query.Where(entry => entry.LobbyType == lobbyType);
        if (targetMode == PlayerEntryStatisticsContract.GameModeCoop)
            query = query.Where(entry => entry.TargetModeSnapshot == PlayerEntryStatisticsContract.GameModeCoop);
        else if (targetMode == PlayerEntryStatisticsContract.GameModeVersus)
            query = query.Where(entry => entry.TargetModeSnapshot == PlayerEntryStatisticsContract.GameModeVersus ||
                entry.TargetModeSnapshot == null || entry.TargetModeSnapshot == "");
        if (options.TargetServerId is { } targetServerId)
            query = query.Where(entry => entry.TargetServerId == targetServerId);
        if (options.AgentId is { } agentId)
            query = query.Where(entry => entry.AgentId == agentId);

        // Keep the potentially large event set in the database. The UTC-hour groups are enough to
        // reconstruct both Shanghai trend buckets and the fixed 0-23 daily pattern without loading
        // every event into Core memory.
        var totalEntries = await query.CountAsync(cancellationToken);
        var hourAggregates = await query
            .GroupBy(entry => new
            {
                Year = entry.OccurredAtUtc.Year,
                Month = entry.OccurredAtUtc.Month,
                Day = entry.OccurredAtUtc.Day,
                Hour = entry.OccurredAtUtc.Hour,
            })
            .Select(group => new EventHourAggregate(
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Key.Hour,
                group.Count()))
            .ToListAsync(cancellationToken);
        var agentAggregates = await query
            .GroupBy(entry => new { entry.AgentId, entry.AgentNameSnapshot })
            .Select(group => new EventAgentAggregate(
                group.Key.AgentId,
                group.Key.AgentNameSnapshot,
                group.Count(),
                group.Max(entry => entry.OccurredAtUtc)))
            .ToListAsync(cancellationToken);
        var regionAggregates = await query
            .GroupBy(entry => entry.DownloadRegionSnapshot)
            .Select(group => new EventRegionAggregate(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var currentAgentsQuery = dbContext.WarmupAgents
            .AsNoTracking()
            .Select(agent => new CurrentAgentRow(agent.Id, agent.Name));
        if (options.AgentId is { } selectedAgentId)
            currentAgentsQuery = currentAgentsQuery.Where(agent => agent.AgentId == selectedAgentId);
        var currentAgents = await currentAgentsQuery.ToListAsync(cancellationToken);
        var hours = Math.Max((toUtc - fromUtc).TotalHours, double.Epsilon);
        var trend = BuildTrend(hourAggregates, fromUtc, toUtc, granularity);
        var dailyPattern = Enumerable.Range(0, 24)
            .Select(hour => new PlayerEntryDailyPatternBucket(
                hour,
                $"{hour:00}:00",
                hourAggregates
                    .Where(entry => ToShanghai(entry.UtcHour).Hour == hour)
                    .Sum(entry => entry.Count)))
            .ToArray();
        var agents = BuildAgentRows(agentAggregates, currentAgents, hours);
        var regions = BuildRegionRows(regionAggregates, hours);

        return new PlayerEntryStatisticsResult(
            fromUtc,
            toUtc,
            TimeZoneId,
            granularity,
            totalEntries,
            totalEntries / hours,
            trend,
            dailyPattern,
            agents,
            regions);
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeRange(
        PlayerEntryStatisticsQueryOptions options,
        DateTimeOffset now)
    {
        var from = (options.FromUtc ?? now.AddHours(-24)).ToUniversalTime();
        var to = (options.ToUtc ?? now).ToUniversalTime();
        if (from >= to)
            throw new PlayerEntryStatisticsQueryValidationException("player_entry_statistics_range_empty");
        if (from < now.AddDays(-180) || to - from > TimeSpan.FromDays(180))
            throw new PlayerEntryStatisticsQueryValidationException("player_entry_statistics_range_too_old");
        if (to > now.AddMinutes(5))
            throw new PlayerEntryStatisticsQueryValidationException("player_entry_statistics_range_in_future");
        return (from, to);
    }

    private static string NormalizeGranularity(string? value, DateTimeOffset from, DateTimeOffset to)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant();
        if (!Granularities.Contains(normalized, StringComparer.Ordinal))
            throw new PlayerEntryStatisticsQueryValidationException("player_entry_statistics_granularity_invalid");
        if (normalized == "auto")
        {
            var duration = to - from;
            normalized = duration <= TimeSpan.FromHours(48)
                ? "hour"
                : duration <= TimeSpan.FromDays(31)
                    ? "day"
                    : "week";
        }
        return normalized;
    }

    private static string NormalizeDimension(string? value, IReadOnlyCollection<string> allowed, string error)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
            throw new PlayerEntryStatisticsQueryValidationException($"player_entry_statistics_{error}");
        return normalized;
    }

    private static void ValidateBucketCount(DateTimeOffset fromUtc, DateTimeOffset toUtc, string granularity)
    {
        var count = CountBuckets(
            ToShanghai(fromUtc).DateTime,
            ToShanghai(toUtc).DateTime,
            granularity);
        if (count > 5000)
            throw new PlayerEntryStatisticsQueryValidationException("player_entry_statistics_bucket_limit_exceeded");
    }

    private static int CountBuckets(DateTime localFrom, DateTime localTo, string granularity)
    {
        var bucket = StartOfBucket(localFrom, granularity);
        var count = 0;
        while (bucket < localTo)
        {
            count++;
            bucket = NextBucket(bucket, granularity);
        }
        return count;
    }

    private static IReadOnlyList<PlayerEntryTrendBucket> BuildTrend(
        IReadOnlyList<EventHourAggregate> hourAggregates,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string granularity)
    {
        var localFrom = ToShanghai(fromUtc).DateTime;
        var localTo = ToShanghai(toUtc).DateTime;
        var bucket = StartOfBucket(localFrom, granularity);
        var counts = new Dictionary<DateTimeOffset, int>();
        foreach (var aggregate in hourAggregates)
        {
            var localHour = ToShanghai(aggregate.UtcHour).DateTime;
            var bucketUtc = ToUtc(StartOfBucket(localHour, granularity));
            counts[bucketUtc] = counts.GetValueOrDefault(bucketUtc) + aggregate.Count;
        }
        var result = new List<PlayerEntryTrendBucket>();
        while (bucket < localTo)
        {
            var next = NextBucket(bucket, granularity);
            var startUtc = ToUtc(bucket);
            var count = counts.GetValueOrDefault(startUtc);
            result.Add(new PlayerEntryTrendBucket(startUtc, FormatBucket(bucket, granularity), count));
            bucket = next;
        }
        return result;
    }

    private static IReadOnlyList<PlayerEntryAgentStatistics> BuildAgentRows(
        IReadOnlyList<EventAgentAggregate> eventAggregates,
        IReadOnlyList<CurrentAgentRow> currentAgents,
        double hours)
    {
        var rows = eventAggregates
            .GroupBy(entry => entry.AgentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var agent in currentAgents)
            rows.TryAdd(agent.AgentId, []);
        return rows.Select(pair =>
        {
            var eventRows = pair.Value;
            var currentName = currentAgents.FirstOrDefault(agent => agent.AgentId == pair.Key)?.Name;
            var snapshots = eventRows.Select(entry => entry.AgentNameSnapshot)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var name = eventRows.OrderByDescending(entry => entry.LastEntryAtUtc)
                .Select(entry => entry.AgentNameSnapshot)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? currentName
                ?? pair.Key.ToString();
            var entries = eventRows.Sum(entry => entry.Count);
            return new PlayerEntryAgentStatistics(
                pair.Key,
                name,
                snapshots,
                entries,
                entries / hours,
                eventRows.Length == 0 ? null : eventRows.Max(entry => entry.LastEntryAtUtc));
        }).OrderByDescending(row => row.Entries).ThenBy(row => row.AgentName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<PlayerEntryDownloadRegionStatistics> BuildRegionRows(
        IReadOnlyList<EventRegionAggregate> eventAggregates,
        double hours)
    {
        return eventAggregates
            .GroupBy(entry => PlayerEntryStatisticsContract.NormalizeDownloadRegion(entry.DownloadRegionSnapshot))
            .Select(group => new PlayerEntryDownloadRegionStatistics(
                group.Key,
                group.Key is null ? "默认" : group.Key,
                group.Sum(entry => entry.Count),
                group.Sum(entry => entry.Count) / hours))
            .OrderByDescending(row => row.Entries)
            .ThenBy(row => row.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static DateTime StartOfBucket(DateTime value, string granularity) => granularity switch
    {
        "hour" => FloorHour(value),
        "day" => value.Date,
        "week" => StartOfWeek(value),
        _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
    };

    private static DateTime NextBucket(DateTime value, string granularity) => granularity switch
    {
        "hour" => value.AddHours(1),
        "day" => value.AddDays(1),
        "week" => value.AddDays(7),
        _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
    };

    private static string FormatBucket(DateTime value, string granularity) => granularity switch
    {
        "hour" => value.ToString("MM-dd HH:00", System.Globalization.CultureInfo.InvariantCulture),
        "day" => value.ToString("MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        "week" => $"{value:yyyy-MM-dd} 周",
        _ => value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static DateTime FloorHour(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, 0, 0);

    private static DateTime StartOfWeek(DateTime value)
    {
        var date = value.Date;
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static DateTimeOffset ToUtc(DateTime local) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ShanghaiTimeZone));

    private static DateTimeOffset ToShanghai(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc.ToUniversalTime(), ShanghaiTimeZone);

    private static TimeZoneInfo FindShanghaiTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
    }

    private sealed record EventHourAggregate(int Year, int Month, int Day, int Hour, int Count)
    {
        public DateTimeOffset UtcHour => new(new DateTime(Year, Month, Day, Hour, 0, 0, DateTimeKind.Utc));
    }

    private sealed record EventAgentAggregate(
        Guid AgentId,
        string AgentNameSnapshot,
        int Count,
        DateTimeOffset LastEntryAtUtc);

    private sealed record EventRegionAggregate(string? DownloadRegionSnapshot, int Count);

    private sealed record CurrentAgentRow(Guid AgentId, string Name);

}

public sealed class PlayerEntryStatisticsQueryValidationException(string code) : InvalidOperationException(code);
