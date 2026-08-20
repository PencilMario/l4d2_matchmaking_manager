# Global Warm-up Disable Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为管理前端提供一个可持久化的全局暖服/调度开关，关闭时安全排空当前任务并暂停后台调度，同时保留 Agent 容器运行。

**Architecture:** 在既有 `CoreSettings` 增加默认开启的 `WarmupSchedulingEnabled`，沿用 `/v1/settings` GET 返回完整设置，并新增 `/v1/settings/warmup-scheduling` GET/PUT 资源。`WarmupAttemptDrainService` 增加全量排空方法；单例 gate 串行化设置切换与 scheduler recovery/tick，关闭先持久化禁用再排空，失败保持禁用并返回 409。两套前端设置组件共用相同交互：关闭先确认，开启直接保存。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/PostgreSQL、MSTest、React 19、TypeScript、Vitest。

**Baseline / Authority Refs:** `CONTEXT.md`、`docs/matchmaking-core-frontend-api.md`、`docs/frontend-redesign-spec.md`、本任务 `00-intent.md` 与 `10-baseline-readset.md`。

**Compatibility Boundary:** 保留旧 `/v1/settings` GET/PUT、现有数据库列和鉴权；新增 JSON 字段只向后兼容扩展；不停止 Docker 容器、不修改 Target Server `enabled`、不改变五秒轮询。

**Verification:** 先运行每个新增测试确认 RED，再运行目标后端/前端测试、`dotnet test L4d2MatchmakingManager.sln`、`npm test -- --run`、`npm run build` 与 `git diff --check`。

---

### Task 1: 全局设置契约与持久化

**Files:**
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- Create: `src/L4d2MatchmakingCore/Data/Migrations/202608200001_GlobalWarmupScheduling.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsEndpoints.cs`
- Test: `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs`

**Why this task exists:** 开关必须是重启后仍然安全的 Core 状态；读取不应泄露任何已有密钥，旧组合设置保存不能意外改写开关。

**Impact / Compatibility:** `CoreSettings.WarmupSchedulingEnabled` 默认 `true`；`GlobalSettingsResponse` 仅增加 `warmupSchedulingEnabled`；专用 PUT 接受 `{ "enabled": false }`，关闭失败返回 `409 "global_warmup_drain_failed"`，状态仍是 disabled。

- [ ] **Step 1: Write the failing HTTP tests.** Add tests that GET exposes the default enabled flag, PUT false persists false in both dedicated and combined GET, PUT true persists true, and the legacy `/v1/settings` PUT leaves the flag unchanged.
- [ ] **Step 2: Run the tests and confirm RED.** Run:

  ```text
  dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~GlobalSettingsEndpointTests --no-restore
  ```

  Expected failure: the response has no `warmupSchedulingEnabled` value and the dedicated route is not mapped.
- [ ] **Step 3: Add the persisted field and migration.** Add `WarmupSchedulingEnabled = true` to `CoreSettings`, configure `HasDefaultValue(true)`, and create a migration with `AddColumn<bool>(name: "WarmupSchedulingEnabled", table: "CoreSettings", nullable: false, defaultValue: true)`.
- [ ] **Step 4: Add DTOs and read/write methods.** Extend `GlobalSettingsResponse`, add `UpdateWarmupSchedulingRequest` and `WarmupSchedulingSettingsResponse`, keep `UpdateGlobalSettingsAsync` from touching the new field, and implement dedicated read/update methods that delegate disabled draining to Task 2.
- [ ] **Step 5: Map authenticated routes and verify GREEN.** Map `GET/PUT /v1/settings/warmup-scheduling`; return `400` for argument errors and `409` for `global_warmup_drain_failed`/`global_warmup_drain_pending`. Re-run the focused tests and confirm they pass.

**Repair Track:** The canonical owner is `GlobalSettingsService`; the smallest repair is a new nullable-free setting column plus a dedicated side-effecting resource. Existing combined settings remain readable/writable for old clients.

**Retirement Track:** No old route is removed. New frontend writes retire the combined PUT as their active owner; the combined route remains only for legacy compatibility until all old clients migrate.

### Task 2: 全量排空与调度暂停

**Files:**
- Create: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulingGate.cs`
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupAttemptDrainService.cs`
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupAttemptDrainServiceTests.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`

**Why this task exists:** 关闭必须停止每个 active/uncertain operation、释放所有 reservation lease、清理 restart_pending，并保证后台 tick 不会在排空过程中启动新任务。

**Impact / Compatibility:** Agent 容器不经过 `IAgentContainerRuntime.StopAsync`；远程 stop 失败保留 attempt、隔离对应 Agent 并返回 false。成功停止的 Agent 使用既有 `RestartSteamAsync` 后置流程并保持容器运行；scheduler 在重启健康检查通过前不重新分配。

- [ ] **Step 1: Write failing drain tests.** Add a test with two agents, active/uncertain/restart_pending attempts and reservation leases; assert all operations are stopped, completed attempts have leases removed, restart_pending is completed without a remote stop, successful agents receive Steam restart, and no container runtime is involved. Add a failure test proving a stop exception leaves the attempt and returns false.
- [ ] **Step 2: Run drain tests and confirm RED.** Run:

  ```text
  dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupAttemptDrainServiceTests --no-restore
  ```

  Expected failure: `DrainAllAsync` does not exist.
- [ ] **Step 3: Implement `DrainAllAsync`.** Query `active`, `uncertain`, and `restart_pending`; stop only active/uncertain operations; preserve failed rows; complete successful rows and delete matching leases; restart touched agents and set them to `restarting`; save once and return whether every stoppable row was confirmed.
- [ ] **Step 4: Write failing scheduler tests.** Add tests proving a disabled setting prevents `TickAsync` from calling A2S, selector, or `StartOperationAsync`; `RecoverAsync` drains leftovers while disabled; and a shared gate makes a tick wait for an in-progress toggle.
- [ ] **Step 5: Run scheduler tests and confirm RED.** Run:

  ```text
  dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests --no-restore
  ```

  Expected failure: scheduler currently does not read `CoreSettings` or coordinate with the toggle.
- [ ] **Step 6: Implement the gate and scheduler guard.** Register `WarmupSchedulingGate` as singleton. Wrap recovery and ticks with it, return before any scheduling work when `WarmupSchedulingEnabled` is false, and use `DrainAllAsync` during disabled recovery. Preserve all existing maintenance, health, A2S, reservation, and user-owned restart assertions.
- [ ] **Step 7: Run the focused backend suite.** Run the GlobalSettings, WarmupAttemptDrain, and WarmupScheduler filters together; expected result is zero failures.

**Repair Track:** `WarmupAttemptDrainService` becomes the canonical owner for cross-agent task cleanup; the scheduler only owns deciding when to start work and no longer races with a global toggle.

**Retirement Track:** No existing per-agent/per-target drain method is removed. The new all-agent path reuses their state semantics but owns the new global operation; duplicate fallback starts are forbidden.

### Task 3: 前端开关、确认交互与 API wiring

**Files:**
- Modify: `frontend/src/api/models.ts`
- Modify: `frontend/src/api/core-client.ts`
- Modify: `frontend/src/api/core-client.test.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/components/GlobalSettingsView.tsx`
- Modify: `frontend/src/components/GlobalSettingsView.test.tsx`
- Modify: `frontend/src/features/settings/SettingsWorkspace.tsx`
- Modify: `frontend/src/features/settings/SettingsWorkspace.test.tsx`
- Modify: `frontend/src/state/useCoreSnapshot.ts`
- Modify: `frontend/src/features/workspace/WorkspaceShell.tsx`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/types/index.ts`
- Modify: `frontend/src/utils/statusMapping.ts`
- Modify: `frontend/src/state/display.ts`

**Why this task exists:** 用户需要在真实部署入口和新版工作台入口都能看到当前状态，并能明确确认会清空任务的危险关闭动作。

**Impact / Compatibility:** 两套组件的关闭文案统一为“关闭后会停止并清空当前暖服任务，暖服节点容器保持运行”；关闭失败保留开关打开/旧状态并提示可重试；已有 VNC/API Key 表单互不影响。

- [ ] **Step 1: Write failing API/component tests.** Assert `CoreClient.updateWarmupScheduling({ enabled: false })` sends `PUT /v1/settings/warmup-scheduling`; both settings components render the checked switch, opening the switch shows confirmation without calling the callback, cancel leaves it checked, and confirm calls `{ enabled: false }`.
- [ ] **Step 2: Run frontend tests and confirm RED.** Run:

  ```text
  npm test -- --run src/api/core-client.test.ts src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx
  ```

  Expected failure: missing client method, prop, and switch behavior.
- [ ] **Step 3: Implement types and API calls.** Add `warmupSchedulingEnabled` to `GlobalSettings`, add request/response types, expose `get/updateWarmupScheduling` in `CoreClient`, and add `get/updateWarmupSchedulingSettings` in `ApiService`.
- [ ] **Step 4: Implement both settings components.** Add a controlled checkbox and local confirmation dialog; save false only from the confirmation action, save true directly, update local state from the response, and surface `CoreApiError`/legacy error text.
- [ ] **Step 5: Wire both application shells.** Pass the new callback from `App.tsx` and require it in `WorkspaceShell.tsx`; add `restarting` display/status handling so touched Agents are not rendered as an unexplained failure.
- [ ] **Step 6: Run component/API tests and production build.** Run the focused frontend tests, then `npm run build`; expected result is no TypeScript errors and all focused tests passing.

**Repair Track:** The deployed `App` and the parallel workspace implementation both become consumers of the dedicated global resource, with the backend response as state source.

**Retirement Track:** The old combined settings PUT remains available but is no longer called by either frontend. No UI component is deleted because both are still compiled/tested and one is the current deployment entry.

### Task 4: API docs, full regression, and evidence

**Files:**
- Modify: `docs/matchmaking-core-frontend-api.md`
- Modify: `docs/matchmaking-core-api.md`
- Create: `docs/aegis/work/2026-08-20-global-warmup-disable/50-evidence.md`

**Why this task exists:** The frontend contract must document the side effect, conflict behavior, persistence, and container boundary so future clients cannot accidentally treat it as a simple local checkbox.

- [ ] **Step 1: Document routes and behavior.** Add the new settings field and GET/PUT resource, request/response examples, `409` retry semantics, and explicit statement that Docker containers remain running.
- [ ] **Step 2: Run all verification commands.** Run:

  ```text
  dotnet test L4d2MatchmakingManager.sln
  npm test -- --run
  npm run build
  git diff --check
  ```

  Record exact exit status and test counts in `50-evidence.md`.
- [ ] **Step 3: Re-read the plan and inspect the diff.** Verify every acceptance item, confirm the pre-existing scheduler test edit remains, confirm no container stop call was added, and capture residual risk if browser E2E/live Core deployment was not run.
