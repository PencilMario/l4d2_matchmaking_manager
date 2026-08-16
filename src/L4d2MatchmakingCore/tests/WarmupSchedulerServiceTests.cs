using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class WarmupSchedulerServiceTests
{
    [TestMethod]
    public async Task RestartKeepsLeaseUntilAgentSnapshotIsReconciled()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = Guid.NewGuid();
        var operation = Guid.NewGuid();
        db.WarmupAgents.Add(agent);
        db.WarmupAttempts.Add(new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target, WarmupAgentId = agent.Id, OperationId = operation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow, ObservedAt = DateTimeOffset.UtcNow });
        db.ReservationLeases.Add(new ReservationLease { TargetServerId = target, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db));

        await scheduler.RecoverAsync(CancellationToken.None);

        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == target));
    }

    [TestMethod]
    public async Task MaintenanceLockPreventsNewScheduling()
    {
        await using var db = CreateDb();
        var agents = new FakeAgents(null);
        var maintenance = new SharedLibraryMaintenanceService(db);
        await maintenance.AcquireAsync(CancellationToken.None);
        var scheduler = new WarmupSchedulerService(db, agents, maintenance);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, agents.StartCalls);
    }

    [TestMethod]
    public async Task TickStartsReservedAttemptWithOneLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.StartCalls);
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id));
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.TargetServerId == server.Id && attempt.State == "active"));
    }

    [TestMethod]
    public async Task TickRecreatesQuietLobbyOnTheSameTarget()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, server, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow, Phase = "active", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-1), QuietSince = DateTimeOffset.UtcNow.AddSeconds(-30), ExternalMemberIdsJson = "[\"player\"]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(
            operation,
            "active",
            new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null), new LobbyMemberSnapshot("player", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            null,
            DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "completed"));
        Assert.AreEqual(1, agents.StopCalls);
    }

    private static MatchmakingDbContext CreateDb() => new(new DbContextOptionsBuilder<MatchmakingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private sealed class FakeAgents(AgentOperationSnapshot? snapshot) : IAgentControlClient
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.FromResult(new AgentOperationStartResult(new AgentOperationSnapshot(request.OperationId, "active", null, null, DateTimeOffset.UtcNow), false));
        }
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) { StopCalls++; return Task.CompletedTask; }
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeSelector(WarmupAgent agent) : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(agent);
    }

    private sealed class FakeA2s : ISourceA2sClient
    {
        public Task<A2sServerInfo> GetInfoAsync(System.Net.IPEndPoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new A2sServerInfo(0, DateTimeOffset.UtcNow));
    }
}
