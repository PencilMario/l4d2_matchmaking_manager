using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Servers;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupSchedulerService(
    MatchmakingDbContext dbContext,
    IAgentControlClient agents,
    SharedLibraryMaintenanceService maintenance,
    IHealthyAgentSelector? selector = null,
    ISourceA2sClient? a2s = null,
    WarmupDecisionEngine? engine = null,
    IRconCredentialProtector? rconCredentials = null)
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var attempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.State == "active" || attempt.State == "uncertain")
            .ToListAsync(cancellationToken);
        foreach (var attempt in attempts)
        {
            var agent = await dbContext.WarmupAgents.FindAsync([attempt.WarmupAgentId], cancellationToken);
            if (agent is null)
                continue;
            AgentOperationSnapshot? snapshot;
            try
            {
                snapshot = await agents.GetOperationAsync(agent, attempt.OperationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                QuarantineAgent(agent, attempt.OperationId, "agent_operation_recovery_failed", DateTimeOffset.UtcNow);
                continue;
            }
            if (snapshot?.State == "active")
            {
                attempt.State = "active";
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
            .Where(attempt => attempt.State == "active" || attempt.State == "uncertain")
            .ToListAsync(cancellationToken);
        var schedulingEngine = engine ?? new WarmupDecisionEngine();
        var recreateTargetServerIds = new HashSet<Guid>();
        var recreateAttemptStartedAt = new Dictionary<Guid, DateTimeOffset>();
        foreach (var attempt in activeAttempts)
        {
            var target = await dbContext.TargetServers.FindAsync([attempt.TargetServerId], cancellationToken);
            var activeAgent = await dbContext.WarmupAgents.FindAsync([attempt.WarmupAgentId], cancellationToken);
            if (target is null || activeAgent is null)
                continue;
            AgentOperationSnapshot? snapshot;
            try
            {
                snapshot = await agents.GetOperationAsync(activeAgent, attempt.OperationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                QuarantineAgent(activeAgent, attempt.OperationId, "agent_operation_observation_failed", DateTimeOffset.UtcNow);
                continue;
            }
            if (snapshot?.State != "active")
            {
                attempt.State = "completed";
                attempt.CompletedAt = DateTimeOffset.UtcNow;
                var missingOperationLease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
                if (missingOperationLease?.OperationId == attempt.OperationId)
                    dbContext.ReservationLeases.Remove(missingOperationLease);
                continue;
            }
            attempt.State = "active";
            if (snapshot.Lobby is null)
                continue;
            A2sServerInfo liveInfo;
            try
            {
                var liveAddresses = await Dns.GetHostAddressesAsync(target.Host, cancellationToken);
                var liveAddress = liveAddresses.FirstOrDefault(candidate => candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (liveAddress is null)
                    continue;
                liveInfo = await a2s.GetInfoAsync(new IPEndPoint(liveAddress, target.Port), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                continue;
            }
            var priorMembers = JsonSerializer.Deserialize<HashSet<string>>(attempt.ExternalMemberIdsJson) ?? [];
            var observed = schedulingEngine.ObserveExternalMembers(
                new WarmupAttemptSnapshot(
                    Enum.TryParse<WarmupPhase>(attempt.Phase, true, out var phase) ? phase : WarmupPhase.AwaitingFirstMember,
                    attempt.StartedAt.AddSeconds(target.AttemptWindowSeconds),
                    attempt.LobbyReadyAt ?? attempt.StartedAt,
                    attempt.FirstExternalMemberAt,
                    attempt.QuietSince,
                    priorMembers),
                snapshot.Lobby.Members.Where(member => member.SteamId != snapshot.Lobby.OwnerSteamId).Select(member => member.SteamId).ToHashSet(),
                DateTimeOffset.UtcNow);
            attempt.Phase = observed.Phase.ToString();
            attempt.LobbyReadyAt = observed.LobbyReadyAt;
            attempt.FirstExternalMemberAt = observed.FirstExternalMemberAt;
            attempt.QuietSince = observed.QuietSince;
            attempt.ExternalMemberIdsJson = JsonSerializer.Serialize(observed.ExternalMemberIds);
            attempt.LobbyId = snapshot.Lobby.LobbyId;
            attempt.ObservedAt = snapshot.ObservedAt;
            var decision = schedulingEngine.Evaluate(target, liveInfo.PlayerCount, observed, DateTimeOffset.UtcNow);
            if (decision is WarmupDecision.RecreateSameTarget or WarmupDecision.ReleaseAndReschedule or WarmupDecision.SkipTarget)
            {
                try
                {
                    await agents.StopOperationAsync(activeAgent, attempt.OperationId, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    QuarantineAgent(activeAgent, attempt.OperationId, "agent_operation_stop_uncertain", DateTimeOffset.UtcNow);
                    continue;
                }
                attempt.State = "completed";
                attempt.CompletedAt = DateTimeOffset.UtcNow;
                var lease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
                if (lease?.OperationId == attempt.OperationId)
                    dbContext.ReservationLeases.Remove(lease);
                if (decision == WarmupDecision.RecreateSameTarget)
                {
                    recreateTargetServerIds.Add(target.Id);
                    recreateAttemptStartedAt[target.Id] = attempt.StartedAt;
                }
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        activeAttempts = activeAttempts.Where(attempt => attempt.State is "active" or "uncertain").ToList();
        var targetServers = await dbContext.TargetServers.Where(candidate => candidate.Enabled).ToListAsync(cancellationToken);
        var selectionPool = recreateTargetServerIds.Count == 0
            ? targetServers
            : targetServers.Where(candidate => recreateTargetServerIds.Contains(candidate.Id)).ToList();
        var activeWarmups = activeAttempts
            .GroupBy(attempt => attempt.TargetServerId)
            .ToDictionary(group => group.Key, group => group.Count());
        var agent = await selector.SelectAsync(cancellationToken);
        if (agent is null || activeAttempts.Any(attempt => attempt.WarmupAgentId == agent.Id))
            return;

        var candidatePool = selectionPool.ToList();
        var attemptedTargetIds = new HashSet<Guid>();
        var usedGlobalFallback = recreateTargetServerIds.Count == 0;
        TargetServer? server = null;
        TargetServerRotationCursor? cursor = null;
        IPAddress? address = null;
        while (true)
        {
            var candidatePriority = candidatePool
                .Where(candidate => activeWarmups.GetValueOrDefault(candidate.Id) < schedulingEngine.GetEffectiveConcurrency(candidate))
                .Select(candidate => (int?)candidate.Priority)
                .Max();
            cursor = candidatePriority is null
                ? null
                : await dbContext.TargetServerRotationCursors.FindAsync([candidatePriority.Value], cancellationToken);
            server = schedulingEngine.SelectNextTarget(candidatePool, activeWarmups, cursor?.LastTargetServerId);
            if (server is null)
            {
                if (usedGlobalFallback)
                    return;
                candidatePool = targetServers.Where(target => !attemptedTargetIds.Contains(target.Id)).ToList();
                usedGlobalFallback = true;
                continue;
            }

            attemptedTargetIds.Add(server.Id);
            candidatePool.RemoveAll(candidate => candidate.Id == server.Id);
            A2sServerInfo info;
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(server.Host, cancellationToken);
                address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (address is null)
                    continue;
                info = await a2s.GetInfoAsync(new IPEndPoint(address, server.Port), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                continue;
            }
            if (info.PlayerCount >= server.PlayerTarget || (server.RequiresReservation && info.PlayerCount > 0))
                continue;
            break;
        }

        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var attemptStartedAt = recreateAttemptStartedAt.TryGetValue(server.Id, out var parentStartedAt)
            ? parentStartedAt
            : now;
        var attemptToStart = new WarmupAttempt
        {
            Id = Guid.NewGuid(),
            TargetServerId = server.Id,
            WarmupAgentId = agent.Id,
            OperationId = operationId,
            Mode = server.RequiresReservation ? "reserved" : "standard",
            State = "uncertain",
            Phase = WarmupPhase.AwaitingFirstMember.ToString(),
            LobbyReadyAt = now,
            ExternalMemberIdsJson = "[]",
            StartedAt = attemptStartedAt,
            ObservedAt = now,
        };
        dbContext.WarmupAttempts.Add(attemptToStart);
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

        AgentOperationStartResult start;
        var rconPassword = !server.RequiresReservation || server.RconPasswordCiphertext is null
            ? null
            : (rconCredentials ?? throw new InvalidOperationException("rcon_credential_protector_not_configured"))
                .Unprotect(server.RconPasswordCiphertext);
        try
        {
            start = await agents.StartOperationAsync(agent, new AgentOperationRequest(
                operationId,
                server.RequiresReservation ? AgentLobbyMode.Reserved : AgentLobbyMode.Standard,
                address.ToString(),
                checked((ushort)server.Port),
                rconPassword), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            QuarantineAgent(agent, operationId, "agent_operation_start_uncertain", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        if (cursor is null)
        {
            cursor = new TargetServerRotationCursor { Priority = server.Priority };
            dbContext.TargetServerRotationCursors.Add(cursor);
        }
        cursor.LastTargetServerId = server.Id;
        cursor.UpdatedAt = now;
        attemptToStart.State = start.Operation.State;
        attemptToStart.LobbyId = start.Operation.Lobby?.LobbyId;
        attemptToStart.ObservedAt = start.Operation.ObservedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

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
