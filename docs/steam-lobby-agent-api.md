# Steam Lobby Agent 调用指南

本文描述当前已部署的单 Steam 账号 Lobby Agent 如何由核心控制服务调用。

## 当前能力与边界

- Agent 绑定一个已登录的 Steam Desktop 账号，且只提供只读健康检查。
- Agent 不启动 L4D2 客户端，不创建/加入/离开大厅，不执行 reservation，也不执行 RCON。
- 每次大厅或 reservation 的实际控制仍应由后续控制面定义新的受管接口；不要通过 SSH 直接调用容器内的 `SteamLobbyProbe` 作为常规业务 API。
- `/v1/probe/status` 不保存状态；每次请求都会运行一次新的只读 Probe。返回的 `observedAt` 是本次观测时间，不是缓存时间。

## 网络与鉴权

Compose 只把 Agent HTTP 服务发布到 Docker 主机回环地址 `127.0.0.1:8080`。它没有 HTTP 登录 token 或 API key 校验，也不得向第三方或公网暴露。

当前允许的调用方只有核心控制服务。若核心不与 Agent 位于同一主机，应由核心使用专用 SSH 主机密钥建立隧道；SSH 主机密钥、主机访问权限和回环绑定共同构成当前的访问控制边界。

```text
Core service
  |
  | SSH tunnel (dedicated key)
  v
Agent Docker host: 127.0.0.1:8080
  |
  v
steam-lobby-agent container
```

示例，在核心所在主机建立到某个 Agent 的本地隧道：

```sh
ssh -N -L 18080:127.0.0.1:8080 <agent-ssh-user>@<agent-host>
```

隧道存活期间，核心通过 `http://127.0.0.1:18080` 调用该 Agent。生产核心应维护每个 Agent 的独立连接、主机指纹校验、重连与超时；不要使用动态端口转发或把 `8080` 直接映射到公网。

未来项目级的“登录 token + API key”鉴权属于核心的管理/只读对外 API，不是当前本地 Agent HTTP 合约的一部分。

## HTTP 接口

### `GET /healthz`

仅表示 Agent HTTP 进程存活，不访问 Steam，也不会验证登录状态。

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json

{"status":"alive"}
```

核心可用它做快速存活探测，但不得把 `200` 视为该 Steam 账号可用。

### `GET /v1/probe/status`

执行一次新的只读 Steam Probe。该调用受 Agent 内部单一执行器串行化：同一账号的并发请求会排队，避免同时初始化多个 Steam API helper 进程。

Probe 依次确认：

1. Steam Desktop 进程存在。
2. `libsteam_api.so` 可加载且 Steam API 初始化成功。
3. 当前 AppID 为 `550`。
4. Steam 账号已登录。
5. Manual Dispatch 初始化成功，并完成一次 lobby list callback。

就绪响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "ready": true,
  "failure": null,
  "observedAt": "2026-08-16T11:29:03.7766781+00:00",
  "checks": {
    "steamDesktop": "ok",
    "steamApiInit": "ok",
    "appId": 550,
    "loggedOn": "ok",
    "manualDispatch": "ok",
    "lobbyListCallback": "ok",
    "lobbyCount": 50
  }
}
```

未就绪时仍返回相同 JSON 结构，但 HTTP 状态为 `503 Service Unavailable`，`ready` 为 `false`。响应不会包含 Steam 凭据、Steam Guard 数据、native library 路径、Probe 原始输出或大厅 metadata。

## 失败处理

核心应按 HTTP 状态和 `failure` 处理，不应依赖错误消息文本。当前稳定失败码如下：

| 失败码 | 含义 | 核心动作 |
| --- | --- | --- |
| `steam_desktop_unavailable` | Steam Desktop 进程不存在。 | 将 Agent 标记为不可调度，等待其恢复。 |
| `steam_api_library_not_configured` | 未设置 Steam API 库路径。 | 需要 Agent 管理员修复部署配置。 |
| `steam_api_load_failed` | Steam API 库不能加载。 | 停止调度；检查共享 Steam 内容和库兼容性。 |
| `steam_api_init_failed` | Steam API 初始化失败。 | 停止调度，确认 Steam Desktop 与登录数据。 |
| `appid_mismatch` | Steam API 返回的 AppID 不是 550。 | 停止调度，检查 Steam 环境。 |
| `steam_not_logged_on` | Steam Desktop 未登录或当前账号无有效会话。 | 等待或人工完成 Steam 登录/Steam Guard。 |
| `lobby_list_request_failed` | Steam 未接受 lobby list 请求。 | 短暂退避后重试。 |
| `lobby_list_callback_failed` | lobby list API call 回调失败。 | 短暂退避后重试；连续失败则下线 Agent。 |
| `lobby_list_timeout` | lobby list callback 未在 Probe 时间限制内返回。 | 短暂退避后重试；不要并发重试。 |
| `probe_execution_timeout` | Agent 启动的 Probe 进程超时。 | 短暂退避后重试；连续失败则下线 Agent。 |
| `probe_execution_failed` | Probe 进程无法启动。 | 停止调度，检查 Agent 镜像和配置。 |
| `probe_output_invalid` | Probe 输出未通过 Agent 的严格 JSON 校验。 | 停止调度，视为部署版本不兼容。 |
| `probe_check_failed` | Probe 返回了未映射的失败状态。 | 停止调度并收集 Agent 日志。 |

建议策略：HTTP `503` 使用有限次数的指数退避重试；若失败码属于部署、登录或版本问题，则不要对同一 Agent 高频重试。HTTP 连接失败和 `5xx` 表示 Agent/隧道故障，应与 Steam 检查失败分开计数。

## 核心调用示例

```sh
curl --fail-with-body --max-time 15 \
  http://127.0.0.1:18080/v1/probe/status
```

核心只有在响应为 HTTP `200`、`ready=true`、`checks.appId=550` 且 `checks.loggedOn="ok"` 时，才可将此账号视为可调度。`lobbyCount` 只是本次 Steam lobby list callback 的返回数量，不能表示该账号持有的大厅数量，也不能表示暖服状态。

## 部署不变量

- 每个 Steam 账号必须拥有独立的 `steam-data-<agent>` volume，保存登录、Steam Guard、userdata 和客户端配置。
- 所有 Agent 共享同一受控 Steam library 挂载（`/mnt/steam-library`），该目录不保存账号登录凭据。
- 共享 library 的安装、更新、校验与卸载必须串行，不能由多个 Agent 同时执行。
- 停止服务用 `docker compose down`；除非明确废弃账号登录数据，否则不得附加 `-v`。

部署与首次登录细节见 `deploy/steam-lobby-agent/README.md`。
