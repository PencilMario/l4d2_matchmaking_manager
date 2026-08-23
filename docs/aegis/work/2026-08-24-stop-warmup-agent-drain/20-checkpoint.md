# Todo Checkpoint Draft

## Current slice

已完成停止节点的排出门禁、冲突映射和 endpoint 回归测试；正在做文档与最终验证。

## Completed

- 完成并提交设计记录：`30b4b9a`。
- 在失败测试中确认旧实现会提前停止容器并保留任务。
- `StopCoreAsync` 现在先调用 `DrainAgentAsync`，排出失败返回专用 `409`。
- `quarantined` 节点的重试停止路径已覆盖并可在排出成功后停止容器。
- 聚焦 Agent endpoint 测试：17/17 通过。
- Core 测试项目：205 通过、3 个既有 PostgreSQL 环境测试跳过。
- Solution 测试：7 + 13 + 24 + 25 + 205，通过；Core 仍有 3 个既有环境测试跳过。

## Evidence refs

- `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs`
- `src/L4d2MatchmakingCore/Agents/WarmupAgentEndpoints.cs`
- `src/L4d2MatchmakingCore/tests/WarmupAgentEndpointTests.cs`
- `docs/matchmaking-core-api.md`
- `docs/matchmaking-core-frontend-api.md`

## Drift check

- Scope: 仍只修改 Agent 停止生命周期、对应 API 文档和 Core 集成测试。
- Compatibility: 保留 API 路径/DTO/数据库结构；失败不停止容器，成功才完成任务。
- Retirement: 未增加并行排出实现，继续使用 `WarmupAttemptDrainService`。
- Decision: `continue`。

## Next

复核完整 diff，运行最终 `git diff --check`，记录证据，提交实现分支并合回当前 `main`。
