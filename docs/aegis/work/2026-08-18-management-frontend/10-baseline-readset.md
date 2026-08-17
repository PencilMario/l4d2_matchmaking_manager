# Baseline Read Set

## 权威文档

- `docs/aegis/specs/2026-08-18-management-frontend-design.md`：已确认的桌面视觉、页面、Observation 契约和验收边界。
- `docs/matchmaking-core-api.md`：Core 部署、鉴权、服务器/Agent/Lobby/暖服 API 与安全边界。
- `docs/matchmaking-core-frontend-api.md`：浏览器请求约定、轮询策略、响应模型和失败处理。
- `CONTEXT.md`：Target Server、Warm-up Agent、Reservation Lobby 等领域术语。

## 实现基线

- `src/L4d2MatchmakingCore/A2s/A2sServerInfo.cs` 当前只有 `PlayerCount` 和 `ObservedAt`。
- `src/L4d2MatchmakingCore/A2s/SourceA2sClient.cs` 已处理 A2S challenge，解析四个 C-string 并读取当前人数；解析器是 A2S 字段的唯一 owner。
- `src/L4d2MatchmakingCore/tests/SourceA2sClientTests.cs` 使用本地 UDP fake server，已有 challenge 往返断言。
- `src/L4d2MatchmakingCore/Program.cs` 以 singleton 注册 `ISourceA2sClient`，只在非 Testing 环境启动 `WarmupSchedulerBackgroundService`。
- `src/L4d2MatchmakingCore/Servers/TargetServerEndpoints.cs` 使用 `/v1/servers` route group，`/{serverId:guid}` 已存在；字面量 `observations` 必须在该参数路由前映射。
- `src/L4d2MatchmakingCore/tests` 使用 MSTest、`WebApplicationFactory`、EF InMemory 和 Testcontainers PostgreSQL。
- 仓库没有 `package.json`、Vite 配置或 React 前端；前端需创建为根目录 `frontend/`，不加入 `.sln`。

## 事实、假设与未知

### 事实

- Core API 只绑定回环地址，浏览器应经同源反向代理或 BFF 请求。
- `/v1/warmups` 是数据库快照且不触发 Agent/A2S；写操作成功后前端必须刷新列表。
- `rconPassword` 只写、不可回填；`quarantined` Agent 不应自动复用。

### 假设

- 桌面部署会由现有反向代理将静态 `frontend/dist` 与 `/v1`、`/healthz` 同源提供。
- 前端采用 npm + Vite；React Bits 组件按固定 revision 复制到源码，而非依赖未发布的运行时包。

### 未知

- 生产反向代理的具体配置文件尚未在仓库中定义；本计划只提供开发代理和静态产物说明。
- 真实目标服务器的 A2S 服名编码可能包含非 UTF-8 字节；解析测试先锁定 UTF-8/ASCII 正常包，异常字节按既有 invalid/truncated 失败路径处理。
