# A2S Player-Present Target Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an A2S-based selection layer that prefers eligible servers with players after existing warm-up continuation and before the current priority/round-robin fresh-target fallback.

**Architecture:** Keep `WarmupSchedulerService` as the scheduling owner. For each idle Warm-up Agent, after continuation candidates fail, query live A2S for eligible fresh candidates, discard empty/full/unavailable candidates, and rank occupied standard targets by `Priority + PlayerCount * 0.2 * Priority`; if none remain, preserve the existing priority-descending and persisted round-robin path. Reuse one live probe per target within that Agent's planning pass, without carrying player samples across Agents.

**Tech Stack:** C#, .NET, Entity Framework Core, MSTest, Source A2S client.

**Baseline / Authority Refs:** `CONTEXT.md`; `docs/steam-lobby-automation.md`; `docs/aegis/specs/2026-08-21-warmup-target-concentration-design.md`; `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`; `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`.

**Compatibility Boundary:** Existing same-target recreation, ordinary warm-up concentration, effective concurrency, reservation lease/empty-server admission, A2S/DNS failure handling, and priority round-robin fallback remain unchanged. UI observation snapshots do not become the scheduling authority; selection uses the scheduler's live A2S path.

**Verification:** Run the new focused scheduler tests through a red-green cycle, then the complete `L4d2MatchmakingCore.Tests` project and a warnings-as-errors solution build. Inspect the final diff and verify the main worktree status is unchanged.

---

### Task 1: Define the new selection behavior in tests

**Files:**
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** Protect the requested selection order and the exact weighted score at the scheduler boundary using real planning state and the existing A2S test double.

**Impact / Compatibility:** Tests must prove that an occupied eligible standard target outranks an empty target regardless of the empty target's higher priority, that multiple occupied targets use the supplied score, and that the old empty-target fallback remains available.

**Verification:** The focused new tests must fail before the scheduler implementation because the current code chooses by priority/round-robin without an occupied-target layer.

- [x] **Step 1: Add a test for occupied-target preference over a higher-priority empty target.** Use `playersByPort` with one player on the lower-priority target and zero on the higher-priority target; assert the started attempt targets the occupied server.
- [x] **Step 2: Add a test for multiple occupied targets.** Use `(Priority=3, Players=1)` and `(Priority=2, Players=5)`; assert the second target wins because scores are `3.6` and `4` respectively.
- [x] **Step 3: Run only the new tests and confirm the expected selection failure.**

### Task 2: Implement the live occupied-target selection layer

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`

**Why this task exists:** Make the scheduler honor the new rule without weakening existing admission checks or moving ownership to the UI observation collector.

**Impact / Compatibility:** Insert one call between `PlanContinuationStartAsync` and `PlanNextStartAsync`. The new layer considers only ordinary targets with capacity and `PlayerCount > 0` but below `PlayerTarget`; reserved targets remain subject to their existing empty-server rule. A2S/DNS failures are marked unavailable for the current tick and are still skipped by the existing fallback.

**Repair Track:** The canonical owner is the per-tick planning flow in `WarmupSchedulerService`; the current priority/round-robin selector stays as the final fallback. Add a focused helper for live A2S candidate ranking and reuse `PlanStartAsync` for final admission.

**Retirement Track:** No old owner or fallback is deleted. The new occupied-target branch supersedes priority ordering only when at least one eligible occupied target exists; the priority/round-robin branch remains active when all candidates are empty or occupied candidates fail admission.

**Verification:** The new tests pass, existing scheduler selection tests remain green, and the implementation compiles without warnings.

- [x] **Step 1: Add the minimal occupied-target planner.** Query live A2S for eligible candidates, rank with `double` arithmetic using the exact supplied formula, use stable ID as the deterministic tie-breaker, and try candidates in order.
- [x] **Step 2: Invoke it before `PlanNextStartAsync`.** Preserve planned counts, unavailable-target state, cursor state, and per-Agent fallback behavior.
- [x] **Step 3: Run the focused scheduler suite and confirm green.**

### Task 3: Align current scheduling documentation

**Files:**
- Modify: `docs/steam-lobby-automation.md`

**Why this task exists:** Keep the current operational description aligned with the executable selection order.

**Impact / Compatibility:** Document the new layer between continuation and fresh priority/round-robin selection, including the exact score and the ordinary/reservation boundary.

**Verification:** Read the changed section against the implementation and tests; no code behavior changes are introduced by this task.

- [x] **Step 1: Update the target-selection bullets with the new A2S occupied-target rule.**
- [x] **Step 2: Run the complete core test project and warnings-as-errors solution build.**
- [x] **Step 3: Inspect `git diff`, branch status, and the main worktree status.**
