# 调度优先级非负约束实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 拒绝负数调度优先级，并让管理前端只接受大于等于 0 的整数。

**Architecture:** 复用 `TargetServerService.Normalize` 作为 Core 创建/更新的统一校验 owner，继续使用现有 `ArgumentException` 到 HTTP 400 的映射。前端只收紧 `TargetServerForm` 的输入属性和提交校验，不引入新的共享验证层。

**Tech Stack:** .NET 10 / ASP.NET Core Minimal API / EF Core / MSTest；React 19 / TypeScript / Vitest / Testing Library。

**Baseline / Authority Refs:** `CONTEXT.md`、`docs/aegis/specs/2026-08-23-server-priority-nonnegative-design.md`、`src/L4d2MatchmakingCore/Servers/TargetServerService.cs`、`src/L4d2MatchmakingCore/Servers/TargetServerEndpoints.cs`、`frontend/src/features/servers/TargetServerForm.tsx`。

**Compatibility Boundary:** `priority = null` 仍默认为 0；0 和正整数的 API、数据库、排序和调度行为不变；不做 migration；错误字符串仍为 `invalid_target_server_configuration`。

**Verification:** Core 目标服务器端点测试；工作台 `TargetServerForm`、旧版 `TargetServerModal` 前端测试；前端 `npm run build`；最终检查 diff 和工作区状态。

---

### Task 1: Core API 负数优先级校验

**Files:**
- Modify: `src/L4d2MatchmakingCore/Servers/TargetServerService.cs`
- Test: `src/L4d2MatchmakingCore/tests/TargetServerEndpointTests.cs`

**Why this task exists:** 直接调用 API 的客户端不能绕过前端写入负数配置；创建和更新必须共享同一个规则。

**Impact / Compatibility:** 只收紧 `Normalize` 的输入域；保留现有异常到 HTTP 400 的映射、默认值 0 和合法优先级行为。

**Repair Track:** 当前唯一写入归一化 owner 缺少 Priority 下界校验；在该 owner 增加最小判断，不增加 fallback 或 adapter。

**Retirement Track:** 前端原先允许负数的校验路径和帮助文案将被收紧；后端旧的“任意 int 优先级”输入边界不再有效，排序算法继续保留。

**Verification:**
- `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter "FullyQualifiedName~TargetServerEndpointTests"`

- [ ] **Step 1: Write failing tests**

  在 `TargetServerEndpointTests` 增加创建负数和更新负数测试。创建测试先用 `priority = -1` POST，断言 `BadRequest` 和错误文本；更新测试先创建合法服务器，再用 `priority = -1` PUT，断言 `BadRequest`、错误文本和数据库中的优先级仍为 0。

- [ ] **Step 2: Run the focused Core tests and verify RED**

  Run the command above. Expected: the new negative-priority tests fail because current `Normalize` accepts `-1`; existing tests must remain distinguishable from the new failures.

- [ ] **Step 3: Implement the minimal validation**

  In `Normalize`, calculate `effectivePriority = priority ?? 0`, include `effectivePriority < 0` in the existing invalid-configuration guard, and return `effectivePriority` in `TargetServerConfiguration`.

- [ ] **Step 4: Run the focused Core tests and verify GREEN**

  Run the same command. Expected: all `TargetServerEndpointTests` pass with zero failures.

### Task 2: Frontend priority input and regression coverage

**Files:**
- Modify: `frontend/src/features/servers/TargetServerForm.tsx`
- Test: `frontend/src/features/servers/TargetServerForm.test.tsx`
- Test: `frontend/src/components/TargetServersView.test.tsx`

**Why this task exists:** 管理员在 UI 中应立即看到优先级范围，并在提交前得到明确反馈，不必等待 API 返回。

**Impact / Compatibility:** `min=0` 是浏览器输入约束；客户端仍提交 number/null 的既有 `TargetServerInput` 形态，0 和正整数行为不变。

**Repair Track:** 当前表单只检查整数且帮助文本允许负数；更新同一提交 owner 的边界检查和文案，并补充真实表单交互测试。

**Retirement Track:** 旧的负数允许提示和仅整数校验被替换；不新增第二套表单验证或服务端错误处理。

**Verification:**
- `npm test -- --run src/features/servers/TargetServerForm.test.tsx`
- `npm run build`

- [ ] **Step 1: Write failing tests**

  渲染表单，定位 `调度优先级` 数字输入，断言 `min` 为 `0`；输入 `-1` 后提交，断言显示“大于等于 0”的错误且 `onSubmit` 未调用；再用 `0` 提交，断言回调收到 `priority: 0`。

- [ ] **Step 2: Run the focused frontend test and verify RED**

  Run the focused Vitest command. Expected: negative input is currently accepted and the input has no `min` attribute, so the new assertions fail for the missing behavior.

- [ ] **Step 3: Implement the minimal frontend change**

  Change the priority validation to reject `Number(values.priority) < 0`, change the error/help text to non-negative wording, and pass `min={0}` to the `NumberField` used for priority. Extend `NumberField` props only as needed to forward the minimum attribute to its `input`. Apply the same `min={0}` and submit guard to `frontend/src/components/TargetServerModal.tsx`, the legacy target-server editor.

- [ ] **Step 4: Run focused tests and build**

  Run the focused Vitest command and `npm run build`. Expected: the three target-server test files pass and TypeScript/Vite build exits 0.

### Task 3: Domain wording and final regression evidence

**Files:**
- Modify: `CONTEXT.md`
- Modify: `docs/frontend-redesign-spec.md`
- Modify: `docs/matchmaking-core-frontend-api.md`
- Create: `docs/aegis/work/2026-08-23-server-priority-nonnegative/50-evidence.md`

**Why this task exists:** 项目文档不能继续把已经废止的负数规则作为领域事实，避免后续实现重新开放该输入。

**Impact / Compatibility:** 只修正文档规则；不涉及运行时数据、migration 或调度排序。

**Verification:**
- `rg -n -i "允许负数|可以为负数|可为负数" CONTEXT.md docs/frontend-redesign-spec.md frontend/src src/L4d2MatchmakingCore`
- `git diff --check`
- 复跑 Core 目标服务器端点测试、前端表单测试和前端构建。

- [x] **Step 1: Update the domain/docs wording**

  Change `Server Priority` to “默认值为 0，必须为大于等于 0 的整数”，and update the frontend redesign and public API contracts from negative values to the same non-negative rule.

- [x] **Step 2: Run the complete scoped verification**

  Run the exact Core test, frontend focused test, frontend build, and `git diff --check` commands; record exit codes and test counts in `50-evidence.md`.

- [x] **Step 3: Review the diff and residual risk**

  Confirm only the approved files plus task records changed, no migration was added, the untracked UI brief from the base workspace was not copied, and no old fallback/adapter was introduced.

## Execution Status

- Task 1: completed; Core RED showed 2 failures, then the focused suite passed 13/13.
- Task 2: completed; frontend focused suite passed 6/6 and the production build passed.
- Task 3: completed; full Core suite passed 203/203 with 3 existing skips, full frontend suite passed 84/84, and `git diff --check` passed.
