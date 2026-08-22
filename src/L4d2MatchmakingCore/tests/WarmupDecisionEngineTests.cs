using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class WarmupDecisionEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddMinutes(10);
    private readonly WarmupDecisionEngine _engine = new();

    [TestMethod]
    public void ReservedTargetWithPlayersSkipsBeforeLease() =>
        Assert.AreEqual(WarmupDecision.SkipTarget, _engine.Evaluate(Reserved(), 1, Selecting(), Now));

    [TestMethod]
    public void ReservedNoMemberFor120SecondsRecreatesSameTarget() =>
        Assert.AreEqual(WarmupDecision.RecreateSameTarget, _engine.Evaluate(Reserved(), 0, AwaitingFirstMember(Now.AddSeconds(-120)), Now));

    [TestMethod]
    public void StandardNoMemberFor120SecondsRecreatesSameTarget() =>
        Assert.AreEqual(WarmupDecision.RecreateSameTarget, _engine.Evaluate(Normal(), 0, AwaitingFirstMember(Now.AddSeconds(-120)), Now));

    [TestMethod]
    public void NormalQuietFor30SecondsRecreatesSameTarget() =>
        Assert.AreEqual(WarmupDecision.RecreateSameTarget, _engine.Evaluate(Normal(), 0, Active(Now.AddSeconds(-30)), Now));

    [TestMethod]
    public void PlayerTargetOrDeadlineReleasesAttempt()
    {
        Assert.AreEqual(WarmupDecision.ReleaseAndReschedule, _engine.Evaluate(Normal(), 6, Active(Now), Now));
        Assert.AreEqual(WarmupDecision.ReleaseAndReschedule, _engine.Evaluate(Normal(), 0, Active(Now, Now), Now));
    }

    [TestMethod]
    public void NewExternalMemberStartsAndResetsQuietTimer()
    {
        var initial = AwaitingFirstMember(Now.AddSeconds(-10));
        var afterFirstJoin = _engine.ObserveExternalMembers(initial, new HashSet<string> { "player-1" }, Now);
        var afterSameMembers = _engine.ObserveExternalMembers(afterFirstJoin, new HashSet<string> { "player-1" }, Now.AddSeconds(10));
        var afterNextJoin = _engine.ObserveExternalMembers(afterSameMembers, new HashSet<string> { "player-1", "player-2" }, Now.AddSeconds(20));

        Assert.AreEqual(WarmupPhase.Active, afterFirstJoin.Phase);
        Assert.AreEqual(Now, afterFirstJoin.FirstExternalMemberAt);
        Assert.AreEqual(Now, afterSameMembers.QuietSince);
        Assert.AreEqual(Now.AddSeconds(20), afterNextJoin.QuietSince);
    }

    [TestMethod]
    public void ReservationAlwaysHasOneEffectiveWarmupSlot()
    {
        Assert.AreEqual(1, _engine.GetEffectiveConcurrency(new TargetServer { RequiresReservation = true, MaxConcurrentWarmups = 36 }));
        Assert.AreEqual(36, _engine.GetEffectiveConcurrency(new TargetServer { RequiresReservation = false, MaxConcurrentWarmups = 36 }));
    }

    [TestMethod]
    public void SelectNextTargetHonorsPriorityAndEffectiveConcurrency()
    {
        var reserved = new TargetServer { Id = Guid.NewGuid(), Enabled = true, RequiresReservation = true, MaxConcurrentWarmups = 36, Priority = 10 };
        var normal = new TargetServer { Id = Guid.NewGuid(), Enabled = true, RequiresReservation = false, MaxConcurrentWarmups = 2, Priority = 5 };

        var selected = _engine.SelectNextTarget([reserved, normal], new Dictionary<Guid, int> { [reserved.Id] = 1, [normal.Id] = 1 });

        Assert.AreEqual(normal.Id, selected?.Id);
    }

    private static TargetServer Reserved() => new() { RequiresReservation = true, PlayerTarget = 6 };
    private static TargetServer Normal() => new() { RequiresReservation = false, PlayerTarget = 6 };
    private static WarmupAttemptSnapshot Selecting() => new(WarmupPhase.Selecting, Now.AddMinutes(2), Now, null, null);
    private static WarmupAttemptSnapshot AwaitingFirstMember(DateTimeOffset readyAt) => new(WarmupPhase.AwaitingFirstMember, Now.AddMinutes(2), readyAt, null, null);
    private static WarmupAttemptSnapshot Active(DateTimeOffset quietSince, DateTimeOffset? deadline = null) => new(WarmupPhase.Active, deadline ?? Now.AddMinutes(2), Now, Now.AddMinutes(-1), quietSince);
}
