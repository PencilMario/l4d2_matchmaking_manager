# Non-Reserved Lobby Recreate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recreate a standard warm-up lobby after 120 seconds without an external member, while preserving the existing attempt deadline and post-admission quiet-lobby behavior.

**Architecture:** Extend the existing `WarmupDecisionEngine` admission timeout branch to cover both reserved and standard targets. The scheduler already handles `RecreateSameTarget`, carries the original `StartedAt`, and ticks every five seconds, so no persistence or scheduling-flow changes are needed.

**Tech Stack:** .NET 10, C#, MSTest, Markdown.

**Baseline / Authority Refs:** `CONTEXT.md`; `docs/steam-lobby-automation.md`; `src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs`; `src/L4d2MatchmakingCore/tests/WarmupDecisionEngineTests.cs`; `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerBackgroundService.cs`.

**Compatibility Boundary:** Preserve reserved-target admission behavior, the 30-second quiet timer after external members join, A2S player-target release, the configured/original `AttemptWindowSeconds` deadline, and the existing `RecreateSameTarget` scheduler path. Do not add configuration, migrations, or API changes.

**Verification:** Run the new focused MSTest in a red-green cycle, then run the complete `L4d2MatchmakingCore.Tests` project and inspect the final diff/status.

---

### Task 1: Apply the common admission timeout

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs:53-66`
- Test: `src/L4d2MatchmakingCore/tests/WarmupDecisionEngineTests.cs:18-23`
- Modify: `docs/steam-lobby-automation.md:40-48`

**Why this task exists:** Standard lobbies currently wait until the total attempt deadline when no external member arrives. Applying the existing 120-second admission timeout keeps lobby discovery fresh while retaining the same target and total warm-up budget.

**Impact / Compatibility:** The timeout applies only while `Phase == AwaitingFirstMember` and `FirstExternalMemberAt` is unset. Reserved and standard targets use the same timeout. Once a member joins, the existing 30-second quiet rule remains the owner of recreation decisions.

**Repair Track:** The canonical decision owner is `WarmupDecisionEngine.Evaluate`; remove the reservation-only guard from the existing admission timeout branch and add a standard-target regression test.

**Retirement Track:** The old standard-lobby behavior of waiting silently until the attempt deadline retires. The shared 120-second admission rule remains active for both lobby modes; no fallback branch is added.

**Verification:**

- [ ] **Step 1: Add the failing standard-lobby test**

Add this test beside `ReservedNoMemberFor120SecondsRecreatesSameTarget`:

```csharp
[TestMethod]
public void StandardNoMemberFor120SecondsRecreatesSameTarget() =>
    Assert.AreEqual(WarmupDecision.RecreateSameTarget, _engine.Evaluate(Normal(), 0, AwaitingFirstMember(Now.AddSeconds(-120)), Now));
```

- [ ] **Step 2: Run the focused test and verify the expected failure**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~StandardNoMemberFor120SecondsRecreatesSameTarget" --no-restore
```

Expected: one failed assertion because the current reservation-only branch returns `Continue` for `Normal()`.

- [ ] **Step 3: Implement the minimal common rule**

Change the admission timeout condition to:

```csharp
if (attempt.Phase == WarmupPhase.AwaitingFirstMember &&
    attempt.FirstExternalMemberAt is null && now >= attempt.LobbyReadyAt.AddSeconds(120))
{
    return WarmupDecision.RecreateSameTarget;
}
```

- [ ] **Step 4: Update the standard-lobby lifecycle documentation**

Replace the standard-server paragraph with wording that states an empty standard lobby is recreated after 120 seconds, while the 30-second quiet rule applies after a member enters and the original attempt window remains unchanged.

- [ ] **Step 5: Run focused and related regression tests**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~WarmupDecisionEngineTests" --no-restore
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore
```

Expected: all tests pass with zero failures.

- [ ] **Step 6: Inspect the diff and status**

Run:

```powershell
git diff --check
git diff -- src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs src/L4d2MatchmakingCore/tests/WarmupDecisionEngineTests.cs docs/steam-lobby-automation.md
git status --short
```

Confirm only the planned source, test, and documentation files are changed in this worktree.
