# Baseline Read Set

## Facts

- `WarmupAgentService.StopCoreAsync` 当前直接关闭 VNC、调用容器 stop，再写入
  `stopped`，没有调用任务排出服务。
- `WarmupAgentService.RecreateCoreAsync` 已调用 `DrainAgentAsync`，失败时抛出
  `warmup_agent_recreate_drain_failed`。
- `WarmupAttemptDrainService.DrainAgentAsync` 会停止该 Agent 的
  `active`/`uncertain` operation，成功后完成 attempt 并释放匹配的 reservation
  lease；停止异常时保留 attempt 并隔离 Agent。
- `WarmupAgentEndpoints.StopAsync` 当前使用通用 `MutateAsync`，没有把排出失败转换
  为 `409`。

## Authority and compatibility references

- `docs/aegis/specs/2026-08-24-stop-warmup-agent-drain-design.md`
- `CONTEXT.md` 中 Warm-up Agent、Warm-up Attempt、Core Controller 的术语定义。
- `docs/matchmaking-core-api.md` 与 `docs/matchmaking-core-frontend-api.md` 的 Agent
  生命周期 API 约定。
- `src/L4d2MatchmakingCore/Scheduling/WarmupAttemptDrainService.cs` 既有排出实现。

## Verification baseline

在隔离工作树执行：

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore
```

当前基线命令退出码为 `0`。
