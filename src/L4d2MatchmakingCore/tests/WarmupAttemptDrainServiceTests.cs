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

    [TestMethod]
    public async Task DrainAllStopsEveryCurrentAttemptReleasesLeasesAndRestartsTouchedAgents()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running" };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running" };
        var firstTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015 };
        var secondTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016 };
        var pendingTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27017 };
        var activeOperation = Guid.NewGuid();
        var uncertainOperation = Guid.NewGuid();
        var pendingOperation = Guid.NewGuid();
        db.AddRange(
            firstAgent,
            secondAgent,
            firstTarget,
            secondTarget,
            pendingTarget,
            new WarmupAttempt
            {
                Id = Guid.NewGuid(),
                TargetServerId = firstTarget.Id,
                WarmupAgentId = firstAgent.Id,
                OperationId = activeOperation,
                State = "active",
                StartedAt = DateTimeOffset.UtcNow,
                ObservedAt = DateTimeOffset.UtcNow,
            },
            new WarmupAttempt
            {
                Id = Guid.NewGuid(),
                TargetServerId = secondTarget.Id,
                WarmupAgentId = secondAgent.Id,
                OperationId = uncertainOperation,
                State = "uncertain",
                StartedAt = DateTimeOffset.UtcNow,
                ObservedAt = DateTimeOffset.UtcNow,
            },
            new WarmupAttempt
            {
                Id = Guid.NewGuid(),
                TargetServerId = pendingTarget.Id,
                WarmupAgentId = firstAgent.Id,
                OperationId = pendingOperation,
                State = "restart_pending",
                StartedAt = DateTimeOffset.UtcNow,
                ObservedAt = DateTimeOffset.UtcNow,
            },
            new ReservationLease
            {
                TargetServerId = firstTarget.Id,
                OperationId = activeOperation,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            },
            new ReservationLease
            {
                TargetServerId = secondTarget.Id,
                OperationId = uncertainOperation,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            },
            new ReservationLease
            {
                TargetServerId = pendingTarget.Id,
                OperationId = pendingOperation,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            });
        await db.SaveChangesAsync();

        var control = new FakeAgentControlClient();
        var service = new WarmupAttemptDrainService(db, control);
        var drainAll = typeof(WarmupAttemptDrainService).GetMethod("DrainAllAsync");
        Assert.IsNotNull(drainAll);

        var drained = await (Task<bool>)drainAll!.Invoke(service, [CancellationToken.None])!;

        Assert.IsTrue(drained);
        CollectionAssert.AreEquivalent(new[] { activeOperation, uncertainOperation }, control.StoppedOperations);
        CollectionAssert.AreEquivalent(new[] { firstAgent.Id, secondAgent.Id }, control.RestartedAgents);
        Assert.AreEqual(0, await db.WarmupAttempts.CountAsync(attempt => attempt.State == "active" || attempt.State == "uncertain" || attempt.State == "restart_pending"));
        Assert.IsFalse(await db.ReservationLeases.AnyAsync());
        Assert.AreEqual("restarting", firstAgent.Status);
        Assert.AreEqual("restarting", secondAgent.Status);
    }

    private static MatchmakingDbContext CreateDb() => new(new DbContextOptionsBuilder<MatchmakingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private sealed class FakeAgentControlClient : IAgentControlClient
    {
        public List<Guid> StoppedOperations { get; } = [];
        public List<Guid> RestartedAgents { get; } = [];
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

        public Task RestartSteamAsync(WarmupAgent agent, CancellationToken cancellationToken)
        {
            RestartedAgents.Add(agent.Id);
            return Task.CompletedTask;
        }

        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
