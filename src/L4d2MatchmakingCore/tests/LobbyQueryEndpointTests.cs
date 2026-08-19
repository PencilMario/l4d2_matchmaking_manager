using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Profiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
        var agentClient = new FakeClient(lobby);
        await using var factory = new QueryFactory(new FakeSelector(agent), agentClient);
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
        Assert.AreEqual(agent.Id, agentClient.QueryCalls.Single().AgentId);
        Assert.IsTrue(agentClient.QueryCalls.Single().IncludeMembers);
        Assert.AreEqual(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [TestMethod]
    public async Task QueryMergesSteamProfilesIntoCompleteMemberData()
    {
        using var environment = new TestEnvironment();
        const string steamId = "76561198000000000";
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var lobby = Snapshot() with
        {
            Members = [new LobbyMemberSnapshot(steamId, null)],
        };
        var profiles = new FakeProfileService(new Dictionary<string, SteamProfileData>
        {
            [steamId] = new("Player One", "https://cdn.example/player.jpg"),
        });
        await using var factory = new QueryFactory(new FakeSelector(agent), new FakeClient(lobby), profiles);
        using var client = Authorized(factory);

        var response = await client.GetAsync($"/v1/lobbies/{lobby.LobbyId}");
        var result = await response.Content.ReadFromJsonAsync<LobbySnapshot>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual("Player One", result.Members.Single().PersonaName);
        Assert.AreEqual("https://cdn.example/player.jpg", result.Members.Single().AvatarUrl);
        Assert.AreEqual(1, profiles.Calls);
    }

    [TestMethod]
    public async Task QueryPrefersIdleAgentOverStandardAndReservationWarmups()
    {
        using var environment = new TestEnvironment();
        var idle = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var standard = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var reserved = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot());
        await using var factory = new QueryFactory(new FakeSelector(reserved, standard, idle), agentClient);
        await factory.SeedAttemptsAsync(
            Attempt(standard.Id, "standard", "active"),
            Attempt(reserved.Id, "reserved", "active"));
        using var client = Authorized(factory);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(idle.Id, agentClient.QueryCalls.Single().AgentId);
        Assert.IsTrue(agentClient.QueryCalls.Single().IncludeMembers);
    }

    [TestMethod]
    public async Task QueryUsesStandardWarmupWhenNoIdleAgentExists()
    {
        using var environment = new TestEnvironment();
        var standard = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var reserved = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot());
        await using var factory = new QueryFactory(new FakeSelector(reserved, standard), agentClient);
        await factory.SeedAttemptsAsync(
            Attempt(standard.Id, "standard", "active"),
            Attempt(reserved.Id, "reserved", "active"));
        using var client = Authorized(factory);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(standard.Id, agentClient.QueryCalls.Single().AgentId);
        Assert.IsTrue(agentClient.QueryCalls.Single().IncludeMembers);
    }

    [TestMethod]
    public async Task QueryUsesTheAgentOwningTheRequestedActiveLobbyBeforeAnIdleAgent()
    {
        using var environment = new TestEnvironment();
        const string lobbyId = "109775242170052468";
        var owner = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var idle = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot());
        await using var factory = new QueryFactory(new FakeSelector(idle, owner), agentClient);
        var attempt = Attempt(owner.Id, "reserved", "active");
        attempt.LobbyId = lobbyId;
        await factory.SeedAttemptsAsync(attempt);
        using var client = Authorized(factory);

        var response = await client.GetAsync($"/v1/lobbies/{lobbyId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(owner.Id, agentClient.QueryCalls.Single().AgentId);
        Assert.IsTrue(agentClient.QueryCalls.Single().IncludeMembers);
    }

    [TestMethod]
    public async Task QueryUsesMetadataOnlyFallbackWhenOnlyReservationAgentsAreHealthy()
    {
        using var environment = new TestEnvironment();
        var reserved = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot(LobbyMemberDataStatus.MetadataOnlyNoQueryAgent));
        await using var factory = new QueryFactory(new FakeSelector(reserved), agentClient);
        await factory.SeedAttemptsAsync(Attempt(reserved.Id, "reserved", "active"));
        using var client = Authorized(factory);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");
        var result = await response.Content.ReadFromJsonAsync<LobbySnapshot>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.AreEqual(LobbyMemberDataStatus.MetadataOnlyNoQueryAgent, result.MemberDataStatus);
        Assert.AreEqual(reserved.Id, agentClient.QueryCalls.Single().AgentId);
        Assert.IsFalse(agentClient.QueryCalls.Single().IncludeMembers);
        Assert.AreEqual(0, factory.ProfileService.Calls);
    }

    [TestMethod]
    public async Task QueryReturnsExplicitUnavailableWhenAgentCannotConfirmLobbyData()
    {
        using var environment = new TestEnvironment();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot())
        {
            QueryException = new AgentLobbyQueryException("lobby_data_unavailable"),
        };
        await using var factory = new QueryFactory(new FakeSelector(agent), agentClient);
        using var client = Authorized(factory);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("\"lobby_data_unavailable\"", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task QueryQuarantinesAgentWhenTemporaryQueryCannotPreserveItsWarmupLobby()
    {
        using var environment = new TestEnvironment();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var agentClient = new FakeClient(Snapshot())
        {
            QueryException = new AgentLobbyQueryException("lobby_operation_preservation_failed"),
        };
        await using var factory = new QueryFactory(new FakeSelector(agent), agentClient);
        await factory.SeedAgentsAsync(agent);
        await factory.SeedAttemptsAsync(Attempt(agent.Id, "standard", "active"));
        using var client = Authorized(factory);

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");
        var status = await factory.GetAgentStatusAsync(agent.Id);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("\"lobby_operation_preservation_failed\"", await response.Content.ReadAsStringAsync());
        Assert.AreEqual("quarantined", status);
    }

    [TestMethod]
    public async Task ConcurrentQueriesDoNotQueueASecondTemporaryJoinOnTheSameAgent()
    {
        using var environment = new TestEnvironment();
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var pendingQuery = new TaskCompletionSource<LobbySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agentClient = new FakeClient(Snapshot()) { PendingQuery = pendingQuery };
        await using var factory = new QueryFactory(new FakeSelector(agent), agentClient);
        using var firstClient = Authorized(factory);
        using var secondClient = Authorized(factory);

        var first = firstClient.GetAsync("/v1/lobbies/109775242170052468");
        await agentClient.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await secondClient.GetAsync("/v1/lobbies/109775242170052468");
        pendingQuery.SetResult(Snapshot());
        var firstResponse = await first;

        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.AreEqual("\"lobby_query_agent_unavailable\"", await second.Content.ReadAsStringAsync());
        Assert.AreEqual(1, agentClient.QueryCalls.Count);
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

    private sealed class QueryFactory(IHealthyAgentSelector selector, IAgentControlClient client, FakeProfileService? profileService = null) : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");
        private readonly InMemoryDatabaseRoot _databaseRoot = new();
        public FakeProfileService ProfileService { get; } = profileService ?? new FakeProfileService(new Dictionary<string, SteamProfileData>());

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
            services.AddDbContext<MatchmakingDbContext>(options => options.UseInMemoryDatabase(_databaseName, _databaseRoot));
            services.RemoveAll<IHealthyAgentSelector>();
            services.RemoveAll<IAgentControlClient>();
            services.RemoveAll<ISteamProfileService>();
            services.AddSingleton(selector);
            services.AddSingleton(client);
            services.AddSingleton<ISteamProfileService>(ProfileService);
        });

        public async Task SeedAttemptsAsync(params WarmupAttempt[] attempts)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            dbContext.WarmupAttempts.AddRange(attempts);
            await dbContext.SaveChangesAsync();
        }

        public async Task SeedAgentsAsync(params WarmupAgent[] agents)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            dbContext.WarmupAgents.AddRange(agents);
            await dbContext.SaveChangesAsync();
        }

        public async Task<string?> GetAgentStatusAsync(Guid agentId)
        {
            await using var scope = Services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>()
                .WarmupAgents.FindAsync(agentId))?.Status;
        }
    }

    private sealed class FakeSelector(params WarmupAgent[] agents) : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(agents.FirstOrDefault());
        public Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WarmupAgent>>(agents);
    }

    private sealed class EmptySelector : IHealthyAgentSelector
    {
        public Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken) => Task.FromResult<WarmupAgent?>(null);
        public Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WarmupAgent>>([]);
    }

    private sealed class FakeProfileService(IReadOnlyDictionary<string, SteamProfileData> profiles) : ISteamProfileService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyDictionary<string, SteamProfileData>> ResolveAsync(
            IReadOnlyCollection<string> steamIds,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyDictionary<string, SteamProfileData>>(
                steamIds.Distinct(StringComparer.Ordinal).ToDictionary(
                    id => id,
                    id => profiles.GetValueOrDefault(id, new SteamProfileData(null, null)),
                    StringComparer.Ordinal));
        }
    }

    private sealed class FakeClient(LobbySnapshot lobby) : IAgentControlClient
    {
        public List<QueryCall> QueryCalls { get; } = [];
        public Exception? QueryException { get; init; }
        public TaskCompletionSource<LobbySnapshot>? PendingQuery { get; init; }
        public TaskCompletionSource<bool> QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => Task.FromResult(lobby);
        public Task<LobbySnapshot> QueryLobbyAsync(WarmupAgent agent, string lobbyId, bool includeMembers, CancellationToken cancellationToken)
        {
            QueryCalls.Add(new QueryCall(agent.Id, includeMembers));
            QueryStarted.TrySetResult(true);
            if (QueryException is not null)
                return Task.FromException<LobbySnapshot>(QueryException);
            if (PendingQuery is not null)
                return PendingQuery.Task;
            return Task.FromResult(lobby);
        }
    }

    private sealed record QueryCall(Guid AgentId, bool IncludeMembers);

    private static HttpClient Authorized(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestEnvironment.Token);
        return client;
    }

    private static LobbySnapshot Snapshot(string status = LobbyMemberDataStatus.Complete) => new(
        "109775242170052468",
        "76561198000000000",
        [],
        new Dictionary<string, string> { ["Game:campaign"] = "L4D2C1" },
        DateTimeOffset.UnixEpoch,
        status);

    private static WarmupAttempt Attempt(Guid agentId, string mode, string state) => new()
    {
        Id = Guid.NewGuid(),
        WarmupAgentId = agentId,
        TargetServerId = Guid.NewGuid(),
        OperationId = Guid.NewGuid(),
        Mode = mode,
        State = state,
        StartedAt = DateTimeOffset.UtcNow,
        ObservedAt = DateTimeOffset.UtcNow,
    };

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
