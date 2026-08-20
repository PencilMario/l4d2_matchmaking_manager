using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Settings;
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
public sealed class GlobalSettingsEndpointTests
{
    [TestMethod]
    public async Task SettingsCanBeReadAndUpdatedWithoutReturningCredentials()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var initial = await client.GetAsync("/v1/settings");
        var update = await client.PutAsJsonAsync("/v1/settings", new { steamProxyUrl = "http://127.0.0.1:7890" });
        var current = await update.Content.ReadFromJsonAsync<GlobalSettingsResponse>();

        Assert.AreEqual(HttpStatusCode.OK, initial.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.IsNotNull(current);
        Assert.AreEqual("http://127.0.0.1:7890/", current.SteamProxyUrl);
        Assert.IsFalse((await client.GetStringAsync("/v1/settings")).Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SettingsRejectProxyCredentialsAndNonHttpSchemes()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var withCredentials = await client.PutAsJsonAsync("/v1/settings", new { steamProxyUrl = "http://user:pass@127.0.0.1:7890" });
        var withInvalidScheme = await client.PutAsJsonAsync("/v1/settings", new { steamProxyUrl = "socks5://127.0.0.1:1080" });

        Assert.AreEqual(HttpStatusCode.BadRequest, withCredentials.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, withInvalidScheme.StatusCode);
    }

    [TestMethod]
    public async Task SteamWebApiKeyIsStoredEncryptedAndNeverReturned()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var update = await client.PutAsJsonAsync("/v1/settings", new { steamWebApiKey = "steam-test-key" });
        var current = await update.Content.ReadAsStringAsync();
        var get = await client.GetStringAsync("/v1/settings");

        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.IsFalse(current.Contains("steam-test-key", StringComparison.Ordinal));
        Assert.IsFalse(get.Contains("steam-test-key", StringComparison.Ordinal));
        StringAssert.Contains(current, "\"steamWebApiKeyConfigured\":true");
        StringAssert.Contains(get, "steamWebApiKeyConfigured");
        StringAssert.Contains(get, "true");
    }

    [TestMethod]
    public async Task SteamWebApiKeyCanBeCleared()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        await client.PutAsJsonAsync("/v1/settings", new { steamWebApiKey = "steam-test-key" });
        var clear = await client.PutAsJsonAsync("/v1/settings", new { clearSteamWebApiKey = true });

        Assert.AreEqual(HttpStatusCode.OK, clear.StatusCode);
        StringAssert.Contains(await clear.Content.ReadAsStringAsync(), "\"steamWebApiKeyConfigured\":false");
    }

    [TestMethod]
    public async Task VncProxyCanBeUpdatedWithoutChangingSteamWebApiKey()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var key = await client.PutAsJsonAsync("/v1/settings/steam-web-api-key", new { apiKey = "steam-test-key" });
        var proxy = await client.PutAsJsonAsync("/v1/settings/vnc-proxy", new { proxyUrl = "http://127.0.0.1:7890" });
        var currentProxy = await client.GetFromJsonAsync<VncProxySettingsResponse>("/v1/settings/vnc-proxy");
        var currentKey = await client.GetFromJsonAsync<SteamWebApiKeySettingsResponse>("/v1/settings/steam-web-api-key");

        Assert.AreEqual(HttpStatusCode.OK, key.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, proxy.StatusCode);
        Assert.IsNotNull(currentProxy);
        Assert.IsNotNull(currentKey);
        Assert.AreEqual("http://127.0.0.1:7890/", currentProxy.ProxyUrl);
        Assert.IsTrue(currentKey.Configured);
    }

    [TestMethod]
    public async Task SteamWebApiKeyCanBeClearedWithoutChangingVncProxy()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        await client.PutAsJsonAsync("/v1/settings/vnc-proxy", new { proxyUrl = "http://127.0.0.1:7890" });
        await client.PutAsJsonAsync("/v1/settings/steam-web-api-key", new { apiKey = "steam-test-key" });
        var clear = await client.PutAsJsonAsync("/v1/settings/steam-web-api-key", new { clear = true });
        var currentProxy = await client.GetFromJsonAsync<VncProxySettingsResponse>("/v1/settings/vnc-proxy");
        var currentKey = await client.GetFromJsonAsync<SteamWebApiKeySettingsResponse>("/v1/settings/steam-web-api-key");

        Assert.AreEqual(HttpStatusCode.OK, clear.StatusCode);
        Assert.IsNotNull(currentProxy);
        Assert.IsNotNull(currentKey);
        Assert.AreEqual("http://127.0.0.1:7890/", currentProxy.ProxyUrl);
        Assert.IsFalse(currentKey.Configured);
    }

    private sealed record VncProxySettingsResponse(string? ProxyUrl, DateTimeOffset UpdatedAt);

    private sealed record SteamWebApiKeySettingsResponse(bool Configured, DateTimeOffset UpdatedAt);

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        return client;
    }

    private sealed class SettingsFactory : WebApplicationFactory<global::Program>
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
        public const string ApiToken = "test-settings-api-token";
        private readonly string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");
        private readonly string? encryptionKey = Environment.GetEnvironmentVariable("CORE_RCON_ENCRYPTION_KEY");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", ApiToken);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
            Environment.SetEnvironmentVariable("CORE_RCON_ENCRYPTION_KEY", Convert.ToBase64String(new byte[32]));
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", connection);
            Environment.SetEnvironmentVariable("CORE_RCON_ENCRYPTION_KEY", encryptionKey);
        }
    }
}
