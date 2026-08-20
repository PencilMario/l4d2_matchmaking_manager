using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2Matchmaking.Contracts;
using L4d2MatchmakingCore.Configuration;
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
    IRconCredentialProtector? rconCredentials = null,
    CoreOptions? coreOptions = null)
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

        await RestoreRestartedAgentsAsync(cancellationToken);

        var activeAttempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.State == "active" || attempt.State == "uncertain")
            .ToListAsync(cancellationToken);
        var restartPendingAttempts = await dbContext.WarmupAttempts
            .Where(attempt => attempt.State == "restart_pending")
            .ToListAsync(cancellationToken);
        var serverStartedAt = activeAttempts
            .GroupBy(attempt => attempt.TargetServerId)
            .ToDictionary(group => group.Key, group => group.Min(attempt => attempt.StartedAt));
        var schedulingEngine = engine ?? new WarmupDecisionEngine();
        var recreateRequests = new List<RecreateRequest>();
        foreach (var attempt in restartPendingAttempts)
        {
            var agent = await dbContext.WarmupAgents.FindAsync([attempt.WarmupAgentId], cancellationToken);
            if (agent?.Status == "running")
                recreateRequests.Add(new RecreateRequest(attempt.TargetServerId, attempt.StartedAt, attempt));
        }
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
                await RestartSteamAfterAttemptAsync(activeAgent, attempt.OperationId, cancellationToken);
                continue;
            }
            attempt.State = "active";
            if (snapshot.Lobby is null)
                continue;
            A2sServerInfo liveInfo;
            try
            {
                var liveEndpoint = await A2sEndpointResolver.ResolveIpv4Async(target.Host, target.Port, cancellationToken);
                if (liveEndpoint is null)
                    continue;
                liveInfo = await a2s.GetInfoAsync(liveEndpoint, cancellationToken);
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
                    serverStartedAt.GetValueOrDefault(attempt.TargetServerId, attempt.StartedAt).AddSeconds(target.AttemptWindowSeconds),
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
                attempt.State = decision == WarmupDecision.RecreateSameTarget ? "restart_pending" : "completed";
                attempt.CompletedAt = decision == WarmupDecision.RecreateSameTarget ? null : DateTimeOffset.UtcNow;
                var lease = await dbContext.ReservationLeases.FindAsync([attempt.TargetServerId], cancellationToken);
                if (lease?.OperationId == attempt.OperationId)
                    dbContext.ReservationLeases.Remove(lease);
                await RestartSteamAfterAttemptAsync(activeAgent, attempt.OperationId, cancellationToken);
                if (decision == WarmupDecision.RecreateSameTarget)
                    recreateRequests.Add(new RecreateRequest(target.Id, attempt.StartedAt, attempt));
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        activeAttempts = activeAttempts.Where(attempt => attempt.State is "active" or "uncertain").ToList();
        var targetServers = await dbContext.TargetServers.Where(candidate => candidate.Enabled).ToListAsync(cancellationToken);
        var activeWarmups = activeAttempts
            .GroupBy(attempt => attempt.TargetServerId)
            .ToDictionary(group => group.Key, group => group.Count());
        var leaseTargetServerIds = (await dbContext.ReservationLeases
                .Select(lease => lease.TargetServerId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var healthyAgentIds = (await selector.ListHealthyAsync(cancellationToken))
            .Select(agent => agent.Id)
            .Distinct()
            .ToList();
        if (healthyAgentIds.Count == 0)
            return;
        var trackedAgents = await dbContext.WarmupAgents
            .Where(agent => healthyAgentIds.Contains(agent.Id) && agent.Status == "running")
            .ToDictionaryAsync(agent => agent.Id, cancellationToken);
        var idleAgents = healthyAgentIds
            .Where(agentId => trackedAgents.ContainsKey(agentId) && activeAttempts.All(attempt => attempt.WarmupAgentId != agentId))
            .Select(agentId => trackedAgents[agentId])
            .Take(coreOptions?.SchedulerMaxStartsPerTick ?? CoreOptions.DefaultSchedulerMaxStartsPerTick)
            .ToList();
        if (idleAgents.Count == 0)
            return;
        var cursors = await dbContext.TargetServerRotationCursors
            .ToDictionaryAsync(cursor => cursor.Priority, cancellationToken);
        var plannedCursorTargets = cursors.ToDictionary(pair => pair.Key, pair => pair.Value.LastTargetServerId);
        var continuationStartedAt = activeAttempts
            .Where(attempt => attempt.State is "active" or "uncertain")
            .GroupBy(attempt => attempt.TargetServerId)
            .ToDictionary(group => group.Key, group => group.Min(attempt => attempt.StartedAt));
        var skippedContinuationTargets = new HashSet<Guid>();
        var plannedStarts = new List<PlannedStart>();
        var nextAgentIndex = 0;
        foreach (var request in recreateRequests)
        {
            if (nextAgentIndex >= idleAgents.Count)
                break;
            var agent = idleAgents[nextAgentIndex++];
            var target = targetServers.SingleOrDefault(candidate => candidate.Id == request.TargetServerId);
            if (target is null)
                continue;
            var planned = await PlanStartAsync(
                agent,
                target,
                request.StartedAt,
                schedulingEngine,
                activeWarmups,
                leaseTargetServerIds,
                plannedCursorTargets,
                cancellationToken);
            if (planned is not null)
            {
                plannedStarts.Add(planned);
                if (request.PendingAttempt is not null)
                {
                    request.PendingAttempt.State = "completed";
                    request.PendingAttempt.CompletedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        while (nextAgentIndex < idleAgents.Count)
        {
            var agent = idleAgents[nextAgentIndex++];
            var planned = await PlanContinuationStartAsync(
                agent,
                targetServers,
                continuationStartedAt,
                skippedContinuationTargets,
                schedulingEngine,
                activeWarmups,
                leaseTargetServerIds,
                plannedCursorTargets,
                cancellationToken);
            planned ??= await PlanNextStartAsync(
                agent,
                targetServers,
                schedulingEngine,
                activeWarmups,
                leaseTargetServerIds,
                plannedCursorTargets,
                cancellationToken);
            if (planned is not null)
                plannedStarts.Add(planned);
        }
        if (plannedStarts.Count == 0)
            return;

        await dbContext.SaveChangesAsync(cancellationToken);
        var startResults = await Task.WhenAll(plannedStarts.Select(plan => StartPlannedAsync(plan, cancellationToken)));
        foreach (var result in startResults)
        {
            if (result.Exception is not null)
            {
                QuarantineAgent(result.Plan.Agent, result.Plan.Attempt.OperationId, "agent_operation_start_uncertain", DateTimeOffset.UtcNow);
                continue;
            }
            var start = result.Start!;
            if (!cursors.TryGetValue(result.Plan.Server.Priority, out var cursor))
            {
                cursor = new TargetServerRotationCursor { Priority = result.Plan.Server.Priority };
                dbContext.TargetServerRotationCursors.Add(cursor);
                cursors.Add(cursor.Priority, cursor);
            }
            cursor.LastTargetServerId = result.Plan.Server.Id;
            cursor.UpdatedAt = DateTimeOffset.UtcNow;
            result.Plan.Attempt.State = start.Operation.State;
            result.Plan.Attempt.LobbyId = start.Operation.Lobby?.LobbyId;
            result.Plan.Attempt.ObservedAt = start.Operation.ObservedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PlannedStart?> PlanContinuationStartAsync(
        WarmupAgent agent,
        IReadOnlyList<TargetServer> targetServers,
        IReadOnlyDictionary<Guid, DateTimeOffset> continuationStartedAt,
        HashSet<Guid> skippedContinuationTargets,
        WarmupDecisionEngine schedulingEngine,
        Dictionary<Guid, int> activeWarmups,
        HashSet<Guid> leaseTargetServerIds,
        Dictionary<int, Guid> plannedCursorTargets,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = targetServers
            .Where(server => !server.RequiresReservation &&
                continuationStartedAt.TryGetValue(server.Id, out var startedAt) &&
                startedAt.AddSeconds(server.AttemptWindowSeconds) > now &&
                activeWarmups.GetValueOrDefault(server.Id) < server.MaxConcurrentWarmups &&
                !skippedContinuationTargets.Contains(server.Id))
            .OrderBy(server => continuationStartedAt[server.Id])
            .ThenBy(server => server.Id)
            .FirstOrDefault();
        if (candidate is null)
            return null;

        var planned = await PlanStartAsync(
            agent,
            candidate,
            continuationStartedAt[candidate.Id],
            schedulingEngine,
            activeWarmups,
            leaseTargetServerIds,
            plannedCursorTargets,
            cancellationToken);
        if (planned is null)
            skippedContinuationTargets.Add(candidate.Id);
        return planned;
    }

    private async Task<PlannedStart?> PlanNextStartAsync(
        WarmupAgent agent,
        IReadOnlyList<TargetServer> targetServers,
        WarmupDecisionEngine schedulingEngine,
        Dictionary<Guid, int> activeWarmups,
        HashSet<Guid> leaseTargetServerIds,
        Dictionary<int, Guid> plannedCursorTargets,
        CancellationToken cancellationToken)
    {
        var candidates = targetServers.ToList();
        while (true)
        {
            var priority = candidates
                .Where(candidate => activeWarmups.GetValueOrDefault(candidate.Id) < schedulingEngine.GetEffectiveConcurrency(candidate))
                .Select(candidate => (int?)candidate.Priority)
                .Max();
            if (priority is null)
                return null;
            var target = schedulingEngine.SelectNextTarget(
                candidates,
                activeWarmups,
                plannedCursorTargets.GetValueOrDefault(priority.Value));
            if (target is null)
                return null;
            candidates.RemoveAll(candidate => candidate.Id == target.Id);
            var planned = await PlanStartAsync(
                agent,
                target,
                null,
                schedulingEngine,
                activeWarmups,
                leaseTargetServerIds,
                plannedCursorTargets,
                cancellationToken);
            if (planned is not null)
                return planned;
        }
    }

    private async Task<PlannedStart?> PlanStartAsync(
        WarmupAgent agent,
        TargetServer server,
        DateTimeOffset? recreateStartedAt,
        WarmupDecisionEngine schedulingEngine,
        Dictionary<Guid, int> activeWarmups,
        HashSet<Guid> leaseTargetServerIds,
        Dictionary<int, Guid> plannedCursorTargets,
        CancellationToken cancellationToken)
    {
        if (activeWarmups.GetValueOrDefault(server.Id) >= schedulingEngine.GetEffectiveConcurrency(server) ||
            (server.RequiresReservation && leaseTargetServerIds.Contains(server.Id)))
        {
            return null;
        }
        IPEndPoint? endpoint;
        A2sServerInfo info;
        try
        {
            endpoint = await A2sEndpointResolver.ResolveIpv4Async(server.Host, server.Port, cancellationToken);
            if (endpoint is null)
                return null;
            info = await a2s!.GetInfoAsync(endpoint, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        if (info.PlayerCount >= server.PlayerTarget || (server.RequiresReservation && info.PlayerCount > 0))
            return null;

        var now = DateTimeOffset.UtcNow;
        var attempt = new WarmupAttempt
        {
            Id = Guid.NewGuid(),
            TargetServerId = server.Id,
            WarmupAgentId = agent.Id,
            OperationId = Guid.NewGuid(),
            Mode = server.RequiresReservation ? "reserved" : "standard",
            State = "uncertain",
            Phase = WarmupPhase.AwaitingFirstMember.ToString(),
            LobbyReadyAt = now,
            ExternalMemberIdsJson = "[]",
            StartedAt = recreateStartedAt ?? now,
            ObservedAt = now,
        };
        dbContext.WarmupAttempts.Add(attempt);
        if (server.RequiresReservation)
        {
            dbContext.ReservationLeases.Add(new ReservationLease
            {
                TargetServerId = server.Id,
                OperationId = attempt.OperationId,
                ExpiresAt = now.AddSeconds(server.AttemptWindowSeconds),
            });
            leaseTargetServerIds.Add(server.Id);
        }
        activeWarmups[server.Id] = activeWarmups.GetValueOrDefault(server.Id) + 1;
        plannedCursorTargets[server.Priority] = server.Id;
        return new PlannedStart(agent, server, endpoint.Address, attempt);
    }

    private async Task<StartResult> StartPlannedAsync(PlannedStart plan, CancellationToken cancellationToken)
    {
        try
        {
            var rconPassword = !plan.Server.RequiresReservation || plan.Server.RconPasswordCiphertext is null
                ? null
                : (rconCredentials ?? throw new InvalidOperationException("rcon_credential_protector_not_configured"))
                    .Unprotect(plan.Server.RconPasswordCiphertext);
            var start = await agents.StartOperationAsync(plan.Agent, new AgentOperationRequest(
                plan.Attempt.OperationId,
                plan.Server.RequiresReservation ? AgentLobbyMode.Reserved : AgentLobbyMode.Standard,
                plan.Address.ToString(),
                checked((ushort)plan.Server.Port),
                rconPassword), cancellationToken);
            return new StartResult(plan, start, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new StartResult(plan, null, exception);
        }
    }

    private sealed record RecreateRequest(Guid TargetServerId, DateTimeOffset StartedAt, WarmupAttempt? PendingAttempt = null);
    private sealed record PlannedStart(WarmupAgent Agent, TargetServer Server, IPAddress Address, WarmupAttempt Attempt);
    private sealed record StartResult(PlannedStart Plan, AgentOperationStartResult? Start, Exception? Exception);

    private async Task RestartSteamAfterAttemptAsync(WarmupAgent agent, Guid operationId, CancellationToken cancellationToken)
    {
        try
        {
            await agents.RestartSteamAsync(agent, cancellationToken);
            agent.Status = "restarting";
            agent.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            QuarantineAgent(agent, operationId, "agent_steam_restart_failed", DateTimeOffset.UtcNow);
        }
    }

    private async Task RestoreRestartedAgentsAsync(CancellationToken cancellationToken)
    {
        var restartingAgents = await dbContext.WarmupAgents
            .Where(agent => agent.Status == "restarting")
            .ToListAsync(cancellationToken);
        foreach (var agent in restartingAgents)
        {
            try
            {
                if (!(await agents.GetHealthAsync(agent, cancellationToken)).Ready)
                    continue;
                agent.Status = "running";
                agent.UpdatedAt = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Keep the Agent out of scheduling until a later health probe confirms Steam recovered.
            }
        }
        if (restartingAgents.Count > 0)
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
