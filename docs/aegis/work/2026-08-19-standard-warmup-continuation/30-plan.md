# Non-Reservation Warm-up Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prefer idle Agents for an already-running non-reservation Target Server until that server's shared warm-up window succeeds or expires.

**Architecture:** Keep `WarmupSchedulerService` as the orchestration owner and derive a server-level start time from the earliest active/uncertain attempt for that Target Server. Add a continuation selection layer before ordinary priority selection, while preserving same-target recreation precedence and real-time A2S admission. New attempts for continuation inherit the server-level `StartedAt` so adding Agents cannot extend the 12-minute window.

**Tech Stack:** .NET 8, EF Core, MSTest, React/TypeScript frontend with Vitest.

**Baseline / Authority Refs:** `docs/aegis/specs/2026-08-19-standard-warmup-continuation-design.md`, `CONTEXT.md`, `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`, `src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs`, `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`, and the committed frontend changes in `80abd61`.

**Compatibility Boundary:** Preserve reservation admission/lease behavior, same-target recreation precedence, A2S as the runtime capacity authority, `Priority` plus persistent same-priority round-robin when no continuation candidate exists, batch start limits, and existing API field shapes. Frontend must display the shared server window without treating an Agent's individual start time as authoritative.

**Verification:** Run targeted Core MSTest cases and the full Core test project after restore; run frontend Vitest and TypeScript build after installing frontend dependencies; inspect the final diff for unrelated frontend changes.

---

### Task 1: Add failing scheduler tests for server-level continuation

**Files:**
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** The requested behavior is cross-priority scheduling and shared timing; tests must prove it before changing production code.

**Impact / Compatibility:** Tests use the existing fake Agent selector and A2S client. They must prove low-priority continuation beats a high-priority fresh target, new attempts inherit the earliest server `StartedAt`, expired continuation loses preference, and reservation targets do not enter this path.

- [ ] **Step 1: Write the failing tests**

Add tests near the existing batch and recreation tests:

```csharp
[TestMethod]
public async Task TickPrefersUnexpiredStandardContinuationOverHigherPriorityFreshTarget()
{
    // Arrange one idle agent, a low-priority standard server with one active attempt
    // started five minutes ago, and a high-priority empty server.
    // Assert the new operation targets the low-priority server.
}

[TestMethod]
public async Task TickContinuationInheritsEarliestServerStartedAt()
{
    // Arrange a standard server with an existing attempt started five minutes ago.
    // Assert the additional attempt has exactly that StartedAt, not DateTimeOffset.UtcNow.
}

[TestMethod]
public async Task TickDoesNotPreferExpiredStandardContinuation()
{
    // Arrange a low-priority standard attempt whose StartedAt + AttemptWindowSeconds is past.
    // Arrange a high-priority empty server and assert the high-priority server is selected.
}

[TestMethod]
public async Task TickDoesNotApplyContinuationPreferenceToReservationServer()
{
    // Arrange an unexpired reservation attempt and a higher-priority fresh standard server.
    // Assert the normal priority candidate is selected and no second reservation attempt is created.
}
```

- [ ] **Step 2: Run the tests and verify the expected failure**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~WarmupSchedulerServiceTests"
```

Expected: the new tests fail because ordinary selection still uses only `Priority`, and continuation attempts currently receive a fresh `StartedAt` unless an explicit recreation timestamp is supplied.

### Task 2: Implement server-level continuation selection and timing

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupDecisionEngine.cs` only if a focused helper is needed

**Why this task exists:** This is the canonical owner of Agent allocation and attempt lifecycle timing.

**Impact / Compatibility:** The existing `recreateRequests` loop remains first. Continuation selection is inserted before `PlanNextStartAsync` ordinary selection. A candidate is standard-only, has an active/uncertain attempt, has remaining shared time based on the earliest attempt `StartedAt`, and has capacity. Each PlanStart call still performs real-time DNS/A2S checks.

- [ ] **Step 1: Build a server-level continuation candidate map from active attempts**

Use the current active/uncertain attempt list and target configuration. For each non-reservation target, compute the minimum `StartedAt` and retain it only when:

```csharp
server.StartedAt.AddSeconds(server.AttemptWindowSeconds) > now
&& activeWarmups.GetValueOrDefault(server.Id) < server.MaxConcurrentWarmups
```

- [ ] **Step 2: Add a continuation-aware planning method**

Before ordinary `PlanNextStartAsync`, select continuation targets using stable per-priority cursor ordering or a deterministic server-id ordering among continuation candidates, skip candidates that fail `PlanStartAsync`, and continue to the next candidate. Pass the candidate's earliest server `StartedAt` as `recreateStartedAt` so `WarmupAttempt.StartedAt` is shared.

- [ ] **Step 3: Preserve ordinary scheduling behavior**

Leave `PlanNextStartAsync` and `SelectNextTarget` as the fallback path. Continue to update the existing rotation cursor only after a successful Agent start. Do not add a migration or new persistence entity.

- [ ] **Step 4: Run the new tests**

Run the focused MSTest filter and expect all new continuation tests plus existing scheduler tests to pass.

### Task 3: Align frontend semantics with server-level timing

**Files:**
- Modify: `frontend/src/components/OverviewView.tsx`
- Modify: `frontend/src/services/api.ts` if the adapter hard-codes or discards the backend timing fields
- Modify: `frontend/src/types/index.ts` only if the adapter needs an explicit server start/deadline field
- Modify: `frontend/src/components/OverviewView.test.tsx`

**Why this task exists:** The latest frontend groups tasks by Target Server and already labels the remaining duration as shared, but its detail header still says `节点开始时间`, which contradicts server-level `StartedAt`.

**Impact / Compatibility:** Keep the existing backend response shape. Preserve per-task `startedAt` data for diagnostics, but label the group-level timing as server-level and derive the displayed server remaining time from the shared window represented by the backend values.

- [ ] **Step 1: Add a failing frontend assertion**

Change the overview test to assert `服务器暖服开始时间` and assert that `节点开始时间` is absent from the expanded server detail.

- [ ] **Step 2: Run the focused frontend test and verify it fails**

Run:

```powershell
npm test -- --run src/components/OverviewView.test.tsx
```

Expected: failure because the current detail table header is `节点开始时间`.

- [ ] **Step 3: Update the label and preserve shared timing**

Change the detail header to `服务器暖服开始时间`; do not replace the grouped server remaining calculation with an individual Agent start calculation. If API adaptation is required, map the backend `startedAt`/`deadline` consistently for all attempts belonging to the same Target Server.

- [ ] **Step 4: Run frontend unit tests and build**

Run:

```powershell
npm test -- --run
npm run build
```

Expected: all frontend tests pass and the production build completes.

### Task 4: Full verification and change review

**Files:**
- Inspect: all changed files

**Why this task exists:** Scheduling changes affect cross-module behavior and the user specifically requested a frontend re-check.

**Impact / Compatibility:** Confirm no frontend files changed outside the semantic label/timing adapter, no reservation behavior changed, and no API contract/migration churn was introduced.

- [ ] **Step 1: Restore dependencies and run full tests**

Run:

```powershell
dotnet restore L4d2MatchmakingManager.sln
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj
Push-Location frontend
npm install
npm test -- --run
npm run build
Pop-Location
```

- [ ] **Step 2: Review the final diff**

Run:

```powershell
git diff --check
git diff --stat
git status --short
```

Expected: only the scheduler tests/implementation, the focused frontend timing label or adapter adjustment, and task evidence are changed.

- [ ] **Step 3: Record evidence**

Update `docs/aegis/work/2026-08-19-standard-warmup-continuation/50-evidence.md` with test commands, pass/fail results, and any dependency or environment limitation.
