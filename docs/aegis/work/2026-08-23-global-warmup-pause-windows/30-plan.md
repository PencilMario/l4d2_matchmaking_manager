# 时间段暂停暖服和调度 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Core 和两套管理前端增加可持久化的 Asia/Shanghai 多时间段暖服/调度暂停，并将每项全局设置拆为独立卡片。

**Architecture:** 在 `CoreSettings` 的单行记录中以 JSONB 数组保存 `HH:mm` 时间段。纯规则组件负责校验、排序和按固定 UTC+8 判断当前时间；`GlobalSettingsService`、scheduler recovery/tick 和既有 `WarmupSchedulingGate` 共同保证配置写入与全量排空不竞争。Core 增加专用时间段资源，旧组合设置接口保留兼容。两套 React 设置入口使用专用 API，各自渲染四张同级卡片。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/Npgsql、MSTest、React 19、TypeScript、Vitest、Testing Library。

**Baseline / Authority Refs:** `CONTEXT.md`、`docs/frontend-redesign-spec.md`、`docs/matchmaking-core-frontend-api.md`、`docs/matchmaking-core-api.md`、`docs/aegis/specs/2026-08-23-global-warmup-pause-windows-design.md`、`docs/aegis/work/2026-08-23-global-warmup-pause-windows/10-baseline-readset.md`。

**Compatibility Boundary:** 保留旧 `/v1/settings` GET/PUT、现有 `WarmupSchedulingEnabled` 手动开关、Bearer 鉴权、五秒 tick、任务排空失败保留状态、Agent 容器生命周期和密钥不回显行为；数据库只新增时间段列。

**Verification:** 每个 Core 行为先执行目标 MSTest 看到预期 RED，再执行目标测试 GREEN；前端执行目标 Vitest 和 `npm run build`；最终执行 `dotnet test L4d2MatchmakingManager.sln`、`npm test -- --run`、`npm run build`、`git diff --check`。

**Facts:** 当前基线 Core 159 通过、3 跳过；前端 21 个测试文件、60 项通过。当前已有 `CoreSettings.WarmupSchedulingEnabled`、`WarmupSchedulingGate`、`WarmupAttemptDrainService.DrainAllAsync` 和两套设置入口。

**Assumptions:** `Asia/Shanghai` 使用固定 UTC+8 比较；相同起止时间是非法配置；重叠时间段保留并按任意命中处理；时间段设置保存后若当前已暂停则立即尝试排空。

**Repair Track:** 将“是否允许调度”的 canonical owner 从只读取手动开关扩展为手动开关与时间段的有效状态判断；时间段进入时复用 `WarmupAttemptDrainService`，不新建第二套任务清理语义。

**Retirement Track:** 旧组合 settings PUT 不删除，继续服务旧客户端，但不再作为新前端时间段设置的写入 owner；不保留 scheduler 绕过有效暂停状态的 fallback 分支。

---

### Task 1: 时间段纯规则与 Core 持久化模型

**Files:**
- Create: `src/L4d2MatchmakingCore/Scheduling/WarmupPauseWindowRules.cs`
- Create: `src/L4d2MatchmakingCore/tests/WarmupPauseWindowRulesTests.cs`
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- Create: `src/L4d2MatchmakingCore/Data/Migrations/202608230001_GlobalWarmupPauseWindows.cs`

**Why this task exists:** 时间判断是暖服是否启动的业务规则，必须独立于 HTTP、数据库和 scheduler，并在上海时区边界、跨午夜和非法输入上可重复验证。

**Impact / Compatibility:** 新列非空、默认 `[]`；现有 CoreSettings 列和默认手动开关不变。规则组件不得读取系统本地时区或直接访问数据库。

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupPauseWindowRulesTests --no-restore`。

- [ ] **Step 1: Write failing rule tests.** Create tests with the public API `WarmupPauseWindowRules.Normalize` and `WarmupPauseWindowRules.IsActive`:

```csharp
[TestMethod]
public void IsActiveUsesShanghaiTimeAndTreatsEndAsExclusive()
{
    var windows = WarmupPauseWindowRules.Normalize([
        new WarmupPauseWindow("00:00", "08:00")
    ]);

    Assert.IsTrue(WarmupPauseWindowRules.IsActive(
        new DateTimeOffset(2026, 8, 22, 23, 59, 0, TimeSpan.Zero), windows));
    Assert.IsFalse(WarmupPauseWindowRules.IsActive(
        new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero), windows));
    Assert.IsTrue(WarmupPauseWindowRules.IsActive(
        new DateTimeOffset(2026, 8, 23, 7, 59, 0, TimeSpan.Zero), windows));
    Assert.IsFalse(WarmupPauseWindowRules.IsActive(
        new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero), windows));
}
```

Add separate tests for `23:00–00:00`, multiple windows, empty windows, sorted output, invalid format, and equal start/end. Use UTC instants whose Shanghai local times are explicit; do not use `DateTime.Now`.

- [ ] **Step 2: Run the rule tests and verify RED.** Run the command above. Expected result: compilation fails because `WarmupPauseWindow`, `WarmupPauseWindowRules.Normalize`, and `IsActive` do not yet exist.

- [ ] **Step 3: Add the minimal rule and DTO types.** Add `WarmupPauseWindow(string Start, string End)` and expose exactly two rule methods from `WarmupPauseWindowRules`: `Normalize(IReadOnlyList<WarmupPauseWindow>)` and `IsActive(DateTimeOffset, IReadOnlyList<WarmupPauseWindow>)`. Use `TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)`, compare total minutes, sort by start then end, and throw `ArgumentException("invalid_warmup_pause_window")` for invalid input. For cross-midnight windows, return true when local minutes are greater than or equal to start or less than end.

- [ ] **Step 4: Add the persisted JSON field and migration.** Add `WarmupPauseWindowsJson = "[]"` to `CoreSettings`; configure it as required, `jsonb`, and default `[]` in `MatchmakingDbContext`. Create migration `202608230001_GlobalWarmupPauseWindows` with `AddColumn<string>(name: "WarmupPauseWindowsJson", table: "CoreSettings", type: "jsonb", nullable: false, defaultValue: "[]")` and matching `Down` drop.

- [ ] **Step 5: Run the rule tests and compile the Core project.** Re-run the focused rule tests, then `dotnet build src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj --no-restore`. Expected result: all new rule tests pass and the project builds without warnings introduced by this task.

- [ ] **Step 6: Commit the slice.** Run `git diff --check`, stage only the rule, entity, DbContext, migration, and rule test files, then commit with `feat(settings): 增加暖服暂停时间段规则`.

---

### Task 2: 时间段专用 Settings API 与持久化服务

**Files:**
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsEndpoints.cs`
- Modify: `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs`

**Why this task exists:** 管理 UI 需要独立保存时间段，且保存配置时不能覆盖代理、密钥或手动调度开关。

**Impact / Compatibility:** 新增认证 `/v1/settings/warmup-pause-windows` GET/PUT；组合 GET 只增加 `warmupPauseWindows` 和 `warmupPauseWindowsActive`；组合 PUT 不读取或写入时间段 JSON。更新当前已暂停的时间段时，在共享 gate 内先保存再调用排空，失败返回 409。

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~GlobalSettingsEndpointTests --no-restore`。

- [ ] **Step 1: Write failing endpoint tests.** Add tests that:
  - initial combined and dedicated GET return an empty windows list and inactive status;
  - PUT saves `00:00–08:00` and `23:00–00:00`, and both GET forms return the sorted list;
  - legacy proxy PUT preserves the saved time windows;
  - malformed time and equal endpoints return `400` with `invalid_warmup_pause_window`;
  - when the saved current window contains an active attempt, PUT calls the existing drain client and returns 200 on success, while a stop exception returns 409 and leaves the new windows persisted.

Use explicit DTO records for response assertions and seed attempts through the existing `SettingsFactory`/`SeedAttemptAsync` pattern. Do not assert wall-clock-dependent `active` in endpoint tests unless the window is constructed around a fixed rule helper; assert the dedicated response shape and persistence separately.

- [ ] **Step 2: Run the endpoint tests and verify RED.** Run the command above. Expected result: the new response properties and route are absent, and the time-window request cannot be deserialized into a mapped endpoint.

- [ ] **Step 3: Add DTOs and response mapping.** Add `UpdateWarmupPauseWindowsRequest(IReadOnlyList<WarmupPauseWindow> Windows)` and `WarmupPauseWindowsSettingsResponse(IReadOnlyList<WarmupPauseWindow> Windows, bool Active, DateTimeOffset UpdatedAt)`. Extend `GlobalSettingsResponse` with the two combined GET properties while leaving existing constructor ordering compatible for named JSON consumers. Deserialize stored JSON with `JsonSerializer`, normalize through `WarmupPauseWindowRules`, and treat an empty/missing legacy value as `[]`.

- [ ] **Step 4: Implement service methods.** Add `GetWarmupPauseWindowsAsync` and `UpdateWarmupPauseWindowsAsync` to `GlobalSettingsService`. `Get` returns normalized windows and `Active` using `DateTimeOffset.UtcNow`. `Update` normalizes before changing the entity; saves the JSON and `UpdatedAt` once; if `WarmupSchedulingEnabled` is true and the normalized windows are active, calls `attemptDrain.DrainAllAsync` while already holding `WarmupSchedulingGate`; throws `InvalidOperationException("global_warmup_drain_failed")` when it returns false. Existing VNC/key/legacy update paths must not assign the new JSON field.

- [ ] **Step 5: Map the authenticated routes.** Add GET/PUT handlers under `/v1/settings/warmup-pause-windows`. Catch `ArgumentException` and return `400` with the existing string body convention; catch `global_warmup_drain_failed` and return `409`. Keep all existing routes unchanged.

- [ ] **Step 6: Verify GREEN and compatibility.** Re-run the focused endpoint tests and the existing `GlobalSettingsEndpointTests` filter. Confirm legacy PUT preserves the new list and no response contains a secret or ciphertext.

- [ ] **Step 7: Commit the slice.** Run `git diff --check`, stage the settings DTO/service/endpoint and endpoint test changes, and commit with `feat(settings): 增加暖服暂停时间段接口`.

**Repair Track:** `GlobalSettingsService` remains the canonical owner for persisted settings and side-effecting updates; the new resource reuses the existing drain service and gate.

**Retirement Track:** The legacy combined PUT remains active only for older clients; the new frontend will use the dedicated resource and will not send time windows through the legacy body.

---

### Task 3: Scheduler effective-pause enforcement

**Files:**
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`
- Modify: `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs` (only if a cross-layer assertion is needed)

**Why this task exists:** 保存配置本身不能 guarantee 未来 tick 不会启动任务；恢复和每五秒调度都必须在任何 A2S/selector/start 行为前尊重时间段。

**Impact / Compatibility:** 现有 scheduler gate 和手动开关行为保留。有效暂停时仅新增一次可重试排空调用；正常时间、空配置和手动启用路径保持现有 priority、续暖、重建和恢复逻辑。

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~WarmupSchedulerServiceTests --no-restore`。

- [ ] **Step 1: Write failing scheduler tests.** Add tests that seed `CoreSettings` with a currently active window and a running healthy agent/target, then assert `TickAsync` creates no attempt, does not call A2S, does not list healthy agents, and does not call `StartOperationAsync`. Add a test with an existing active attempt proving a paused tick calls `StopOperationAsync`, marks the attempt completed, and removes its lease. Add a test with a window that has ended proving normal scheduling still creates an attempt.

Extend fake selector/A2S/agent classes with call counters only where assertions need them; preserve existing fake behavior and user-owned scheduler assertions.

- [ ] **Step 2: Run the scheduler tests and verify RED.** Run the command above. Expected result: the paused tick currently proceeds to maintenance/selector/A2S or starts a new operation, so the new assertions fail for the missing time-window check.

- [ ] **Step 3: Add the effective-state query.** Replace the scheduler's single-purpose `IsSchedulingEnabledAsync` guard with a query that reads `WarmupSchedulingEnabled` and `WarmupPauseWindowsJson`, deserializes and normalizes the windows, and returns whether scheduling is allowed. If either manual disable or current pause is active, call `drain?.DrainAllAsync(cancellationToken)` and return before maintenance, restart restoration, A2S, selector, or target queries. A missing `CoreSettings` row remains equivalent to manual enabled and empty windows.

- [ ] **Step 4: Preserve existing recovery/tick flow after the guard.** Do not move or alter existing `RestoreRestartedAgentsAsync`, continuation selection, reservation checks, A2S decisions, or batch start behavior. The only new branch is the early effective-pause path. Use `WarmupPauseWindowRules` as the sole time comparison owner.

- [ ] **Step 5: Verify GREEN and regression.** Re-run the focused scheduler tests, then the GlobalSettings, WarmupAttemptDrain, and WarmupScheduler filters together. Expected result: new pause tests pass and all existing warmup behavior remains green.

- [ ] **Step 6: Commit the slice.** Run `git diff --check`, stage scheduler and test changes, and commit with `fix(scheduler): 按时间段暂停暖服调度`.

**Repair Track:** The scheduler's effective pause guard becomes the canonical owner for all background recovery/tick entry points; the endpoint's immediate drain remains the write-time owner for configuration changes.

**Retirement Track:** No old scheduling branch is removed. The old boolean-only guard is retired as a standalone decision and replaced by the combined effective-state query; no bypass fallback is allowed.

---

### Task 4: Frontend API contracts and clients

**Files:**
- Modify: `frontend/src/api/models.ts`
- Modify: `frontend/src/api/core-client.ts`
- Modify: `frontend/src/api/core-client.test.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/state/useCoreSnapshot.ts`

**Why this task exists:** Both frontend settings implementations need one typed, dedicated persistence path and the combined response must carry the saved windows for initialization.

**Impact / Compatibility:** Existing client methods and legacy settings input remain available. New method names are `getWarmupPauseWindows`, `updateWarmupPauseWindows`, `getWarmupPauseWindowsSettings`, and `updateWarmupPauseWindowsSettings` as appropriate to each client.

**Verification:** `npm test -- --run src/api/core-client.test.ts` and `npm run build` from `frontend`.

- [ ] **Step 1: Write failing API tests.** Add a `CoreClient` test that calls `getWarmupPauseWindows()` and `updateWarmupPauseWindows({ windows: [{ start: '23:00', end: '00:00' }] })`, then asserts GET `/v1/settings/warmup-pause-windows` and PUT body `{"windows":[{"start":"23:00","end":"00:00"}]}`. Add the same dedicated method coverage to the legacy `ApiService` request shape if service tests are introduced; otherwise verify it through the component callback wiring in Task 5.

- [ ] **Step 2: Run the API tests and verify RED.** Run the focused Vitest command. Expected result: TypeScript reports missing model/client methods or the fetcher receives no dedicated request.

- [ ] **Step 3: Add typed models and client methods.** Add `WarmupPauseWindow`, `WarmupPauseWindowsSettings`, and `WarmupPauseWindowsSettingsInput` to `frontend/src/api/models.ts`; extend `GlobalSettings` with `warmupPauseWindows` and `warmupPauseWindowsActive`. Add the dedicated GET/PUT methods to `CoreClient` and raw request/response types plus methods to `ApiService`.

- [ ] **Step 4: Extend the snapshot client interface.** Add optional `getWarmupPauseWindows`/`updateWarmupPauseWindows` methods to `CoreSnapshotClient` using the new model types so `WorkspaceShell` can pass them without `any` casts.

- [ ] **Step 5: Verify GREEN.** Re-run the focused API tests and `npm run build`; expected result is no type errors and the dedicated path/body assertions pass.

- [ ] **Step 6: Commit the slice.** Run `git diff --check`, stage the typed model/client/interface/test changes, and commit with `feat(frontend): 接入暖服暂停时间段接口`.

---

### Task 5: 两套设置页面拆卡与时间段编辑器

**Files:**
- Modify: `frontend/src/components/GlobalSettingsView.tsx`
- Modify: `frontend/src/components/GlobalSettingsView.test.tsx`
- Modify: `frontend/src/features/settings/SettingsWorkspace.tsx`
- Modify: `frontend/src/features/settings/SettingsWorkspace.test.tsx`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/features/workspace/WorkspaceShell.tsx`
- Modify: `frontend/src/styles/layout.css`
- Modify: `frontend/src/styles/forms.css`

**Why this task exists:** 用户需要能独立理解和保存每个设置，且时间段编辑必须支持多行、跨午夜和值校验提示。

**Impact / Compatibility:** 两套现有组件继续编译和测试；现有代理、API Key、手动开关行为不变。新增时间卡片不会把秘密字段回显，也不会将时间段合并到旧 PUT。

**Verification:**

```text
npm test -- --run src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx src/api/core-client.test.ts
npm run build
```

- [ ] **Step 1: Write failing component tests.** Update fixtures with `warmupPauseWindows: []` and `warmupPauseWindowsActive: false`, then add tests in both component suites that:
  - render four separately titled cards;
  - render an empty-state message for no pause windows;
  - click “新增时间段”, fill `开始时间 1` and `结束时间 1`, add a second row, remove the first row, and save exactly the remaining window;
  - assert saving the pause card calls only its pause callback, while saving proxy/key still does not call pause or the other setting callback;
  - retain current manual-toggle confirmation, key secrecy, and proxy tests.

Use `fireEvent.change` on inputs with labels `开始时间 1` and `结束时间 1`; do not depend on generated DOM ids or CSS class names.

- [ ] **Step 2: Run component tests and verify RED.** Run the focused command above. Expected result: the current single-card markup has no pause card, no add/remove controls, and the new callback prop is absent.

- [ ] **Step 3: Implement pause-window card state and actions.** Add a local array initialized from `GlobalSettings.warmupPauseWindows`. Implement add with a blank `start`/`end` row, remove by index (leaving at least zero rows), and submit with the exact `{ windows }` payload. Display a per-card error for incomplete rows before calling the API; update the array, active label, and updated time from the response on success.

- [ ] **Step 4: Split the existing settings markup into four sibling cards.** Give each card its own heading and feedback region. Keep the existing dangerous confirmation around disabling the manual switch. Use the existing Tailwind classes in `GlobalSettingsView`; use `settings-panel__cards`, `settings-card`, and `settings-card__header` styles in `SettingsWorkspace` and add responsive grid rules without introducing nested cards.

- [ ] **Step 5: Wire both application shells.** Add the pause callback to the old `App.tsx` `GlobalSettingsView` wiring and to `WorkspaceShell`/`SettingsWorkspace`. After the dedicated pause or manual scheduling update resolves, refresh the relevant snapshot. If a client lacks the new method, preserve the existing “全局设置客户端不可用” fallback.

- [ ] **Step 6: Verify GREEN and visual contract.** Re-run the focused component/API tests and `npm run build`. Inspect the rendered DOM with tests for four sibling card headings and run `git diff --check`; manually verify at 1280px-equivalent layout that cards do not overflow horizontally.

- [ ] **Step 7: Commit the slice.** Stage both settings components/tests, shell wiring, and CSS changes; commit with `feat(frontend): 将全局设置拆分为独立卡片`.

**Repair Track:** The old monolithic settings surface is replaced by four UI owners while both deployment entry points remain supported; each save callback owns only its own resource.

**Retirement Track:** The outer combined settings card and any frontend call to the legacy combined PUT retire from active UI use. The legacy API remains for old clients and is not deleted.

---

### Task 6: API 文档、证据与全量验证

**Files:**
- Modify: `docs/matchmaking-core-frontend-api.md`
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/aegis/INDEX.md`
- Modify: `docs/aegis/work/2026-08-23-global-warmup-pause-windows/40-atomic-tasks.md`
- Create: `docs/aegis/work/2026-08-23-global-warmup-pause-windows/50-evidence.md`

**Why this task exists:** 后续客户端必须知道时区、跨午夜、排空副作用、409 语义和容器边界；全量验证需要记录基线与新证据。

**Impact / Compatibility:** 文档描述新专用资源和组合 GET 扩展，明确旧组合 PUT 不处理时间段；不更改运行时代码。

**Verification:**

```text
dotnet test L4d2MatchmakingManager.sln
npm test -- --run
npm run build
git diff --check
```

- [ ] **Step 1: Write the atomic task checklist.** Record each completed red/green slice and its exact focused command in `40-atomic-tasks.md`; no task is marked complete before its command output exists.

- [ ] **Step 2: Update both API documents.** Add the new route list, request/response examples, `Asia/Shanghai` minute semantics, `[start,end)` boundaries, cross-midnight behavior, invalid input response, immediate drain behavior, conflict response, automatic resume, and explicit “containers remain running” statement.

- [ ] **Step 3: Run the full regression suite.** Run the four commands above and capture exact pass/skip/failure counts, build result, and `git diff --check` result in `50-evidence.md`. If a test fails, follow the systematic debugging/TDD loop before claiming completion.

- [ ] **Step 4: Inspect final scope.** Re-read the approved design, inspect `git diff main HEAD`, confirm no code path calls a container stop for this feature, verify main worktree user changes were not touched, and record any residual risk such as no live deployment/browser E2E.

- [ ] **Step 5: Commit docs and evidence.** Stage only API docs, atomic checklist, and evidence; commit with `docs(settings): 记录暖服暂停时间段契约`.
