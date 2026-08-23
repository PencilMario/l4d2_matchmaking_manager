# 玩家进入统计基线读取集

## 权威与术语

- `CONTEXT.md`：Core Controller、Warm-up Agent、Target Server 和 Reservation Lobby 的项目术语。
- `docs/aegis/specs/2026-08-23-player-entry-statistics-design.md`：本功能的口径、边界和验收标准。

## 生产边界

- `src/L4d2Matchmaking.Contracts/AgentContracts.cs`：Agent 操作请求兼容边界。
- `research/SteamLobbyProbe/ActiveLobbyJoinDataResponder.cs`、`SteamNativeRuntime.cs`：ReplyJoinData 生成和发送时机。
- `src/L4d2MatchmakingCore/Data/Entities.cs`、`MatchmakingDbContext.cs`：事件实体与 PostgreSQL 模型。
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`：操作上下文快照来源。
- `src/L4d2MatchmakingCore/Program.cs`：认证 scheme、端点和 hosted service 挂载。
- `frontend/src/App.tsx`、`components/Sidebar.tsx`、`services/api.ts`：现用管理页挂载与 API 约定。

## 验证基线

- `.NET` 现有测试在隔离 worktree 进行，使用 `--no-restore`。
- 前端现有测试和 production build 已在功能实现前通过。
- PostgreSQL 集成测试遵循 `PostgresPersistenceTests` 的 Testcontainers 模式；Docker 不可用时报告跳过原因，不伪造通过证据。
