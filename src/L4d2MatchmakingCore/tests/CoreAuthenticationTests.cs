using System.Net;
using System.Net.Http.Headers;
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
public sealed class CoreAuthenticationTests
{
    [TestMethod]
    public async Task ManagementEndpointsRequireTheConfiguredBearerToken()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new CoreFactory();
        using var anonymousClient = factory.CreateClient();
        using var incorrectClient = factory.CreateClient();
        using var authorizedClient = factory.CreateClient();
        incorrectClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreFactory.ApiToken);

        var anonymous = await anonymousClient.GetAsync("/v1/servers");
        var incorrect = await incorrectClient.GetAsync("/v1/servers");
        var authorized = await authorizedClient.GetAsync("/v1/servers");

        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, incorrect.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, authorized.StatusCode);
    }

    private sealed class CoreFactory : WebApplicationFactory<global::Program>
    {
        public const string ApiToken = "test-core-api-token";
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
        private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? _token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? _connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", CoreFactory.ApiToken);
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
