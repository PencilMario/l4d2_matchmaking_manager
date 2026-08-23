using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ContractPlayerEntryEvent = L4d2Matchmaking.Contracts.PlayerEntryEvent;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PlayerEntryEventEndpointTests
{
    [TestMethod]
    public async Task ReportingEndpointAcceptsOnlyTheAgentScopedToken()
    {
        using var environment = new CoreTestEnvironment();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var credentials = AgentReportingTokenService.Create(agentId);
        var otherCredentials = AgentReportingTokenService.Create(otherAgentId);
        await using var factory = new CoreFactory(agentId, credentials.Token, otherAgentId, otherCredentials.Token);
        using var anonymous = factory.CreateClient();
        using var management = factory.CreateClient();
        using var valid = factory.CreateClient();
        using var wrongAgent = factory.CreateClient();
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        valid.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
        wrongAgent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherCredentials.Token);
        var request = new PlayerEntryEventBatchRequest([Event(agentId)]);

        var anonymousResponse = await anonymous.PostAsJsonAsync("/v1/internal/player-entry-events", request);
        var managementResponse = await management.PostAsJsonAsync("/v1/internal/player-entry-events", request);
        var validResponse = await valid.PostAsJsonAsync("/v1/internal/player-entry-events", request);
        var wrongAgentResponse = await wrongAgent.PostAsJsonAsync("/v1/internal/player-entry-events", request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, managementResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, validResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, wrongAgentResponse.StatusCode);
        var result = await validResponse.Content.ReadFromJsonAsync<PlayerEntryEventBatchResponse>();
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Accepted);
    }

    private static ContractPlayerEntryEvent Event(Guid agentId) => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddMinutes(-1),
        Guid.NewGuid(),
        "lobby-1",
        PlayerEntryStatisticsContract.LobbyTypeStandard,
        agentId,
        "agent",
        null,
        Guid.NewGuid(),
        "203.0.113.7:27015",
        null,
        PlayerEntryStatisticsContract.GameModeVersus);

    private sealed class CoreFactory(
        Guid agentId,
        string token,
        Guid otherAgentId,
        string otherToken) : WebApplicationFactory<global::Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

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
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            if (db.WarmupAgents.Any())
                return;

            db.WarmupAgents.AddRange(
                AgentReportingTokenService.BuildAgent(agentId, token),
                AgentReportingTokenService.BuildAgent(otherAgentId, otherToken));
            db.SaveChanges();
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-core-api-token";
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
