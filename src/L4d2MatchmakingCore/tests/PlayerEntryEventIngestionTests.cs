using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ContractPlayerEntryEvent = L4d2Matchmaking.Contracts.PlayerEntryEvent;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class PlayerEntryEventIngestionTests
{
    [TestMethod]
    public async Task NewEventIsAcceptedAndSameEventIdIsCountedAsDuplicate()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T08:00:00Z");
        var service = new PlayerEntryEventIngestionService(db, () => now);
        var entry = Event(agentId, now.AddMinutes(-1));

        var first = await service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([entry]), CancellationToken.None);
        var second = await service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([entry]), CancellationToken.None);

        Assert.AreEqual(new PlayerEntryEventBatchResponse(1, 0), first);
        Assert.AreEqual(new PlayerEntryEventBatchResponse(0, 1), second);
        Assert.AreEqual(1, await db.PlayerEntryEvents.CountAsync());
    }

    [TestMethod]
    public async Task ConflictingEventIdIsRejectedWithoutOverwritingTheOriginal()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T08:00:00Z");
        var service = new PlayerEntryEventIngestionService(db, () => now);
        var eventId = Guid.NewGuid();
        var original = Event(agentId, now.AddMinutes(-1), eventId);
        var conflict = original with { LobbyId = "different-lobby" };
        await service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([original]), CancellationToken.None);

        var exception = await Assert.ThrowsExceptionAsync<PlayerEntryEventConflictException>(
            () => service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([conflict]), CancellationToken.None));

        Assert.AreEqual(eventId, exception.EventId);
        Assert.AreEqual("lobby-1", (await db.PlayerEntryEvents.SingleAsync()).LobbyId);
    }

    [TestMethod]
    public async Task InvalidModeAndFutureEventAreRejected()
    {
        await using var db = CreateDb();
        var agentId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T08:00:00Z");
        var service = new PlayerEntryEventIngestionService(db, () => now);

        var invalidMode = Event(agentId, now.AddMinutes(-1)) with { TargetModeSnapshot = "campaign" };
        await Assert.ThrowsExceptionAsync<PlayerEntryEventValidationException>(
            () => service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([invalidMode]), CancellationToken.None));

        var future = Event(agentId, now.AddMinutes(6));
        await Assert.ThrowsExceptionAsync<PlayerEntryEventValidationException>(
            () => service.IngestAsync(agentId, new PlayerEntryEventBatchRequest([future]), CancellationToken.None));
    }

    [TestMethod]
    public async Task ConcurrentSameEventIdIsAcceptedOnceAndCountedAsDuplicates()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var agentId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-08-23T08:00:00Z");
        var entry = Event(agentId, now.AddMinutes(-1));
        const int callers = 16;
        var saveGate = new SaveChangesGate(callers);

        var tasks = Enumerable.Range(0, callers).Select(async _ =>
        {
            await using var db = CreateDb(databaseName, saveGate);
            var service = new PlayerEntryEventIngestionService(db, () => now);
            return await service.IngestAsync(
                agentId,
                new PlayerEntryEventBatchRequest([entry]),
                CancellationToken.None);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.AreEqual(1, responses.Count(response => response.Accepted == 1));
        Assert.AreEqual(callers - 1, responses.Count(response => response.Duplicates == 1));
        await using var verificationDb = CreateDb(databaseName);
        Assert.AreEqual(1, await verificationDb.PlayerEntryEvents.CountAsync());
    }

    private static MatchmakingDbContext CreateDb(
        string? databaseName = null,
        SaveChangesGate? saveGate = null)
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"));
        if (saveGate is not null)
            options.AddInterceptors(saveGate);
        return new MatchmakingDbContext(options.Options);
    }

    private sealed class SaveChangesGate(int participants) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> allParticipantsEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int entered;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref entered) == participants)
                allParticipantsEntered.TrySetResult(true);
            await allParticipantsEntered.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private static ContractPlayerEntryEvent Event(Guid agentId, DateTimeOffset occurredAt, Guid? eventId = null) => new(
        eventId ?? Guid.NewGuid(),
        occurredAt,
        Guid.NewGuid(),
        "lobby-1",
        PlayerEntryStatisticsContract.LobbyTypeStandard,
        agentId,
        "agent",
        null,
        Guid.NewGuid(),
        "203.0.113.7:27015",
        null,
        PlayerEntryStatisticsContract.GameModeVersus);
}
