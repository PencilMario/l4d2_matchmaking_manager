# Matchmaking Core 运维与 API

Core 是单 Docker 主机上的 L4D2 暖服控制面。它管理 PostgreSQL 配置、创建带独立账号卷
的 Agent 容器、以 A2S 和 Agent 实时观察执行调度，并代理任意 Steam lobby 的只读查询。
Core API 与动态 noVNC 端口都只绑定宿主机 `127.0.0.1`。

面向浏览器管理端的完整请求/响应契约见
[matchmaking-core-frontend-api.md](matchmaking-core-frontend-api.md)。

## 部署

先从仓库根目录构建 Agent 镜像：

```sh
docker build -f deploy/steam-lobby-agent/Dockerfile -t l4d2-steam-lobby-agent:local .
```

创建由所有 Agent 共享的宿主机游戏库目录，并创建只含 Core Bearer token 的文件：

```sh
sudo install -d -o 1000 -g 1000 -m 0750 /mnt/storage/l4d2-steam-library
openssl rand -hex 32 | sudo tee /etc/l4d2-matchmaking-core-token >/dev/null
sudo chmod 0600 /etc/l4d2-matchmaking-core-token
openssl rand -base64 32 | sudo tee /etc/l4d2-matchmaking-core-rcon-key >/dev/null
sudo chmod 0600 /etc/l4d2-matchmaking-core-rcon-key
```

进入 `deploy/matchmaking-core`，从 `.env.example` 创建 `.env`，至少填写：

```dotenv
CORE_API_TOKEN_FILE_HOST=/etc/l4d2-matchmaking-core-token
CORE_RCON_ENCRYPTION_KEY_FILE_HOST=/etc/l4d2-matchmaking-core-rcon-key
POSTGRES_PASSWORD=<unique-postgresql-password>
CORE_SHARED_LIBRARY_HOST_PATH=/mnt/storage/l4d2-steam-library
CORE_AGENT_STEAM_API_LIBRARY_PATH=<container-visible-path-to-libsteam_api.so>
CORE_AGENT_STEAM_LOGIN_UI_MODE=auto
CORE_SCHEDULER_MAX_STARTS_PER_TICK=16
CORE_IMAGE=l4d2-matchmaking-core:local
CORE_AGENT_IMAGE=l4d2-steam-lobby-agent:local
CORE_AGENT_NETWORK=l4d2-matchmaking
CORE_AGENT_REPORTING_ORIGIN=http://core:8080
```

`CORE_AGENT_STEAM_API_LIBRARY_PATH` 必须是 Agent 容器内的路径，且共享库前缀固定为
`/mnt/steam-library`。首次安装 AppID 550 前可以填写预期路径；完成下载后，在宿主机
执行 `find /mnt/storage/l4d2-steam-library -name libsteam_api.so -type f`，将相对路径映射
为 `/mnt/steam-library/...` 后更新 `.env`。重启 Core 并重建该 Agent，使新路径生效。

```sh
docker compose up -d --build
docker compose ps
```

Core 自动应用数据库迁移。Core 是唯一挂载 `/var/run/docker.sock` 的服务；PostgreSQL
没有宿主机端口。不要把 Core API、Agent HTTP 或 noVNC 映射到公网。远程管理使用私有
SSH 端口转发，例如 `ssh -L 18080:127.0.0.1:18080 <host>`。

首次创建 Agent 后，通过其 `noVncPort` 完成 Steam 登录；在 Steam UI 中安装 AppID 550
及所需共享运行时。不要让多个 Agent 同时安装、校验、更新或卸载共享游戏库。

## 鉴权

`GET /healthz` 不需要鉴权。所有 `/v1/*` 路由使用：

```http
Authorization: Bearer <token-file-content>
```

缺失或错误 token 返回 `401`。Core 永不在 API 响应中返回 Docker container ID、账号卷名、
Steam 凭据、Steam Guard 数据、Docker socket 或原始 Steam 日志。

## 服务器 API

`POST /v1/servers` 创建，`GET /v1/servers` 列表，`GET /v1/servers/{id}` 读取，
`PUT /v1/servers/{id}` 整体更新，`DELETE /v1/servers/{id}` 删除。创建和更新的 body：

```json
{
  "endpoint": "example.org:27015",
  "requiresReservation": true,
  "priority": 0,
  "maxConcurrentWarmups": 36,
  "attemptWindowSeconds": 720,
  "playerTarget": 6,
  "gameMode": null,
  "enabled": true,
  "rconPassword": "<optional-server-rcon-password>"
}
```

`GET /v1/servers/observations` 是认证的只读展示模型。它返回每一台已配置 Target Server
的最新内存态 A2S 观察结果，不写 PostgreSQL，也不会在 HTTP 请求中直接发出 UDP 查询。实现时该
字面量路由必须先于 `/{id}` 注册。响应的每一项为：

```json
{
  "targetServerId": "c691ca6a-6c2a-4ece-b7bd-2e951eee7caa",
  "status": "online",
  "serverName": "L4D2 HK Versus #1",
  "playerCount": 2,
  "maxPlayers": 12,
  "observedAt": "2026-08-18T12:00:05+00:00"
}
```

- `online` 表示本轮 A2S_INFO 成功，服名、当前人数和最大人数可用。
- `unavailable` 表示本轮 DNS/A2S 超时或包无效；`serverName`、`playerCount` 与
  `maxPlayers` 都为 `null`，不回显先前成功值，`observedAt` 是本次失败时间。
- `pending` 表示尚未完成第一轮采样；全部实时字段和 `observedAt` 都为 `null`。

观察器每五秒更新一次且只服务 UI 读模型。调度器仍为独立的实时 A2S 决策 owner，不得使用此
缓存替代调度事实。响应始终不包含 RCON、Docker、Steam 或 Agent 身份数据。

`endpoint` 接受 hostname 或 IPv4，省略端口时为 `27015`；不接受 URL、IPv6、凭据或非法
端口。可选数值传 `null` 时恢复缺省值：优先级 0、非预留并发 36、尝试窗口 720 秒、人数
目标 6。`gameMode` 可为 `null`、`coop` 或 `versus`；`null` 保持现有 versus metadata 默认值，其他字符串返回 `400 invalid_game_mode`。
预留目标的有效并发始终强制为 1。创建成功返回 `201`，读取/更新成功返回 `200`，
删除成功返回 `204`，非法配置返回 `400`，未知 ID 返回 `404`。

将 `enabled` 更新为 `false` 或删除服务器时，Core 会先持久化禁用状态，再停止该服务器的所有
`active`/`uncertain` 暖服操作。只有全部 stop 确认成功才完成请求；若任一 stop 无法确认，Agent
会被隔离，未确认的尝试及其 reservation lease 会保留，接口返回 `409`。删除失败时服务器保留在
禁用状态，之后可再次更新为 `enabled: false` 重试 drain。

`rconPassword` 是仅写字段，仅可在 `requiresReservation: true` 时提供，用于对 reservation UDP
超时执行服务器 `status` 二次验证。普通服务器提供非空密码返回 `400 rcon_requires_reservation`；将
预留服务器更新为普通服务器会清除已保存的凭据。Core
使用 `CORE_RCON_ENCRYPTION_KEY_FILE_HOST` 指向的 32-byte Base64 密钥以 AES-GCM 加密保存它；
读取响应只提供 `hasRconCredentials`，不会返回密码、密文或密钥。更新时传 `null` 可清除既有
凭据。对于带凭据的预留服务器，Agent 仅在 reservation UDP 超时时以同一游戏端口连接 Source
RCON，并且 `status` 含有精确的当前 lobby cookie 才保持大厅；明确拒绝、认证失败或不匹配
仍按 reservation 失败处理。

## Agent API

`POST /v1/agents` 创建并启动一个 Agent：

```json
{"name":"steam-account-01","downloadRegion":"hongkong"}
```

返回 `201` 和如下受限信息：

```json
{
  "id":"2b5baaf4-d85d-4a7d-a515-3bf87ff32036",
  "name":"steam-account-01",
  "status":"running",
  "downloadRegion":"hongkong",
  "noVncPort":18083,
  "createdAt":"2026-08-17T12:00:00+00:00",
  "updatedAt":"2026-08-17T12:00:00+00:00"
}
```

可用路由为：

- `GET /v1/agents`、`GET /v1/agents/{id}`
- `PUT /v1/agents/{id}`，body 与创建相同
- `POST /v1/agents/{id}/start`
- `POST /v1/agents/{id}/stop`
- `POST /v1/agents/{id}/recreate`
- `DELETE /v1/agents/{id}`

创建和 `recreate` 会分配/保留独立账号卷；停止、删除和重建都不会删除这些卷。`recreate`
适用于更换镜像、下载地区、Steam API 路径或需要再次显示 Steam 登录 UI 的场景。名称重复、
无可用 noVNC 端口返回 `409`；请求字段不合法返回 `400`；未知 ID 返回 `404`。Docker
创建、启动、停止、删除或查询失败目前返回 `500`，调用方不应把它当作可安全重试的输入错误。

要重新认证一个已登录账号，暂时将 Core `.env` 的
`CORE_AGENT_STEAM_LOGIN_UI_MODE` 改为 `always`，执行 `docker compose up -d` 使 Core
重载配置，再调用目标 Agent 的 `recreate`。完成后将该值恢复为 `auto` 并再次重建该
Agent，使其回到静默小内存模式。

创建和重建 Agent 时，Core 会为该 Agent 生成独立的高熵上报 token，只保存其 SHA-256 哈希，
并将 `PLAYER_ENTRY_REPORTING_ORIGIN`（默认 `http://core:8080`）和
`PLAYER_ENTRY_REPORTING_TOKEN` 注入新容器。token 不出现在 Agent API 响应、日志或前端；普通
`start` 不轮换 token。数据库中已有、尚未重建的 Agent 没有上报凭据，但仍可正常暖服；需要
重建一次后才开始产生统计事件。缺少任一 Agent 上报变量时，只禁用统计旁路，不影响 Steam
回调、暖服调度或大厅生命周期。

## 玩家进入事件上报与统计

Agent 统计的是成功发送 `ReplyJoinData` 的响应次数，不是已连接游戏服务器的真实玩家数；重复
响应重复计数，不保存玩家 Steam ID。Agent 使用专用 token 调用：

```http
POST /v1/internal/player-entry-events
Authorization: Bearer <agent-reporting-token>
Content-Type: application/json
```

body 为最多 100 条事件的批次。Core 要求 token 所属 Agent 与每条事件的 `agentId` 相同，校验
事件时间位于最近 180 天且不超过当前时间 5 分钟。首次事件返回 `202` 并计入 `accepted`；同一
内容的重复事件计入 `duplicates`；同一 `eventId` 内容冲突返回 `409`，不覆盖原记录。管理
`CORE_API_TOKEN` 对此端点无效，Agent token 不能访问管理接口。

管理端统计查询为 `GET /v1/statistics/player-entries`，仅接受管理 Core Bearer。可选参数
`from`、`to`（UTC ISO-8601，默认最近 24 小时）、`granularity=auto|hour|day|week`、
`lobbyType=all|standard|reserved`、`targetMode=all|coop|versus`、`agentId` 和
`targetServerId`。范围采用 `[from,to)`，不能超过 180 天、结束时间不能超过当前时间 5 分钟，
趋势桶不能超过 5000。`auto` 在不超过 48 小时用小时、不超过 31 天用日，否则用周。
响应固定使用 `Asia/Shanghai`，包含总响应次数、按实际范围小时数计算的平均每小时、趋势零桶、
上海 0–23 点规律、Agent 聚合和下载区域聚合。目标模式未指定时归入 `versus`；只有 Core 保存
的显式 Agent 下载区域参与区域快照，未配置归入 `默认`。原始事件每小时清理一次，保留 180 天；
清理失败仅记录日志并等待下一周期，不影响事件接收和暖服调度。

## Lobby 查询

`GET /v1/lobbies/{lobbyId}` 查询任意非零十进制 Steam lobby ID。Core 选择一个健康 Agent
并转发其 Steam actor 的实时快照：

```json
{
  "lobbyId":"109775242170052468",
  "ownerSteamId":"76561198000000000",
  "members":[{"steamId":"76561198000000000","personaName":"Agent"}],
  "metadata":{"Game:campaign":"L4D2C2","Game:state":"game"},
  "observedAt":"2026-08-17T12:00:00+00:00"
}
```

非法 ID 返回 `400`；没有健康 Agent 或 Agent 通信失败返回 `503`。查询不会暴露用于读取的
Agent 身份，也不会创建、加入或离开目标 lobby。

## 当前暖服状态

`GET /v1/warmups` 返回当前数据库中 `active` 或 `uncertain` 的暖服尝试，不提供已完成历史。每一项
包含目标服务器 ID/endpoint、Agent ID/display name、operation/lobby ID、mode/state/phase、生命周期
时间戳、deadline 和 `remainingSeconds`。该接口是只读快照，不会为查询额外调用 Agent 或 A2S，且不会
返回 RCON 密文、Docker 信息、Steam 凭据或账号卷信息。

调度器每五秒最多启动 `CORE_SCHEDULER_MAX_STARTS_PER_TICK` 个操作，默认 `16`。这个值只限制单次
启动吞吐量；目标服的并发上限、reservation 独占和服务器优先级仍由目标服配置及调度规则决定。

## 全局暖服和调度开关

`GET /v1/settings/warmup-scheduling` 和 `PUT /v1/settings/warmup-scheduling` 使用 Core Bearer
鉴权。设置持久化在 `CoreSettings`，默认 `enabled: true`；控制服务重启后仍保留禁用状态。

```json
{
  "enabled": false
}
```

将 `enabled` 设为 `false` 时，Core 先保存禁用状态，再排空所有 `active`、`uncertain` 和
`restart_pending` 暖服任务。已确认停止的任务变为 `completed`，关联 reservation lease 被移除；
`restart_pending` 不发起远程停止而直接清理。成功停止的 Agent 会请求既有 Steam 恢复流程并进入
`restarting`，但 Core 不会调用容器停止、删除或重建操作，节点容器继续运行。

禁用期间，调度器的恢复流程和每五秒 tick 都会直接返回，不执行 A2S 查询、目标选择或新暖服启动。
再次写入 `{"enabled": true}` 后从空任务状态恢复，不会恢复已经清理的旧任务。

如果任一 `active`/`uncertain` 操作无法确认停止，响应为 `409 "global_warmup_drain_failed"`；
仍存在未确认任务时启用也返回 `409 "global_warmup_drain_pending"`。冲突不会把未确认任务误标为
完成，且开关继续保持禁用，调用方可在处理后重试。

旧的组合 `GET/PUT /v1/settings` 继续兼容，但组合 PUT 不会改写该开关，也不执行暖服排空。

## 全局暖服和调度暂停时间段

`GET /v1/settings/warmup-pause-windows` 和 `PUT /v1/settings/warmup-pause-windows` 使用
Core Bearer 鉴权。时间段保存在 `CoreSettings`，按分钟使用固定的 `Asia/Shanghai`（UTC+8）
时钟判断；控制服务所在机器的系统时区不会影响结果。

读取响应和写入请求的形状如下：

```json
{
  "windows": [
    { "start": "00:00", "end": "08:00" },
    { "start": "23:00", "end": "00:00" }
  ],
  "active": false,
  "updatedAt": "2026-08-23T10:00:00+00:00"
}
```

写入时只提交 `windows`：

```json
{
  "windows": [
    { "start": "23:00", "end": "00:00" }
  ]
}
```

`start` 和 `end` 必须是严格的 `HH:mm`，精度为分钟；区间使用 `[start, end)`，因此开始时刻
包含、结束时刻不包含。`start > end` 表示跨午夜区间，例如 `23:00–00:00`；多个区间可以
重叠，命中任意一个即暂停。空数组表示不配置暂停；相同起止时间或非法格式返回
`400 "invalid_warmup_pause_window"`。

当手动暖服开关启用且保存后的配置当前命中时间段时，Core 先持久化配置，再在共享调度锁内
立即排空 `active`、`uncertain` 和 `restart_pending` 任务。排空失败返回 `409
"global_warmup_drain_failed"`，但新配置仍然保留，后续每五秒 tick 会重试排空；未确认任务
不会被误标记为完成。若旧配置当前正在暂停而新配置将退出暂停，Core 会先排空未确认任务，
排空失败时保留旧配置并返回同一 `409`，排空成功后才保存新配置。暂停期间恢复流程和调度
tick 不执行 A2S 查询、目标选择或新暖服启动。时间段结束后自动恢复调度，但不会恢复已经
清理的旧任务。

排空成功的 Agent 继续使用既有 Steam 恢复流程；本功能不会停止、删除或重建 Warm-up Agent
容器。组合 `GET /v1/settings` 同时增加 `warmupPauseWindows` 和
`warmupPauseWindowsActive` 两个非敏感字段；组合 `PUT /v1/settings` 保持兼容，但不会覆盖
时间段配置。

## 调度可见性

调度自动运行，当前管理 API 不提供手工创建 lobby、注入 metadata 或覆盖实时 A2S 观察的接口。
目标服务器的写接口仅可配置其可选 RCON 密码，调度操作和公开响应均不回显该值。预留服务器的
互斥和 120/30 秒规则见
[steam-lobby-automation.md](steam-lobby-automation.md)。Agent API 仅位于 Docker 内网，见
[steam-lobby-agent-api.md](steam-lobby-agent-api.md)。
