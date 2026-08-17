using System.Net;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class AgentControlClientTests
{
    [TestMethod]
    public async Task ClientUsesInternalAgentRoutesAndMapsIdempotentStart()
    {
        var operationId = Guid.NewGuid();
        var operation = new AgentOperationSnapshot(operationId, "active", null, null, DateTimeOffset.UnixEpoch);
        var lobby = new LobbySnapshot("109775242170052468", "76561198000000000", [], new Dictionary<string, string>(), DateTimeOffset.UnixEpoch);
        var handler = new RecordingHandler(operation, lobby);
        using var httpClient = new HttpClient(handler);
        var client = new AgentControlClient(httpClient);
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };

        var health = await client.GetHealthAsync(agent, CancellationToken.None);
        var start = await client.StartOperationAsync(agent, new AgentOperationRequest(operationId, AgentLobbyMode.Standard, "203.0.113.7", 27015), CancellationToken.None);
        var current = await client.GetOperationAsync(agent, operationId, CancellationToken.None);
        await client.StopOperationAsync(agent, operationId, CancellationToken.None);
        var snapshot = await client.ReadLobbyAsync(agent, lobby.LobbyId, CancellationToken.None);

        Assert.IsTrue(health.Ready);
        Assert.IsTrue(start.AlreadyExists);
        Assert.AreEqual(operation, start.Operation);
        Assert.AreEqual(operation, current);
        Assert.AreEqual(lobby.LobbyId, snapshot.LobbyId);
        Assert.AreEqual(lobby.OwnerSteamId, snapshot.OwnerSteamId);
        Assert.AreEqual(lobby.ObservedAt, snapshot.ObservedAt);
        CollectionAssert.AreEqual(
            new[]
            {
                "GET /v1/probe/status",
                "POST /v1/operations",
                $"GET /v1/operations/{operationId}",
                $"DELETE /v1/operations/{operationId}",
                $"GET /v1/lobbies/{lobby.LobbyId}",
            },
            handler.Requests);
        Assert.IsTrue(handler.Hosts.All(host => host == $"l4d2-agent-{agent.Id:N}:8080"));
    }

    [TestMethod]
    public async Task StartSendsRconPasswordOnlyInThePrivateOperationRequest()
    {
        var operationId = Guid.NewGuid();
        var operation = new AgentOperationSnapshot(operationId, "active", null, null, DateTimeOffset.UnixEpoch);
        var handler = new RecordingHandler(operation, new LobbySnapshot("109775242170052468", "owner", [], new Dictionary<string, string>(), DateTimeOffset.UnixEpoch));
        using var httpClient = new HttpClient(handler);
        var client = new AgentControlClient(httpClient);
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Status = "running" };
        var constructor = typeof(AgentOperationRequest).GetConstructor(
            [typeof(Guid), typeof(AgentLobbyMode), typeof(string), typeof(ushort), typeof(string)]);

        Assert.IsNotNull(constructor, "The private operation contract must carry the optional RCON password.");
        var request = (AgentOperationRequest)constructor.Invoke([operationId, AgentLobbyMode.Reserved, "203.0.113.7", (ushort)27083, "private-rcon-password"]);

        await client.StartOperationAsync(agent, request, CancellationToken.None);

        Assert.AreEqual("private-rcon-password", handler.RconPassword);
    }

    private sealed class RecordingHandler(AgentOperationSnapshot operation, LobbySnapshot lobby) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Hosts { get; } = [];
        public string? RconPassword { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            Hosts.Add($"{request.RequestUri.Host}:{request.RequestUri.Port}");
            if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/v1/operations")
            {
                var body = await request.Content!.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken);
                RconPassword = body.GetProperty("rconPassword").GetString();
            }
            object payload = request.RequestUri.AbsolutePath switch
            {
                "/v1/probe/status" => new AgentHealthSnapshot(true, null, DateTimeOffset.UnixEpoch),
                "/v1/operations" when request.Method == HttpMethod.Post => operation,
                _ when request.RequestUri.AbsolutePath.StartsWith("/v1/operations/", StringComparison.Ordinal) && request.Method == HttpMethod.Get => operation,
                _ when request.RequestUri.AbsolutePath.StartsWith("/v1/lobbies/", StringComparison.Ordinal) => lobby,
                _ => new { },
            };
            var statusCode = request.Method == HttpMethod.Post ? HttpStatusCode.OK : HttpStatusCode.OK;
            return new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(payload),
            };
        }
    }
}
