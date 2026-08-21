using L4d2MatchmakingCore.Agents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class AgentVncSessionServiceTests
{
    [TestMethod]
    public async Task OpeningAStoppedVncStartsItAndCloseStopsOnlyThatSession()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();

        var session = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);
        await manager.CloseAsync(agentId, CancellationToken.None);

        Assert.IsTrue(session.StartedVnc);
        CollectionAssert.AreEqual(new[] { "status", "start", "stop" }, runtime.Commands.ToArray());
        Assert.IsFalse(manager.TryGet(agentId, session.Token, out _));
    }

    [TestMethod]
    public async Task ExistingVncRemainsRunningAfterExpiry()
    {
        var runtime = new FakeRuntime(AgentVncState.Running);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        var session = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        await manager.CleanupExpiredAsync(session.ExpiresAt.AddSeconds(1), CancellationToken.None);

        Assert.IsFalse(session.StartedVnc);
        CollectionAssert.AreEqual(new[] { "status" }, runtime.Commands.ToArray());
        Assert.IsFalse(manager.TryGet(agentId, session.Token, out _));
    }

    [TestMethod]
    public async Task OpeningAPartialVncFailsWithoutTakingOwnershipOfItsProcesses()
    {
        var runtime = new FakeRuntime(AgentVncState.Partial);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => manager.OpenAsync(agentId, "container-id", CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "status" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task ReopeningReplacesTheTokenWithoutRestartingVnc()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        var first = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);
        var second = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        Assert.AreNotEqual(first.Token, second.Token);
        Assert.IsFalse(manager.TryGet(agentId, first.Token, out _));
        Assert.IsTrue(manager.TryGet(agentId, second.Token, out _));
        CollectionAssert.AreEqual(new[] { "status", "start", "status" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task ReopeningAPartialVncKeepsTheExistingSessionAndRefusesANewToken()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        var first = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);
        runtime.State = AgentVncState.Partial;

        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => manager.OpenAsync(agentId, "container-id", CancellationToken.None));

        Assert.AreEqual("agent_vnc_partial_state", exception.Message);
        Assert.IsTrue(manager.TryGet(agentId, first.Token, out _));
        CollectionAssert.AreEqual(new[] { "status", "start", "status" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task OpeningAfterAnExpiredSessionClosesTheSessionOwnedVncFirst()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped);
        var manager = new AgentVncSessionService(runtime, TimeSpan.Zero);
        var agentId = Guid.NewGuid();
        await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "status", "start", "stop", "status", "start" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task ClosingAnExistingVncRevokesTheActiveProxyLifetime()
    {
        var runtime = new FakeRuntime(AgentVncState.Running);
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        var session = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        await manager.CloseAsync(agentId, CancellationToken.None);

        Assert.IsTrue(session.LifetimeToken.IsCancellationRequested);
        Assert.IsFalse(manager.TryGet(agentId, session.Token, out _));
        CollectionAssert.AreEqual(new[] { "status" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task FailedOwnedVncStopIsRetriedByCleanup()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped) { RemainingStopFailures = 1 };
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        var session = await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => manager.CloseAsync(agentId, CancellationToken.None));
        await manager.CleanupExpiredAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.IsTrue(session.LifetimeToken.IsCancellationRequested);
        CollectionAssert.AreEqual(new[] { "status", "start", "stop", "stop" }, runtime.Commands.ToArray());
    }

    [TestMethod]
    public async Task OpeningWaitsForAnInProgressOwnedVncClose()
    {
        var runtime = new FakeRuntime(AgentVncState.Stopped)
        {
            StopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            AllowStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var manager = new AgentVncSessionService(runtime, TimeSpan.FromMinutes(15));
        var agentId = Guid.NewGuid();
        await manager.OpenAsync(agentId, "container-id", CancellationToken.None);

        var closing = manager.CloseAsync(agentId, CancellationToken.None);
        await runtime.StopStarted.Task;
        var opening = manager.OpenAsync(agentId, "container-id", CancellationToken.None);
        Assert.IsFalse(opening.IsCompleted);
        runtime.AllowStop.SetResult();

        await closing;
        await opening;
        CollectionAssert.AreEqual(new[] { "status", "start", "stop", "status", "start" }, runtime.Commands.ToArray());
    }

    private sealed class FakeRuntime(AgentVncState state) : IAgentContainerRuntime
    {
        public List<string> Commands { get; } = [];
        public TaskCompletionSource? AllowStop { get; init; }
        public int RemainingStopFailures { get; set; }
        public TaskCompletionSource? StopStarted { get; init; }
        public AgentVncState State { get; set; } = state;

        public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

        public Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken) =>
            Task.FromResult("container-id");

        public Task<long> GetMemoryLimitAsync(string containerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpdateMemoryLimitAsync(string containerId, long memoryLimitBytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StartAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken)
        {
            Commands.Add("status");
            return Task.FromResult(State);
        }

        public Task StartVncAsync(string containerId, CancellationToken cancellationToken)
        {
            Commands.Add("start");
            State = AgentVncState.Running;
            return Task.CompletedTask;
        }

        public async Task StopVncAsync(string containerId, CancellationToken cancellationToken)
        {
            Commands.Add("stop");
            StopStarted?.TrySetResult();
            if (AllowStop is not null)
                await AllowStop.Task.WaitAsync(cancellationToken);
            if (RemainingStopFailures > 0)
            {
                RemainingStopFailures--;
                throw new InvalidOperationException("vnc_stop_failed");
            }
            State = AgentVncState.Stopped;
        }
    }
}
