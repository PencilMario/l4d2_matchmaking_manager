using System.Collections.Concurrent;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Configuration;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using L4d2MatchmakingCore.Servers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow));
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
    public async Task DisabledGlobalSchedulingSkipsEverySchedulingAction()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(),
            Name = "agent",
            Status = "running",
            SteamDataVolumeName = "steam",
            AccountConfigVolumeName = "config",
            NoVncPort = 18083,
        };
        var server = new TargetServer
        {
            Id = Guid.NewGuid(),
            Host = "localhost",
            Port = 27015,
            Enabled = true,
        };
        db.AddRange(
            agent,
            server,
            new CoreSettings { WarmupSchedulingEnabled = false, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, agents.StartCalls);
        Assert.AreEqual(0, agents.StopCalls);
    }

    [TestMethod]
    public async Task SharedSchedulingGateMakesTickWaitForAnInProgressToggle()
    {
        await using var db = CreateDb();
        var gate = new WarmupSchedulingGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new WarmupSchedulerService(
            db,
            new FakeAgents(null),
            new SharedLibraryMaintenanceService(db),
            schedulingGate: gate);

        var toggle = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
        }, CancellationToken.None);
        await entered.Task;

        var tick = scheduler.TickAsync(CancellationToken.None);

        Assert.IsFalse(tick.IsCompleted);
        release.SetResult();
        await Task.WhenAll(toggle, tick);
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
    public async Task TickPassesConfiguredGameModeToAgent()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer
        {
            Id = Guid.NewGuid(),
            Host = "127.0.0.1",
            Port = 27015,
            Enabled = true,
            RequiresReservation = false,
            GameMode = "coop",
            PlayerTarget = 6,
            AttemptWindowSeconds = 720,
        };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("coop", agents.LastStartRequest?.GameMode);
    }

    [TestMethod]
    public async Task TickPassesImmutableEntryStatisticsContextWithVersusDefault()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", DownloadRegion = "hongkong", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer
        {
            Id = Guid.NewGuid(),
            Host = "localhost",
            Port = 27015,
            Enabled = true,
            RequiresReservation = false,
            GameMode = null,
            PlayerTarget = 6,
            AttemptWindowSeconds = 720,
        };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var observations = new TargetServerObservationStore();
        observations.Replace(server.Id, new TargetServerObservation(server.Id, "online", "target-name", 0, 8, DateTimeOffset.UtcNow));
        var agents = new FakeAgents(null);
        var scheduler = CreateSchedulerWithObservationStore(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(), observations);

        await scheduler.TickAsync(CancellationToken.None);

        var context = agents.LastStartRequest?.EntryStatisticsContext;
        Assert.IsNotNull(context);
        Assert.AreEqual(agent.Id, context.AgentId);
        Assert.AreEqual("agent", context.AgentNameSnapshot);
        Assert.AreEqual("33", context.ConfiguredDownloadRegionSnapshot);
        Assert.AreEqual(server.Id, context.TargetServerId);
        Assert.AreEqual("127.0.0.1:27015", context.TargetServerEndpointSnapshot);
        Assert.AreEqual("target-name", context.TargetServerNameSnapshot);
        Assert.AreEqual("versus", context.TargetModeSnapshot);
    }

    [TestMethod]
    public async Task TickRestartsSteamAndExcludesAgentUntilHealthRecoversAfterAttemptDeadlineExpires()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-13), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "stopped", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.RestartSteamCalls);
        Assert.AreEqual("restarting", agent.Status);
        Assert.AreEqual(0, agents.StartCalls);
    }

    [TestMethod]
    public async Task TickDoesNotRestartSteamWhenOperationStopsBeforeAttemptDeadline()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "stopped", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, agents.RestartSteamCalls);
        Assert.AreEqual("running", agent.Status);
    }

    [TestMethod]
    public async Task TickIgnoresOrphanedAttemptWhenDeterminingAttemptDeadline()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = Guid.NewGuid(), OperationId = Guid.NewGuid(), Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddDays(-1), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "stopped", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, agents.RestartSteamCalls);
        Assert.AreEqual("running", agent.Status);
    }

    [TestMethod]
    public async Task TickRestartsSteamWhenAttemptDeadlineExpiresWhileOperationIsActive()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 60 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddSeconds(-61), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(
            operation,
            "active",
            new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            null,
            DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.StopCalls);
        Assert.AreEqual(1, agents.RestartSteamCalls);
        Assert.AreEqual("restarting", agent.Status);
    }

    [TestMethod]
    public async Task TickDoesNotRestartSteamWhenPlayerTargetIsReachedBeforeAttemptDeadline()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(
            operation,
            "active",
            new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            null,
            DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(new Dictionary<int, int> { [target.Port] = target.PlayerTarget }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.StopCalls);
        Assert.AreEqual(0, agents.RestartSteamCalls);
        Assert.AreEqual("running", agent.Status);
    }

    [TestMethod]
    public async Task TickKeepsAgentOutOfSchedulingUntilHealthRecoversWhenSteamRestartRequestFails()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target, new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-13), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "stopped", null, null, DateTimeOffset.UtcNow), throwOnRestart: true);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("restarting", agent.Status);
        Assert.AreEqual(1, agents.RestartSteamCalls);
    }

    [TestMethod]
    public async Task TickStartsABatchWithoutExceedingTargetConcurrency()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "first-steam", AccountConfigVolumeName = "first-config", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "second-steam", AccountConfigVolumeName = "second-config", NoVncPort = 18084 };
        var preferredTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 1, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var fallbackTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        db.AddRange(firstAgent, secondAgent, preferredTarget, fallbackTarget);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new BatchSelector([firstAgent, secondAgent]),
            new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(2, agents.StartCalls);
        Assert.AreEqual(2, await db.WarmupAttempts.CountAsync(attempt => attempt.State == "active"));
        Assert.AreEqual(1, await db.WarmupAttempts.CountAsync(attempt => attempt.TargetServerId == preferredTarget.Id && attempt.State == "active"));
        CollectionAssert.AreEquivalent(new[] { preferredTarget.Port, fallbackTarget.Port }, agents.StartRequests.Select(request => (int)request.Port).ToArray());
    }

    [TestMethod]
    public async Task TickConcentratesFreshBatchOnOneEqualPriorityStandardTarget()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "first-steam", AccountConfigVolumeName = "first-config", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "second-steam", AccountConfigVolumeName = "second-config", NoVncPort = 18084 };
        var firstTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var secondTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        db.AddRange(firstAgent, secondAgent, firstTarget, secondTarget);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstAgent, secondAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(2, agents.StartCalls);
        CollectionAssert.AreEqual(new[] { firstTarget.Port, firstTarget.Port }, agents.StartRequests.Select(request => (int)request.Port).OrderBy(port => port).ToArray());
    }

    [TestMethod]
    public async Task TickPrefersTargetWithMostExistingWarmups()
    {
        await using var db = CreateDb();
        var firstBusyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-busy-agent", Status = "running", SteamDataVolumeName = "first-busy-steam", AccountConfigVolumeName = "first-busy-config", NoVncPort = 18083 };
        var secondBusyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-busy-agent", Status = "running", SteamDataVolumeName = "second-busy-steam", AccountConfigVolumeName = "second-busy-config", NoVncPort = 18084 };
        var thirdBusyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "third-busy-agent", Status = "running", SteamDataVolumeName = "third-busy-steam", AccountConfigVolumeName = "third-busy-config", NoVncPort = 18085 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18086 };
        var firstTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 3, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var secondTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 3, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var now = DateTimeOffset.UtcNow;
        var firstOperation = Guid.NewGuid();
        var secondOperation = Guid.NewGuid();
        var thirdOperation = Guid.NewGuid();
        db.AddRange(firstBusyAgent, secondBusyAgent, thirdBusyAgent, idleAgent, firstTarget, secondTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = firstTarget.Id, WarmupAgentId = firstBusyAgent.Id, OperationId = firstOperation, Mode = "standard", State = "active", Phase = "active", StartedAt = now.AddMinutes(-5), ObservedAt = now },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = secondTarget.Id, WarmupAgentId = secondBusyAgent.Id, OperationId = secondOperation, Mode = "standard", State = "active", Phase = "active", StartedAt = now.AddMinutes(-3), ObservedAt = now },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = secondTarget.Id, WarmupAgentId = thirdBusyAgent.Id, OperationId = thirdOperation, Mode = "standard", State = "active", Phase = "active", StartedAt = now.AddMinutes(-2), ObservedAt = now });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(firstOperation, "active", null, null, now));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstBusyAgent, secondBusyAgent, thirdBusyAgent, idleAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(secondTarget.Id, (await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id)).TargetServerId);
    }

    [TestMethod]
    public async Task TickFallsBackToNextExistingTargetWhenPreferredTargetCannotPassA2s()
    {
        await using var db = CreateDb();
        var firstBusyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-busy-agent", Status = "running", SteamDataVolumeName = "first-busy-steam", AccountConfigVolumeName = "first-busy-config", NoVncPort = 18083 };
        var secondBusyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-busy-agent", Status = "running", SteamDataVolumeName = "second-steam", AccountConfigVolumeName = "second-config", NoVncPort = 18084 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18085 };
        var firstTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var secondTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var freshTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Host = "127.0.0.1", Port = 27017, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var now = DateTimeOffset.UtcNow;
        var firstOperation = Guid.NewGuid();
        var secondOperation = Guid.NewGuid();
        db.AddRange(firstBusyAgent, secondBusyAgent, idleAgent, firstTarget, secondTarget, freshTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = firstTarget.Id, WarmupAgentId = firstBusyAgent.Id, OperationId = firstOperation, Mode = "standard", State = "active", Phase = "active", StartedAt = now.AddMinutes(-5), ObservedAt = now },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = secondTarget.Id, WarmupAgentId = secondBusyAgent.Id, OperationId = secondOperation, Mode = "standard", State = "active", Phase = "active", StartedAt = now.AddMinutes(-4), ObservedAt = now });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(firstOperation, "active", null, null, now));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstBusyAgent, secondBusyAgent, idleAgent]), new FakeA2s(failingPorts: new HashSet<int> { firstTarget.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(secondTarget.Id, (await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id)).TargetServerId);
    }

    [TestMethod]
    public async Task TickPrefersUnexpiredStandardContinuationOverHigherPriorityFreshTarget()
    {
        await using var db = CreateDb();
        var busyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "busy-agent", Status = "running", SteamDataVolumeName = "busy-steam", AccountConfigVolumeName = "busy-config", NoVncPort = 18083 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18084 };
        var continuationTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var freshTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 1, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var operation = Guid.NewGuid();
        db.AddRange(busyAgent, idleAgent, continuationTarget, freshTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = continuationTarget.Id, WarmupAgentId = busyAgent.Id, OperationId = operation, Mode = "standard", State = "active", Phase = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([busyAgent, idleAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(continuationTarget.Id, (await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id)).TargetServerId);
    }

    [TestMethod]
    public async Task TickContinuationInheritsEarliestServerStartedAt()
    {
        await using var db = CreateDb();
        var busyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "busy-agent", Status = "running", SteamDataVolumeName = "busy-steam", AccountConfigVolumeName = "busy-config", NoVncPort = 18083 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18084 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var operation = Guid.NewGuid();
        db.AddRange(busyAgent, idleAgent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = busyAgent.Id, OperationId = operation, Mode = "standard", State = "active", Phase = "active", StartedAt = startedAt, ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([busyAgent, idleAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        var continuation = await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id);
        Assert.AreEqual(startedAt, continuation.StartedAt);
    }

    [TestMethod]
    public async Task TickUsesEarliestServerStartedAtForEveryAttemptDeadline()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "first-steam", AccountConfigVolumeName = "first-config", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "second-steam", AccountConfigVolumeName = "second-config", NoVncPort = 18084 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = false, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        var earliestStartedAt = DateTimeOffset.UtcNow.AddMinutes(-13);
        db.AddRange(firstAgent, secondAgent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = firstAgent.Id, OperationId = operation, Mode = "standard", State = "active", Phase = "active", StartedAt = earliestStartedAt, ObservedAt = DateTimeOffset.UtcNow },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = secondAgent.Id, OperationId = Guid.NewGuid(), Mode = "standard", State = "active", Phase = "active", StartedAt = earliestStartedAt.AddMinutes(5), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstAgent, secondAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, await db.WarmupAttempts.CountAsync(attempt => attempt.State == "active" || attempt.State == "uncertain"));
    }

    [TestMethod]
    public async Task TickDoesNotPreferExpiredStandardContinuation()
    {
        await using var db = CreateDb();
        var busyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "busy-agent", Status = "running", SteamDataVolumeName = "busy-steam", AccountConfigVolumeName = "busy-config", NoVncPort = 18083 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18084 };
        var expiredTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var freshTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 1, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var operation = Guid.NewGuid();
        db.AddRange(busyAgent, idleAgent, expiredTarget, freshTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = expiredTarget.Id, WarmupAgentId = busyAgent.Id, OperationId = operation, Mode = "standard", State = "active", Phase = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-13), ObservedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([busyAgent, idleAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(freshTarget.Id, (await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id)).TargetServerId);
    }

    [TestMethod]
    public async Task TickDoesNotApplyContinuationPreferenceToReservationServer()
    {
        await using var db = CreateDb();
        var busyAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "busy-agent", Status = "running", SteamDataVolumeName = "busy-steam", AccountConfigVolumeName = "busy-config", NoVncPort = 18083 };
        var idleAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "idle-agent", Status = "running", SteamDataVolumeName = "idle-steam", AccountConfigVolumeName = "idle-config", NoVncPort = 18084 };
        var reservationTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var freshTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 1, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var operation = Guid.NewGuid();
        db.AddRange(busyAgent, idleAgent, reservationTarget, freshTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = reservationTarget.Id, WarmupAgentId = busyAgent.Id, OperationId = operation, Mode = "reserved", State = "active", Phase = "awaiting_first_member", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ObservedAt = DateTimeOffset.UtcNow },
            new ReservationLease { TargetServerId = reservationTarget.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", null, null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([busyAgent, idleAgent]), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(freshTarget.Id, (await db.WarmupAttempts.SingleAsync(attempt => attempt.WarmupAgentId == idleAgent.Id)).TargetServerId);
    }

    [TestMethod]
    public async Task TickDoesNotExceedConfiguredBatchStartLimit()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "first-steam", AccountConfigVolumeName = "first-config", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "second-steam", AccountConfigVolumeName = "second-config", NoVncPort = 18084 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        db.AddRange(firstAgent, secondAgent, target);
        await db.SaveChangesAsync();
        var options = CoreOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CORE_API_TOKEN"] = "test-token",
                ["CORE_DATABASE_CONNECTION_STRING"] = "Host=127.0.0.1;Database=test",
                ["CORE_SCHEDULER_MAX_STARTS_PER_TICK"] = "1",
            })
            .Build());
        var startLimit = typeof(CoreOptions).GetProperty("SchedulerMaxStartsPerTick");
        Assert.IsNotNull(startLimit, "CoreOptions must expose the configured batch start limit.");
        Assert.AreEqual(1, (int)startLimit.GetValue(options)!);
        var agents = new FakeAgents(null);
        var scheduler = CreateSchedulerWithOptions(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new BatchSelector([firstAgent, secondAgent]),
            new FakeA2s(),
            options);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.StartCalls);
    }

    [TestMethod]
    public async Task TickPassesDecryptedRconPasswordOnlyInTheReservedAgentRequest()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer
        {
            Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27083, Enabled = true, RequiresReservation = true,
            PlayerTarget = 6, AttemptWindowSeconds = 720, RconPasswordCiphertext = "encrypted-rcon-password",
        };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var credentials = new FakeRconCredentialProtector("decrypted-rcon-password");
        var scheduler = CreateSchedulerWithCredentials(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(),
            credentials);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("encrypted-rcon-password", credentials.LastCiphertext);
        Assert.IsNotNull(agents.LastStartRequest);
        Assert.AreEqual("decrypted-rcon-password", agents.LastStartRequest.RconPassword);
        Assert.AreEqual(AgentLobbyMode.Reserved, agents.LastStartRequest.Mode);
    }

    [TestMethod]
    public async Task TickNeverDecryptsRconPasswordForStandardAgentRequest()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer
        {
            Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27083, Enabled = true, RequiresReservation = false,
            PlayerTarget = 6, AttemptWindowSeconds = 720, RconPasswordCiphertext = "unexpected-standard-credential",
        };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var credentials = new FakeRconCredentialProtector("must-not-be-decrypted");
        var scheduler = CreateSchedulerWithCredentials(
            db,
            agents,
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(),
            credentials);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.IsNull(credentials.LastCiphertext);
        Assert.IsNotNull(agents.LastStartRequest);
        Assert.IsNull(agents.LastStartRequest.RconPassword);
        Assert.AreEqual(AgentLobbyMode.Standard, agents.LastStartRequest.Mode);
    }

    [TestMethod]
    public async Task TickPersistsRoundRobinCursorBetweenEqualPriorityTargets()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var firstTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 5 };
        var secondTarget = new TargetServer { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 5 };
        db.AddRange(agent, firstTarget, secondTarget);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        var completedAttempt = await db.WarmupAttempts.SingleAsync();
        Assert.AreEqual(completedAttempt.TargetServerId, (await db.TargetServerRotationCursors.SingleAsync()).LastTargetServerId);
        completedAttempt.State = "completed";
        completedAttempt.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await scheduler.TickAsync(CancellationToken.None);

        var activeAttempt = await db.WarmupAttempts.SingleAsync(attempt => attempt.State == "active");
        Assert.AreNotEqual(completedAttempt.TargetServerId, activeAttempt.TargetServerId);
        Assert.AreEqual(activeAttempt.TargetServerId, (await db.TargetServerRotationCursors.SingleAsync()).LastTargetServerId);
    }

    [TestMethod]
    public async Task AgentOperationReadFailureQuarantinesAgentAndKeepsReservationLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, server,
            new WarmupAttempt
            {
                Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation,
                Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow,
            },
            new ReservationLease { TargetServerId = server.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null, throwOnGetOperation: true);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("quarantined", agent.Status);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id && lease.OperationId == operation));
        Assert.AreEqual(0, agents.StopCalls);
    }

    [TestMethod]
    public async Task RecoveryOperationReadFailureQuarantinesAgentAndKeepsReservationLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var targetServerId = Guid.NewGuid();
        var operation = Guid.NewGuid();
        db.AddRange(agent,
            new WarmupAttempt
            {
                Id = Guid.NewGuid(), TargetServerId = targetServerId, WarmupAgentId = agent.Id, OperationId = operation,
                Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow, ObservedAt = DateTimeOffset.UtcNow,
            },
            new ReservationLease { TargetServerId = targetServerId, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var scheduler = new WarmupSchedulerService(db, new FakeAgents(null, throwOnGetOperation: true), new SharedLibraryMaintenanceService(db));

        await scheduler.RecoverAsync(CancellationToken.None);

        Assert.AreEqual("quarantined", agent.Status);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == targetServerId && lease.OperationId == operation));
    }

    [TestMethod]
    public async Task TickRecreatesQuietLobbyOnTheSameTargetWithoutRestartingSteam()
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

        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.TargetServerId == server.Id && attempt.State == "active" && attempt.OperationId != operation));
        Assert.AreEqual(1, agents.StopCalls);
        Assert.AreEqual(0, agents.RestartSteamCalls);
    }

    [TestMethod]
    public async Task TickRecreatesQuietLobbyOnItsOriginalTargetBeforeHigherPriorityServers()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var quietTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var higherPriorityTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, quietTarget, higherPriorityTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = quietTarget.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow, Phase = "active", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-1), QuietSince = DateTimeOffset.UtcNow.AddSeconds(-30), ExternalMemberIdsJson = "[\"player\"]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(
            operation,
            "active",
            new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null), new LobbyMemberSnapshot("player", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            null,
            DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);
        await scheduler.TickAsync(CancellationToken.None);

        var replacement = await db.WarmupAttempts.SingleAsync(attempt => attempt.State == "active");
        Assert.AreEqual(quietTarget.Id, replacement.TargetServerId);
        Assert.AreEqual(0, agents.RestartSteamCalls);
    }

    [TestMethod]
    public async Task TickSkipsFullHigherPriorityTargetAndStartsNextTarget()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var fullTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var nextTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        db.AddRange(agent, fullTarget, nextTarget);
        await db.SaveChangesAsync();
        var scheduler = new WarmupSchedulerService(
            db,
            new FakeAgents(null),
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(new Dictionary<int, int> { [fullTarget.Port] = fullTarget.PlayerTarget }));

        await scheduler.TickAsync(CancellationToken.None);

        var activeAttempt = await db.WarmupAttempts.SingleAsync(attempt => attempt.State == "active");
        Assert.AreEqual(nextTarget.Id, activeAttempt.TargetServerId);
    }

    [TestMethod]
    public async Task TickRecreationPreservesTheParentAttemptWindow()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        db.AddRange(agent, server,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = startedAt, ObservedAt = DateTimeOffset.UtcNow, Phase = "active", LobbyReadyAt = startedAt, QuietSince = DateTimeOffset.UtcNow.AddSeconds(-30), ExternalMemberIdsJson = "[\"player\"]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(
            operation,
            "active",
            new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null), new LobbyMemberSnapshot("player", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            null,
            DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);
        await scheduler.TickAsync(CancellationToken.None);

        var replacement = await db.WarmupAttempts.SingleAsync(attempt => attempt.State == "active");
        Assert.AreEqual(startedAt, replacement.StartedAt);
    }

    [TestMethod]
    public async Task StartFailureQuarantinesAgentAndPreservesReservationExclusion()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        db.AddRange(agent, server);
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null, throwOnGetOperation: true, throwOnStart: true);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());

        await scheduler.TickAsync(CancellationToken.None);

        var uncertainAttempt = await db.WarmupAttempts.SingleAsync();
        Assert.AreEqual("uncertain", uncertainAttempt.State);
        Assert.AreEqual("quarantined", agent.Status);
        var lease = await db.ReservationLeases.SingleAsync();
        Assert.AreEqual(uncertainAttempt.OperationId, lease.OperationId);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(1, agents.StartCalls);
    }

    [TestMethod]
    public async Task StopFailureQuarantinesAgentAndKeepsReservationLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, server,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExternalMemberIdsJson = "[]" },
            new ReservationLease { TargetServerId = server.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(
            new AgentOperationSnapshot(operation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow),
            throwOnStop: true);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(new Dictionary<int, int> { [server.Port] = server.PlayerTarget }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("quarantined", agent.Status);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id && lease.OperationId == operation));
        Assert.AreEqual(1, agents.StopCalls);
    }

    [TestMethod]
    public async Task ActiveA2sFailureStopsOperationAndReleasesReservationLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, server,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" },
            new ReservationLease { TargetServerId = server.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(operation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { server.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("running", agent.Status);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "completed"));
        Assert.IsFalse(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id && lease.OperationId == operation));
        Assert.AreEqual(1, agents.StopCalls);
    }

    [TestMethod]
    public async Task ActiveA2sFailureBeforeAttemptDeadlineKeepsOperationAndLease()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var server = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 60 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, server,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddSeconds(-5), ExternalMemberIdsJson = "[]" },
            new ReservationLease { TargetServerId = server.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(55) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { server.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(0, agents.StopCalls);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id && lease.OperationId == operation));
    }

    [TestMethod]
    public async Task A2sUnavailableTargetReleasesAttemptWhenOperationObservationFails()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null, throwOnGetOperationIds: new HashSet<Guid> { operation });
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { target.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("running", agent.Status);
        Assert.AreEqual(1, agents.StopCalls);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "completed"));
    }

    [TestMethod]
    public async Task A2sUnavailableTargetStopFailureQuarantinesAgentAndKeepsAttempt()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var operation = Guid.NewGuid();
        db.AddRange(agent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = operation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" },
            new ReservationLease { TargetServerId = target.Id, OperationId = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null, throwOnStop: true);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { target.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual("quarantined", agent.Status);
        Assert.AreEqual(1, agents.StopCalls);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == operation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == target.Id && lease.OperationId == operation));
    }

    [TestMethod]
    public async Task A2sUnavailableTargetKeepsLeaseWhenAnotherAttemptStopFails()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "steam-1", AccountConfigVolumeName = "config-1", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "steam-2", AccountConfigVolumeName = "config-2", NoVncPort = 18085 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = true, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var firstOperation = Guid.NewGuid();
        var secondOperation = Guid.NewGuid();
        db.AddRange(firstAgent, secondAgent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = firstAgent.Id, OperationId = firstOperation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = secondAgent.Id, OperationId = secondOperation, Mode = "reserved", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" },
            new ReservationLease { TargetServerId = target.Id, OperationId = firstOperation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(
            new AgentOperationSnapshot(firstOperation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow),
            throwOnStopIds: new HashSet<Guid> { secondOperation });
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstAgent, secondAgent]), new FakeA2s(failingPorts: new HashSet<int> { target.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(2, agents.StopCalls);
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == firstOperation && attempt.State == "completed"));
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == secondOperation && attempt.State == "active"));
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == target.Id && lease.OperationId == firstOperation));
    }

    [TestMethod]
    public async Task A2sUnavailableTargetDropsRestartPendingAttemptWithoutRestoringIt()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var oldOperation = Guid.NewGuid();
        db.AddRange(agent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = oldOperation, Mode = "standard", State = "restart_pending", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow, ExternalMemberIdsJson = "[]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(null);
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { target.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.OperationId == oldOperation && attempt.State == "completed"));
        Assert.AreEqual(0, agents.StartCalls);
    }

    [TestMethod]
    public async Task ActiveA2sFailureReleasesEveryAttemptForTheUnavailableTarget()
    {
        await using var db = CreateDb();
        var firstAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "first-agent", Status = "running", SteamDataVolumeName = "steam-1", AccountConfigVolumeName = "config-1", NoVncPort = 18083 };
        var secondAgent = new WarmupAgent { Id = Guid.NewGuid(), Name = "second-agent", Status = "running", SteamDataVolumeName = "steam-2", AccountConfigVolumeName = "config-2", NoVncPort = 18085 };
        var unavailableTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var nextTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        var firstOperation = Guid.NewGuid();
        var secondOperation = Guid.NewGuid();
        db.AddRange(firstAgent, secondAgent, unavailableTarget, nextTarget,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = unavailableTarget.Id, WarmupAgentId = firstAgent.Id, OperationId = firstOperation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" },
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = unavailableTarget.Id, WarmupAgentId = secondAgent.Id, OperationId = secondOperation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(firstOperation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow));
        var scheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new BatchSelector([firstAgent, secondAgent]), new FakeA2s(failingPorts: new HashSet<int> { unavailableTarget.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(2, agents.StopCalls);
        Assert.AreEqual(2, await db.WarmupAttempts.CountAsync(attempt => attempt.TargetServerId == unavailableTarget.Id && attempt.State == "completed"));
        Assert.IsTrue(await db.WarmupAttempts.AnyAsync(attempt => attempt.TargetServerId == nextTarget.Id));
    }

    [TestMethod]
    public async Task A2sUnavailableTargetCanBeScheduledAgainAfterItRecoversWithoutRestoringOldAttempt()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var target = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720 };
        var oldOperation = Guid.NewGuid();
        db.AddRange(agent, target,
            new WarmupAttempt { Id = Guid.NewGuid(), TargetServerId = target.Id, WarmupAgentId = agent.Id, OperationId = oldOperation, Mode = "standard", State = "active", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20), ObservedAt = DateTimeOffset.UtcNow, Phase = "awaiting_first_member", LobbyReadyAt = DateTimeOffset.UtcNow.AddMinutes(-20), ExternalMemberIdsJson = "[]" });
        await db.SaveChangesAsync();
        var agents = new FakeAgents(new AgentOperationSnapshot(oldOperation, "active", new LobbySnapshot("109775242170052468", "owner", [new LobbyMemberSnapshot("owner", null)], new Dictionary<string, string>(), DateTimeOffset.UtcNow), null, DateTimeOffset.UtcNow));
        var unavailableScheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s(failingPorts: new HashSet<int> { target.Port }));

        await unavailableScheduler.TickAsync(CancellationToken.None);

        var recoveredScheduler = new WarmupSchedulerService(db, agents, new SharedLibraryMaintenanceService(db), new FakeSelector(agent), new FakeA2s());
        await recoveredScheduler.TickAsync(CancellationToken.None);

        var newAttempt = await db.WarmupAttempts.SingleAsync(attempt => attempt.TargetServerId == target.Id && attempt.State != "completed");
        Assert.AreNotEqual(oldOperation, newAttempt.OperationId);
        Assert.AreEqual(1, agents.StartCalls);
    }

    [TestMethod]
    public async Task TickSkipsA2sUnavailableHigherPriorityTargetAndStartsNextTarget()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var unavailableTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var nextTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        db.AddRange(agent, unavailableTarget, nextTarget);
        await db.SaveChangesAsync();
        var scheduler = new WarmupSchedulerService(
            db,
            new FakeAgents(null),
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(failingPorts: new HashSet<int> { unavailableTarget.Port }));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(nextTarget.Id, (await db.WarmupAttempts.SingleAsync()).TargetServerId);
    }

    [TestMethod]
    public async Task TickSkipsTargetWithUnavailableObservationAndStartsNextTarget()
    {
        await using var db = CreateDb();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "agent", Status = "running", SteamDataVolumeName = "steam", AccountConfigVolumeName = "config", NoVncPort = 18083 };
        var unavailableTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27015, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 10 };
        var nextTarget = new TargetServer { Id = Guid.NewGuid(), Host = "127.0.0.1", Port = 27016, Enabled = true, RequiresReservation = false, PlayerTarget = 6, AttemptWindowSeconds = 720, Priority = 0 };
        db.AddRange(agent, unavailableTarget, nextTarget);
        await db.SaveChangesAsync();

        var observations = new TargetServerObservationStore();
        observations.Replace(unavailableTarget.Id, new TargetServerObservation(
            unavailableTarget.Id,
            "unavailable",
            null,
            null,
            null,
            DateTimeOffset.UtcNow));
        var scheduler = CreateSchedulerWithObservationStore(
            db,
            new FakeAgents(null),
            new SharedLibraryMaintenanceService(db),
            new FakeSelector(agent),
            new FakeA2s(),
            observations);

        await scheduler.TickAsync(CancellationToken.None);

        Assert.AreEqual(nextTarget.Id, (await db.WarmupAttempts.SingleAsync()).TargetServerId);
    }

    private static MatchmakingDbContext CreateDb() => new(new DbContextOptionsBuilder<MatchmakingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static WarmupSchedulerService CreateSchedulerWithCredentials(
        MatchmakingDbContext db,
        IAgentControlClient agents,
        SharedLibraryMaintenanceService maintenance,
        IHealthyAgentSelector selector,
        ISourceA2sClient a2s,
        IRconCredentialProtector credentials)
    {
        var constructor = typeof(WarmupSchedulerService)
            .GetConstructors()
            .SingleOrDefault(candidate => candidate.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(IRconCredentialProtector)));
        Assert.IsNotNull(constructor, "WarmupSchedulerService must receive the credential protector at the Agent request boundary.");
        return (WarmupSchedulerService)constructor.Invoke([db, agents, maintenance, selector, a2s, null, credentials, null, null, null, null]);
    }

    private static WarmupSchedulerService CreateSchedulerWithObservationStore(
        MatchmakingDbContext db,
        IAgentControlClient agents,
        SharedLibraryMaintenanceService maintenance,
        IHealthyAgentSelector selector,
        ISourceA2sClient a2s,
        TargetServerObservationStore observations)
    {
        var constructor = typeof(WarmupSchedulerService)
            .GetConstructors()
            .SingleOrDefault(candidate => candidate.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(TargetServerObservationStore)));
        Assert.IsNotNull(constructor, "WarmupSchedulerService must receive the target server observation store.");
        var arguments = constructor.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(MatchmakingDbContext))
                return (object?)db;
            if (parameter.ParameterType == typeof(IAgentControlClient))
                return agents;
            if (parameter.ParameterType == typeof(SharedLibraryMaintenanceService))
                return maintenance;
            if (parameter.ParameterType == typeof(IHealthyAgentSelector))
                return selector;
            if (parameter.ParameterType == typeof(ISourceA2sClient))
                return a2s;
            if (parameter.ParameterType == typeof(TargetServerObservationStore))
                return observations;
            return null;
        }).ToArray();
        return (WarmupSchedulerService)constructor.Invoke(arguments);
    }

    private static WarmupSchedulerService CreateSchedulerWithOptions(
        MatchmakingDbContext db,
        IAgentControlClient agents,
        SharedLibraryMaintenanceService maintenance,
        IHealthyAgentSelector selector,
        ISourceA2sClient a2s,
        CoreOptions options)
    {
        var constructor = typeof(WarmupSchedulerService).GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(MatchmakingDbContext))
                return (object?)db;
            if (parameter.ParameterType == typeof(IAgentControlClient))
                return agents;
            if (parameter.ParameterType == typeof(SharedLibraryMaintenanceService))
                return maintenance;
            if (parameter.ParameterType == typeof(IHealthyAgentSelector))
                return selector;
            if (parameter.ParameterType == typeof(ISourceA2sClient))
                return a2s;
            if (parameter.ParameterType == typeof(CoreOptions))
                return options;
            return null;
        }).ToArray();
        return (WarmupSchedulerService)constructor.Invoke(arguments);
    }

    private sealed class FakeAgents(
        AgentOperationSnapshot? snapshot,
        bool throwOnGetOperation = false,
        bool throwOnStart = false,
        bool throwOnStop = false,
        bool throwOnRestart = false,
        IReadOnlySet<Guid>? throwOnGetOperationIds = null,
        IReadOnlySet<Guid>? throwOnStopIds = null) : IAgentControlClient
    {
        private int _startCalls;
        private int _stopCalls;
        private int _restartSteamCalls;
        public int StartCalls => _startCalls;
        public int StopCalls => _stopCalls;
        public int RestartSteamCalls => _restartSteamCalls;
        public ConcurrentQueue<AgentOperationRequest> StartRequests { get; } = new();
        public AgentOperationRequest? LastStartRequest { get; private set; }
        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));
        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _startCalls);
            StartRequests.Enqueue(request);
            LastStartRequest = request;
            return throwOnStart
                ? Task.FromException<AgentOperationStartResult>(new HttpRequestException("agent_unavailable"))
                : Task.FromResult(new AgentOperationStartResult(new AgentOperationSnapshot(request.OperationId, "active", null, null, DateTimeOffset.UtcNow), false));
        }
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) =>
            throwOnGetOperation || throwOnGetOperationIds?.Contains(operationId) == true
                ? Task.FromException<AgentOperationSnapshot?>(new HttpRequestException("agent_unavailable"))
                : Task.FromResult(snapshot);
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _stopCalls);
            return throwOnStop || throwOnStopIds?.Contains(operationId) == true
                ? Task.FromException(new HttpRequestException("agent_unavailable"))
                : Task.CompletedTask;
        }
        public Task RestartSteamAsync(WarmupAgent agent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _restartSteamCalls);
            return throwOnRestart
                ? Task.FromException(new HttpRequestException("agent_unavailable"))
                : Task.CompletedTask;
        }
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRconCredentialProtector(string password) : IRconCredentialProtector
    {
        public string? LastCiphertext { get; private set; }
        public string Protect(string password) => throw new NotSupportedException();
        public string? Unprotect(string? ciphertext)
        {
            LastCiphertext = ciphertext;
            return ciphertext is null ? null : password;
        }
    }

    private sealed class FakeSelector(WarmupAgent agent) : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(agent);
        public Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WarmupAgent>>([agent]);
    }

    private sealed class BatchSelector(IReadOnlyList<WarmupAgent> agents) : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(agents.FirstOrDefault());
        public Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken) =>
            Task.FromResult(agents);
    }

    private sealed class FakeA2s(
        IReadOnlyDictionary<int, int>? playersByPort = null,
        IReadOnlySet<int>? failingPorts = null) : ISourceA2sClient
    {
        public Task<A2sServerInfo> GetInfoAsync(System.Net.IPEndPoint endpoint, CancellationToken cancellationToken) =>
            failingPorts?.Contains(endpoint.Port) == true
                ? Task.FromException<A2sServerInfo>(new TimeoutException("a2s_timeout"))
                : Task.FromResult(new A2sServerInfo("test-server", playersByPort?.GetValueOrDefault(endpoint.Port) ?? 0, 8, DateTimeOffset.UtcNow));
    }
}
