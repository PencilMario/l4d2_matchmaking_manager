# Baseline Read Set

- `CONTEXT.md`: 当前领域术语把 Server Priority 定义为允许负数，需要同步修正。
- `src/L4d2MatchmakingCore/Servers/TargetServerService.cs`: 创建和更新共用的 `Normalize` 配置入口，是后端校验的唯一现有 owner。
- `src/L4d2MatchmakingCore/Servers/TargetServerEndpoints.cs`: 将 `ArgumentException` 映射为 `400 Bad Request`。
- `src/L4d2MatchmakingCore/Servers/TargetServerDtos.cs`: `Priority` 为可空请求字段，空值默认行为需要保留。
- `src/L4d2MatchmakingCore/tests/TargetServerEndpointTests.cs`: Core HTTP 测试夹具和目标服务器创建/更新回归测试。
- `frontend/src/features/servers/TargetServerForm.tsx`: 优先级输入和客户端提交校验 owner。
- `frontend/src/features/servers/TargetServerForm.test.tsx`: 前端表单测试文件（如不存在则沿用目标服务器组件的现有测试位置）。
- `frontend/package.json` 和 `frontend/vite.config.ts`: Vitest 与构建命令。

## Facts

- 现有后端 `Normalize` 只校验并发、时限和玩家目标，不校验优先级。
- 现有前端提示“可以为负数”，且只校验整数。
- `TargetServer.Priority` 和数据库列类型保持 `int`，无需 schema 变更。

## Assumptions

- 现有 Core 测试环境可用 InMemory 数据库运行 HTTP 测试。
- 前端表单测试可通过 `TargetServerForm` 的公开提交行为验证客户端规则。
