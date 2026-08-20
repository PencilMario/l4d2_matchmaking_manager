# Baseline Read Set

- `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`：现有组合请求/响应契约。
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`：设置持久化、代理校验和密钥加密的 canonical owner。
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsEndpoints.cs`：现有认证路由。
- `src/L4d2MatchmakingCore/Data/Entities.cs`：`CoreSettings` 持久化列，保持兼容。
- `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs`：后端设置契约测试。
- `frontend/src/components/GlobalSettingsView.tsx`：当前部署入口的设置表单。
- `frontend/src/features/settings/SettingsWorkspace.tsx`：新版工作台设置表单。
- `frontend/src/api/core-client.ts`、`frontend/src/services/api.ts`：两套前端 API owner。
- `docs/matchmaking-core-frontend-api.md`：对外 Core API 契约。

## Facts

- `CoreSettings` 已有 `SteamProxyUrl` 与 `SteamWebApiKeyCiphertext` 两列。
- API Key 通过 `ISecretProtector` 加密保存，读取响应只返回 configured 状态。
- Agent 容器只在 `KeepVncAlive` 时读取代理设置并注入代理环境变量。

## Unknowns resolved by tests

- 新端点的独立响应形状和 clear 行为由新增测试固定。
