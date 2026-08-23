# Baseline Read Set

- `src/L4d2MatchmakingCore/Agents/WarmupAgentService.cs`: Agent 更新 owner；当前只保存字段。
- `src/L4d2MatchmakingCore/Agents/AgentControlClient.cs`: Core 到 Agent 的内部 HTTP client。
- `src/L4d2LobbyAgent/Program.cs`: Agent 内部路由注册；已有 `/v1/steam/restart`。
- `src/L4d2LobbyAgent/Steam/SteamDesktopController.cs`: Supervisor Steam 重启实现。
- `deploy/steam-lobby-agent/91-enable-steam-supervisor.sh`: 账号级 Millennium 目标文件路径与初始化写入。
- `deploy/steam-lobby-agent/millennium/steam-region-bridge/backend/main.lua`: Bridge 目标读取与默认值回退逻辑。
- `src/L4d2MatchmakingCore/tests/WarmupAgentEndpointTests.cs`: Core Agent 更新集成测试。
- `src/L4d2LobbyAgent/tests/AgentLobbyEndpointTests.cs`: Agent 内部路由测试。
- `src/L4d2MatchmakingCore/tests/AgentControlClientTests.cs`: Core 内部 HTTP client 测试。
- `docs/steam-lobby-agent-api.md`, `docs/matchmaking-core-api.md`, `docs/matchmaking-core-frontend-api.md`: API 行为说明。

## Facts

- 运行中的 Core Agent 状态为 `running` 时才有可用的 Agent HTTP 控制路径。
- 既有 `/v1/steam/restart` 调用 `supervisorctl restart steam`。
- 账号私有目标文件为 `~/.config/millennium/steam-region-bridge-region`。

## Assumptions

- Agent 进程用户拥有 Millennium 配置目录写权限。
- 空文件/空白内容代表使用 Steam 默认区域。
