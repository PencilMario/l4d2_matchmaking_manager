using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Servers;
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
public sealed class TargetServerEndpointTests
{
    [TestMethod]
    public async Task CreateNormalizesEndpointAndAppliesConfirmedDefaults()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var response = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "Example.org",
            false,
            null,
            null,
            null,
            null,
            null));
        var server = await response.Content.ReadFromJsonAsync<TargetServerResponse>();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(server);
        Assert.AreEqual("example.org:27015", server.Endpoint);
        Assert.AreEqual(0, server.Priority);
        Assert.AreEqual(36, server.MaxConcurrentWarmups);
        Assert.AreEqual(720, server.AttemptWindowSeconds);
        Assert.AreEqual(6, server.PlayerTarget);
        Assert.IsTrue(server.Enabled);
    }

    [TestMethod]
    public async Task ModeAcceptsPresetsPreservesBlankAsNullAndRejectsCustomValues()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var coop = await client.PostAsJsonAsync("/v1/servers", new
        {
            endpoint = "203.0.113.7:27015",
            requiresReservation = false,
            gameMode = "coop",
        });
        var blank = await client.PostAsJsonAsync("/v1/servers", new
        {
            endpoint = "203.0.113.8:27015",
            requiresReservation = false,
            gameMode = " ",
        });
        var invalid = await client.PostAsJsonAsync("/v1/servers", new
        {
            endpoint = "203.0.113.9:27015",
            requiresReservation = false,
            gameMode = "deathmatch",
        });
        var coopJson = await coop.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var blankJson = await blank.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.AreEqual(HttpStatusCode.Created, coop.StatusCode);
        Assert.AreEqual("coop", coopJson.GetProperty("gameMode").GetString());
        Assert.AreEqual(HttpStatusCode.Created, blank.StatusCode);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, blankJson.GetProperty("gameMode").ValueKind);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        StringAssert.Contains(await invalid.Content.ReadAsStringAsync(), "invalid_game_mode");
    }

    [TestMethod]
    public async Task UpdateChangesConfiguredGameMode()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var created = await client.PostAsJsonAsync("/v1/servers", new
        {
            endpoint = "203.0.113.7:27015",
            requiresReservation = false,
            gameMode = "coop",
        });
        var createdJson = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var serverId = createdJson.GetProperty("id").GetGuid();
        var updated = await client.PutAsJsonAsync($"/v1/servers/{serverId}", new
        {
            endpoint = "203.0.113.7:27015",
            requiresReservation = false,
            gameMode = "versus",
        });
        var updatedJson = await updated.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        Assert.AreEqual("versus", updatedJson.GetProperty("gameMode").GetString());
    }

    [TestMethod]
    public async Task ReservationServerUsesOneEffectiveWarmupAndRejectsMalformedEndpoint()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var malformed = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "https://example.org:27015",
            false,
            null,
            null,
            null,
            null,
            null));
        var reserved = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "203.0.113.7:28015",
            true,
            5,
            12,
            600,
            8,
            null));
        var server = await reserved.Content.ReadFromJsonAsync<TargetServerResponse>();

        Assert.AreEqual(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, reserved.StatusCode);
        Assert.IsNotNull(server);
        Assert.AreEqual(1, server.MaxConcurrentWarmups);
        Assert.AreEqual(5, server.Priority);
    }

    [TestMethod]
    public async Task UpdateListGetAndDeleteManageTheSameServer()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        var created = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "example.org",
            false,
            null,
            null,
            null,
            null,
            null));
        var server = await created.Content.ReadFromJsonAsync<TargetServerResponse>();
        Assert.IsNotNull(server);

        var updated = await client.PutAsJsonAsync($"/v1/servers/{server.Id}", new UpdateTargetServerRequest(
            "203.0.113.7:28015",
            false,
            -2,
            9,
            900,
            7,
            false));
        var listed = await client.GetFromJsonAsync<List<TargetServerResponse>>("/v1/servers");
        var fetched = await client.GetAsync($"/v1/servers/{server.Id}");
        var deleted = await client.DeleteAsync($"/v1/servers/{server.Id}");
        var missing = await client.GetAsync($"/v1/servers/{server.Id}");

        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        Assert.IsNotNull(listed);
        Assert.AreEqual(1, listed.Count);
        Assert.AreEqual("203.0.113.7:28015", listed[0].Endpoint);
        Assert.AreEqual(HttpStatusCode.OK, fetched.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task CreateRequiresAuthentication()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "example.org",
            false,
            null,
            null,
            null,
            null,
            null));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RconPasswordIsRedactedAndCanBeCleared()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var reserved = await client.PostAsJsonAsync("/v1/servers", new
        {
            endpoint = "203.0.113.7:27083",
            requiresReservation = true,
            rconPassword = "not-returned-to-clients",
        });
        var createdJson = await reserved.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var serverId = createdJson.GetProperty("id").GetGuid();

        var updated = await client.PutAsJsonAsync($"/v1/servers/{serverId}", new
        {
            endpoint = "203.0.113.7:27083",
            requiresReservation = true,
            priority = 0,
            maxConcurrentWarmups = 36,
            attemptWindowSeconds = 720,
            playerTarget = 6,
            enabled = true,
            rconPassword = (string?)null,
        });
        var updatedJson = await updated.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.AreEqual(HttpStatusCode.Created, reserved.StatusCode);
        Assert.IsTrue(createdJson.GetProperty("hasRconCredentials").GetBoolean());
        Assert.IsFalse(createdJson.TryGetProperty("rconPassword", out _));
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        Assert.IsFalse(updatedJson.GetProperty("hasRconCredentials").GetBoolean());
    }

    [TestMethod]
    public async Task NormalTargetRejectsRconPasswordAndClearsItWhenLeavingReservationMode()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var normal = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "203.0.113.7:27083", false, null, null, null, null, null, "must-not-be-accepted"));
        var reserved = await client.PostAsJsonAsync("/v1/servers", new CreateTargetServerRequest(
            "203.0.113.7:27083", true, null, null, null, null, null, "credential-to-clear"));
        var reservedServer = await reserved.Content.ReadFromJsonAsync<TargetServerResponse>();
        Assert.IsNotNull(reservedServer);

        var converted = await client.PutAsJsonAsync($"/v1/servers/{reservedServer.Id}", new UpdateTargetServerRequest(
            "203.0.113.7:27083", false, null, null, null, null, null));
        var convertedServer = await converted.Content.ReadFromJsonAsync<TargetServerResponse>();

        Assert.AreEqual(HttpStatusCode.BadRequest, normal.StatusCode);
        StringAssert.Contains(await normal.Content.ReadAsStringAsync(), "rcon_requires_reservation");
        Assert.AreEqual(HttpStatusCode.Created, reserved.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, converted.StatusCode);
        Assert.IsNotNull(convertedServer);
        Assert.IsFalse(convertedServer.HasRconCredentials);
    }

    [TestMethod]
    public async Task DisableStopsCurrentAttemptAndReleasesItsReservationLease()
    {
        using var environment = new CoreTestEnvironment();
        var agents = new FakeAgentClient();
        await using var factory = new ServerFactory(agents);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        var (server, agent, attempt) = await SeedReservedAttemptAsync(factory);

        var response = await client.PutAsJsonAsync($"/v1/servers/{server.Id}", new UpdateTargetServerRequest(
            "203.0.113.7:27015",
            true,
            0,
            1,
            720,
            6,
            false));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, agents.StopCalls);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.IsFalse((await db.TargetServers.SingleAsync(candidate => candidate.Id == server.Id)).Enabled);
        Assert.AreEqual("completed", (await db.WarmupAttempts.SingleAsync(candidate => candidate.Id == attempt.Id)).State);
        Assert.IsFalse(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id));
    }

    [TestMethod]
    public async Task DeleteStopsCurrentAttemptBeforeRemovingTargetServer()
    {
        using var environment = new CoreTestEnvironment();
        var agents = new FakeAgentClient();
        await using var factory = new ServerFactory(agents);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        var (server, _, attempt) = await SeedReservedAttemptAsync(factory);

        var response = await client.DeleteAsync($"/v1/servers/{server.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(1, agents.StopCalls);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.IsFalse(await db.TargetServers.AnyAsync(candidate => candidate.Id == server.Id));
        Assert.AreEqual("completed", (await db.WarmupAttempts.SingleAsync(candidate => candidate.Id == attempt.Id)).State);
        Assert.IsFalse(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id));
    }

    [TestMethod]
    public async Task FailedDisableDrainQuarantinesAgentAndKeepsTargetDisabled()
    {
        using var environment = new CoreTestEnvironment();
        var agents = new FakeAgentClient(throwOnStop: true);
        await using var factory = new ServerFactory(agents);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        var (server, agent, attempt) = await SeedReservedAttemptAsync(factory);

        var response = await client.PutAsJsonAsync($"/v1/servers/{server.Id}", new UpdateTargetServerRequest(
            "203.0.113.7:27015",
            true,
            0,
            1,
            720,
            6,
            false));

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual(1, agents.StopCalls);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.IsFalse((await db.TargetServers.SingleAsync(candidate => candidate.Id == server.Id)).Enabled);
        Assert.AreEqual("quarantined", (await db.WarmupAgents.SingleAsync(candidate => candidate.Id == agent.Id)).Status);
        Assert.AreEqual("active", (await db.WarmupAttempts.SingleAsync(candidate => candidate.Id == attempt.Id)).State);
        Assert.IsTrue(await db.ReservationLeases.AnyAsync(lease => lease.TargetServerId == server.Id && lease.OperationId == attempt.OperationId));
    }

    private static async Task<(TargetServer Server, WarmupAgent Agent, WarmupAttempt Attempt)> SeedReservedAttemptAsync(ServerFactory factory)
    {
        var server = new TargetServer
        {
            Id = Guid.NewGuid(), Host = "203.0.113.7", Port = 27015, RequiresReservation = true,
            Enabled = true, MaxConcurrentWarmups = 1, AttemptWindowSeconds = 720, PlayerTarget = 6,
        };
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(), Name = $"agent-{Guid.NewGuid():N}", Status = "running",
            SteamDataVolumeName = $"steam-{Guid.NewGuid():N}", AccountConfigVolumeName = $"config-{Guid.NewGuid():N}",
            NoVncPort = 18083,
        };
        var attempt = new WarmupAttempt
        {
            Id = Guid.NewGuid(), TargetServerId = server.Id, WarmupAgentId = agent.Id, OperationId = Guid.NewGuid(),
            Mode = "reserved", State = "active", Phase = "awaiting_first_member", StartedAt = DateTimeOffset.UtcNow,
            ObservedAt = DateTimeOffset.UtcNow,
        };
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        db.AddRange(server, agent, attempt, new ReservationLease
        {
            TargetServerId = server.Id,
            OperationId = attempt.OperationId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        });
        await db.SaveChangesAsync();
        return (server, agent, attempt);
    }

    private sealed class ServerFactory(IAgentControlClient? agentClient = null) : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
                services.AddDbContext<MatchmakingDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                if (agentClient is not null)
                {
                    services.RemoveAll<IAgentControlClient>();
                    services.AddSingleton(agentClient);
                }
            });
        }
    }

    private sealed class FakeAgentClient(bool throwOnStop = false) : IAgentControlClient
    {
        public int StopCalls { get; private set; }
        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
        {
            StopCalls++;
            return throwOnStop
                ? Task.FromException(new HttpRequestException("agent_unavailable"))
                : Task.CompletedTask;
        }
        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-target-server-token";
        private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? _token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? _connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");
        private readonly string? _rconEncryptionKey = Environment.GetEnvironmentVariable("CORE_RCON_ENCRYPTION_KEY");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", ApiToken);
            Environment.SetEnvironmentVariable(
                "CORE_DATABASE_CONNECTION_STRING",
                "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
            Environment.SetEnvironmentVariable(
                "CORE_RCON_ENCRYPTION_KEY",
                Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()));
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", _token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", _connection);
            Environment.SetEnvironmentVariable("CORE_RCON_ENCRYPTION_KEY", _rconEncryptionKey);
        }
    }
}
