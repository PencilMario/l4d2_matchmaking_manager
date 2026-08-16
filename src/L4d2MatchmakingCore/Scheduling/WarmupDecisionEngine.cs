using L4d2MatchmakingCore.Data;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupDecisionEngine
{
    public int GetEffectiveConcurrency(TargetServer server) =>
        server.RequiresReservation ? 1 : server.MaxConcurrentWarmups;

    public TargetServer? SelectNextTarget(
        IEnumerable<TargetServer> servers,
        IReadOnlyDictionary<Guid, int> activeWarmups) =>
        servers
            .Where(server => server.Enabled && activeWarmups.GetValueOrDefault(server.Id) < GetEffectiveConcurrency(server))
            .OrderByDescending(server => server.Priority)
            .ThenBy(server => server.Id)
            .FirstOrDefault();

    public WarmupAttemptSnapshot ObserveExternalMembers(
        WarmupAttemptSnapshot attempt,
        IReadOnlySet<string> externalMemberIds,
        DateTimeOffset now)
    {
        var previous = attempt.ExternalMemberIds ?? new HashSet<string>(StringComparer.Ordinal);
        var hasNewMember = externalMemberIds.Any(member => !previous.Contains(member));
        var hasFirstMember = externalMemberIds.Count > 0 && attempt.FirstExternalMemberAt is null;
        return attempt with
        {
            Phase = externalMemberIds.Count > 0 ? WarmupPhase.Active : attempt.Phase,
            FirstExternalMemberAt = hasFirstMember ? now : attempt.FirstExternalMemberAt,
            QuietSince = hasNewMember ? now : attempt.QuietSince,
            ExternalMemberIds = new HashSet<string>(externalMemberIds, StringComparer.Ordinal),
        };
    }

    public WarmupDecision Evaluate(TargetServer server, int a2sPlayers, WarmupAttemptSnapshot attempt, DateTimeOffset now)
    {
        if (server.RequiresReservation && attempt.Phase == WarmupPhase.Selecting && a2sPlayers > 0)
            return WarmupDecision.SkipTarget;
        if (a2sPlayers >= server.PlayerTarget || now >= attempt.Deadline)
            return WarmupDecision.ReleaseAndReschedule;
        if (server.RequiresReservation && attempt.Phase == WarmupPhase.AwaitingFirstMember &&
            attempt.FirstExternalMemberAt is null && now >= attempt.LobbyReadyAt.AddSeconds(120))
        {
            return WarmupDecision.RecreateSameTarget;
        }
        if (attempt.Phase == WarmupPhase.Active && attempt.QuietSince is { } quietSince && now >= quietSince.AddSeconds(30))
            return WarmupDecision.RecreateSameTarget;
        return WarmupDecision.Continue;
    }
}
