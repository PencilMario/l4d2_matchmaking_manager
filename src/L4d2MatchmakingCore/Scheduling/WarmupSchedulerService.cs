using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupSchedulerService(
    MatchmakingDbContext dbContext,
    IAgentControlClient agents,
    SharedLibraryMaintenanceService maintenance,
    IHealthyAgentSelector? selector = null,
    ISourceA2sClient? a2s = null,
    WarmupDecisionEngine? engine = null)
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var attempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.State == "active")
            .ToListAsync(cancellationToken);
        foreach (var attempt in attempts)
        {
            var agent = await dbContext.WarmupAgents.FindAsync([attempt.WarmupAgentId], cancellationToken);
            if (agent is null)
                continue;
            var snapshot = await agents.GetOperationAsync(agent, attempt.OperationId, cancellationToken);
            if (snapshot?.State == "active")
            {
                attempt.ObservedAt = snapshot.ObservedAt;
                continue;
            }
            attempt.State = "completed";
            attempt.CompletedAt = DateTimeOffset.UtcNow;
            var lease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
            if (lease?.OperationId == attempt.OperationId)
                dbContext.ReservationLeases.Remove(lease);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        if (await maintenance.IsHeldAsync(cancellationToken))
            return;
        if (selector is null || a2s is null)
            return;

        var activeAttempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.State == "active")
            .ToListAsync(cancellationToken);
        var schedulingEngine = engine ?? new WarmupDecisionEngine();
        var server = schedulingEngine.SelectNextTarget(
            await dbContext.TargetServers.Where(candidate => candidate.Enabled).ToListAsync(cancellationToken),
            activeAttempts.GroupBy(attempt => attempt.TargetServerId).ToDictionary(group => group.Key, group => group.Count()));
        if (server is null)
            return;
        var agent = await selector.SelectAsync(cancellationToken);
        if (agent is null || activeAttempts.Any(attempt => attempt.WarmupAgentId == agent.Id))
            return;
        var addresses = await Dns.GetHostAddressesAsync(server.Host, cancellationToken);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (address is null)
            return;
        var info = await a2s.GetInfoAsync(new IPEndPoint(address, server.Port), cancellationToken);
        if (info.PlayerCount >= server.PlayerTarget || (server.RequiresReservation && info.PlayerCount > 0))
            return;

        var operationId = Guid.NewGuid();
        var start = await agents.StartOperationAsync(agent, new AgentOperationRequest(
            operationId,
            server.RequiresReservation ? AgentLobbyMode.Reserved : AgentLobbyMode.Standard,
            address.ToString(),
            checked((ushort)server.Port)), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        dbContext.WarmupAttempts.Add(new WarmupAttempt
        {
            Id = Guid.NewGuid(),
            TargetServerId = server.Id,
            WarmupAgentId = agent.Id,
            OperationId = operationId,
            Mode = server.RequiresReservation ? "reserved" : "standard",
            State = start.Operation.State,
            LobbyId = start.Operation.Lobby?.LobbyId,
            StartedAt = now,
            ObservedAt = start.Operation.ObservedAt,
        });
        if (server.RequiresReservation)
        {
            dbContext.ReservationLeases.Add(new ReservationLease
            {
                TargetServerId = server.Id,
                OperationId = operationId,
                ExpiresAt = now.AddSeconds(server.AttemptWindowSeconds),
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
