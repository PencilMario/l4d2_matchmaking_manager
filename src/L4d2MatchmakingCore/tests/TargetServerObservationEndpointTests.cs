using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Servers;
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
public sealed class TargetServerObservationEndpointTests
{
    [TestMethod]
    public async Task ObservationsRequireTheConfiguredBearerToken()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        using var anonymous = factory.CreateClient();
        using var incorrect = factory.CreateClient();
        incorrect.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");

        var anonymousResponse = await anonymous.GetAsync("/v1/servers/observations");
        var incorrectResponse = await incorrect.GetAsync("/v1/servers/observations");

        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, incorrectResponse.StatusCode);
    }

    [TestMethod]
    public async Task ObservationsReturnEmptyArrayWhenNoTargetServersAreConfigured()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        using var client = CreateAuthorizedClient(factory);

        var observations = await client.GetFromJsonAsync<TargetServerObservationResponse[]>("/v1/servers/observations");

        Assert.IsNotNull(observations);
        Assert.AreEqual(0, observations.Length);
    }

    [TestMethod]
    public async Task ObservationsProjectPendingWhenTargetHasNoSnapshot()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        var target = CreateTarget();
        await factory.SeedAsync(target);
        using var client = CreateAuthorizedClient(factory);

        var observations = await client.GetFromJsonAsync<TargetServerObservationResponse[]>("/v1/servers/observations");

        Assert.IsNotNull(observations);
        Assert.AreEqual(1, observations.Length);
        Assert.AreEqual(target.Id, observations[0].TargetServerId);
        Assert.AreEqual("pending", observations[0].Status);
        Assert.IsNull(observations[0].ServerName);
        Assert.IsNull(observations[0].PlayerCount);
        Assert.IsNull(observations[0].MaxPlayers);
        Assert.IsNull(observations[0].ObservedAt);
    }

    [TestMethod]
    public async Task ObservationsProjectOnlineSnapshot()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        var target = CreateTarget();
        var observedAt = DateTimeOffset.UtcNow;
        await factory.SeedAsync(target);
        factory.Store.Replace(target.Id, new TargetServerObservation(
            target.Id,
            "online",
            "L4D1 HK Versus #1",
            2,
            12,
            observedAt));
        using var client = CreateAuthorizedClient(factory);

        var observations = await client.GetFromJsonAsync<TargetServerObservationResponse[]>("/v1/servers/observations");

        Assert.IsNotNull(observations);
        Assert.AreEqual(1, observations.Length);
        Assert.AreEqual("online", observations[0].Status);
        Assert.AreEqual("L4D1 HK Versus #1", observations[0].ServerName);
        Assert.AreEqual(2, observations[0].PlayerCount);
        Assert.AreEqual(12, observations[0].MaxPlayers);
        Assert.AreEqual(observedAt, observations[0].ObservedAt);
    }

    [TestMethod]
    public async Task ObservationsProjectUnavailableSnapshotWithNullLiveFields()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        var target = CreateTarget();
        await factory.SeedAsync(target);
        factory.Store.Replace(target.Id, new TargetServerObservation(
            target.Id,
            "unavailable",
            null,
            null,
            null,
            DateTimeOffset.UtcNow));
        using var client = CreateAuthorizedClient(factory);

        var observations = await client.GetFromJsonAsync<TargetServerObservationResponse[]>("/v1/servers/observations");

        Assert.IsNotNull(observations);
        Assert.AreEqual(1, observations.Length);
        Assert.AreEqual("unavailable", observations[0].Status);
        Assert.IsNull(observations[0].ServerName);
        Assert.IsNull(observations[0].PlayerCount);
        Assert.IsNull(observations[0].MaxPlayers);
        Assert.IsNotNull(observations[0].ObservedAt);
    }

    [TestMethod]
    public async Task LiteralObservationRouteIsNotCapturedByServerIdRoute()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ObservationFactory();
        await factory.SeedAsync(CreateTarget());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/v1/servers/observations");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpClient CreateAuthorizedClient(ObservationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        return client;
    }

    private static TargetServer CreateTarget() => new()
    {
        Id = Guid.NewGuid(),
        Host = "127.0.0.1",
        Port = 27015,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private sealed class ObservationFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");
        private readonly InMemoryDatabaseRoot _databaseRoot = new();

        public TargetServerObservationStore Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
                services.AddDbContext<MatchmakingDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName, _databaseRoot));
                services.RemoveAll<TargetServerObservationStore>();
                services.AddSingleton(Store);
            });
        }

        public async Task SeedAsync(params TargetServer[] targets)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            dbContext.TargetServers.AddRange(targets);
            await dbContext.SaveChangesAsync();
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-observation-token";

        private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? _token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? _connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", ApiToken);
            Environment.SetEnvironmentVariable(
                "CORE_DATABASE_CONNECTION_STRING",
                "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", _token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", _connection);
        }
    }
}
