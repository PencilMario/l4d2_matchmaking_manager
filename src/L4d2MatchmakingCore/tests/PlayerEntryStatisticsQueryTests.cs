using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class PlayerEntryStatisticsQueryTests
{
    [TestMethod]
    public async Task QueryUsesShanghaiBucketsAndActualHourDenominator()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        db.WarmupAgents.Add(new WarmupAgent
        {
            Id = agentId,
            Name = "current-agent",
            DownloadRegion = "47",
            NoVncPort = 18083,
        });
        db.PlayerEntryEvents.AddRange(
            Event(agentId, targetId, DateTimeOffset.Parse("2026-08-23T16:45:00Z")),
            Event(agentId, targetId, DateTimeOffset.Parse("2026-08-23T17:05:00Z")),
            Event(agentId, targetId, DateTimeOffset.Parse("2026-08-23T23:30:00Z")));
        await db.SaveChangesAsync();

        var now = DateTimeOffset.Parse("2026-08-24T00:30:00Z");
        var result = await new PlayerEntryStatisticsQuery(db, () => now).QueryAsync(
            new PlayerEntryStatisticsQueryOptions(
                DateTimeOffset.Parse("2026-08-23T16:30:00Z"),
                now,
                "hour"));

        Assert.AreEqual(3, result.TotalEntries);
        Assert.AreEqual(3d / 8d, result.EntriesPerHour, 0.000001);
        Assert.AreEqual("Asia/Shanghai", result.TimeZone);
        Assert.AreEqual(9, result.Trend.Count);
        Assert.AreEqual("08-24 00:00", result.Trend[0].Label);
        Assert.AreEqual(1, result.Trend[0].Count);
        Assert.AreEqual(1, result.Trend[1].Count);
        Assert.AreEqual(1, result.Trend[7].Count);
        Assert.IsTrue(result.Trend.Where(bucket => bucket.Count == 0).Any());
        Assert.AreEqual(24, result.DailyPattern.Count);
        Assert.AreEqual(1, result.DailyPattern[0].Count);
        Assert.AreEqual(1, result.DailyPattern[1].Count);
        Assert.AreEqual(1, result.DailyPattern[7].Count);
    }

    [TestMethod]
    public async Task WeekTrendSplitsAtMondayMidnightInShanghai()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        db.PlayerEntryEvents.AddRange(
            Event(agentId, targetId, DateTimeOffset.Parse("2026-08-23T15:45:00Z")),
            Event(agentId, targetId, DateTimeOffset.Parse("2026-08-23T16:15:00Z")));
        await db.SaveChangesAsync();

        var result = await new PlayerEntryStatisticsQuery(
                db,
                () => DateTimeOffset.Parse("2026-08-23T16:30:00Z"))
            .QueryAsync(new PlayerEntryStatisticsQueryOptions(
                DateTimeOffset.Parse("2026-08-23T15:30:00Z"),
                DateTimeOffset.Parse("2026-08-23T16:30:00Z"),
                "week"));

        Assert.AreEqual(2, result.Trend.Count);
        Assert.AreEqual("2026-08-17 周", result.Trend[0].Label);
        Assert.AreEqual(1, result.Trend[0].Count);
        Assert.AreEqual("2026-08-24 周", result.Trend[1].Label);
        Assert.AreEqual(1, result.Trend[1].Count);
    }

    [TestMethod]
    public async Task QueryFiltersLobbyModeAgentAndTargetServerAndNormalizesNullMode()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var otherTargetId = Guid.NewGuid();
        db.PlayerEntryEvents.AddRange(
            Event(agentId, targetId, DateTimeOffset.UtcNow.AddMinutes(-10), lobbyType: "standard", mode: "versus"),
            Event(agentId, targetId, DateTimeOffset.UtcNow.AddMinutes(-9), lobbyType: "reserved", mode: "coop"),
            Event(agentId, targetId, DateTimeOffset.UtcNow.AddMinutes(-8), lobbyType: "standard", mode: ""),
            Event(otherAgentId, otherTargetId, DateTimeOffset.UtcNow.AddMinutes(-7), lobbyType: "standard", mode: "coop"));
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;

        var result = await new PlayerEntryStatisticsQuery(db, () => now).QueryAsync(
            new PlayerEntryStatisticsQueryOptions(
                now.AddHours(-1), now, "hour", "standard", "versus", targetId, agentId));

        Assert.AreEqual(2, result.TotalEntries);
        Assert.AreEqual(2, result.Agents.Single().Entries);
        Assert.AreEqual(2, result.DownloadRegions.Single().Entries);
    }

    [TestMethod]
    public async Task QueryRejectsInvalidRangeAndUnknownDimensions()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.Parse("2026-08-24T00:00:00Z");
        var query = new PlayerEntryStatisticsQuery(db, () => now);

        await Assert.ThrowsExceptionAsync<PlayerEntryStatisticsQueryValidationException>(
            () => query.QueryAsync(new PlayerEntryStatisticsQueryOptions(now, now, "hour")));
        await Assert.ThrowsExceptionAsync<PlayerEntryStatisticsQueryValidationException>(
            () => query.QueryAsync(new PlayerEntryStatisticsQueryOptions(now.AddDays(-181), now, "auto")));
        await Assert.ThrowsExceptionAsync<PlayerEntryStatisticsQueryValidationException>(
            () => query.QueryAsync(new PlayerEntryStatisticsQueryOptions(now.AddHours(-1), now, "minute")));
    }

    [TestMethod]
    public async Task QueryKeepsHistoricalAgentAndRegionSnapshotsWithoutAddingCurrentZeroDimensions()
    {
        await using var db = CreateDb();
        var deletedAgentId = Guid.NewGuid();
        var deletedTargetId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-24T00:00:00Z");
        var historical = Event(
            deletedAgentId,
            deletedTargetId,
            now.AddMinutes(-10),
            mode: "versus");
        historical.AgentNameSnapshot = "deleted-agent";
        historical.DownloadRegionSnapshot = "47";
        db.PlayerEntryEvents.Add(historical);
        await db.SaveChangesAsync();

        var result = await new PlayerEntryStatisticsQuery(db, () => now).QueryAsync(
            new PlayerEntryStatisticsQueryOptions(now.AddHours(-1), now, "hour"));

        Assert.AreEqual("deleted-agent", result.Agents.Single().AgentName);
        Assert.AreEqual("47", result.DownloadRegions.Single().Key);
        Assert.AreEqual(1, result.DownloadRegions.Single().Entries);
    }

    [TestMethod]
    public async Task QueryIncludesCurrentAgentWithZeroEntriesButNotItsUnobservedRegion()
    {
        await using var db = CreateDb();
        var currentAgentId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-24T00:00:00Z");
        db.WarmupAgents.Add(new WarmupAgent
        {
            Id = currentAgentId,
            Name = "idle-agent",
            DownloadRegion = "33",
            NoVncPort = 18083,
        });
        await db.SaveChangesAsync();

        var result = await new PlayerEntryStatisticsQuery(db, () => now).QueryAsync(
            new PlayerEntryStatisticsQueryOptions(now.AddHours(-1), now, "hour"));

        Assert.AreEqual(0, result.TotalEntries);
        Assert.AreEqual(0, result.Agents.Single().Entries);
        Assert.AreEqual("idle-agent", result.Agents.Single().AgentName);
        Assert.AreEqual(0, result.DownloadRegions.Count);
    }

    [TestMethod]
    public void PostgresHourGroupingIsTranslatedToSql()
    {
        using var db = new MatchmakingDbContext(new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=matchmaking;Username=matchmaking;Password=unused")
            .Options);

        var sql = db.PlayerEntryEvents
            .AsNoTracking()
            .Where(entry => entry.OccurredAtUtc >= DateTimeOffset.UtcNow.AddHours(-1))
            .GroupBy(entry => new
            {
                Year = entry.OccurredAtUtc.Year,
                Month = entry.OccurredAtUtc.Month,
                Day = entry.OccurredAtUtc.Day,
                Hour = entry.OccurredAtUtc.Hour,
            })
            .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Day, group.Key.Hour, Count = group.Count() })
            .ToQueryString();

        StringAssert.Contains(sql, "GROUP BY");
        StringAssert.Contains(sql, "AT TIME ZONE 'UTC'");
    }

    private static MatchmakingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static PlayerEntryEvent Event(
        Guid agentId,
        Guid targetId,
        DateTimeOffset occurredAt,
        string lobbyType = "standard",
        string mode = "versus") => new()
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = occurredAt,
            IngestedAtUtc = occurredAt,
            OperationId = Guid.NewGuid(),
            LobbyId = "lobby-1",
            LobbyType = lobbyType,
            AgentId = agentId,
            AgentNameSnapshot = "agent-snapshot",
            DownloadRegionSnapshot = null,
            TargetServerId = targetId,
            TargetServerEndpointSnapshot = "203.0.113.7:27015",
            TargetServerNameSnapshot = "target-snapshot",
            TargetModeSnapshot = mode,
        };
}
