namespace L4d2MatchmakingCore.Scheduling;

public enum WarmupPhase { Selecting, AwaitingFirstMember, Active }
public enum WarmupDecision { Continue, RecreateSameTarget, ReleaseAndReschedule, SkipTarget, QuarantineAgent }

public sealed record WarmupAttemptSnapshot(
    WarmupPhase Phase,
    DateTimeOffset Deadline,
    DateTimeOffset LobbyReadyAt,
    DateTimeOffset? FirstExternalMemberAt,
    DateTimeOffset? QuietSince);
