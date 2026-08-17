using System.Net;
using System.Net.Http.Json;
using L4d2LobbyAgent.Probe;
using L4d2Matchmaking.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class ProbeStatusEndpointTests
{
    [TestMethod]
    public async Task HealthzDoesNotRunTheProbe()
    {
        var sessionService = new FakeSessionService(new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));
        await using var factory = new AgentFactory(sessionService, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, sessionService.HealthCalls);
    }

    [TestMethod]
    public async Task ProbeStatusReturnsOkForAReadyFreshCheck()
    {
        var sessionService = new FakeSessionService(new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));
        await using var factory = new AgentFactory(sessionService, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/probe/status");
        var payload = await response.Content.ReadFromJsonAsync<ProbeStatusResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(payload);
        Assert.IsTrue(payload.Ready);
        Assert.AreEqual(1, sessionService.HealthCalls);
    }

    [TestMethod]
    public async Task ProbeStatusReturnsServiceUnavailableForAFailedFreshCheck()
    {
        var sessionService = new FakeSessionService(new AgentHealthSnapshot(false, "steam_not_logged_on", DateTimeOffset.UtcNow));
        await using var factory = new AgentFactory(sessionService, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/probe/status");
        var payload = await response.Content.ReadFromJsonAsync<ProbeStatusResponse>();

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsNotNull(payload);
        Assert.AreEqual("steam_not_logged_on", payload.Failure);
        Assert.AreEqual(1, sessionService.HealthCalls);
    }

    private sealed class AgentFactory(IAgentSteamSessionService sessionService, bool desktopRunning) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentSteamSessionService>();
                services.RemoveAll<ISteamDesktopDetector>();
                services.RemoveAll<IAgentReadinessMarker>();
                services.AddSingleton(sessionService);
                services.AddSingleton<ISteamDesktopDetector>(new FakeDesktopDetector(desktopRunning));
                services.AddSingleton<IAgentReadinessMarker>(new FakeReadinessMarker());
            });
        }
    }

    private sealed class FakeSessionService(AgentHealthSnapshot result) : IAgentSteamSessionService
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

    private sealed class FakeReadinessMarker : IAgentReadinessMarker
    {
        public Task MarkReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
