using L4d2LobbyAgent.Probe;
using L4d2Matchmaking.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class ProbeStatusServiceTests
{
    [TestMethod]
    public async Task GetAsyncObservesTheActorForEveryRequestAndReturnsFreshObservations()
    {
        var sessionService = new BlockingFakeSessionService();
        var service = new ProbeStatusService(sessionService, new FakeDesktopDetector(true));

        var results = await Task.WhenAll(
            service.GetAsync(CancellationToken.None),
            service.GetAsync(CancellationToken.None));

        Assert.AreEqual(2, sessionService.HealthCalls);
        Assert.IsTrue(results.All(result => result.Ready));
        Assert.AreNotEqual(results[0].ObservedAt, results[1].ObservedAt);
    }

    [TestMethod]
    public async Task GetAsyncReportsDesktopFailureWithoutPersistingThePreviousSuccess()
    {
        var sessionService = new FixedFakeSessionService(new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));
        var service = new ProbeStatusService(sessionService, new FakeDesktopDetector(false));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_desktop_unavailable", result.Failure);
        Assert.AreEqual(1, sessionService.HealthCalls);
    }

    [TestMethod]
    public async Task GetAsyncMarksSteamApiInitializationFailureAsFailed()
    {
        var service = new ProbeStatusService(
            new FixedFakeSessionService(new AgentHealthSnapshot(false, "steam_api_init_failed", DateTimeOffset.UtcNow)),
            new FakeDesktopDetector(true));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_api_init_failed", result.Failure);
        Assert.AreEqual("failed", result.Checks.SteamApiInit);
    }

    [TestMethod]
    public async Task GetAsyncMarksInitializationAsUnknownWhenTheProbeWasNotConfigured()
    {
        var service = new ProbeStatusService(
            new FixedFakeSessionService(new AgentHealthSnapshot(false, "steam_api_library_not_configured", DateTimeOffset.UtcNow)),
            new FakeDesktopDetector(true));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("unknown", result.Checks.SteamApiInit);
    }

    private sealed class BlockingFakeSessionService : IAgentSteamSessionService
    {
        public int HealthCalls { get; private set; }

        public async Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken)
        {
            HealthCalls++;
            await Task.Delay(50, cancellationToken);
            return new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow);
        }

        public Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> StopAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedFakeSessionService(AgentHealthSnapshot result) : IAgentSteamSessionService
    {
        public int HealthCalls { get; private set; }

        public Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken)
        {
            HealthCalls++;
            return Task.FromResult(result);
        }

        public Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> StopAsync(Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeDesktopDetector(bool isRunning) : ISteamDesktopDetector
    {
        public bool IsRunning() => isRunning;
    }
}
