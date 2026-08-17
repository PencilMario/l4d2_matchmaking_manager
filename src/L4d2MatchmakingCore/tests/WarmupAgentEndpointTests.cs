using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using L4d2MatchmakingCore.Agents;
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

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<global::Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        return client;
    }

    private sealed class AgentFactory(FakeRuntime runtime) : WebApplicationFactory<global::Program>
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
            });
        }
    }

    private sealed class FakeRuntime(IReadOnlySet<int>? usedPorts = null) : IAgentContainerRuntime
    {
        private readonly IReadOnlySet<int> _usedPorts = usedPorts ?? new HashSet<int>();
        public int CreateCalls { get; private set; }
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public List<(string ContainerId, bool DeleteVolumes)> DeleteRequests { get; } = [];

        public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_usedPorts);

        public Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult($"container-{CreateCalls}");
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
