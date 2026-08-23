using System.Text.Json;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Agents;

public sealed class WarmupAgentService(
    MatchmakingDbContext dbContext,
    WarmupAgentContainerService containers,
    AgentContainerOptions options,
    AgentVncSessionService vncSessions,
    AgentLifecycleCoordinator lifecycleCoordinator,
    IAgentControlClient agentControlClient,
    WarmupAttemptDrainService attemptDrain)
{
    public Task<WarmupAgentResponse> CreateAsync(
        CreateWarmupAgentRequest request,
        CancellationToken cancellationToken)
    {
        var agentId = Guid.NewGuid();
        return lifecycleCoordinator.ExecuteAsync(
            agentId,
            () => CreateCoreAsync(agentId, request, cancellationToken),
            cancellationToken);
    }

    private async Task<WarmupAgentResponse> CreateCoreAsync(
        Guid agentId,
        CreateWarmupAgentRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        if (await dbContext.WarmupAgents.AnyAsync(agent => agent.Name == name, cancellationToken))
            throw new InvalidOperationException("warmup_agent_name_exists");

        var now = DateTimeOffset.UtcNow;
        var reportingToken = AgentReportingTokenService.Create(agentId);
        var agent = new WarmupAgent
        {
            Id = agentId,
            Name = name,
            DownloadRegion = NormalizeRegion(request.DownloadRegion),
            KeepVncAlive = request.KeepVncAlive,
            NoVncPort = await AllocateNoVncPortAsync(cancellationToken),
            SteamDataVolumeName = $"steam-data-{agentId:N}",
            AccountConfigVolumeName = $"agent-config-{agentId:N}",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "created",
            EntryReportingTokenHash = reportingToken.Hash,
        };
        dbContext.WarmupAgents.Add(agent);
        AddAudit("warmup_agent_created", agent.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await containers.CreateAsync(agent, cancellationToken, reportingToken.Token);
            await containers.StartAsync(agent, cancellationToken);
            agent.Status = "running";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            AddAudit("warmup_agent_started", agent.Id, agent.UpdatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(agent);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(agent.ContainerId))
                await containers.DeleteAsync(agent, CancellationToken.None);
            dbContext.WarmupAgents.Remove(agent);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<WarmupAgentResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var agents = await dbContext.WarmupAgents
            .AsNoTracking()
            .OrderBy(agent => agent.CreatedAt)
            .ToListAsync(cancellationToken);
        var responses = new List<WarmupAgentResponse>(agents.Count);
        foreach (var agent in agents)
        {
            var ready = false;
            AgentHealthSnapshot? health = null;
            if (agent.Status == "running")
            {
                try
                {
                    var observedHealth = await agentControlClient.GetHealthAsync(agent, cancellationToken);
                    health = observedHealth;
                    ready = observedHealth.Ready;
                }
                catch (HttpRequestException)
                {
                }
            }
            responses.Add(ToResponse(agent, ready, agent.DownloadRegion ?? health?.CurrentDownloadRegion));
        }
        return responses;
    }

    public async Task<WarmupAgentResponse?> GetAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        return agent is null ? null : ToResponse(agent);
    }

    public async Task<WarmupAgentResponse?> UpdateAsync(
        Guid agentId,
        UpdateWarmupAgentRequest request,
        CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        var name = NormalizeName(request.Name);
        if (await dbContext.WarmupAgents.AnyAsync(candidate => candidate.Id != agentId && candidate.Name == name, cancellationToken))
            throw new InvalidOperationException("warmup_agent_name_exists");
        agent.Name = name;
        agent.DownloadRegion = NormalizeRegion(request.DownloadRegion);
        agent.KeepVncAlive = request.KeepVncAlive;
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit("warmup_agent_updated", agent.Id, agent.UpdatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(agent);
    }

    public Task<WarmupAgentResponse?> StartAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => StartCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<WarmupAgentResponse?> StartCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (agent.Status != "running")
        {
            await containers.StartAsync(agent, cancellationToken);
            agent.Status = "running";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            AddAudit("warmup_agent_started", agent.Id, agent.UpdatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return ToResponse(agent);
    }

    public Task<WarmupAgentResponse?> StopAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => StopCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<WarmupAgentResponse?> StopCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (agent.Status is "running" or "quarantined")
        {
            if (!await attemptDrain.DrainAgentAsync(agent.Id, cancellationToken))
                throw new InvalidOperationException("warmup_agent_stop_drain_failed");
            await vncSessions.CloseAsync(agent.Id, cancellationToken);
            await containers.StopAsync(agent, cancellationToken);
            agent.Status = "stopped";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            AddAudit("warmup_agent_stopped", agent.Id, agent.UpdatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return ToResponse(agent);
    }

    public Task<WarmupAgentResponse?> RecreateAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => RecreateCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<WarmupAgentResponse?> RecreateCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (!await attemptDrain.DrainAgentAsync(agent.Id, cancellationToken))
            throw new InvalidOperationException("warmup_agent_recreate_drain_failed");
        await vncSessions.CloseAsync(agent.Id, cancellationToken);
        var previousContainerId = agent.ContainerId;
        var previousReportingTokenHash = agent.EntryReportingTokenHash;
        agent.ContainerId = null;
        agent.Status = "recreate_pending";
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        // Establish a durable safe boundary before deleting the old container. If any later
        // database operation fails, the persisted record will not claim the old container is running.
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousContainerId))
        {
            agent.ContainerId = previousContainerId;
            try
            {
                await containers.DeleteAsync(agent, cancellationToken);
            }
            catch
            {
                agent.Status = "recreate_pending";
                agent.UpdatedAt = DateTimeOffset.UtcNow;
                try
                {
                    await dbContext.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // The persisted pending/null boundary remains recoverable by deterministic container name.
                }
                throw;
            }
        }

        agent.ContainerId = null;
        agent.Status = "created";
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var reportingToken = AgentReportingTokenService.Create(agent.Id);
        try
        {
            await containers.CreateAsync(agent, cancellationToken, reportingToken.Token);
            await containers.StartAsync(agent, cancellationToken);
        }
        catch
        {
            var replacementContainerId = agent.ContainerId;
            var replacementRemoved = await TryDeleteContainerAsync(agent);
            agent.EntryReportingTokenHash = previousReportingTokenHash;
            agent.ContainerId = replacementRemoved ? null : replacementContainerId;
            agent.Status = replacementRemoved ? "created" : "recreate_pending";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                // The persisted created/null boundary remains safe if compensation is unavailable.
            }
            throw;
        }
        agent.EntryReportingTokenHash = reportingToken.Hash;
        agent.Status = "running";
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        var recreationAudit = AddAudit("warmup_agent_recreated", agent.Id, agent.UpdatedAt);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            var replacementContainerId = agent.ContainerId;
            var replacementRemoved = await TryDeleteContainerAsync(agent);
            dbContext.Entry(recreationAudit).State = EntityState.Detached;
            agent.EntryReportingTokenHash = previousReportingTokenHash;
            agent.ContainerId = replacementRemoved ? null : replacementContainerId;
            agent.Status = replacementRemoved ? "created" : "recreate_pending";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original failure if the database is still unavailable during compensation.
            }
            throw;
        }
        return ToResponse(agent);
    }

    private async Task<bool> TryDeleteContainerAsync(WarmupAgent agent)
    {
        if (string.IsNullOrWhiteSpace(agent.ContainerId))
            return true;

        try
        {
            await containers.DeleteAsync(agent, CancellationToken.None);
            return true;
        }
        catch
        {
            try
            {
                await containers.StopAsync(agent, CancellationToken.None);
            }
            catch
            {
                // Keep the container ID for a later managed cleanup attempt.
            }
            return false;
        }
    }

    public Task<bool> DeleteAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => DeleteCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<bool> DeleteCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return false;
        await vncSessions.CloseAsync(agent.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(agent.ContainerId))
            await containers.DeleteAsync(agent, cancellationToken);
        dbContext.WarmupAgents.Remove(agent);
        AddAudit("warmup_agent_deleted", agent.Id, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<AgentVncSession?> OpenVncSessionAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => OpenVncSessionCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<AgentVncSession?> OpenVncSessionCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (agent.Status != "running")
            throw new InvalidOperationException("warmup_agent_not_running");
        return await vncSessions.OpenAsync(
            agent.Id,
            agent.ContainerId ?? throw new InvalidOperationException("agent_container_not_created"),
            cancellationToken);
    }

    public Task<bool> CloseVncSessionAsync(Guid agentId, CancellationToken cancellationToken) =>
        lifecycleCoordinator.ExecuteAsync(agentId, () => CloseVncSessionCoreAsync(agentId, cancellationToken), cancellationToken);

    private async Task<bool> CloseVncSessionCoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        if (await FindAsync(agentId, cancellationToken) is null)
            return false;
        await vncSessions.CloseAsync(agentId, cancellationToken);
        return true;
    }

    private async Task<WarmupAgent?> FindAsync(Guid agentId, CancellationToken cancellationToken) =>
        await dbContext.WarmupAgents.SingleOrDefaultAsync(agent => agent.Id == agentId, cancellationToken);

    private async Task<int> AllocateNoVncPortAsync(CancellationToken cancellationToken)
    {
        if (options.NoVncPortEnd < options.NoVncPortStart)
            throw new InvalidOperationException("invalid_novnc_port_range");
        var usedByAgents = await dbContext.WarmupAgents
            .Where(agent => agent.NoVncPort > 0)
            .Select(agent => agent.NoVncPort)
            .ToListAsync(cancellationToken);
        var usedPorts = usedByAgents.ToHashSet();
        usedPorts.UnionWith(await containers.GetUsedHostPortsAsync(cancellationToken));
        for (var port = options.NoVncPortStart; port <= options.NoVncPortEnd; port++)
        {
            if (!usedPorts.Contains(port))
                return port;
        }
        throw new InvalidOperationException("no_free_novnc_port");
    }

    private LobbyOperationAudit AddAudit(string eventType, Guid agentId, DateTimeOffset observedAt)
    {
        var audit = new LobbyOperationAudit
        {
            WarmupAgentId = agentId,
            EventType = eventType,
            DetailsJson = JsonSerializer.Serialize(new { agentId }),
            ObservedAt = observedAt,
        };
        dbContext.LobbyOperationAudits.Add(audit);
        return audit;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 128)
            throw new ArgumentException("invalid_warmup_agent_name");
        return normalized;
    }

    private static string? NormalizeRegion(string? region)
    {
        var normalized = region?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static WarmupAgentResponse ToResponse(WarmupAgent agent, bool ready = false, string? downloadRegion = null) => new(
        agent.Id,
        agent.Name,
        agent.Status,
        downloadRegion ?? agent.DownloadRegion,
        agent.KeepVncAlive,
        agent.NoVncPort,
        agent.CreatedAt,
        agent.UpdatedAt,
        ready);
}
