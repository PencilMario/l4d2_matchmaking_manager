using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class AgentMemoryLimitServiceTests
{
    [TestMethod]
    public async Task Applies600MiBOnlyAfterCefGuardHasBeenAppliedFor60Seconds()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateDb();
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(),
            Name = "agent-1",
            Status = "running",
            ContainerId = "container-1",
            NoVncPort = 18083,
            UpdatedAt = now,
        };
        db.WarmupAgents.Add(agent);
        await db.SaveChangesAsync();

        var runtime = new RecordingRuntime();
        var control = new FixedAgentControlClient(
            new AgentHealthSnapshot(true, null, now, CefGuardAppliedAt: now));
        var clock = new MutableTimeProvider(now.AddSeconds(59));
        var service = new AgentMemoryLimitService(
            db,
            control,
            runtime,
            new AgentLifecycleCoordinator(),
            timeProvider: clock);

        await service.ApplyAsync(CancellationToken.None);

        Assert.AreEqual(0, runtime.Updates.Count);

        clock.UtcNow = now.AddSeconds(60);
        await service.ApplyAsync(CancellationToken.None);

        Assert.AreEqual(1, runtime.Updates.Count);
        var update = runtime.Updates[0];
        Assert.AreEqual("container-1", update.ContainerId);
        Assert.AreEqual(600L * 1024 * 1024, update.MemoryLimitBytes);

        await service.ApplyAsync(CancellationToken.None);
        Assert.AreEqual(1, runtime.Updates.Count);
    }

    [TestMethod]
    public async Task DoesNotApplyWithoutACefGuardTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateDb();
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(),
            Name = "agent-without-marker",
            Status = "running",
            ContainerId = "container-without-marker",
            NoVncPort = 18084,
            UpdatedAt = now,
        };
        db.WarmupAgents.Add(agent);
        await db.SaveChangesAsync();

        var runtime = new RecordingRuntime();
        var service = new AgentMemoryLimitService(
            db,
            new FixedAgentControlClient(new AgentHealthSnapshot(true, null, now)),
            runtime,
            new AgentLifecycleCoordinator(),
            timeProvider: new MutableTimeProvider(now.AddMinutes(5)));

        await service.ApplyAsync(CancellationToken.None);

        Assert.AreEqual(0, runtime.Updates.Count);
    }

    [TestMethod]
    public async Task DoesNotApplyWhenCefGuardTimestampIsInTheFuture()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateDb();
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(),
            Name = "agent-with-future-marker",
            Status = "running",
            ContainerId = "container-with-future-marker",
            NoVncPort = 18085,
            UpdatedAt = now,
        };
        db.WarmupAgents.Add(agent);
        await db.SaveChangesAsync();

        var runtime = new RecordingRuntime();
        var service = new AgentMemoryLimitService(
            db,
            new FixedAgentControlClient(new AgentHealthSnapshot(
                true,
                null,
                now,
                CefGuardAppliedAt: now.AddSeconds(1))),
            runtime,
            new AgentLifecycleCoordinator(),
            timeProvider: new MutableTimeProvider(now));

        await service.ApplyAsync(CancellationToken.None);

        Assert.AreEqual(0, runtime.Updates.Count);
    }

    [TestMethod]
    public async Task DoesNotApplyWhenContainerIsAlreadyAtOrBelowTheReclaimedLimit()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = CreateDb();
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(),
            Name = "agent-already-reclaimed",
            Status = "running",
            ContainerId = "container-already-reclaimed",
            NoVncPort = 18086,
            UpdatedAt = now,
        };
        db.WarmupAgents.Add(agent);
        await db.SaveChangesAsync();

        var runtime = new RecordingRuntime
        {
            CurrentLimit = AgentMemoryLimitService.ReclaimedMemoryLimitBytes,
        };
        var service = new AgentMemoryLimitService(
            db,
            new FixedAgentControlClient(new AgentHealthSnapshot(
                true,
                null,
                now,
                CefGuardAppliedAt: now.AddMinutes(-2))),
            runtime,
            new AgentLifecycleCoordinator(),
            timeProvider: new MutableTimeProvider(now));

        await service.ApplyAsync(CancellationToken.None);

        Assert.AreEqual(0, runtime.Updates.Count);
    }

    private static MatchmakingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RecordingRuntime : IAgentContainerRuntime
    {
        public List<(string ContainerId, long MemoryLimitBytes)> Updates { get; } = [];
        public long CurrentLimit { get; set; } = 1536L * 1024 * 1024;

        public Task<long> GetMemoryLimitAsync(string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentLimit);

        public Task UpdateMemoryLimitAsync(string containerId, long memoryLimitBytes, CancellationToken cancellationToken)
        {
            Updates.Add((containerId, memoryLimitBytes));
            CurrentLimit = memoryLimitBytes;
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

        public Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StartAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StartVncAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopVncAsync(string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedAgentControlClient(AgentHealthSnapshot health) : IAgentControlClient
    {
        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
            Task.FromResult(health);

        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
