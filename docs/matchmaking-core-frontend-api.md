# Matchmaking Core Frontend API

本文件是管理前端的完整 HTTP API 契约。它只覆盖 Core 的公开接口；Agent Docker
内网 API 不属于前端接口。除少数特别标记的“实际采样”外，UUID、时间和地址均为便于
实现而给出的示意值，不能作为固定值或测试夹具。

## 接入与约定

- Core 默认监听宿主机 `127.0.0.1:18080`，且未配置 CORS。浏览器管理端应经同源反向
  代理或 BFF 请求 Core。
- `GET /healthz` 不需要鉴权。其余每个接口都需要：

  ```http
  Authorization: Bearer <CORE_API_TOKEN>
  ```

- 写请求使用 `Content-Type: application/json`。字段名为 camelCase；UUID 为标准
  字符串；时间为 ISO 8601 UTC。
- Core 没有 `{ "data": ... }` 包装层：成功响应直接是对象或数组；由业务代码返回的
  错误 body 是 JSON 字符串，例如 `"invalid_lobby_id"`。
- 除 Lobby 查询外，`401`、`404`、`503` 及 `204` 均没有可依赖的 body。Lobby 查询的
  `503` body 是 JSON 字符串：`"lobby_data_unavailable"`、
  `"lobby_operation_preservation_failed"` 或 `"lobby_query_agent_unavailable"`。
- 服务端会持续生成动态值，例如 `id`、`createdAt`、`updatedAt`、
  `observedAt`、`deadline` 和 `remainingSeconds`。前端不应回写响应中的这些值。

## 可用路由

```text
GET    /healthz

GET    /v1/servers
POST   /v1/servers
GET    /v1/servers/{serverId}
PUT    /v1/servers/{serverId}
DELETE /v1/servers/{serverId}

GET    /v1/agents
POST   /v1/agents
GET    /v1/agents/{agentId}
PUT    /v1/agents/{agentId}
POST   /v1/agents/{agentId}/start
POST   /v1/agents/{agentId}/stop
POST   /v1/agents/{agentId}/recreate
DELETE /v1/agents/{agentId}

GET    /v1/warmups
GET    /v1/lobbies/{lobbyId}
```

## 真实响应采样

以下 body 于 2026-08-17 从部署在 `100.72.137.92` 的 Core 只读取得。它们证明当前
响应的实际序列化形式；其中 Steam ID 和 lobby ID 是该时刻的公开 Steam 实体标识，不是
登录凭据或 API token。

健康检查：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"status":"alive"}
```

目标服务器列表为空：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[]
```

已注册 Agent 列表：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[{"id":"aa75055a-fa44-40e0-bdc1-3a68a735396c","name":"edge-steam-1421932260","status":"running","downloadRegion":null,"noVncPort":18083,"createdAt":"2026-08-17T07:51:57.338363+00:00","updatedAt":"2026-08-17T10:57:42.232436+00:00"}]
```

当前暖服列表为空：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[]
```

非法 lobby ID：

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

"invalid_lobby_id"
```

## 健康检查

### `GET /healthz`

无需鉴权，也没有请求 body。用于反向代理、容器编排或前端连接状态检查。

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"status":"alive"}
```

## 目标服务器

### 写入 body

`POST /v1/servers` 和 `PUT /v1/servers/{serverId}` 都使用完整的下列 body：

```json
{
  "endpoint": "203.0.113.7:27015",
  "requiresReservation": false,
  "priority": 0,
  "maxConcurrentWarmups": 36,
  "attemptWindowSeconds": 720,
  "playerTarget": 6,
  "enabled": true,
  "rconPassword": null
}
```

| 字段 | 类型 | 规则 |
| --- | --- | --- |
| `endpoint` | string | hostname 或 IPv4；未写端口时使用 `27015`。不接受 URL、IPv6、空白、凭据或端口 `0`。 |
| `requiresReservation` | boolean | `true` 为预留大厅规则，预留服务器的有效并发固定为 `1`。 |
| `priority` | integer/null | 越高越优先，可为负数；`null` 使用 `0`。 |
| `maxConcurrentWarmups` | integer/null | 非预留服务器并发上限，必须大于 `0`；`null` 使用 `36`。预留服务器仍返回 `1`。 |
| `attemptWindowSeconds` | integer/null | 单次暖服窗口，必须大于 `0`；`null` 使用 `720`。 |
| `playerTarget` | integer/null | A2S 人数达到此值即停止暖服，必须大于 `0`；`null` 使用 `6`。 |
| `enabled` | boolean/null | 是否参与调度；`null` 使用 `true`。 |
| `rconPassword` | string/null | 仅预留服务器可写；仅用于 reservation UDP 超时后的 RCON `status` 验证。读取响应永远不会返回它。传 `null` 清除已有凭据。 |

所有服务器读取接口返回同一种对象：

```json
{
  "id": "c691ca6a-6c2a-4ece-b7bd-2e951eee7caa",
  "endpoint": "203.0.113.7:27015",
  "requiresReservation": false,
  "priority": 0,
  "maxConcurrentWarmups": 36,
  "attemptWindowSeconds": 720,
  "playerTarget": 6,
  "enabled": true,
  "hasRconCredentials": false,
  "createdAt": "2026-08-17T12:00:00+00:00",
  "updatedAt": "2026-08-17T12:00:00+00:00"
}
```

`hasRconCredentials` 仅表示 Core 保存了加密后的凭据，不能读取或还原
`rconPassword`。

### `GET /v1/servers`

返回按 `priority` 降序、再按创建时间升序排列的数组。

成功响应，部署实例的实际采样：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[]
```

### `GET /v1/servers/observations`

返回每一台已配置 Target Server 的内存态 A2S 展示快照。它是认证的只读路由，服务端每五秒
采样一次；浏览器只能读取该结果，不能因轮询而直接触发 UDP A2S 查询。该字面量路由在
`GET /v1/servers/{serverId}` 之前匹配。

```json
[
  {
    "targetServerId": "c691ca6a-6c2a-4ece-b7bd-2e951eee7caa",
    "status": "online",
    "serverName": "L4D2 HK Versus #1",
    "playerCount": 2,
    "maxPlayers": 12,
    "observedAt": "2026-08-18T12:00:05+00:00"
  }
]
```

| `status` | 实时字段与前端行为 |
| --- | --- |
| `online` | 使用 `serverName` 作为主名称，并显示 `playerCount / maxPlayers` 与观测时间。 |
| `unavailable` | `serverName`、`playerCount`、`maxPlayers` 必为 `null`；显示 A2S 不可用与 `-- / --`，不能显示旧成功样本。 |
| `pending` | 还未完成首轮采样；全部实时字段和 `observedAt` 为 `null`，显示等待首次观测。 |

空数组表示尚未配置 Target Server。该响应不包含 RCON、Docker、Steam 凭据或 Agent 身份。

### `POST /v1/servers`

请求：

```http
POST /v1/servers
Authorization: Bearer <CORE_API_TOKEN>
Content-Type: application/json

{
  "endpoint": "203.0.113.7:27015",
  "requiresReservation": false,
  "priority": 0,
  "maxConcurrentWarmups": 36,
  "attemptWindowSeconds": 720,
  "playerTarget": 6,
  "enabled": true,
  "rconPassword": null
}
```

成功响应（示意）：

```http
HTTP/1.1 201 Created
Location: /v1/servers/c691ca6a-6c2a-4ece-b7bd-2e951eee7caa
Content-Type: application/json; charset=utf-8

{"id":"c691ca6a-6c2a-4ece-b7bd-2e951eee7caa","endpoint":"203.0.113.7:27015","requiresReservation":false,"priority":0,"maxConcurrentWarmups":36,"attemptWindowSeconds":720,"playerTarget":6,"enabled":true,"hasRconCredentials":false,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:00:00+00:00"}
```

典型失败：

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

"rcon_requires_reservation"
```

`400` 还可能返回 `"invalid_target_server_endpoint"`、
`"invalid_target_server_configuration"`、`"invalid_rcon_password"`、
`"rcon_encryption_key_not_configured"` 或 `"core_rcon_encryption_key_invalid"`。

### `GET /v1/servers/{serverId}`

`serverId` 是 UUID。

成功响应（示意）：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"c691ca6a-6c2a-4ece-b7bd-2e951eee7caa","endpoint":"203.0.113.7:27015","requiresReservation":false,"priority":0,"maxConcurrentWarmups":36,"attemptWindowSeconds":720,"playerTarget":6,"enabled":true,"hasRconCredentials":false,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:00:00+00:00"}
```

不存在时（部署实例已实际采样）：

```http
HTTP/1.1 404 Not Found
```

### `PUT /v1/servers/{serverId}`

请求 body 使用“[写入 body](#写入-body)”的完整模型。该请求是整体更新，前端应始终提交
当前值加上用户修改后的值，而不是只提交一个局部字段。

```http
PUT /v1/servers/c691ca6a-6c2a-4ece-b7bd-2e951eee7caa
Authorization: Bearer <CORE_API_TOKEN>
Content-Type: application/json

{
  "endpoint": "203.0.113.7:27015",
  "requiresReservation": false,
  "priority": 10,
  "maxConcurrentWarmups": 24,
  "attemptWindowSeconds": 720,
  "playerTarget": 6,
  "enabled": true,
  "rconPassword": null
}
```

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"c691ca6a-6c2a-4ece-b7bd-2e951eee7caa","endpoint":"203.0.113.7:27015","requiresReservation":false,"priority":10,"maxConcurrentWarmups":24,"attemptWindowSeconds":720,"playerTarget":6,"enabled":true,"hasRconCredentials":false,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:05:00+00:00"}
```

将 `enabled` 改为 `false` 时，Core 会先禁用服务器并停止它的全部
`active`/`uncertain` 暖服。如果任一停止操作无法确认，接口返回：

```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

"target_server_drain_failed"
```

此时服务器已是 disabled，前端应刷新 `GET /v1/servers` 与
`GET /v1/warmups`，保留表单并允许再次以 `enabled: false` 提交来重试 drain。其他
业务错误与 `POST /v1/servers` 相同；未知 UUID 返回 `404`。

### `DELETE /v1/servers/{serverId}`

没有请求 body。Core 会先禁用此服务器，停止全部 `active`/`uncertain` 暖服，再删除
服务器记录。

成功响应：

```http
HTTP/1.1 204 No Content
```

无法完成 drain 时：

```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

"target_server_drain_failed"
```

未知 UUID 返回 `404`。收到 `409` 后不得从前端列表乐观移除该服务器：它仍被保留为
disabled，直到后续 drain 成功。

## 暖服机

### 写入与读取模型

`POST /v1/agents` 和 `PUT /v1/agents/{agentId}` 的 body：

```json
{
  "name": "steam-account-01",
  "downloadRegion": "hongkong"
}
```

| 字段 | 类型 | 规则 |
| --- | --- | --- |
| `name` | string | 去除首尾空白后长度为 1-128，且必须唯一。 |
| `downloadRegion` | string/null | Steam 下载区域；`null` 或空白字符串表示使用 Steam 默认设置。 |

所有 Agent 读取操作返回：

```json
{
  "id": "b2bda1dd-7e9d-4e30-8900-366f608e26f4",
  "name": "steam-account-01",
  "status": "running",
  "downloadRegion": "hongkong",
  "noVncPort": 18083,
  "createdAt": "2026-08-17T12:00:00+00:00",
  "updatedAt": "2026-08-17T12:00:00+00:00"
}
```

`status` 当前可能为 `created`、`running`、`stopped` 或 `quarantined`。
`quarantined` 表示 Core 未能安全确认某个暖服操作；前端不应自动重启或复用该 Agent。
`noVncPort` 是宿主机回环地址上的端口，不能直接作为公网连接地址。

### `GET /v1/agents`

返回按创建时间升序排列的数组。

成功响应示例：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[{"id":"aa75055a-fa44-40e0-bdc1-3a68a735396c","name":"edge-steam-1421932260","status":"running","downloadRegion":null,"noVncPort":18083,"createdAt":"2026-08-17T07:51:57.338363+00:00","updatedAt":"2026-08-17T10:57:42.232436+00:00"}]
```

### `POST /v1/agents`

创建一个独立账号卷，并创建、启动对应 Agent 容器。首次登录仍需通过返回的
`noVncPort` 完成 Steam 配置。

请求：

```http
POST /v1/agents
Authorization: Bearer <CORE_API_TOKEN>
Content-Type: application/json

{"name":"steam-account-02","downloadRegion":"hongkong"}
```

成功响应（示意）：

```http
HTTP/1.1 201 Created
Location: /v1/agents/b2bda1dd-7e9d-4e30-8900-366f608e26f4
Content-Type: application/json; charset=utf-8

{"id":"b2bda1dd-7e9d-4e30-8900-366f608e26f4","name":"steam-account-02","status":"running","downloadRegion":"hongkong","noVncPort":18084,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:00:01+00:00"}
```

名称冲突：

```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

"warmup_agent_name_exists"
```

没有剩余 noVNC 端口时也返回 `409 "no_free_novnc_port"`。名称不合法返回
`400 "invalid_warmup_agent_name"`。容器镜像、Docker daemon 或运行配置失败可能返回
`500`；这类失败不应被前端当作字段校验错误。

### `GET /v1/agents/{agentId}`

`agentId` 是 UUID。

成功响应，部署实例实际采样：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"aa75055a-fa44-40e0-bdc1-3a68a735396c","name":"edge-steam-1421932260","status":"running","downloadRegion":null,"noVncPort":18083,"createdAt":"2026-08-17T07:51:57.338363+00:00","updatedAt":"2026-08-17T10:57:42.232436+00:00"}
```

未知 UUID 返回 `404`。

### `PUT /v1/agents/{agentId}`

请求 body 使用“[写入与读取模型](#写入与读取模型)”中的写入模型。此接口仅更新名称与
下载区域；已在运行的容器不会自动重建，所以新的下载区域需要调用 `recreate` 后才会
进入容器环境。

```http
PUT /v1/agents/b2bda1dd-7e9d-4e30-8900-366f608e26f4
Authorization: Bearer <CORE_API_TOKEN>
Content-Type: application/json

{"name":"steam-account-02","downloadRegion":"tokyo"}
```

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"b2bda1dd-7e9d-4e30-8900-366f608e26f4","name":"steam-account-02","status":"running","downloadRegion":"tokyo","noVncPort":18084,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:05:00+00:00"}
```

名称冲突返回 `409 "warmup_agent_name_exists"`，名称不合法返回
`400 "invalid_warmup_agent_name"`，未知 UUID 返回 `404`。

### `POST /v1/agents/{agentId}/start`

没有请求 body。若 Agent 已是 `running`，此操作为幂等读取式成功，不会重启容器。

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"b2bda1dd-7e9d-4e30-8900-366f608e26f4","name":"steam-account-02","status":"running","downloadRegion":"tokyo","noVncPort":18084,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:06:00+00:00"}
```

未知 UUID 返回 `404`。Docker 未能启动容器时可能返回 `500`，前端应刷新
`GET /v1/agents/{agentId}` 再决定是否重试。

### `POST /v1/agents/{agentId}/stop`

没有请求 body。若 Agent 已不是 `running`，此操作为幂等读取式成功。

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"b2bda1dd-7e9d-4e30-8900-366f608e26f4","name":"steam-account-02","status":"stopped","downloadRegion":"tokyo","noVncPort":18084,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:07:00+00:00"}
```

未知 UUID 返回 `404`；Docker 停止失败可能返回 `500`。

### `POST /v1/agents/{agentId}/recreate`

没有请求 body。Core 删除并重新创建该 Agent 的容器，但保留 Steam 登录与账号配置卷。
用于镜像、Steam API 路径、下载区域或登录 UI 模式更新后使环境生效。

成功响应：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":"b2bda1dd-7e9d-4e30-8900-366f608e26f4","name":"steam-account-02","status":"running","downloadRegion":"tokyo","noVncPort":18084,"createdAt":"2026-08-17T12:00:00+00:00","updatedAt":"2026-08-17T12:08:00+00:00"}
```

未知 UUID 返回 `404`；Docker 删除、创建或启动失败可能返回 `500`。该接口不是无副作用
查询，前端通常应先请求用户确认。

### `DELETE /v1/agents/{agentId}`

没有请求 body。删除容器与 Core 的 Agent 记录，但保留 Steam 登录和账号配置卷，避免删除
凭据。

成功响应：

```http
HTTP/1.1 204 No Content
```

未知 UUID 返回 `404`；Docker 删除失败可能返回 `500`。删除后无需尝试从 API 读取旧
Agent，而应刷新整个 `GET /v1/agents` 列表。

## 当前暖服

### `GET /v1/warmups`

仅返回数据库状态为 `active` 或 `uncertain` 的尝试，没有暖服历史。该接口不请求
Agent 或 A2S，适合前端每 5 秒轮询。

成功响应，部署实例实际采样：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[]
```

非空响应的每一项使用：

```json
{
  "targetServerId": "c691ca6a-6c2a-4ece-b7bd-2e951eee7caa",
  "targetEndpoint": "203.0.113.7:27015",
  "warmupAgentId": "b2bda1dd-7e9d-4e30-8900-366f608e26f4",
  "warmupAgentName": "steam-account-01",
  "operationId": "51d7d790-2f17-48a3-9d04-8c1be865f1a2",
  "lobbyId": "109775242170052468",
  "mode": "standard",
  "state": "active",
  "phase": "Active",
  "startedAt": "2026-08-17T12:00:00+00:00",
  "lobbyReadyAt": "2026-08-17T12:00:04+00:00",
  "firstExternalMemberAt": null,
  "quietSince": null,
  "observedAt": "2026-08-17T12:00:05+00:00",
  "deadline": "2026-08-17T12:12:00+00:00",
  "remainingSeconds": 715
}
```

| 字段 | 说明 |
| --- | --- |
| `mode` | `standard` 或 `reserved`。 |
| `state` | `active` 表示 Agent 已确认操作；`uncertain` 表示 Core 已持久化计划但未能确认启动结果。 |
| `phase` | 当前可见 `AwaitingFirstMember`、`Active`、`Selecting`；前端应按字符串展示，不能假定枚举封闭。 |
| `lobbyId` | 可为 `null`，例如操作尚未返回 lobby ID。 |
| `remainingSeconds` | 到 `deadline` 的非负整数秒，每次查询都会重新计算。 |

空数组表示没有运行中的暖服，不代表服务器或 Agent 配置不存在。

## Lobby 查询

### `GET /v1/lobbies/{lobbyId}`

`lobbyId` 必须为非零十进制 Steam lobby ID。Core 优先选择空闲 Agent，其次选择仅执行
`standard` 暖服的 Agent。后者会由 Agent 临时加入目标 lobby、读取成员并离开；原暖服
大厅无法保持时会隔离该 Agent。没有可安全临时加入的 Agent 时，Core 仍使用健康 Agent
获取已确认 metadata，并返回明确的 metadata-only 状态；不会暴露查询所用 Agent 的身份。

成功响应，部署实例实际采样：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"lobbyId":"109775242425650097","ownerSteamId":"76561199692804388","members":[{"steamId":"76561198000000001","personaName":null}],"metadata":{"Game:campaign":"L4D2C5","Game:state":"game","Members:numPlayers":"1"},"observedAt":"2026-08-17T12:57:39.27695+00:00","memberDataStatus":"complete"}
```

响应模型：

```json
{
  "lobbyId": "109775242425650097",
  "ownerSteamId": "76561199692804388",
  "members": [
    {
      "steamId": "76561198000000000",
      "personaName": "Player name or null"
    }
  ],
  "metadata": {
    "Game:campaign": "L4D2C5",
    "Game:state": "game"
  },
  "observedAt": "2026-08-17T12:57:39.27695+00:00",
  "memberDataStatus": "complete"
}
```

| 字段 | 说明 |
| --- | --- |
| `members` | 仅当 `memberDataStatus` 为 `complete` 时是已确认的目标大厅成员列表；查询 Agent 自己已经被移除，因此 `members.length` 就是本 API 返回的玩家人数。 |
| `memberDataStatus` | `complete`、`metadata_only_no_query_agent`、`metadata_only_join_denied`、`metadata_only_join_timeout` 或 `metadata_only_agent_state_changed`。metadata-only 时 `members` 必为空数组。 |
| `metadata["Members:numPlayers"]` | Steam lobby owner 写入的原始 metadata；前端不得用它计算本 API 的玩家人数，也不会被 Core 改写。 |

`metadata` 是 Steam 返回的字符串字典。不要假定键集合固定：除 L4D2 游戏字段外，Steam
也可能返回 `__gameserverIP`、`__gameserverPort`、`__gameserverSteamID` 等内部
键。前端应原样展示或按允许列表挑选显示，不能依赖某个空值 metadata 键一定存在。

非法 ID（实际采样）：

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

"invalid_lobby_id"
```

metadata 确认失败时：

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json; charset=utf-8

"lobby_data_unavailable"
```

没有健康 Agent、所有 Agent 查询租约已被占用，或 Core 无法访问被选 Agent 时：

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json; charset=utf-8

"lobby_query_agent_unavailable"
```

## 前端状态处理

- 首屏可并行读取 `/v1/servers`、`/v1/servers/observations`、`/v1/agents`、`/v1/warmups`；
  每五秒刷新 observations、agents 和 warmups。只在用户输入或点击查询时调用
  `/v1/lobbies/{lobbyId}`。
- 服务器或 Agent 写操作返回成功后，立即刷新对应列表、`/v1/servers/observations` 和
  `/v1/warmups`。不要在 `409`、
  `500`、`503` 上做不可逆的乐观更新。
- 对 `401` 清理管理会话并返回登录/令牌配置流程；对服务器 `409` 保留表单内容并刷新
  服务器和暖服状态；对 `500`/`503` 显示“操作未确认”。
- `rconPassword` 只能作为写入表单字段，读取模型没有该字段，前端不可回填或缓存它。
- 没有手工创建 lobby、手工开始暖服、写入 metadata、读取暖服历史或直接调用 Agent 内网
  API 的公开路由。
