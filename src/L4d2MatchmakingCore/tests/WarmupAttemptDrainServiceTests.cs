using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class WarmupAttemptDrainServiceTests
{
    [TestMethod]
    public async Task DrainAgentStopsActiveAttemptsAndReleasesReservationLeases()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running" };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015 };
        var operation = Guid.NewGuid();
        db.AddRange(
            agent,
            target,
            new WarmupAttempt
            {
                Id = Guid.NewGuid(),
                TargetServerId = target.Id,
                WarmupAgentId = agent.Id,
                OperationId = operation,
                State = "active",
                StartedAt = DateTimeOffset.UtcNow,
                ObservedAt = DateTimeOffset.UtcNow,
            },
            new ReservationLease
            {
                TargetServerId = target.Id,
                OperationId = operation,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            });
        await db.SaveChangesAsync();

        var control = new FakeAgentControlClient();
        var service = new WarmupAttemptDrainService(db, control);

        var drained = await service.DrainAgentAsync(agent.Id, CancellationToken.None);

        Assert.IsTrue(drained);
        CollectionAssert.AreEqual(new[] { operation }, control.StoppedOperations);
        var attempt = await db.WarmupAttempts.SingleAsync();
        Assert.AreEqual("completed", attempt.State);
        Assert.IsFalse(await db.ReservationLeases.AnyAsync());
    }

    [TestMethod]
    public async Task DrainAgentReportsFailureAndPreservesAttemptWhenStopIsUncertain()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running" };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt
        {
            Id = Guid.NewGuid(),
            TargetServerId = target.Id,
            WarmupAgentId = agent.Id,
            OperationId = operation,
            State = "active",
            StartedAt = DateTimeOffset.UtcNow,
            ObservedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new WarmupAttemptDrainService(db, new FakeAgentControlClient
        {
            StopException = new HttpRequestException("agent_unreachable"),
        });

        var drained = await service.DrainAgentAsync(agent.Id, CancellationToken.None);

        Assert.IsFalse(drained);
        Assert.AreEqual("quarantined", agent.Status);
        Assert.AreEqual("active", (await db.WarmupAttempts.SingleAsync()).State);
    }

    private static MatchmakingDbContext CreateDb() => new(new DbContextOptionsBuilder<MatchmakingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private sealed class FakeAgentControlClient : IAgentControlClient
    {
        public List<Guid> StoppedOperations { get; } = [];
        public Exception? StopException { get; init; }

        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
        {
            if (StopException is not null)
                return Task.FromException(StopException);
            StoppedOperations.Add(operationId);
            return Task.CompletedTask;
        }

        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
