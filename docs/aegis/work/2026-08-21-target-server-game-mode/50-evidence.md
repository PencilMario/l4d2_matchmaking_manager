# 最终验证证据

验证时间：2026-08-21（Asia/Hong_Kong）。以下命令均在当前工作区新鲜执行，未依赖之前会话的测试输出。

## 验证命令与结果

| Claim checked | Command | Result |
| --- | --- | --- |
| Core 目标服务器 API 回归 | `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~TargetServerEndpointTests --logger "console;verbosity=minimal"` | exit 0；11 passed、0 skipped、0 failed |
| Core 全量回归 | `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | exit 0；143 passed、3 skipped、0 failed |
| Protocol metadata | `dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | exit 0；13 passed、0 skipped、0 failed |
| Steam lobby probe | `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | exit 0；15 passed、0 skipped、0 failed |
| Lobby Agent | `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | exit 0；20 passed、0 skipped、0 failed |
| Contracts | `dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj --no-restore --logger "console;verbosity=minimal"` | exit 0；2 passed、0 skipped、0 failed |
| 前端目标表单 | `npm test -- --run src/features/servers/TargetServerForm.test.tsx` | exit 0；1 file、2 tests passed |
| 前端全量测试 | `npm test -- --run` | exit 0；18 files、53 tests passed |
| 前端生产构建 | `npm run build` | exit 0；`tsc -b` 与 Vite build 均通过 |
| 解决方案构建 | `dotnet build L4d2MatchmakingManager.sln --no-restore` | exit 0；0 warnings、0 errors |
| 解决方案测试 | `dotnet test L4d2MatchmakingManager.sln --no-build --no-restore --logger "console;verbosity=minimal"` | exit 0；所有项目 0 failures（Core 143/3 skipped） |
| 工作区空白检查 | `git diff --check` | exit 0；无 whitespace error |

## 验收覆盖

- Core 仅接受 `coop`、`versus`、空白/`null`；空白规范化为 `null`，非法值为 `400 invalid_game_mode`。
- 旧记录和未指定模式保持 `versus` metadata；`coop` 覆盖 `Members:numSlots=4`、`Game:Mode=coop`、`Game:sk_versus=19`。
- Scheduler 和 HTTP Agent 请求携带可选 `gameMode`；reservation settings 二进制编码仍走原有 versus 默认。
- active App 与 workspace 表单均为受限 `<select>`，空选提交 `null`，详情显示未指定/预设值。

## Side effects / Remove-restore

- 未使用临时 instrumentation、fixture 或运行时 guard；无需恢复。
- 未执行 commit、push、数据库写入、容器操作或 Steam lobby 创建。
- 工作区中以下已有部署脚本改动保持原样，未纳入本功能判断：`deploy/steam-lobby-agent/Test-SteamStatusLauncher.ps1`、`l4d2-steam-status.desktop`、`l4d2-steam-status.sh`。

## Residual risk and authority boundary

- 未验证：真实 Steam API/native runtime 在已登录账号上的 lobby 创建；PostgreSQL 生产实例实际应用 `202608210001_TargetServerGameMode` migration。
- 未验证：生产反向代理下 active App 的浏览器端到端旅程；组件测试和 TypeScript/Vite build 已覆盖表单行为与编译。
- Confidence：B（核心数据流有直接测试和全量回归，真实外部运行时/生产迁移仍属环境风险）。这是 verified evidence，不是生产环境 authoritative completion signal。

## Evidence boundary

- 使用：原子测试输出、全量测试输出、构建输出、`git diff --check`、当前 diff/status。
- 未加载：完整历史会话、完整部署日志、生产 Steam/数据库日志。
- 后续证据：部署前在 staging 应用 migration，并用已登录 Steam Agent 创建一次 `coop` 与一次默认大厅，读取 metadata 三键确认。
