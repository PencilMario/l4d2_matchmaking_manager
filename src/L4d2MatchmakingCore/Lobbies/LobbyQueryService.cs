using System.Collections.Concurrent;
using System.Text.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Lobbies;

public sealed class LobbyQueryService(
    MatchmakingDbContext dbContext,
    IHealthyAgentSelector selector,
    IAgentControlClient agents)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> QueryLeases = [];

    public async Task<LobbySnapshot> QueryAsync(string lobbyId, CancellationToken cancellationToken)
    {
        var healthyAgents = await selector.ListHealthyAsync(cancellationToken);
        if (healthyAgents.Count == 0)
            throw new LobbyQueryAgentUnavailableException();

        var agentIds = healthyAgents.Select(agent => agent.Id).Distinct().ToList();
        var attempts = await dbContext.WarmupAttempts
            .AsNoTracking()
            .Where(attempt => agentIds.Contains(attempt.WarmupAgentId) &&
                (attempt.State == "active" || attempt.State == "uncertain"))
            .ToListAsync(cancellationToken);
        var attemptsByAgent = attempts
            .GroupBy(attempt => attempt.WarmupAgentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var membershipCandidates = healthyAgents
            .Where(agent => !attemptsByAgent.ContainsKey(agent.Id))
            .Concat(healthyAgents.Where(agent => IsActiveStandardOnly(attemptsByAgent.GetValueOrDefault(agent.Id))))
            .DistinctBy(agent => agent.Id)
            .ToList();

        foreach (var candidate in membershipCandidates)
        {
            var result = await TryQueryAsync(candidate, lobbyId, includeMembers: true, cancellationToken);
            if (result is not null)
                return result;
        }

        foreach (var candidate in healthyAgents)
        {
            var result = await TryQueryAsync(candidate, lobbyId, includeMembers: false, cancellationToken);
            if (result is not null)
                return result;
        }

        throw new LobbyQueryAgentUnavailableException();
    }

    private async Task<LobbySnapshot?> TryQueryAsync(
        WarmupAgent agent,
        string lobbyId,
        bool includeMembers,
        CancellationToken cancellationToken)
    {
        var lease = QueryLeases.GetOrAdd(agent.Id, static _ => new SemaphoreSlim(1, 1));
        if (!await lease.WaitAsync(0, cancellationToken))
            return null;

        try
        {
            return await agents.QueryLobbyAsync(agent, lobbyId, includeMembers, cancellationToken);
        }
        catch (AgentLobbyQueryException exception) when (exception.Code == "lobby_operation_preservation_failed")
        {
            await QuarantineAgentAsync(agent.Id, cancellationToken);
            throw;
        }
        finally
        {
            lease.Release();
        }
    }

    private static bool IsActiveStandardOnly(IReadOnlyList<WarmupAttempt>? attempts) =>
        attempts is { Count: 1 } && attempts[0].State == "active" && attempts[0].Mode == "standard";

    private async Task QuarantineAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await dbContext.WarmupAgents.FindAsync([agentId], cancellationToken);
        if (agent is null || agent.Status == "quarantined")
            return;

        var now = DateTimeOffset.UtcNow;
        agent.Status = "quarantined";
        agent.UpdatedAt = now;
        dbContext.LobbyOperationAudits.Add(new LobbyOperationAudit
        {
            WarmupAgentId = agentId,
            EventType = "warmup_agent_quarantined",
            DetailsJson = JsonSerializer.Serialize(new { reason = "lobby_operation_preservation_failed" }),
            ObservedAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class LobbyQueryAgentUnavailableException : HttpRequestException
{
    public LobbyQueryAgentUnavailableException() : base("lobby_query_agent_unavailable")
    {
    }
}
