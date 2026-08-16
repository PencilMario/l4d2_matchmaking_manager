using System.Text.Json;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Agents;

public sealed class WarmupAgentService(
    MatchmakingDbContext dbContext,
    WarmupAgentContainerService containers,
    AgentContainerOptions options)
{
    public async Task<WarmupAgentResponse> CreateAsync(
        CreateWarmupAgentRequest request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        if (await dbContext.WarmupAgents.AnyAsync(agent => agent.Name == name, cancellationToken))
            throw new InvalidOperationException("warmup_agent_name_exists");

        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var agent = new WarmupAgent
        {
            Id = agentId,
            Name = name,
            DownloadRegion = NormalizeRegion(request.DownloadRegion),
            NoVncPort = await AllocateNoVncPortAsync(cancellationToken),
            SteamDataVolumeName = $"steam-data-{agentId:N}",
            AccountConfigVolumeName = $"agent-config-{agentId:N}",
            CreatedAt = now,
            UpdatedAt = now,
            Status = "created",
        };
        dbContext.WarmupAgents.Add(agent);
        AddAudit("warmup_agent_created", agent.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await containers.CreateAsync(agent, cancellationToken);
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

    public async Task<IReadOnlyList<WarmupAgentResponse>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.WarmupAgents
            .AsNoTracking()
            .OrderBy(agent => agent.CreatedAt)
            .Select(agent => ToResponse(agent))
            .ToListAsync(cancellationToken);

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
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit("warmup_agent_updated", agent.Id, agent.UpdatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(agent);
    }

    public async Task<WarmupAgentResponse?> StartAsync(Guid agentId, CancellationToken cancellationToken)
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

    public async Task<WarmupAgentResponse?> StopAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (agent.Status == "running")
        {
            await containers.StopAsync(agent, cancellationToken);
            agent.Status = "stopped";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
            AddAudit("warmup_agent_stopped", agent.Id, agent.UpdatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return ToResponse(agent);
    }

    public async Task<WarmupAgentResponse?> RecreateAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return null;
        if (!string.IsNullOrWhiteSpace(agent.ContainerId))
            await containers.DeleteAsync(agent, cancellationToken);
        agent.ContainerId = null;
        agent.Status = "created";
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        await containers.CreateAsync(agent, cancellationToken);
        await containers.StartAsync(agent, cancellationToken);
        agent.Status = "running";
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit("warmup_agent_recreated", agent.Id, agent.UpdatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(agent);
    }

    public async Task<bool> DeleteAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await FindAsync(agentId, cancellationToken);
        if (agent is null)
            return false;
        if (!string.IsNullOrWhiteSpace(agent.ContainerId))
            await containers.DeleteAsync(agent, cancellationToken);
        dbContext.WarmupAgents.Remove(agent);
        AddAudit("warmup_agent_deleted", agent.Id, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
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

    private void AddAudit(string eventType, Guid agentId, DateTimeOffset observedAt) =>
        dbContext.LobbyOperationAudits.Add(new LobbyOperationAudit
        {
            WarmupAgentId = agentId,
            EventType = eventType,
            DetailsJson = JsonSerializer.Serialize(new { agentId }),
            ObservedAt = observedAt,
        });

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

    private static WarmupAgentResponse ToResponse(WarmupAgent agent) => new(
        agent.Id,
        agent.Name,
        agent.Status,
        agent.DownloadRegion,
        agent.NoVncPort,
        agent.CreatedAt,
        agent.UpdatedAt);
}
