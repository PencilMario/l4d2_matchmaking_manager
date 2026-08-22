using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
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

    [TestMethod]
    public async Task WarmupSchedulingSettingDefaultsEnabledAndCanBePausedAndResumed()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var initial = await client.GetStringAsync("/v1/settings");
        var disabled = await client.PutAsJsonAsync("/v1/settings/warmup-scheduling", new { enabled = false });
        var disabledDedicated = await client.GetStringAsync("/v1/settings/warmup-scheduling");
        var disabledCombined = await client.GetStringAsync("/v1/settings");
        var enabled = await client.PutAsJsonAsync("/v1/settings/warmup-scheduling", new { enabled = true });
        var enabledCombined = await client.GetStringAsync("/v1/settings");

        Assert.IsTrue(JsonDocument.Parse(initial).RootElement.GetProperty("warmupSchedulingEnabled").GetBoolean());
        Assert.AreEqual(HttpStatusCode.OK, disabled.StatusCode);
        Assert.IsFalse(JsonDocument.Parse(disabledDedicated).RootElement.GetProperty("enabled").GetBoolean());
        Assert.IsFalse(JsonDocument.Parse(disabledCombined).RootElement.GetProperty("warmupSchedulingEnabled").GetBoolean());
        Assert.AreEqual(HttpStatusCode.OK, enabled.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(enabledCombined).RootElement.GetProperty("warmupSchedulingEnabled").GetBoolean());
    }

    [TestMethod]
    public async Task LegacySettingsUpdatePreservesWarmupSchedulingDisabledState()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        await client.PutAsJsonAsync("/v1/settings/warmup-scheduling", new { enabled = false });
        var legacyUpdate = await client.PutAsJsonAsync("/v1/settings", new { steamProxyUrl = "http://127.0.0.1:7890" });
        var current = await client.GetStringAsync("/v1/settings");

        Assert.AreEqual(HttpStatusCode.OK, legacyUpdate.StatusCode);
        Assert.IsFalse(JsonDocument.Parse(current).RootElement.GetProperty("warmupSchedulingEnabled").GetBoolean());
    }

    [TestMethod]
    public async Task DisablingWarmupSchedulingDrainsCurrentAttemptsWithoutStoppingContainers()
    {
        using var environment = new CoreTestEnvironment();
        var control = new RecordingAgentControlClient();
        await using var factory = new SettingsFactory(control);
        using var client = CreateAuthorizedClient(factory);
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        await SeedAttemptAsync(factory, agentId, targetId, operationId);

        var disabled = await client.PutAsJsonAsync("/v1/settings/warmup-scheduling", new { enabled = false });

        Assert.AreEqual(HttpStatusCode.OK, disabled.StatusCode);
        CollectionAssert.AreEqual(new[] { operationId }, control.StoppedOperations);
        CollectionAssert.AreEqual(new[] { agentId }, control.RestartedAgents);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.AreEqual("completed", (await db.WarmupAttempts.SingleAsync()).State);
        Assert.IsFalse(await db.ReservationLeases.AnyAsync());
        Assert.AreEqual("restarting", (await db.WarmupAgents.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task DisablingWarmupSchedulingReturnsConflictAndRemainsDisabledWhenDrainFails()
    {
        using var environment = new CoreTestEnvironment();
        var control = new RecordingAgentControlClient { StopException = new HttpRequestException("agent_unreachable") };
        await using var factory = new SettingsFactory(control);
        using var client = CreateAuthorizedClient(factory);
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await SeedAttemptAsync(factory, agentId, targetId, Guid.NewGuid());

        var disabled = await client.PutAsJsonAsync("/v1/settings/warmup-scheduling", new { enabled = false });
        var settings = await client.GetStringAsync("/v1/settings");

        Assert.AreEqual(HttpStatusCode.Conflict, disabled.StatusCode);
        StringAssert.Contains(await disabled.Content.ReadAsStringAsync(), "global_warmup_drain_failed");
        Assert.IsFalse(JsonDocument.Parse(settings).RootElement.GetProperty("warmupSchedulingEnabled").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.AreEqual("active", (await db.WarmupAttempts.SingleAsync()).State);
        Assert.AreEqual("quarantined", (await db.WarmupAgents.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task WarmupPauseWindowsDefaultToEmptyAndCanBeUpdatedIndependently()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var initialDedicated = await client.GetAsync("/v1/settings/warmup-pause-windows");
        var initialCombined = await client.GetStringAsync("/v1/settings");
        var update = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[]
            {
                new { start = "23:00", end = "00:00" },
                new { start = "00:00", end = "08:00" },
            },
        });
        var currentDedicated = await client.GetStringAsync("/v1/settings/warmup-pause-windows");
        var currentCombined = await client.GetStringAsync("/v1/settings");

        Assert.AreEqual(HttpStatusCode.OK, initialDedicated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        Assert.AreEqual(0, JsonDocument.Parse(await initialDedicated.Content.ReadAsStringAsync())
            .RootElement.GetProperty("windows").GetArrayLength());
        Assert.AreEqual(0, JsonDocument.Parse(initialCombined)
            .RootElement.GetProperty("warmupPauseWindows").GetArrayLength());
        var dedicatedWindows = JsonDocument.Parse(currentDedicated).RootElement.GetProperty("windows");
        Assert.AreEqual("00:00", dedicatedWindows[0].GetProperty("start").GetString());
        Assert.AreEqual("23:00", dedicatedWindows[1].GetProperty("start").GetString());
        Assert.AreEqual(2, JsonDocument.Parse(currentCombined)
            .RootElement.GetProperty("warmupPauseWindows").GetArrayLength());
    }

    [TestMethod]
    public async Task LegacySettingsUpdatePreservesWarmupPauseWindows()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = "22:30", end = "07:45" } },
        });
        var legacyUpdate = await client.PutAsJsonAsync("/v1/settings", new
        {
            steamProxyUrl = "http://127.0.0.1:7890",
        });
        var current = JsonDocument.Parse(await client.GetStringAsync("/v1/settings"));

        Assert.AreEqual(HttpStatusCode.OK, legacyUpdate.StatusCode);
        var windows = current.RootElement.GetProperty("warmupPauseWindows");
        Assert.AreEqual(1, windows.GetArrayLength());
        Assert.AreEqual("22:30", windows[0].GetProperty("start").GetString());
        Assert.AreEqual("07:45", windows[0].GetProperty("end").GetString());
    }

    [TestMethod]
    public async Task WarmupPauseWindowsRejectInvalidTimeValues()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new SettingsFactory();
        using var client = CreateAuthorizedClient(factory);

        var invalidFormat = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = "8:00", end = "08:00" } },
        });
        var equalEndpoints = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = "08:00", end = "08:00" } },
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, invalidFormat.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, equalEndpoints.StatusCode);
        StringAssert.Contains(await invalidFormat.Content.ReadAsStringAsync(), "invalid_warmup_pause_window");
        StringAssert.Contains(await equalEndpoints.Content.ReadAsStringAsync(), "invalid_warmup_pause_window");
    }

    [TestMethod]
    public async Task SavingAnActiveWarmupPauseWindowDrainsAttemptsWithoutStoppingContainers()
    {
        using var environment = new CoreTestEnvironment();
        var control = new RecordingAgentControlClient();
        await using var factory = new SettingsFactory(control);
        using var client = CreateAuthorizedClient(factory);
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        await SeedAttemptAsync(factory, agentId, targetId, operationId);
        var window = CreateActiveWindow();

        var update = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = window.Start, end = window.End } },
        });

        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        CollectionAssert.AreEqual(new[] { operationId }, control.StoppedOperations);
        CollectionAssert.AreEqual(new[] { agentId }, control.RestartedAgents);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.AreEqual("completed", (await db.WarmupAttempts.SingleAsync()).State);
        Assert.AreEqual("restarting", (await db.WarmupAgents.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task SavingAnActiveWarmupPauseWindowReturnsConflictWhenDrainFails()
    {
        using var environment = new CoreTestEnvironment();
        var control = new RecordingAgentControlClient { StopException = new HttpRequestException("agent_unreachable") };
        await using var factory = new SettingsFactory(control);
        using var client = CreateAuthorizedClient(factory);
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await SeedAttemptAsync(factory, agentId, targetId, Guid.NewGuid());
        var window = CreateActiveWindow();

        var update = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = window.Start, end = window.End } },
        });
        var settings = JsonDocument.Parse(await client.GetStringAsync("/v1/settings"));

        Assert.AreEqual(HttpStatusCode.Conflict, update.StatusCode);
        StringAssert.Contains(await update.Content.ReadAsStringAsync(), "global_warmup_drain_failed");
        Assert.AreEqual(1, settings.RootElement.GetProperty("warmupPauseWindows").GetArrayLength());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.AreEqual("active", (await db.WarmupAttempts.SingleAsync()).State);
        Assert.AreEqual("quarantined", (await db.WarmupAgents.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task LeavingAnActivePauseWindowDrainsBeforeRestoringScheduling()
    {
        using var environment = new CoreTestEnvironment();
        var control = new RecordingAgentControlClient
        {
            StopException = new HttpRequestException("agent_unreachable"),
        };
        await using var factory = new SettingsFactory(control);
        using var client = CreateAuthorizedClient(factory);
        var agentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        await SeedAttemptAsync(factory, agentId, targetId, operationId);
        var activeWindow = CreateActiveWindow();

        var enterPause = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = new[] { new { start = activeWindow.Start, end = activeWindow.End } },
        });

        var failedLeavePause = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = Array.Empty<object>(),
        });
        var retainedSettings = JsonDocument.Parse(await client.GetStringAsync("/v1/settings"));
        control.StopException = null;
        var leavePause = await client.PutAsJsonAsync("/v1/settings/warmup-pause-windows", new
        {
            windows = Array.Empty<object>(),
        });
        var settings = JsonDocument.Parse(await client.GetStringAsync("/v1/settings"));

        Assert.AreEqual(HttpStatusCode.Conflict, enterPause.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, failedLeavePause.StatusCode);
        Assert.AreEqual(1, retainedSettings.RootElement.GetProperty("warmupPauseWindows").GetArrayLength());
        Assert.AreEqual(HttpStatusCode.OK, leavePause.StatusCode);
        CollectionAssert.AreEqual(new[] { operationId }, control.StoppedOperations);
        Assert.AreEqual(0, settings.RootElement.GetProperty("warmupPauseWindows").GetArrayLength());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        Assert.AreEqual("completed", (await db.WarmupAttempts.SingleAsync()).State);
    }

    private static async Task SeedAttemptAsync(
        WebApplicationFactory<global::Program> factory,
        Guid agentId,
        Guid targetId,
        Guid operationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.AddRange(
            new WarmupAgent
            {
                Id = agentId,
                Name = "global-drain-agent",
                Status = "running",
                SteamDataVolumeName = "steam",
                AccountConfigVolumeName = "config",
                NoVncPort = 18083,
            },
            new TargetServer { Id = targetId, Host = "127.0.0.1", Port = 27015 },
            new WarmupAttempt
            {
                Id = Guid.NewGuid(),
                TargetServerId = targetId,
                WarmupAgentId = agentId,
                OperationId = operationId,
                State = "active",
                Mode = "standard",
                StartedAt = now,
                ObservedAt = now,
            });
        await db.SaveChangesAsync();
    }

    private static (string Start, string End) CreateActiveWindow()
    {
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        return (now.AddMinutes(-30).ToString("HH:mm"), now.AddMinutes(30).ToString("HH:mm"));
    }

    private sealed record VncProxySettingsResponse(string? ProxyUrl, DateTimeOffset UpdatedAt);

    private sealed record SteamWebApiKeySettingsResponse(bool Configured, DateTimeOffset UpdatedAt);

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        return client;
    }

    private sealed class SettingsFactory(IAgentControlClient? control = null) : WebApplicationFactory<global::Program>
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
                if (control is not null)
                {
                    services.RemoveAll<IAgentControlClient>();
                    services.AddSingleton(control);
                }
            });
        }
    }

    private sealed class RecordingAgentControlClient : IAgentControlClient
    {
        public List<Guid> StoppedOperations { get; } = [];
        public List<Guid> RestartedAgents { get; } = [];
        public Exception? StopException { get; set; }

        public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
        {
            if (StopException is not null)
                return Task.FromException(StopException);
            StoppedOperations.Add(operationId);
            return Task.CompletedTask;
        }

        public Task RestartSteamAsync(WarmupAgent agent, CancellationToken cancellationToken)
        {
            RestartedAgents.Add(agent.Id);
            return Task.CompletedTask;
        }

        public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
