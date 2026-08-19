using System.Text.Json;
using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupAttemptDrainService(
    MatchmakingDbContext dbContext,
    IAgentControlClient agents)
{
    public async Task<bool> DrainAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var attempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.WarmupAgentId == agentId &&
                (attempt.State == "active" || attempt.State == "uncertain"))
            .ToListAsync(cancellationToken);
        var agent = await dbContext.WarmupAgents.FindAsync([agentId], cancellationToken);
        if (agent is null)
            return attempts.Count == 0;

        var allConfirmed = true;
        foreach (var attempt in attempts)
        {
            try
            {
                await agents.StopOperationAsync(agent, attempt.OperationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                allConfirmed = false;
                QuarantineAgent(agent, attempt.OperationId, "agent_operation_drain_uncertain", DateTimeOffset.UtcNow);
                continue;
            }

            attempt.State = "completed";
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            var lease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
            if (lease?.OperationId == attempt.OperationId)
                dbContext.ReservationLeases.Remove(lease);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return allConfirmed;
    }

    public async Task<bool> DrainAsync(Guid targetServerId, CancellationToken cancellationToken)
    {
        var attempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.TargetServerId == targetServerId &&
                (attempt.State == "active" || attempt.State == "uncertain"))
            .ToListAsync(cancellationToken);
        var allConfirmed = true;
        foreach (var attempt in attempts)
        {
            var agent = await dbContext.WarmupAgents.FindAsync([attempt.WarmupAgentId], cancellationToken);
            if (agent is null)
            {
                allConfirmed = false;
                AddAudit(attempt, "warmup_attempt_drain_agent_missing");
                continue;
            }
            try
            {
                await agents.StopOperationAsync(agent, attempt.OperationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                allConfirmed = false;
                QuarantineAgent(agent, attempt.OperationId, "agent_operation_drain_uncertain", DateTimeOffset.UtcNow);
                continue;
            }
            attempt.State = "completed";
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            var lease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
            if (lease?.OperationId == attempt.OperationId)
                dbContext.ReservationLeases.Remove(lease);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return allConfirmed;
    }

    private void AddAudit(WarmupAttempt attempt, string eventType) =>
        dbContext.LobbyOperationAudits.Add(new LobbyOperationAudit
        {
            TargetServerId = attempt.TargetServerId,
            WarmupAttemptId = attempt.Id,
            EventType = eventType,
            DetailsJson = JsonSerializer.Serialize(new { attempt.OperationId }),
            ObservedAt = DateTimeOffset.UtcNow,
        });

    private void QuarantineAgent(WarmupAgent agent, Guid operationId, string reason, DateTimeOffset now)
    {
        if (agent.Status == "quarantined")
            return;
        agent.Status = "quarantined";
        agent.UpdatedAt = now;
        dbContext.LobbyOperationAudits.Add(new LobbyOperationAudit
        {
            WarmupAgentId = agent.Id,
            EventType = "warmup_agent_quarantined",
            DetailsJson = JsonSerializer.Serialize(new { operationId, reason }),
            ObservedAt = now,
        });
    }
}
