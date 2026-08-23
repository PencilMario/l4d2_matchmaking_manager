using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class PlayerEntryEventModelTests
{
    [TestMethod]
    public void PlayerEntryEventsKeepSnapshotsAndDimensionTimeIndexes()
    {
        using var db = new MatchmakingDbContext(new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var entity = db.Model.FindEntityType(typeof(PlayerEntryEvent));
        Assert.IsNotNull(entity);
        Assert.AreEqual(nameof(PlayerEntryEvent.EventId), entity.FindPrimaryKey()!.Properties.Single().Name);
        Assert.IsNull(entity.GetForeignKeys().SingleOrDefault());
        Assert.AreEqual(20, entity.FindProperty(nameof(PlayerEntryEvent.LobbyId))!.GetMaxLength());
        Assert.AreEqual(16, entity.FindProperty(nameof(PlayerEntryEvent.LobbyType))!.GetMaxLength());
        Assert.AreEqual(128, entity.FindProperty(nameof(PlayerEntryEvent.AgentNameSnapshot))!.GetMaxLength());
        Assert.AreEqual(128, entity.FindProperty(nameof(PlayerEntryEvent.DownloadRegionSnapshot))!.GetMaxLength());
        Assert.AreEqual(320, entity.FindProperty(nameof(PlayerEntryEvent.TargetServerEndpointSnapshot))!.GetMaxLength());
        Assert.AreEqual(256, entity.FindProperty(nameof(PlayerEntryEvent.TargetServerNameSnapshot))!.GetMaxLength());
        Assert.AreEqual(16, entity.FindProperty(nameof(PlayerEntryEvent.TargetModeSnapshot))!.GetMaxLength());

        var indexes = entity.GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToHashSet(StringComparer.Ordinal);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                nameof(PlayerEntryEvent.OccurredAtUtc),
                $"{nameof(PlayerEntryEvent.AgentId)},{nameof(PlayerEntryEvent.OccurredAtUtc)}",
                $"{nameof(PlayerEntryEvent.TargetServerId)},{nameof(PlayerEntryEvent.OccurredAtUtc)}",
                $"{nameof(PlayerEntryEvent.DownloadRegionSnapshot)},{nameof(PlayerEntryEvent.OccurredAtUtc)}",
                $"{nameof(PlayerEntryEvent.TargetModeSnapshot)},{nameof(PlayerEntryEvent.OccurredAtUtc)}",
                $"{nameof(PlayerEntryEvent.LobbyType)},{nameof(PlayerEntryEvent.OccurredAtUtc)}",
            },
            indexes.ToArray());
    }

    [TestMethod]
    public void PlayerEntryEventsMigrationIsDiscovered()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=matchmaking;Username=matchmaking;Password=unused")
            .Options;
        using var context = new MatchmakingDbContext(options);

        CollectionAssert.Contains(
            context.Database.GetMigrations().ToList(),
            "202608230001_PlayerEntryEvents");
    }
}
