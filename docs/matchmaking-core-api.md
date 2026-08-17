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
  "enabled": true,
  "rconPassword": "<optional-server-rcon-password>"
}
```

`endpoint` 接受 hostname 或 IPv4，省略端口时为 `27015`；不接受 URL、IPv6、凭据或非法
端口。可选数值传 `null` 时恢复缺省值：优先级 0、非预留并发 36、尝试窗口 720 秒、人数
目标 6。预留目标的有效并发始终强制为 1。创建成功返回 `201`，读取/更新成功返回 `200`，
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

## 调度可见性

调度自动运行，当前管理 API 不提供手工创建 lobby、注入 metadata 或覆盖实时 A2S 观察的接口。
目标服务器的写接口仅可配置其可选 RCON 密码，调度操作和公开响应均不回显该值。预留服务器的
互斥和 120/30 秒规则见
[steam-lobby-automation.md](steam-lobby-automation.md)。Agent API 仅位于 Docker 内网，见
[steam-lobby-agent-api.md](steam-lobby-agent-api.md)。
