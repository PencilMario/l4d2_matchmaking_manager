# Stop Warm-up Agent Drain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure stopping one Warm-up Agent exits its current warm-up attempts before its container is stopped.

**Architecture:** Keep `WarmupAttemptDrainService` as the canonical owner of remote operation stop, attempt completion, lease release, and failure quarantine. Make `WarmupAgentService.StopCoreAsync` call that service before VNC/container shutdown, and add a stop-specific conflict mapping at the endpoint boundary.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core, MSTest, EF Core InMemory test host.

**Baseline / Authority Refs:** `docs/aegis/specs/2026-08-24-stop-warmup-agent-drain-design.md`, `CONTEXT.md`, `src/L4d2MatchmakingCore/Scheduling/WarmupAttemptDrainService.cs`, `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs`, `docs/matchmaking-core-api.md`, and `docs/matchmaking-core-frontend-api.md`.

**Compatibility Boundary:** Keep `POST /v1/agents/{agentId}/stop`, response DTO, idempotence for non-running nodes, database schema, and existing recreate/global drain behavior. A failed stop drain must leave the container un-stopped and must not mark an unconfirmed attempt completed.

**Verification:** Run the focused `WarmupAgentEndpointTests`, the full `L4d2MatchmakingCore.Tests` project, `dotnet test L4d2MatchmakingManager.sln --no-restore`, and `git diff --check`.

---

### Task 1: Add endpoint regression tests for node-stop draining

**Files:**

- Modify: `src/L4d2MatchmakingCore/tests/WarmupAgentEndpointTests.cs`

**Why this task exists:** The user-visible stop endpoint must prove both sides of the lifecycle boundary: all current operations are stopped before the container, and an uncertain remote stop prevents container shutdown.

**Impact / Compatibility:** Use the existing `AgentFactory` and `FakeRuntime`; inject a test `IAgentControlClient` so the test exercises the real endpoint, service, drain service, EF state transitions, and container runtime call ordering boundary. Do not change production contracts in the test.

**Repair Track:** The missing owner is `WarmupAgentService.StopCoreAsync`; the test must fail against the current direct-container-stop implementation.

**Retirement Track:** No fallback is introduced. The existing generic `MutateAsync` remains the owner for successful start/stop response shaping; only stop-specific drain errors gain an endpoint mapping.

**Verification:**

- Add a success test with an `active` attempt and matching reservation lease. POST stop and assert the operation was requested to stop, the attempt is `completed`, the lease is removed, the runtime stopped once, and the response is `200` with `stopped`.
- Add a failure test with an active attempt and a fake stop exception. Assert the response is `409` with `warmup_agent_stop_drain_failed`, the attempt remains `active`, the agent is quarantined by existing drain semantics, and the runtime stop count remains `0`.
- In the same failure test, clear the fake exception and call stop again; assert the quarantined Agent retries the drain, the attempt becomes `completed`, and the runtime stops once.

- [ ] **Step 1: Write the failing success and failure tests.** Seed rows through a factory scope after creating the Agent, and add an injectable fake client to `AgentFactory.ConfigureServices`:

```csharp
services.RemoveAll<IAgentControlClient>();
services.AddSingleton(controlClient);
```

The success test should assert `controlClient.StoppedOperations`, `WarmupAttempts.SingleAsync().State == "completed"`, no `ReservationLease`, and `runtime.StopCalls == 1`. The failure test should configure `StopException`, assert `HttpStatusCode.Conflict`, the exact response code, `State == "active"`, `agent.Status == "quarantined"`, and `runtime.StopCalls == 0`.

- [ ] **Step 2: Run only the new endpoint tests and verify RED.**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupAgentEndpointTests --no-restore
```

Expected failure: the current stop path returns `200`, stops the runtime, and leaves the seeded attempt active because it never invokes `DrainAgentAsync`; the failure case also does not return the stop-drain conflict.

### Task 2: Wire drain-before-stop and map the conflict

**Files:**

- Modify: `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs:155-170`
- Modify: `src/L4d2MatchmakingCore/Agents/WarmupAgentEndpoints.cs:75-76`

**Why this task exists:** Make the approved drain service the lifecycle gate before any VNC or container stop side effect.

**Impact / Compatibility:** `DrainAgentAsync` remains unchanged and continues to own task state, reservation lease, and quarantine behavior. The stop endpoint retains the same successful response and gains only the documented `409` error for an unconfirmed drain.

**Repair Track:** In `StopCoreAsync`, after finding a running agent and before `CloseAsync` or `containers.StopAsync`, add:

```csharp
if (!await attemptDrain.DrainAgentAsync(agent.Id, cancellationToken))
    throw new InvalidOperationException("warmup_agent_stop_drain_failed");
```

In `StopAsync`, use the same narrow catch pattern as recreate:

```csharp
private static async Task<IResult> StopAsync(
    Guid agentId,
    WarmupAgentService service,
    CancellationToken cancellationToken)
{
    try
    {
        return await MutateAsync(() => service.StopAsync(agentId, cancellationToken));
    }
    catch (InvalidOperationException exception) when (exception.Message == "warmup_agent_stop_drain_failed")
    {
        return Results.Conflict(exception.Message);
    }
}
```

- [ ] **Step 1: Implement the minimal service and endpoint change above.**
- [ ] **Step 2: Run the focused endpoint tests and verify GREEN.**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupAgentEndpointTests --no-restore
```

Expected result: all `WarmupAgentEndpointTests` pass, including the new stop-drain cases.

### Task 3: Update API contract documentation and run regression verification

**Files:**

- Modify: `docs/matchmaking-core-api.md` in the Agent stop endpoint section.
- Modify: `docs/matchmaking-core-frontend-api.md` in the Agent stop endpoint section.
- Create: `docs/aegis/work/2026-08-24-stop-warmup-agent-drain/40-atomic-tasks.md` and `50-evidence.md` as execution records.

**Why this task exists:** Operators and frontend callers need to know that a stop request can be rejected until related warm-up operations are confirmed stopped.

**Impact / Compatibility:** Document the existing `200`/`404` behavior plus the new `409 "warmup_agent_stop_drain_failed"`; do not alter paths, request bodies, response DTOs, or frontend code.

- [ ] **Step 1: Add the stop-drain contract to both API docs.** State that Core drains `active`/`uncertain` attempts before stopping VNC/container, completes confirmed attempts and releases matching leases, and returns `409` while retaining/quarantining an unconfirmed operation.
- [ ] **Step 2: Run the Core project regression suite.**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore
```

Expected result: all Core tests pass.

- [ ] **Step 3: Run the solution regression suite and whitespace check.**

```powershell
dotnet test L4d2MatchmakingManager.sln --no-restore
git diff --check
```

Expected result: solution tests pass (with only environment-expected skips, if any), and `git diff --check` produces no output.
- [ ] **Step 4: Record command output and changed-file evidence in `50-evidence.md`.**
