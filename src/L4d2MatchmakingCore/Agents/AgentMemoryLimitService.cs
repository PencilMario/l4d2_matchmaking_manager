using Docker.DotNet;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace L4d2MatchmakingCore.Agents;

public sealed class AgentMemoryLimitService(
    MatchmakingDbContext dbContext,
    IAgentControlClient agentControlClient,
    IAgentContainerRuntime containerRuntime,
    AgentLifecycleCoordinator lifecycleCoordinator,
    TimeProvider? timeProvider = null,
    ILogger<AgentMemoryLimitService>? logger = null)
{
    public static readonly TimeSpan CefGuardStabilityDelay = TimeSpan.FromSeconds(60);
    public const long ReclaimedMemoryLimitBytes = 600L * 1024 * 1024;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var agentIds = await dbContext.WarmupAgents
            .AsNoTracking()
            .Where(agent => agent.Status == "running" && agent.ContainerId != null)
            .Select(agent => agent.Id)
            .ToListAsync(cancellationToken);

        foreach (var agentId in agentIds)
        {
            try
            {
                await lifecycleCoordinator.ExecuteAsync(
                    agentId,
                    async () =>
                    {
                        await ApplyToAgentAsync(agentId, cancellationToken);
                        return true;
                    },
                    cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                logger?.LogDebug(exception, "Agent {AgentId} health probe unavailable during memory reclaim", agentId);
            }
            catch (DockerContainerNotFoundException exception)
            {
                logger?.LogDebug(exception, "Agent {AgentId} container disappeared during memory reclaim", agentId);
            }
        }
    }

    private async Task ApplyToAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await dbContext.WarmupAgents.SingleOrDefaultAsync(
            candidate => candidate.Id == agentId && candidate.Status == "running",
            cancellationToken);
        if (agent?.ContainerId is not { Length: > 0 } containerId)
            return;

        var health = await agentControlClient.GetHealthAsync(agent, cancellationToken);
        if (!health.Ready || health.CefGuardAppliedAt is not { } appliedAt)
            return;

        var elapsed = clock.GetUtcNow() - appliedAt;
        if (elapsed < CefGuardStabilityDelay)
            return;

        var currentLimit = await containerRuntime.GetMemoryLimitAsync(containerId, cancellationToken);
        if (currentLimit is > 0 and <= ReclaimedMemoryLimitBytes)
            return;

        await containerRuntime.UpdateMemoryLimitAsync(
            containerId,
            ReclaimedMemoryLimitBytes,
            cancellationToken);
        logger?.LogInformation(
            "Reduced Agent {AgentId} container memory limit to {MemoryLimitBytes} bytes after CEF guard stabilization",
            agentId,
            ReclaimedMemoryLimitBytes);
    }
}
