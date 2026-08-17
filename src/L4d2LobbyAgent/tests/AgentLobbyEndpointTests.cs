using System.Net;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class AgentLobbyEndpointTests
{
    [TestMethod]
    public async Task StartIsIdempotentAndQueryLobbyWorksDuringAnActiveOperation()
    {
        var service = new FakeSessionService();
        await using var factory = new AgentFactory(service);
        using var client = factory.CreateClient();
        var operationId = Guid.NewGuid();
        var request = new AgentOperationRequest(
            operationId,
            AgentLobbyMode.Standard,
            "203.0.113.7",
            27015);

        var first = await client.PostAsJsonAsync("/v1/operations", request);
        var retry = await client.PostAsJsonAsync("/v1/operations", request);
        var lobby = await client.GetAsync("/v1/lobbies/109775242170052468");
        var malformed = await client.GetAsync("/v1/lobbies/not-a-lobby");

        Assert.AreEqual(HttpStatusCode.Accepted, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, retry.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, lobby.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.AreEqual(1, service.StartCalls);
        Assert.AreEqual(1, service.QueryLobbyCalls);
    }

    [TestMethod]
    public async Task QueryReturnsExplicitServiceUnavailableWhenLobbyDataIsNotConfirmed()
    {
        var service = new FakeSessionService
        {
            QueryException = new SteamRuntimeException("lobby_data_unavailable"),
        };
        await using var factory = new AgentFactory(service);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/lobbies/109775242170052468");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("\"lobby_data_unavailable\"", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task OperationStatusAndStopUseTheAgentSessionService()
    {
        var service = new FakeSessionService();
        await using var factory = new AgentFactory(service);
        using var client = factory.CreateClient();
        var operationId = Guid.NewGuid();
        var request = new AgentOperationRequest(
            operationId,
            AgentLobbyMode.Standard,
            "203.0.113.7",
            27015);

        await client.PostAsJsonAsync("/v1/operations", request);
        var active = await client.GetAsync($"/v1/operations/{operationId}");
        var stopped = await client.DeleteAsync($"/v1/operations/{operationId}");
        var unknown = await client.DeleteAsync($"/v1/operations/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.OK, active.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, stopped.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.AreEqual(1, service.StopCalls);
    }

    private sealed class AgentFactory(IAgentSteamSessionService service) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentSteamSessionService>();
                services.AddSingleton(service);
            });
        }
    }

    private sealed class FakeSessionService : IAgentSteamSessionService
    {
        private readonly Dictionary<Guid, AgentOperationSnapshot> _operations = [];

        public int StartCalls { get; private set; }
        public int QueryLobbyCalls { get; private set; }
        public int StopCalls { get; private set; }
        public Exception? QueryException { get; init; }

        public Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AgentHealthSnapshot(true, null, DateTimeOffset.UtcNow));

        public Task<AgentOperationStartResult> StartAsync(AgentOperationRequest request, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(request.OperationId, out var existing))
                return Task.FromResult(new AgentOperationStartResult(existing, true));

            StartCalls++;
            var snapshot = new AgentOperationSnapshot(
                request.OperationId,
                "active",
                Snapshot("109775242170052468"),
                null,
                DateTimeOffset.UtcNow);
            _operations.Add(request.OperationId, snapshot);
            return Task.FromResult(new AgentOperationStartResult(snapshot, false));
        }

        public Task<AgentOperationSnapshot?> GetAsync(Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult(_operations.GetValueOrDefault(operationId));

        public Task<bool> StopAsync(Guid operationId, CancellationToken cancellationToken)
        {
            if (!_operations.TryGetValue(operationId, out var operation))
                return Task.FromResult(false);

            StopCalls++;
            _operations[operationId] = operation with { State = "stopped", ObservedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(true);
        }

        public Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot(lobbyId.ToString()));

        public Task<LobbySnapshot> QueryLobbyAsync(ulong lobbyId, bool includeMembers, CancellationToken cancellationToken)
        {
            QueryLobbyCalls++;
            if (QueryException is not null)
                return Task.FromException<LobbySnapshot>(QueryException);
            return Task.FromResult(Snapshot(lobbyId.ToString()));
        }

        private static LobbySnapshot Snapshot(string lobbyId) => new(
            lobbyId,
            "76561198000000000",
            [new LobbyMemberSnapshot("76561198000000000", "Agent")],
            new Dictionary<string, string> { ["Game:campaign"] = "L4D2C2" },
            DateTimeOffset.UtcNow);
    }
}
