using L4d2MatchmakingCore.Data;

namespace L4d2MatchmakingCore.Scheduling;

public sealed class WarmupDecisionEngine
{
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
