# 目标服务器大厅模式 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为目标服务器增加可选 `coop`/`versus` 大厅模式，并让暖服 Agent 按模式写入 lobby metadata。

**Architecture:** Core 将 nullable `TargetServer.GameMode` 作为受限配置保存，并在调度时传给 Agent；Agent 的 Steam runtime 将模式交给 `RealSessionSettings`，仅在 `coop` 时覆盖三个 metadata 键。前端的 active modal 与并行 workspace form 都使用受限下拉框。

**Tech Stack:** .NET 10/8, C#, EF Core/Npgsql, MSTest, React 19, TypeScript, Vitest.

**Baseline / Authority Refs:** `docs/aegis/work/2026-08-21-target-server-game-mode/00-intent.md`, `10-baseline-readset.md`, `20-spec.md`; `docs/matchmaking-core-api.md`; `docs/matchmaking-core-frontend-api.md`; `docs/steam-lobby-agent-api.md`.

**Compatibility Boundary:** 新列 nullable；空值与旧 Agent 请求继续生成 versus metadata；现有 DTO positional constructors 保持可编译；reservation settings 编码不变；只修改目标服务器模式相关代码与文档/测试。

**Verification:** 先运行新增 Core/Protocol/Frontend 测试确认旧实现红灯，再实现最小代码；运行目标测试、Core/Protocol 全部测试、frontend Vitest/build、解决方案 build 和 `git diff --check`。

---

### Task 1: Core target-server mode contract and persistence

**Files:**
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- Modify: `src/L4d2MatchmakingCore/Servers/TargetServerDtos.cs`
- Modify: `src/L4d2MatchmakingCore/Servers/TargetServerService.cs`
- Create: `src/L4d2MatchmakingCore/Data/Migrations/202608210001_TargetServerGameMode.cs`
- Test: `src/L4d2MatchmakingCore/tests/TargetServerEndpointTests.cs`

**Why this task exists:** 目标服务器必须持久化受限模式，且旧记录/未填写模式不改变当前行为。

**Impact / Compatibility:** nullable `GameMode` 列，响应增加 nullable `gameMode`；`Normalize` 是唯一校验 owner。

**Repair Track:** 将模式字段接入实体、DTO、规范化和响应，并为现有表添加 nullable migration。

**Retirement Track:** 不删除旧 versus 默认生成逻辑；空模式继续由 Agent metadata owner 处理，避免数据库回填和重复默认分支。

**Verification:** `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter FullyQualifiedName~TargetServerEndpointTests --no-restore`；新增测试先在旧代码上失败。

- [x] 写失败测试：创建 `coop` 返回 `gameMode=coop`；空值返回 null；`deathmatch` 返回 400 `invalid_game_mode`；更新可切换到 coop。
- [x] 运行目标测试确认旧实现因缺少字段/校验而失败。
- [x] 增加 `TargetServer.GameMode`, DTO `GameMode`, EF max length 16 nullable column, migration。
- [x] 在 `Normalize` 中 trim 空白并只接受 null/`coop`/`versus`。
- [x] 运行目标测试确认通过。

### Task 2: Agent operation propagation and metadata generation

**Files:**
- Modify: `src/L4d2Matchmaking.Contracts/AgentContracts.cs`
- Modify: `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`
- Modify: `src/L4d2LobbyAgent/Steam/SteamNativeRuntime.cs`
- Modify: `research/L4d2Protocol/RealSessionSettings.cs`
- Test: `src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs`
- Test: `research/L4d2Protocol/tests/CampaignProfileTests.cs`

**Why this task exists:** 只有将模式从配置边界传到 lobby metadata owner，用户选择才会影响暖服大厅。

**Impact / Compatibility:** 新请求保留原 positional constructor，并增加 nullable init-only `GameMode` 属性；空值/versus 不改变 metadata；`coop` 只覆盖指定三项。

**Repair Track:** Scheduler 填充 `AgentOperationRequest.GameMode`；Protocol 为 metadata 提供带模式的 overload；Native runtime 使用该 overload。

**Retirement Track:** 保留 `CreateReservationSettings` 的 versus 默认路径；不在 Core 拼接 metadata，也不删除旧的 parameterless metadata API。

**Verification:** 新增 scheduler 请求断言和 protocol metadata 断言先红后绿；随后运行 Core/Protocol 测试项目。

- [x] 写失败测试：scheduler 的 fake Agent 收到 `coop`；protocol 的 coop metadata 为 `4/coop/19`，versus/空值为 `8/versus/35`。
- [x] 运行目标测试确认旧实现失败。
- [x] 为 `AgentOperationRequest` 增加 nullable `GameMode`，scheduler 传入 `plan.Server.GameMode`。
- [x] 为 `CreateLobbyMetadata` 增加可选模式参数，coop 覆盖三个 metadata 键；`SteamNativeRuntime.CreateLobby` 使用请求模式。
- [x] 运行目标测试和相关全量测试确认通过。

### Task 3: Frontend preset selection and API models

**Files:**
- Modify: `frontend/src/types/index.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/components/TargetServerModal.tsx`
- Modify: `frontend/src/components/TargetServerDrawer.tsx`
- Modify: `frontend/src/api/models.ts`
- Modify: `frontend/src/features/servers/TargetServerForm.tsx`
- Modify: `frontend/src/features/servers/TargetServerDrawer.tsx`
- Test: `frontend/src/features/servers/TargetServerForm.test.tsx`

**Why this task exists:** 用户需要在新增/编辑目标服务器时选择预设模式，且不能自由输入。

**Impact / Compatibility:** active App 和并行 workspace 的模型、表单、详情保持同一 `GameMode` 类型；未指定提交 null。

**Repair Track:** 增加 select options、回填和 payload 映射；详情显示模式。

**Retirement Track:** 不保留可自由编辑的文本输入；旧表单字段仍保留，其余 API 适配不变。

**Verification:** `npm test -- --run src/features/servers/TargetServerForm.test.tsx`; `npm run build`。

- [x] 写失败组件测试：选择 `coop` 提交 `gameMode='coop'`；默认提交 `gameMode=null`；选项集合不含文本输入。
- [x] 运行 Vitest 确认旧表单失败。
- [x] 增加 `GameMode` 类型、两个 API model 字段、active API `serverInput` 映射。
- [x] 在两个表单加入受限 select，并在详情展示值；更新禁用 payload 包含当前 gameMode。
- [x] 运行前端目标测试和 build。

### Task 4: API documentation and verification evidence

**Files:**
- Modify: `docs/matchmaking-core-api.md`
- Modify: `docs/matchmaking-core-frontend-api.md`
- Modify: `docs/steam-lobby-agent-api.md`
- Modify: `docs/aegis/INDEX.md`
- Create: `docs/aegis/work/2026-08-21-target-server-game-mode/40-atomic-tasks.md`
- Create: `docs/aegis/work/2026-08-21-target-server-game-mode/50-evidence.md`

**Why this task exists:** API consumers和后续部署需要知道新 nullable 字段、允许值和 metadata 变化。

**Verification:** `dotnet test ...`; `npm test -- --run`; `npm run build`; `dotnet build L4d2MatchmakingManager.sln --no-restore`; `git diff --check`；将实际输出摘要写入 evidence。

- [x] 更新三份 API 文档示例和字段表，明确空值兼容与 coop 三项 metadata。
- [x] 记录原子任务、测试命令、退出码、覆盖范围和未验证的真实 Steam runtime 风险。
- [x] 复查 `git diff`，确认不触碰当前已有无关改动。
