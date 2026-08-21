# Steam Lobby Agent 内部 API

该 API 只供同一 Docker 主机上的 Core 使用。Core 通过专用 Docker 网络中的
`http://l4d-agent-<agentIdN>:8080` 访问，不经 SSH、`docker exec` 或宿主机
端口转发。Core 创建的 Agent 不发布 8080；首次登录的 noVNC 端口才会按需绑定至
宿主机回环地址。

Agent 没有 HTTP token。Docker 网络隔离、Core 唯一的 Docker socket 挂载和受管容器
标签构成访问控制边界，因此不得将 Agent 端口发布到公网或其他不受信任网络。

## 会话模型

一个 Agent 对应一个 Steam 账号，并由一个持久的 Steam session actor 独占 Steam API。
健康检查、操作、已持有大厅的刷新和任意 lobby 查询都排入同一 Manual Dispatch 命令循环；
活动操作期间不会启动第二个 Probe 进程。任意 lobby 查询先等待匹配的
`LobbyDataUpdate_t` 成功回调，绝不把尚未确认的 Steam 缓存当作成功快照。每个新操作由
Agent 使用从真实 AppID 500 Lobby 捕获的 L4D `Farm` profile，Core 不传入或拼接 campaign metadata。

Steam API 标识符均作为十进制字符串传输，避免 JavaScript 数值精度丢失。

## 路由

### `GET /healthz`

仅说明 Agent HTTP 进程存活，不读取 Steam。

```json
{"status":"alive"}
```

### `GET /v1/probe/status`

从同一 actor 读取当前 Steam 会话状态。就绪时返回 `200`，未就绪时返回 `503`；两种
响应具有相同结构。

```json
{
  "ready": true,
  "failure": null,
  "observedAt": "2026-08-17T12:00:00+00:00",
  "checks": {
    "steamDesktop": "ok",
    "steamApiInit": "ok",
    "appId": 500,
    "loggedOn": "ok",
    "manualDispatch": "ok",
    "lobbyListCallback": "ok",
    "lobbyCount": 50
  }
}
```

Core 只在 `200`、`ready=true` 时调度该 Agent。稳定的 `failure` 包括
`steam_desktop_unavailable`、`steam_api_load_failed`、`steam_api_init_failed`、
`appid_mismatch`、`steam_not_logged_on`、`lobby_list_request_failed`、
`lobby_list_callback_failed` 和 `lobby_list_timeout`。响应不会包含 Steam 凭据、Steam
Guard 数据、库路径或原始 Steam 输出。

### `POST /v1/operations`

创建或恢复一个由 `operationId` 幂等标识的大厅操作。

```json
{
  "operationId": "2b5baaf4-d85d-4a7d-a515-3bf87ff32036",
  "mode": "standard",
  "ipv4Address": "203.0.113.7",
  "port": 27015,
  "gameMode": "coop",
  "rconPassword": null
}
```

`mode` 支持 `standard` 和 `reserved`。reserved 路径保留可用，但尚未在 AppID 500 服务器上完成真机验证。
`gameMode` 为可选的 `coop`、`versus` 字符串，省略时默认 `coop`。`coop` 使用 4 个槽位，`versus` 使用 8 个槽位。
首次请求返回 `202 Accepted`，同一
`operationId` 的重试返回 `200 OK` 且不会新建大厅。响应中的操作快照格式为：

```json
{
  "operationId": "2b5baaf4-d85d-4a7d-a515-3bf87ff32036",
  "state": "active",
  "lobby": {
    "lobbyId": "109775242170052468",
    "ownerSteamId": "76561198000000000",
    "members": [{"steamId":"76561198000000000","personaName":"Agent"}],
    "metadata": {"Game:campaign":"Farm", "Game:mode":"coop", "Game:state":"lobby"},
    "observedAt": "2026-08-17T12:00:00+00:00",
    "memberDataStatus": "complete"
  },
  "failure": null,
  "observedAt": "2026-08-17T12:00:00+00:00"
}
```

### `GET /v1/operations/{operationId}`

读取受管操作快照。存在时返回 `200`，未知 ID 返回 `404`。

### `DELETE /v1/operations/{operationId}`

请求 actor 离开操作持有的 lobby。完成时返回 `204`，未知 ID 返回 `404`。Core 对
停止调用失败会将 Agent 隔离，不会立即把该账号或预留目标复用于其他操作。

### `GET /v1/lobbies/{lobbyId}`

通过同一个 Steam actor 读取任意非零十进制 lobby ID 的 owner、成员、metadata 和
观测时间。`includeMembers` 是 Core 专用的可选查询参数，默认 `true`。允许读取成员时，
空闲 Agent 或正在执行 `standard` 暖服的 Agent 会临时加入目标 lobby、读取成员并在
`finally` 中离开；返回的 `members` 始终移除了该查询 Agent 自己。预留或不确定操作不
会临时加入目标 lobby。非法或零 ID 返回 `400`。

`memberDataStatus` 的值为：

| 值 | 含义 |
| --- | --- |
| `complete` | 已确认 metadata，且已读取成员；`members` 不含查询 Agent。 |
| `metadata_only_no_query_agent` | Core 请求 metadata-only 查询，未尝试加入。 |
| `metadata_only_join_denied` | Steam 拒绝加入，例如目标大厅已满或不可加入。 |
| `metadata_only_join_timeout` | Steam 未在截止时间内完成加入。 |
| `metadata_only_agent_state_changed` | actor 执行时发现自身已处于不允许临时加入的操作状态。 |

metadata 确认失败返回 `503 "lobby_data_unavailable"`。临时查询离开后未能保持原
`standard` 暖服大厅返回 `503 "lobby_operation_preservation_failed"`；Core 必须隔离该
Agent，不能继续使用该暖服操作。

## 运行约束

- 仅 Core 创建的容器使用共享 `/mnt/steam-library`；每个账号的数据和 Steam Guard
  位于独立 Docker volume。
- 每次新建 Agent 都收到 `ENABLE_STEAM=true`、`STEAM_SHARED_LIBRARY_PATH` 和
  `STEAM_API_LIBRARY_PATH`。下载地区是可选的账号级环境配置。
- 首次登录使用 noVNC。发现 `loginusers.vdf` 的 `MostRecent=1` 后，Steam 以
  `-silent -no-browser` 启动；Core 管理的 Agent 可由
  `CORE_AGENT_STEAM_LOGIN_UI_MODE=always` 强制恢复可视 UI 后重建。
- Agent 返回的 reservation 成功表示其 Steam reservation 操作和 lobby 观察成功，
  或在 UDP 超时后由同一游戏端口的 Source RCON `status` 确认当前 reservation cookie。
- `rconPassword` 只允许 Core 通过 Docker 内网在创建预留操作时发送；Agent 不将其写入
  操作快照、日志或 HTTP 响应，也不对非超时结果执行 RCON。
