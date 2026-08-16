using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    private sealed class ServerFactory : WebApplicationFactory<global::Program>
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
            });
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-target-server-token";
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
