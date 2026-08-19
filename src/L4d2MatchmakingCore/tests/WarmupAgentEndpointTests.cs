using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WarmupAgentEndpointTests
{
    [TestMethod]
    public async Task CreateAllocatesFreeNoVncPortAndDoesNotExposeSecrets()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime(new HashSet<int> { 18083 });
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", "hongkong"));
        var json = await response.Content.ReadAsStringAsync();
        var agent = JsonSerializer.Deserialize<WarmupAgentResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(agent);
        Assert.AreEqual("account-1", agent.Name);
        Assert.AreEqual("running", agent.Status);
        Assert.AreEqual(18084, agent.NoVncPort);
        Assert.IsFalse(json.Contains("steam-data-", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("agent-config-", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("container-id", StringComparison.Ordinal));
        Assert.AreEqual(1, runtime.CreateCalls);
        Assert.AreEqual(1, runtime.StartCalls);
    }

    [TestMethod]
    public async Task CreateSkipsPersistedAndDockerBoundNoVncPorts()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime(new HashSet<int> { 18083 });
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);

        var first = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var second = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-2", null));
        var firstAgent = await first.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        var secondAgent = await second.Content.ReadFromJsonAsync<WarmupAgentResponse>();

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.IsNotNull(firstAgent);
        Assert.IsNotNull(secondAgent);
        Assert.AreEqual(18084, firstAgent.NoVncPort);
        Assert.AreEqual(18085, secondAgent.NoVncPort);
    }

    [TestMethod]
    public async Task CreateReturnsConflictForDuplicateName()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new AgentFactory(new FakeRuntime());
        using var client = CreateAuthorizedClient(factory);

        var first = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var duplicate = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [TestMethod]
    public async Task AgentSettingsPersistKeepVncAlive()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new AgentFactory(new FakeRuntime());
        using var client = CreateAuthorizedClient(factory);

        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null, true));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);
        Assert.IsTrue(agent.KeepVncAlive);

        var updated = await client.PutAsJsonAsync($"/v1/agents/{agent.Id}", new UpdateWarmupAgentRequest("account-1", "tokyo", false));
        var updatedAgent = await updated.Content.ReadFromJsonAsync<WarmupAgentResponse>();

        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        Assert.IsNotNull(updatedAgent);
        Assert.IsFalse(updatedAgent.KeepVncAlive);
        Assert.AreEqual("tokyo", updatedAgent.DownloadRegion);
    }

    [TestMethod]
    public async Task LifecycleEndpointsManageContainerWithoutDeletingAccountVolumes()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime();
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);

        var stopped = await client.PostAsync($"/v1/agents/{agent.Id}/stop", null);
        var recreated = await client.PostAsync($"/v1/agents/{agent.Id}/recreate", null);
        var started = await client.PostAsync($"/v1/agents/{agent.Id}/start", null);
        var deleted = await client.DeleteAsync($"/v1/agents/{agent.Id}");

        Assert.AreEqual(HttpStatusCode.OK, stopped.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, recreated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, started.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.AreEqual(2, runtime.CreateCalls);
        Assert.AreEqual(2, runtime.StartCalls);
        Assert.AreEqual(1, runtime.StopCalls);
        Assert.AreEqual(2, runtime.DeleteCalls);
        Assert.IsTrue(runtime.DeleteRequests.All(request => !request.DeleteVolumes));
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/agents/{agent.Id}")).StatusCode);
    }

    [TestMethod]
    public async Task DeletingAnAgentWaitsForItsInProgressCreation()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime
        {
            CreateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            AllowCreate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            DeleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);

        var creating = client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        await runtime.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var pendingAgent = (await client.GetFromJsonAsync<List<WarmupAgentResponse>>("/v1/agents"))?.Single();
        Assert.IsNotNull(pendingAgent);

        var deleting = client.DeleteAsync($"/v1/agents/{pendingAgent.Id}");
        await Assert.ThrowsExceptionAsync<TimeoutException>(
            () => runtime.DeleteStarted.Task.WaitAsync(TimeSpan.FromMilliseconds(100)));

        runtime.AllowCreate.SetResult();
        Assert.AreEqual(HttpStatusCode.Created, (await creating).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await deleting).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/agents/{pendingAgent.Id}")).StatusCode);
    }

    [TestMethod]
    public async Task VncSessionStartsOnlyTheRequestedAgentAndCanBeClosed()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime();
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);

        var opened = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);
        var openedJson = await opened.Content.ReadAsStringAsync();
        var sessionUrl = JsonDocument.Parse(openedJson).RootElement.GetProperty("url").GetString();
        var closed = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions/close", null);

        Assert.AreEqual(HttpStatusCode.Created, opened.StatusCode);
        Assert.IsNotNull(sessionUrl);
        StringAssert.StartsWith(sessionUrl, $"/v1/agents/{agent.Id}/vnc/");
        StringAssert.Contains(sessionUrl, $"path={Uri.EscapeDataString($"v1/agents/{agent.Id}/vnc/websockify")}");
        Assert.AreEqual(1, runtime.StartVncCalls);
        Assert.AreEqual(HttpStatusCode.NoContent, closed.StatusCode);
        Assert.AreEqual(1, runtime.StopVncCalls);
    }

    [TestMethod]
    public async Task VncSessionReturnsServerErrorWhenNoVncRuntimeControlFails()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime { VncStateException = new InvalidOperationException("agent_vnc_status_failed") };
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);

        var opened = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);

        Assert.AreEqual(HttpStatusCode.InternalServerError, opened.StatusCode);
    }

    [TestMethod]
    public async Task VncSessionRenewalReturnsServerErrorWhenTheRuntimeBecomesPartial()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime();
        await using var factory = new AgentFactory(runtime);
        using var client = CreateAuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);
        var opened = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);
        Assert.AreEqual(HttpStatusCode.Created, opened.StatusCode);
        runtime.VncState = AgentVncState.Partial;

        var renewed = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);

        Assert.AreEqual(HttpStatusCode.InternalServerError, renewed.StatusCode);
        Assert.AreEqual(1, runtime.StartVncCalls);
        Assert.AreEqual(0, runtime.StopVncCalls);
    }

    [TestMethod]
    public async Task VncProxyExchangesTheShortLivedTokenForAnAgentScopedCookie()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime();
        var proxy = new FakeVncProxy();
        await using var factory = new AgentFactory(runtime, proxy);
        using var client = CreateAuthorizedClient(factory);
        var created = await client.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);
        var opened = await client.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);
        var url = JsonDocument.Parse(await opened.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString();
        Assert.IsNotNull(url);

        using var noRedirect = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var exchanged = await noRedirect.GetAsync(url);

        Assert.AreEqual(HttpStatusCode.Found, exchanged.StatusCode);
        Assert.AreEqual(0, proxy.Calls);
        Assert.IsTrue(exchanged.Headers.TryGetValues("Set-Cookie", out var setCookie));
        StringAssert.Contains(setCookie.Single().ToLowerInvariant(), $"path=/v1/agents/{agent.Id}/vnc".ToLowerInvariant());
        Assert.AreEqual("no-referrer", exchanged.Headers.GetValues("Referrer-Policy").Single());
        var redirectedUrl = exchanged.Headers.Location?.ToString();
        Assert.IsNotNull(redirectedUrl);
        Assert.IsFalse(redirectedUrl.Contains("session=", StringComparison.OrdinalIgnoreCase));

        using var cookieClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        cookieClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", setCookie.Single().Split(';')[0]);
        var proxied = await cookieClient.GetAsync(redirectedUrl);
        Assert.AreEqual(HttpStatusCode.OK, proxied.StatusCode);
        Assert.AreEqual("proxied", await proxied.Content.ReadAsStringAsync());
        Assert.AreEqual(agent.Id, proxy.AgentId);
        Assert.AreEqual("web/", proxy.Path);

        using var replayClient = factory.CreateClient();
        var replayed = await replayClient.GetAsync(url);
        Assert.AreEqual(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    [TestMethod]
    public async Task NewQueryTokenWinsOverAStaleVncCookie()
    {
        using var environment = new CoreTestEnvironment();
        var runtime = new FakeRuntime();
        await using var factory = new AgentFactory(runtime, new FakeVncProxy());
        using var authorized = CreateAuthorizedClient(factory);
        var created = await authorized.PostAsJsonAsync("/v1/agents", new CreateWarmupAgentRequest("account-1", null));
        var agent = await created.Content.ReadFromJsonAsync<WarmupAgentResponse>();
        Assert.IsNotNull(agent);
        var first = await authorized.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);
        var firstUrl = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString();
        Assert.IsNotNull(firstUrl);

        using var noRedirect = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var firstExchange = await noRedirect.GetAsync(firstUrl);
        var staleCookie = firstExchange.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var closed = await authorized.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions/close", null);
        Assert.AreEqual(HttpStatusCode.NoContent, closed.StatusCode);
        var second = await authorized.PostAsync($"/v1/agents/{agent.Id}/vnc-sessions", null);
        var secondUrl = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString();
        Assert.IsNotNull(secondUrl);

        noRedirect.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", staleCookie);
        var reopened = await noRedirect.GetAsync(secondUrl);

        Assert.AreEqual(HttpStatusCode.Found, reopened.StatusCode);
        Assert.IsTrue(reopened.Headers.TryGetValues("Set-Cookie", out _));
    }

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        return client;
    }

    private sealed class AgentFactory(FakeRuntime runtime, IAgentVncProxy? proxy = null) : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
                services.AddDbContext<MatchmakingDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IAgentContainerRuntime>();
                services.AddSingleton<IAgentContainerRuntime>(runtime);
                services.RemoveAll<AgentContainerOptions>();
                services.AddSingleton(new AgentContainerOptions("image", "/library", "network", 18083, 18183, "/mnt/steam-library/libsteam_api.so"));
                if (proxy is not null)
                {
                    services.RemoveAll<IAgentVncProxy>();
                    services.AddSingleton(proxy);
                }
            });
        }
    }

    private sealed class FakeVncProxy : IAgentVncProxy
    {
        public Guid AgentId { get; private set; }
        public int Calls { get; private set; }
        public string Path { get; private set; } = string.Empty;

        public async Task ProxyAsync(HttpContext context, Guid agentId, string path, CancellationToken cancellationToken)
        {
            Calls++;
            AgentId = agentId;
            Path = path;
            await context.Response.WriteAsync("proxied", cancellationToken);
        }
    }

    private sealed class FakeRuntime(IReadOnlySet<int>? usedPorts = null) : IAgentContainerRuntime
    {
        private readonly IReadOnlySet<int> _usedPorts = usedPorts ?? new HashSet<int>();
        public TaskCompletionSource? AllowCreate { get; init; }
        public TaskCompletionSource? CreateStarted { get; init; }
        public TaskCompletionSource? DeleteStarted { get; init; }
        public Exception? VncStateException { get; init; }
        public AgentVncState VncState { get; set; } = AgentVncState.Stopped;
        public int CreateCalls { get; private set; }
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int StartVncCalls { get; private set; }
        public int StopVncCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public List<(string ContainerId, bool DeleteVolumes)> DeleteRequests { get; } = [];

        public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_usedPorts);

        public async Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken)
        {
            CreateCalls++;
            CreateStarted?.TrySetResult();
            if (AllowCreate is not null)
                await AllowCreate.Task.WaitAsync(cancellationToken);
            return $"container-{CreateCalls}";
        }

        public Task StartAsync(string containerId, CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task StopAsync(string containerId, CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken)
        {
            DeleteCalls++;
            DeleteRequests.Add((containerId, deleteVolumes));
            DeleteStarted?.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken) =>
            VncStateException is null
                ? Task.FromResult(VncState)
                : Task.FromException<AgentVncState>(VncStateException);

        public Task StartVncAsync(string containerId, CancellationToken cancellationToken)
        {
            StartVncCalls++;
            VncState = AgentVncState.Running;
            return Task.CompletedTask;
        }

        public Task StopVncAsync(string containerId, CancellationToken cancellationToken)
        {
            StopVncCalls++;
            VncState = AgentVncState.Stopped;
            return Task.CompletedTask;
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-agent-api-token";
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
