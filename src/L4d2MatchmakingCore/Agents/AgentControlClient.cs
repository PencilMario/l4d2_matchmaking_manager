using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Agents;

public interface IAgentControlClient
{
    Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken);
    Task<AgentOperationStartResult> StartOperationAsync(WarmupAgent agent, AgentOperationRequest request, CancellationToken cancellationToken);
    Task<AgentOperationSnapshot?> GetOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken);
    Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken);
    Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken);
}

public interface IHealthyAgentSelector
{
    Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken);
}

public sealed class AgentControlClient(HttpClient httpClient) : IAgentControlClient
{
    public Task<AgentHealthSnapshot> GetHealthAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
        GetRequiredAsync<AgentHealthSnapshot>(agent, "/v1/probe/status", cancellationToken);

    public async Task<AgentOperationStartResult> StartOperationAsync(
        WarmupAgent agent,
        AgentOperationRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(UriFor(agent, "/v1/operations"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var operation = await ReadRequiredAsync<AgentOperationSnapshot>(response, cancellationToken);
        return new AgentOperationStartResult(operation, response.StatusCode == HttpStatusCode.OK);
    }

    public async Task<AgentOperationSnapshot?> GetOperationAsync(
        WarmupAgent agent,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(UriFor(agent, $"/v1/operations/{operationId}"), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<AgentOperationSnapshot>(response, cancellationToken);
    }

    public async Task StopOperationAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(UriFor(agent, $"/v1/operations/{operationId}"), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<LobbySnapshot> ReadLobbyAsync(WarmupAgent agent, string lobbyId, CancellationToken cancellationToken) =>
        GetRequiredAsync<LobbySnapshot>(agent, $"/v1/lobbies/{Uri.EscapeDataString(lobbyId)}", cancellationToken);

    private async Task<T> GetRequiredAsync<T>(WarmupAgent agent, string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(UriFor(agent, path), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("agent_response_body_missing");

    private static Uri UriFor(WarmupAgent agent, string path) =>
        new($"http://l4d2-agent-{agent.Id:N}:8080{path}", UriKind.Absolute);
}

public sealed class HealthyAgentSelector(
    MatchmakingDbContext dbContext,
    IAgentControlClient client) : IHealthyAgentSelector
{
    private const int MaxConcurrentHealthProbes = 16;

    public async Task<WarmupAgent?> SelectAsync(CancellationToken cancellationToken)
    {
        var candidates = await dbContext.WarmupAgents
            .AsNoTracking()
            .Where(agent => agent.Status == "running")
            .OrderBy(agent => agent.UpdatedAt)
            .ToListAsync(cancellationToken);
        foreach (var agent in await ProbeHealthyAsync(candidates, cancellationToken))
            return agent;
        return null;
    }

    public async Task<IReadOnlyList<WarmupAgent>> ListHealthyAsync(CancellationToken cancellationToken)
    {
        var candidates = await dbContext.WarmupAgents
            .AsNoTracking()
            .Where(agent => agent.Status == "running")
            .OrderBy(agent => agent.UpdatedAt)
            .ToListAsync(cancellationToken);
        return await ProbeHealthyAsync(candidates, cancellationToken);
    }

    private async Task<IReadOnlyList<WarmupAgent>> ProbeHealthyAsync(
        IReadOnlyList<WarmupAgent> candidates,
        CancellationToken cancellationToken)
    {
        var healthyIds = new ConcurrentDictionary<Guid, byte>();
        await Parallel.ForEachAsync(candidates, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = MaxConcurrentHealthProbes,
        }, async (agent, token) =>
        {
            try
            {
                if ((await client.GetHealthAsync(agent, token)).Ready)
                    healthyIds.TryAdd(agent.Id, 0);
            }
            catch (HttpRequestException)
            {
            }
        });
        return candidates.Where(agent => healthyIds.ContainsKey(agent.Id)).ToList();
    }
}
