using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Statistics;
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
public sealed class PlayerEntryStatisticsEndpointTests
{
    [TestMethod]
    public async Task StatisticsEndpointAcceptsManagementBearerAndRejectsAgentBearer()
    {
        using var environment = new CoreTestEnvironment();
        var agentId = Guid.NewGuid();
        var reporting = AgentReportingTokenService.Create(agentId);
        await using var factory = new StatisticsFactory(agentId, reporting.Token);
        using var anonymous = factory.CreateClient();
        using var agent = factory.CreateClient();
        using var management = factory.CreateClient();
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reporting.Token);
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var anonymousResponse = await anonymous.GetAsync("/v1/statistics/player-entries");
        var agentResponse = await agent.GetAsync("/v1/statistics/player-entries");
        var managementResponse = await management.GetAsync("/v1/statistics/player-entries");

        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, agentResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, managementResponse.StatusCode);
    }

    [TestMethod]
    public async Task StatisticsEndpointUsesDefaultRecentRangeAndSupportsDimensionFilters()
    {
        using var environment = new CoreTestEnvironment();
        var agentId = Guid.NewGuid();
        var targetServerId = Guid.NewGuid();
        await using var factory = new StatisticsFactory(agentId, AgentReportingTokenService.Create(agentId).Token,
            seed: db =>
            {
                var now = DateTimeOffset.UtcNow;
                db.WarmupAgents.Add(new WarmupAgent
                {
                    Id = agentId,
                    Name = "current-agent",
                    DownloadRegion = "hongkong",
                    NoVncPort = 18083,
                });
                db.PlayerEntryEvents.AddRange(
                    Event(agentId, targetServerId, now.AddMinutes(-10), "standard", "versus", "snapshot-agent"),
                    Event(agentId, targetServerId, now.AddMinutes(-9), "standard", "", "snapshot-agent"),
                    Event(agentId, Guid.NewGuid(), now.AddMinutes(-8), "reserved", "coop", "other-target"));
            });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var defaultResponse = await client.GetAsync("/v1/statistics/player-entries");
        var defaultResult = await defaultResponse.Content.ReadFromJsonAsync<PlayerEntryStatisticsResult>();

        var from = DateTimeOffset.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
        var to = DateTimeOffset.UtcNow.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss'Z'");
        var filteredResponse = await client.GetAsync(
            $"/v1/statistics/player-entries?from={from}&to={to}&granularity=hour&lobbyType=standard&targetMode=versus&agentId={agentId}&targetServerId={targetServerId}");
        var filteredResult = await filteredResponse.Content.ReadFromJsonAsync<PlayerEntryStatisticsResult>();

        Assert.AreEqual(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.IsNotNull(defaultResult);
        Assert.AreEqual(3, defaultResult.TotalEntries);
        Assert.AreEqual(24, defaultResult.DailyPattern.Count);
        Assert.AreEqual(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.IsNotNull(filteredResult);
        Assert.AreEqual(2, filteredResult.TotalEntries);
        Assert.AreEqual("hour", filteredResult.Granularity);
        Assert.AreEqual(2, filteredResult.Agents.Single().Entries);
        Assert.AreEqual(1, filteredResult.DownloadRegions.Count);
        var defaultRegion = filteredResult.DownloadRegions.Single(row => row.Label == "默认");
        Assert.AreEqual(2, defaultRegion.Entries);
    }

    [TestMethod]
    public async Task StatisticsEndpointRejectsInvalidDateQuery()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new StatisticsFactory(Guid.NewGuid(), "unused-token");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var response = await client.GetAsync("/v1/statistics/player-entries?from=not-a-date");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static PlayerEntryEvent Event(
        Guid agentId,
        Guid targetServerId,
        DateTimeOffset occurredAt,
        string lobbyType,
        string mode,
        string agentName) => new()
    {
        EventId = Guid.NewGuid(),
        OccurredAtUtc = occurredAt,
        IngestedAtUtc = occurredAt,
        OperationId = Guid.NewGuid(),
        LobbyId = "lobby-1",
        LobbyType = lobbyType,
        AgentId = agentId,
        AgentNameSnapshot = agentName,
        DownloadRegionSnapshot = null,
        TargetServerId = targetServerId,
        TargetServerEndpointSnapshot = "203.0.113.7:27015",
        TargetServerNameSnapshot = "target-snapshot",
        TargetModeSnapshot = mode,
    };

    private sealed class StatisticsFactory(
        Guid agentId,
        string token,
        Action<MatchmakingDbContext>? seed = null) : WebApplicationFactory<global::Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");
        private bool seeded;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
                services.AddDbContext<MatchmakingDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            if (seeded)
                return;
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            seed?.Invoke(db);
            if (!db.WarmupAgents.Local.Any())
                db.WarmupAgents.Add(AgentReportingTokenService.BuildAgent(agentId, token));
            db.SaveChanges();
            seeded = true;
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-statistics-api-token";
        private readonly string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", ApiToken);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", connection);
        }
    }
}
