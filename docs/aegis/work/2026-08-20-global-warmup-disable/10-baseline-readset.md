# Baseline Read Set

- `CONTEXT.md`：Warm-up Agent、Target Server、Warmup Attempt 的项目术语。
- `docs/matchmaking-core-frontend-api.md`：前端可见路由、响应形状与 5 秒刷新约定。
- `docs/frontend-redesign-spec.md`：设置页、危险操作确认、中文文案与兼容边界。
- `src/L4d2MatchmakingCore/Data/Entities.cs`：`CoreSettings`、`WarmupAttempt`、`ReservationLease` 持久化模型。
- `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`：EF 模型默认值。
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`、`GlobalSettingsEndpoints.cs`、`GlobalSettingsDtos.cs`：全局设置 canonical owner 与认证路由。
- `src/L4d2MatchmakingCore/Scheduling/WarmupAttemptDrainService.cs`：既有 agent/target 任务排空与失败隔离语义。
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`：调度、任务完成后的 Steam 重启与 agent 恢复逻辑。
- `src/L4d2MatchmakingCore/Scheduling/SharedLibraryMaintenanceService.cs`：调度前已有维护锁检查。
- `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs`、`WarmupAttemptDrainServiceTests.cs`、`WarmupSchedulerServiceTests.cs`：后端测试夹具与失败保留约定。
- `frontend/src/services/api.ts`、`frontend/src/api/core-client.ts`、`frontend/src/api/models.ts`：两套前端 API owner。
- `frontend/src/components/GlobalSettingsView.tsx`、`frontend/src/features/settings/SettingsWorkspace.tsx`：两套设置页。
- `frontend/src/features/workspace/WorkspaceShell.tsx`、`frontend/src/App.tsx`：两套设置页 wiring。

## Baseline evidence

- `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter "FullyQualifiedName~GlobalSettingsEndpointTests|FullyQualifiedName~WarmupAttemptDrainServiceTests|FullyQualifiedName~WarmupSchedulerServiceTests" --no-restore`: 33 passed, 0 failed。
- `npm test -- --run src/components/GlobalSettingsView.test.tsx src/features/settings/SettingsWorkspace.test.tsx src/api/core-client.test.ts`: 11 passed, 0 failed。

## Current worktree constraint

`src/L4d2MatchmakingCore/tests/WarmupSchedulerServiceTests.cs` 已有用户修改：将 Steam restart 失败测试断言为 `restarting`。本任务只在必要位置追加测试，不撤销该修改。
