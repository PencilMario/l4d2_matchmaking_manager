using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Steam;
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
public sealed class SteamDownloadRegionEndpointTests
{
    [TestMethod]
    public async Task AuthorizedRequestReturnsTheSteamRegionDirectoryWithInternalIds()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SteamRegionFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);

        var response = await client.GetAsync("/v1/steam/download-regions");
        var regions = await response.Content.ReadFromJsonAsync<List<SteamDownloadRegionResponse>>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(regions);
        Assert.AreEqual(186, regions.Count);
        Assert.AreEqual(186, regions.Select(region => region.Id).Distinct().Count());
        Assert.AreEqual("中国 - 青岛", regions.Single(region => region.Id == 168).Name);
        Assert.AreEqual("中国 - 上海", regions.Single(region => region.Id == 47).Name);
    }

    [TestMethod]
    public async Task DownloadRegionDirectoryRequiresTheCoreBearerToken()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SteamRegionFactory();

        var response = await factory.CreateClient().GetAsync("/v1/steam/download-regions");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class SteamRegionFactory : WebApplicationFactory<global::Program>
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
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-steam-region-api-token";
        private readonly string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

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
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", connection);
        }
    }
}
