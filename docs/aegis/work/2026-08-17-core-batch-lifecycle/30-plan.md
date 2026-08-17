# Core Batch Scheduling and Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `aegis:executing-plans`
> to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make current Core warm-up state observable, safely drain targets and
schedule a bounded batch of healthy Agents every tick.

**Architecture:** TargetServerService remains the configuration owner but
delegates stop confirmation to one attempt-drain service. The scheduler plans
attempts/leases in one DbContext transaction then starts Agents concurrently.
The status route reads persisted active/uncertain observations without calling
Agents.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core, MSTest.

**Baseline:** `20-spec.md`, Target Server endpoints/service, scheduler,
agent-control contract and existing Core tests.

**Compatibility:** Existing routes retain shapes and authentication. The
single-Agent selector method remains for lobby queries. Reservation rejection
and no-RCON operation behavior remain unchanged.

### Task 1: Reservation-only credentials

**Files:** `TargetServerService.cs`, `WarmupSchedulerService.cs`, Target
Server/scheduler tests.

- [ ] Add endpoint tests that reject RCON input for a normal Target Server and
  assert converting a reserved target to normal removes `hasRconCredentials`.
- [ ] Run the focused test and observe the expected red failure.
- [ ] Reject non-null normal-target passwords in `Normalize`; clear the stored
  ciphertext when reservation is disabled; build Agent requests with a
  password only when `RequiresReservation` is true.
- [ ] Re-run focused tests and commit the green slice.

### Task 2: Current status route

**Files:** new `Scheduling/WarmupStatusService.cs`,
`Scheduling/WarmupStatusEndpoints.cs`, `Program.cs`, endpoint tests,
`docs/matchmaking-core-api.md`.

- [ ] Add a failing authenticated endpoint test that seeds active, uncertain
  and completed attempts and asserts only the first two are returned without
  sensitive Target Server fields.
- [ ] Implement a projection DTO with target/agent IDs and display fields,
  operation/lobby/mode/phase and timing fields. Join data in Core, do not call
  Agent HTTP clients.
- [ ] Map authenticated `GET /v1/warmups`, run focused Core tests and commit.

### Task 3: Bounded batch scheduler

**Files:** `CoreOptions.cs`, agent selector/client contract,
`WarmupSchedulerService.cs`, scheduler tests, Compose/env/docs.

- [ ] Add red tests for selecting two healthy idle Agents in one tick while
  never exceeding target concurrency or reservation lease exclusivity.
- [ ] Add `ListHealthyAsync` while retaining `SelectAsync`; make health
  probing bounded and return only ready running Agents.
- [ ] Add `SchedulerMaxStartsPerTick` (default 16), plan one work item per
  selected Agent, persist all attempts/leases, invoke starts with bounded
  parallelism, then apply results sequentially.
- [ ] Preserve recreate-same-target demand ahead of ordinary priority
  scheduling and update rotation cursors only for successfully started work.
- [ ] Run scheduler and related regression tests; commit the slice.

### Task 4: Target drain lifecycle

**Files:** new `Scheduling/WarmupAttemptDrainService.cs`, Target Server
service/endpoints, tests.

- [ ] Add failing tests for disable/delete stopping operations, marking
  confirmed attempts completed and releasing matching leases, and a failed
  stop returning `409` with the target left disabled.
- [ ] Implement one drain service that stops active/uncertain attempts, marks
  failed Agents quarantined with a credential-free audit event, and never
  removes an unconfirmed lease.
- [ ] On disable persist `Enabled=false` before invoking the drain. On delete
  use the same sequence and only remove the Target Server after drain success.
- [ ] Run endpoint/lifecycle/scheduler regressions and commit.

### Task 5: Integration verification

**Files:** deployment environment example, Compose contract, API docs and
`50-evidence.md`.

- [ ] Add `CORE_SCHEDULER_MAX_STARTS_PER_TICK` to deployment config and its
  contract test; document status API and lifecycle `409` behavior.
- [ ] Run `dotnet test L4d2MatchmakingManager.sln --no-restore` and
  `dotnet build L4d2MatchmakingManager.sln --warnaserror --no-restore`.
- [ ] Rebuild/deploy only after tests are green; record health, status API and
  cleanup evidence without persisting credentials.

**Repair Track:** Replace one-at-a-time scheduling and unsafe raw server
deletion with batch planning and drain confirmation.

**Retirement Track:** The prior single-start tail of `TickAsync` and direct
TargetServer deletion are removed. `SelectAsync` remains for existing
single-agent consumers; it is not a scheduler fallback.
