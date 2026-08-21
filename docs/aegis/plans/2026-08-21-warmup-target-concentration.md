# Warm-up Target Concentration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fill existing non-reservation warm-up targets before opening additional targets, while falling back to the next eligible target when the preferred target can accept only part of a batch.

**Architecture:** Keep scheduling ownership in `WarmupSchedulerService`. Extend its per-tick candidate planning so planned starts become continuation candidates immediately, and order eligible existing standard targets by active warm-up count, priority, and stable ID. Leave fresh-target priority/round-robin selection and all reservation admission logic intact.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core InMemory, MSTest.

**Baseline / Authority Refs:** `CONTEXT.md`; `docs/aegis/specs/2026-08-21-warmup-target-concentration-design.md`; `docs/steam-lobby-automation.md`; `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`; `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`.

**Compatibility Boundary:** Preserve `MaxConcurrentWarmups`, shared attempt-window deadlines, A2S/DNS admission checks, reservation leases/effective concurrency, recreate-same-target precedence, batch start limits, and fresh-target priority/round-robin behavior when no existing standard target can accept work.

**Verification:** Run focused scheduler tests through the red-green cycle, then `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore` and `dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore` from the isolated worktree.

---

### Task 1: Add failing concentration and fallback tests

**Files:**
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** The current same-priority cursor rotates the second planned Agent to another target even when the first target has capacity. The tests must express both full concentration and partial-fill fallback before changing production code.

**Impact / Compatibility:** Tests use the existing InMemory DbContext, fake Agent selector/control client, and fake A2S client. Existing reservation, continuation-window, and round-robin tests remain unchanged.

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter "FullyQualifiedName~WarmupSchedulerServiceTests.TickConcentrates|FullyQualifiedName~WarmupSchedulerServiceTests.TickFallsBack"` must fail because the current scheduler splits or does not try the next existing target.

**Repair Track:** Reproduce the selection bug at the scheduler boundary with real planning state (`activeWarmups`, `continuationStartedAt`, and planned cursors).

**Retirement Track:** The old implicit behavior asserted only by `TickPersistsRoundRobinCursorBetweenEqualPriorityTargets` remains valid for fresh-target selection; the new tests retire the assumption that round-robin applies while an eligible standard target can still absorb planned work.

- [x] **Step 1: Write the failing test for a fresh equal-priority batch.** Add `TickConcentratesFreshBatchOnOneEqualPriorityStandardTarget` with two idle Agents, two enabled equal-priority standard targets with capacity two, and assert both requests target the same server.
- [x] **Step 2: Write the failing test for partial fill and fallback.** Add `TickFallsBackToNextExistingTargetWhenPreferredTargetCannotPassA2s` with two busy Agents already warming target A/B, one idle Agent, and a fresh target; assert an A2S failure on A falls through to existing target B instead of the fresh target.
- [x] **Step 3: Run the focused tests and record the expected red failure.** Use the exact command in the verification section; confirm the failures are assertion mismatches showing split/fresh selection, not compilation or fixture errors.

### Task 2: Implement per-tick concentration and candidate fallback

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs:257-312,348-384`
- Modify: `docs/steam-lobby-automation.md` in the target-selection section

**Why this task exists:** Make planned starts visible to the same tick's continuation selector and try all eligible existing standard targets before falling back to a fresh target.

**Impact / Compatibility:** The scheduler remains the canonical owner. Reservation targets are filtered out of concentration candidates. A failed `PlanStartAsync` candidate is skipped for the current planning pass, while Agent start failures still create the existing uncertain/quarantined state and are not reused in the same tick.

**Verification:** Re-run both new focused tests and the existing scheduler suite. The new tests must pass, `TickStartsABatchWithoutExceedingTargetConcurrency` must still pass, `TickPersistsRoundRobinCursorBetweenEqualPriorityTargets` must still pass, and all reservation/recreate tests must remain green.

**Repair Track:**
- Change `PlanContinuationStartAsync` to enumerate eligible standard continuation candidates ordered by `activeWarmups` descending, `Priority` descending, and stable ID, retrying the next candidate when `PlanStartAsync` returns null.
- After any successful recreate or fresh planned start, add the target's earliest `StartedAt` to the mutable continuation map so later idle Agents in the same tick continue that target while capacity/window remain valid.
- Keep `PlanNextStartAsync` as the fresh-target owner, so priority and persistent round-robin remain the fallback when no standard continuation candidate is available.

**Retirement Track:** Retire the single-candidate continuation branch that immediately returned to fresh priority selection after one A2S/capacity planning failure. Keep `skippedContinuationTargets` only as a per-tick guard against retrying the same failed target.

- [x] **Step 1: Update continuation planning to enumerate candidates.** Build the filtered candidate list once per Agent, sort by current active count then priority then ID, call `PlanStartAsync` for each, and add failed candidates to `skippedContinuationTargets` before trying the next.
- [x] **Step 2: Record planned targets as continuation candidates.** After successful recreate and ordinary planning, update `continuationStartedAt[target.Id]` with the planned attempt start time; preserve the earliest time if the target already has one.
- [x] **Step 3: Update the scheduling documentation.** State that existing standard targets are filled by active count before fresh priority/round-robin selection, and that capacity/A2S failures fall through to the next eligible target.
- [x] **Step 4: Run the focused scheduler tests and confirm green.** Use the new-test filter plus the existing batch, continuation, round-robin, reservation, and A2S-unavailable tests.

### Task 3: Full regression and integration verification

**Files:**
- No additional source changes expected.

**Why this task exists:** The change affects shared scheduling behavior and must prove that no reservation, lifecycle, or persistence contract regressed.

**Impact / Compatibility:** Verification covers the entire Core test project and solution build. No deployment or database migration is required because the change is in-memory ordering only.

**Verification:**
- `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore` exits 0 with zero failures.
- `dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore` exits 0 with zero warnings/errors.
- `git diff --check` exits 0.

- [x] **Step 1: Run the full Core test project and inspect the count.** Confirm all existing and new tests pass, with only the known integration tests skipped.
- [x] **Step 2: Build the full solution with warnings as errors.** Confirm the scheduler and tests compile for all solution projects.
- [ ] **Step 3: Review the diff and commit the implementation slice.** Stage only the scheduler, tests, and scheduling documentation; commit with `fix: 集中非预留暖服目标`.
