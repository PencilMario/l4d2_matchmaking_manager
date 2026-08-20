# Verification Evidence

## TodoCheckpointDraft

- Current todo: 完成远端 readiness、区域值、VNC 与本地契约复核。
- Completed todos: 根因定位、代码修复、bundle 生成、远端部署、远端验收。
- Active slice: 证据归档。
- Blocked-on: 无。
- Next: 向用户报告结果。

## Evidence Bundle

### Local checks

- `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-MillenniumRegionBridgeContract.ps1`：exit 0。
- `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-ComposeContract.ps1`：exit 0。
- `node --check deploy/steam-lobby-agent/millennium/steam-region-bridge/.millennium/Dist/index.js`：exit 0。
- 远端容器执行 `bash -n /etc/cont-init.d/91-enable-steam-supervisor.sh`：exit 0。

### Remote checks

Host: `sirp@100.72.137.92`。Agent ID:
`aa75055a-fa44-40e0-bdc1-3a68a735396c`。

- Agent 容器内绕过代理请求 `/healthz` 和 `/v1/probe/status`：均为 HTTP `200`。
- 最新 probe：`ready=true`、`failure=null`、`currentDownloadRegion=197`。
- 账号目标文件 `~/.config/millennium/steam-region-bridge-region`：`197`。
- Steam 实际状态文件 `~/.steam/steam/config/steam-download-region`：`197`。
- Core `GET /v1/agents`：HTTP `200`，该 Agent `status=running`、`ready=true`、`downloadRegion=197`。
- Core `GET /v1/agents/{id}`：HTTP `200`，`ready=false` 是当前 `GetAsync` 未执行实时 probe 的既有语义；列表接口才执行实时 readiness probe。
- 部署 bundle SHA-256：`592b168bd886e38fed3c2a9f4d1c90446a2a2928622cfc748f8e5e05987307b9`。
- 部署 backend SHA-256：`96cf602c3eebfe369163480eef4304b8e9a4fc14720c4693b843589047fa7a13`。
- VNC 主流程：打开会话 HTTP `201`，页面 HTTP `200`、`15401` bytes，关闭会话 HTTP `204`。

### Proxy diagnostic boundary

Agent 容器继承了 tailnet HTTP proxy。普通 `curl` 访问容器内 loopback API 会得到代理
返回的 `502`；使用 `--noproxy "*"` 后得到上述真实 Agent 响应。该现象是诊断路径的代理
误用，不是 Agent HTTP 或 Steam readiness 故障。

## ImpactStatementDraft

修复影响当前 Steam Region Bridge 的配置读取、区域写入确认和状态持久化；Core 生命周期、
VNC 会话、账号卷和既有环境变量兼容回退保持不变。

## DriftCheckDraft

- Scope: 保持在 Agent 插件、初始化脚本、镜像部署和验证记录内。
- Compatibility: 保留 `STEAM_DOWNLOAD_REGION` 兼容回退；未修改 Core 生命周期接口。
- Retirement: 旧环境变量仅保留兼容用途；账号专属 Millennium 配置文件是当前目标的 canonical owner。
- Decision: continue to final report。

## Residual Risk

- 未在本机运行 `bash -n`，但远端部署实际由 Linux 初始化脚本启动并完成了 Steam/Millennium/Agent 验收。
- Core detail 接口仍不显示实时 readiness；若前端改为依赖 detail 结果，应让该接口显式执行 probe，或继续使用列表接口的实时字段。
