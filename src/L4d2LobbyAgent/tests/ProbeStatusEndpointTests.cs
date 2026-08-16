using System.Net;
using System.Net.Http.Json;
using L4d2LobbyAgent.Probe;
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
        var runner = new FakeRunner(ProbeCommandResult.Success(550, 0));
        await using var factory = new AgentFactory(runner, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, runner.CallCount);
    }

    [TestMethod]
    public async Task ProbeStatusReturnsOkForAReadyFreshCheck()
    {
        var runner = new FakeRunner(ProbeCommandResult.Success(550, 2));
        await using var factory = new AgentFactory(runner, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/probe/status");
        var payload = await response.Content.ReadFromJsonAsync<ProbeStatusResponse>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(payload);
        Assert.IsTrue(payload.Ready);
        Assert.AreEqual(1, runner.CallCount);
    }

    [TestMethod]
    public async Task ProbeStatusReturnsServiceUnavailableForAFailedFreshCheck()
    {
        var runner = new FakeRunner(new ProbeCommandResult(false, "steam_not_logged_on", 550, null));
        await using var factory = new AgentFactory(runner, true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/probe/status");
        var payload = await response.Content.ReadFromJsonAsync<ProbeStatusResponse>();

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsNotNull(payload);
        Assert.AreEqual("steam_not_logged_on", payload.Failure);
        Assert.AreEqual(1, runner.CallCount);
    }

    private sealed class AgentFactory(IProbeCommandRunner runner, bool desktopRunning) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProbeCommandRunner>();
                services.RemoveAll<ISteamDesktopDetector>();
                services.AddSingleton(runner);
                services.AddSingleton<ISteamDesktopDetector>(new FakeDesktopDetector(desktopRunning));
            });
        }
    }

    private sealed class FakeRunner(ProbeCommandResult result) : IProbeCommandRunner
    {
        public int CallCount { get; private set; }

        public Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeDesktopDetector(bool isRunning) : ISteamDesktopDetector
    {
        public bool IsRunning() => isRunning;
    }
}
