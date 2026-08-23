using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class PlayerEntryRetentionServiceTests
{
    [TestMethod]
    public async Task CleanupDeletesOnlyEventsOlderThanRetentionWindow()
    {
        await using var db = new MatchmakingDbContext(new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var now = DateTimeOffset.Parse("2026-08-24T00:00:00Z");
        db.PlayerEntryEvents.AddRange(
            new PlayerEntryEvent { EventId = Guid.NewGuid(), OccurredAtUtc = now.AddDays(-180).AddSeconds(-1), LobbyId = "old", TargetModeSnapshot = "versus" },
            new PlayerEntryEvent { EventId = Guid.NewGuid(), OccurredAtUtc = now.AddDays(-179), LobbyId = "keep", TargetModeSnapshot = "versus" });
        await db.SaveChangesAsync();

        var removed = await new PlayerEntryRetentionService(db, () => now).CleanupAsync(CancellationToken.None);

        Assert.AreEqual(1, removed);
        Assert.AreEqual("keep", (await db.PlayerEntryEvents.SingleAsync()).LobbyId);
    }
}
