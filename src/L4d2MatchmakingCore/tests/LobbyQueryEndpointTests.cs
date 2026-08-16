using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LobbyQueryEndpointTests
{
    [TestMethod]
    public async Task QueryUsesHealthyAgentWithoutExposingItsIdentity()
    {
        using var environment = new TestEnvironment();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var lobby = new LobbySnapshot("109775242170052468", "76561198000000000", [], new Dictionary<string, string>(), DateTimeOffset.UnixEpoch);
        await using var factory = new QueryFactory(new FakeSelector(agent), new FakeClient(lobby));
        using var anonymous = factory.CreateClient();
        using var authorized = factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestEnvironment.Token);

        var unauthorized = await anonymous.GetAsync($"/v1/lobbies/{lobby.LobbyId}");
        var response = await authorized.GetAsync($"/v1/lobbies/{lobby.LobbyId}");
        var result = await response.Content.ReadFromJsonAsync<LobbySnapshot>();
        var malformed = await authorized.GetAsync("/v1/lobbies/0");

        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(lobby.LobbyId, result.LobbyId);
        Assert.AreEqual(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [TestMethod]
    public async Task QueryReturnsServiceUnavailableWhenNoAgentIsHealthy()
    {
        using var environment = new TestEnvironment();
        await using var factory = new QueryFactory(new EmptySelector(), new FakeClient(new LobbySnapshot("1", "1", [], new Dictionary<string, string>(), DateTimeOffset.UnixEpoch)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestEnvironment.Token);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class QueryFactory(IHealthyAgentSelector selector, IAgentControlClient client) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
            services.AddDbContext<MatchmakingDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
            services.RemoveAll<IHealthyAgentSelector>();
            services.RemoveAll<IAgentControlClient>();
            services.AddSingleton(selector);
            services.AddSingleton(client);
        });
    }

    private sealed class FakeSelector(WarmupAgent agent) : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(agent);
    }

    private sealed class EmptySelector : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(null);
    }

    private sealed class FakeClient(LobbySnapshot lobby) : IAgentControlClient
    {
        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => Task.FromResult(lobby);
    }

    private sealed class TestEnvironment : IDisposable
    {
        public const string Token = "test-lobby-query-token";
        private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? _token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? _connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

        public TestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", Token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", _token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", _connection);
        }
    }
}
