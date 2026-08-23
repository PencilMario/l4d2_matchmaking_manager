# Agent Download Region Restart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans (recommended) or aegis:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply a changed Steam download region immediately on a running Agent by updating its account-local target and restarting Steam.

**Architecture:** The Agent exposes a new internal `POST /v1/steam/download-region` operation. It atomically writes the Millennium target file and delegates the actual Steam restart to the existing Supervisor controller. Core calls this operation before saving a changed region for a running or restarting Agent; stopped Agents only persist the setting.

**Tech Stack:** ASP.NET Core Minimal APIs, .NET 10, EF Core, MSTest, Docker/Supervisor, Millennium Lua/TypeScript bridge.

**Baseline / Authority Refs:** `docs/steam-lobby-agent-api.md`, `docs/matchmaking-core-api.md`, `docs/matchmaking-core-frontend-api.md`, `deploy/steam-lobby-agent/91-enable-steam-supervisor.sh`, `CONTEXT.md`.

**Compatibility Boundary:** Existing `POST /v1/steam/restart` remains unchanged for scheduler recovery. Existing container lifecycle and account volumes remain unchanged. Region changes do not recreate containers. Empty region must override the old environment target and select Steam default behavior.

**Verification:** Target MSTest projects, Core Agent endpoint integration tests, Agent internal endpoint/controller tests, deployment contract scripts, solution build, and frontend regression tests if the final diff touches frontend code.

---

### Task 1: Establish baseline and implementation records

**Files:**
- Create: `docs/aegis/work/2026-08-23-agent-download-region-restart/00-intent.md`
- Create: `docs/aegis/work/2026-08-23-agent-download-region-restart/10-baseline-readset.md`
- Create: `docs/aegis/work/2026-08-23-agent-download-region-restart/30-plan.md`
- Create: `docs/aegis/work/2026-08-23-agent-download-region-restart/40-atomic-tasks.md`

**Why this task exists:** Preserve the approved behavior, compatibility boundary, and evidence trail for a cross-module change.

**Verification:** Run the existing Agent and Core tests before changing production code.

- [x] **Step 1: Record atomic tasks.** Add the task checklist in `40-atomic-tasks.md`.
- [x] **Step 2: Run baseline tests.** Run `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore` and `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore`; record any pre-existing failures.

### Task 2: Add Agent target-file application and internal route

**Files:**
- Modify: `src/L4d2Matchmaking.Contracts/AgentContracts.cs`
- Create: `src/L4d2LobbyAgent/Steam/SteamDownloadRegionController.cs`
- Modify: `src/L4d2LobbyAgent/Program.cs`
- Test: `src/L4d2LobbyAgent/tests/AgentLobbyEndpointTests.cs`
- Test: `src/L4d2LobbyAgent/tests/SteamDownloadRegionControllerTests.cs`

**Why this task exists:** The running container cannot receive new environment variables; the Agent must update the account-local Millennium target before restarting Steam.

**Impact / Compatibility:** Keep `/v1/steam/restart` and `ISteamDesktopController` intact. The new controller writes only the target file, then invokes the existing restart mechanism. Use atomic replacement and preserve empty target semantics.

- [x] **Step 1: Write failing controller tests.** Assert a numeric target is atomically persisted before the desktop restart, an empty target is persisted as blank, and a restart failure leaves the new target available for retry.
- [x] **Step 2: Run the controller tests and verify the expected failure.** Run `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore --filter FullyQualifiedName~SteamDownloadRegionControllerTests`; expected failure is missing controller/route behavior.
- [x] **Step 3: Write the failing endpoint/client contract test.** Add `AgentDownloadRegionRequest` and assert `POST /v1/steam/download-region` returns `202` and invokes the controller with the requested value.
- [x] **Step 4: Implement the controller and route.** Register `ISteamDownloadRegionController`, write `~/.config/millennium/steam-region-bridge-region` through an environment-derived path, atomically replace the file, and call `ISteamDesktopController.RestartAsync` only after the write completes.
- [x] **Step 5: Run Agent tests and verify green.** Run the filtered tests, then the full Agent test project.

### Task 3: Apply changed regions from Core with failure-safe persistence

**Files:**
- Modify: `src/L4d2MatchmakingCore/Agents/AgentControlClient.cs`
- Modify: `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs`
- Modify: `src/L4d2MatchmakingCore/tests/AgentControlClientTests.cs`
- Modify: `src/L4d2MatchmakingCore/tests/WarmupAgentEndpointTests.cs`
- Modify: all test fake implementations of `IAgentControlClient` reported by the compiler/search.

**Why this task exists:** Wire the user-visible edit operation to the Agent controller without restarting for unrelated updates or persisting an unapplied running configuration.

**Impact / Compatibility:** Only `agent.Status == "running"` or `agent.Status == "restarting"` and a normalized region value change trigger the new internal call. A stopped/created/quarantined Agent only stores the new value. On remote failure, leave the database region, name, and status unchanged and return the existing server-error/operation failure boundary. On success, mark the Agent `restarting` so scheduling waits for the existing health-based recovery path.

**Repair Track:** Move the canonical “apply running region” owner to `WarmupAgentService` plus `AgentControlClient`; use the Agent’s account-local file as the runtime target owner.

**Retirement Track:** Retire the documented requirement to manually call `recreate` after a region edit. Keep `recreate` for image, API path, and UI-mode changes.

- [x] **Step 1: Write failing Core client test.** Assert `ApplyDownloadRegionAndRestartAsync` posts the numeric/empty request to `/v1/steam/download-region` and preserves the Agent host routing.
- [x] **Step 2: Run the client test and verify RED.** Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~AgentControlClientTests`; expected failure is the missing client method/path.
- [x] **Step 3: Write failing Core update tests.** Cover running and restarting changed regions applying immediately; same region making no call; stopped changed region making no call; stopped start applying the saved region; and failed application leaving the old database state unchanged.
- [x] **Step 4: Implement Core wiring.** Add the client method, serialize `AgentDownloadRegionRequest`, wrap update in the existing lifecycle coordinator, call the Agent before assigning/persisting the changed region, set status `restarting` after success, and save once.
- [x] **Step 5: Update all test doubles.** Implement the new interface method explicitly in scheduler, drain, global-settings, lobby-query, and memory-limit test clients, retaining their existing behavior.
- [x] **Step 6: Run Core tests and verify green.** Run filtered Agent client/update tests, then the full Core test project.

### Task 4: Fix default-region Bridge fallback and update documentation/contracts

**Files:**
- Modify: `deploy/steam-lobby-agent/millennium/steam-region-bridge/backend/main.lua`
- Modify: `deploy/steam-lobby-agent/Test-MillenniumRegionBridgeContract.ps1`
- Modify: `docs/steam-lobby-agent-api.md`
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/matchmaking-core-frontend-api.md`
- Modify: `deploy/steam-lobby-agent/README.md`

**Why this task exists:** An empty target must clear a previously configured region instead of falling back to the container’s stale `STEAM_DOWNLOAD_REGION_ID` environment value, and operators need the new lifecycle semantics documented.

**Impact / Compatibility:** Keep environment-variable fallback for installations that have no account-local target file. Once the target file exists, including empty content, it is authoritative.

- [x] **Step 1: Add the failing contract assertion.** Require the Bridge backend to distinguish an existing empty target file from a missing target file.
- [x] **Step 2: Run the deployment contract and verify RED.** Run `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-MillenniumRegionBridgeContract.ps1`; expected failure is the missing empty-file authority logic.
- [x] **Step 3: Implement the Lua fallback fix.** Use file presence as the authority signal: parse the file value when present, and use environment fallback only when the file cannot be read as absent.
- [x] **Step 4: Update API and deployment docs.** State that running-region edits apply the account-local target and restart Steam, successful responses can be `restarting`, stopped Agents defer application, failures do not commit the new Core setting, and `recreate` is no longer required solely for region changes.
- [x] **Step 5: Run the deployment contract.** Verify the contract script exits 0.

### Task 5: Full verification and handoff

**Files:**
- Modify: `docs/aegis/work/2026-08-23-agent-download-region-restart/50-evidence.md`

**Why this task exists:** Confirm the cross-module behavior and document residual runtime risks before handoff.

**Verification:** Run Agent tests, Core tests, deployment scripts, solution build, and `git diff --check`; inspect the final diff without touching unrelated user files.

- [x] **Step 1: Run all targeted and related tests.** Capture exit codes and test counts.
- [x] **Step 2: Run build and static contracts.** Verify solution build, shell/PowerShell contracts, and `git diff --check`.
- [x] **Step 3: Review compatibility and side effects.** Confirm no container recreate, volume deletion, scheduler restart path, or unrelated frontend behavior changed.
- [x] **Step 4: Record evidence and residual risk.** Note that real Steam/Millennium restart behavior still requires deployment/runtime verification if no live Agent is available.
