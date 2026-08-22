using L4d2MatchmakingCore.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class WarmupPauseWindowRulesTests
{
    [TestMethod]
    public void NormalizeSortsWindowsByStartThenEnd()
    {
        var normalized = WarmupPauseWindowRules.Normalize([
            new WarmupPauseWindow("23:00", "00:00"),
            new WarmupPauseWindow("08:00", "12:00"),
            new WarmupPauseWindow("08:00", "10:00"),
        ]);

        CollectionAssert.AreEqual(
            new[]
            {
                new WarmupPauseWindow("08:00", "10:00"),
                new WarmupPauseWindow("08:00", "12:00"),
                new WarmupPauseWindow("23:00", "00:00"),
            },
            normalized.ToArray());
    }

    [TestMethod]
    public void IsActiveUsesShanghaiTimeAndTreatsEndAsExclusive()
    {
        var windows = WarmupPauseWindowRules.Normalize([
            new WarmupPauseWindow("00:00", "08:00"),
        ]);

        Assert.IsTrue(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 22, 23, 59, 0, TimeSpan.Zero), windows));
        Assert.IsFalse(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero), windows));
        Assert.IsTrue(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.Zero), windows));
        Assert.IsFalse(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.Zero).AddMinutes(8 * 60), windows));
    }

    [TestMethod]
    public void IsActiveSupportsCrossMidnightWindows()
    {
        var windows = WarmupPauseWindowRules.Normalize([
            new WarmupPauseWindow("23:00", "00:00"),
        ]);

        Assert.IsTrue(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 15, 0, 0, TimeSpan.Zero), windows));
        Assert.IsTrue(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 15, 59, 0, TimeSpan.Zero), windows));
        Assert.IsFalse(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.Zero), windows));
    }

    [TestMethod]
    public void IsActiveReturnsTrueWhenAnyWindowMatches()
    {
        var windows = WarmupPauseWindowRules.Normalize([
            new WarmupPauseWindow("00:00", "08:00"),
            new WarmupPauseWindow("18:00", "20:00"),
        ]);

        Assert.IsTrue(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero), windows));
        Assert.IsFalse(WarmupPauseWindowRules.IsActive(
            new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero), windows));
    }

    [TestMethod]
    public void IsActiveReturnsFalseForNoWindows()
    {
        Assert.IsFalse(WarmupPauseWindowRules.IsActive(
            DateTimeOffset.UtcNow, Array.Empty<WarmupPauseWindow>()));
    }

    [TestMethod]
    public void NormalizeRejectsInvalidTimeFormat()
    {
        var exception = Assert.ThrowsException<ArgumentException>(() =>
            WarmupPauseWindowRules.Normalize([
                new WarmupPauseWindow("8:00", "08:00"),
            ]));

        Assert.AreEqual("invalid_warmup_pause_window", exception.Message);
    }

    [TestMethod]
    public void NormalizeRejectsEqualStartAndEnd()
    {
        var exception = Assert.ThrowsException<ArgumentException>(() =>
            WarmupPauseWindowRules.Normalize([
                new WarmupPauseWindow("08:00", "08:00"),
            ]));

        Assert.AreEqual("invalid_warmup_pause_window", exception.Message);
    }
}
