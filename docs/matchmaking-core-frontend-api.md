# Matchmaking Core Frontend API

本文件是管理前端的 API 契约参考。它只覆盖 Core 的公开 HTTP API；Agent 的 Docker
内网 API 不是前端接口。

## 接入

- Core 默认只监听宿主机 `127.0.0.1:18080`，且未配置 CORS。浏览器前端应通过同源反向代理
  或 BFF 访问，不能直接从不同源页面请求 Core。
- 除 `GET /healthz` 外，所有路由都需要：

  ```http
  Authorization: Bearer <CORE_API_TOKEN>
  ```

- 请求和响应均为 JSON；字段名为 camelCase；UUID 为标准字符串；时间为 ISO 8601 UTC。
- `204 No Content` 没有响应 body。缺少或错误 Bearer token 返回 `401`。

## 路由总览

| 方法 | 路径 | 用途 | 成功响应 |
| --- | --- | --- | --- |
| GET | `/healthz` | Core 存活检查 | `200` |
| GET | `/v1/servers` | 目标服务器列表 | `200` |
| POST | `/v1/servers` | 创建目标服务器 | `201` |
| GET | `/v1/servers/{serverId}` | 读取目标服务器 | `200` |
| PUT | `/v1/servers/{serverId}` | 整体更新目标服务器 | `200` |
| DELETE | `/v1/servers/{serverId}` | 回收暖服后删除目标服务器 | `204` |
| GET | `/v1/agents` | 暖服机列表 | `200` |
| POST | `/v1/agents` | 创建并启动暖服机 | `201` |
| GET | `/v1/agents/{agentId}` | 读取暖服机 | `200` |
| PUT | `/v1/agents/{agentId}` | 更新暖服机显示配置 | `200` |
| POST | `/v1/agents/{agentId}/start` | 启动容器 | `200` |
| POST | `/v1/agents/{agentId}/stop` | 停止容器 | `200` |
| POST | `/v1/agents/{agentId}/recreate` | 重建容器，保留账号卷 | `200` |
| DELETE | `/v1/agents/{agentId}` | 删除容器记录，保留账号卷 | `204` |
| GET | `/v1/warmups` | 当前运行中的暖服快照 | `200` |
| GET | `/v1/lobbies/{lobbyId}` | 查询任意 Steam lobby | `200` |

`serverId` 与 `agentId` 都是 UUID。`lobbyId` 必须是非零十进制 Steam lobby ID。

## HTTP 响应示例

Core 没有统一的 `{ "data": ... }` 包装层。成功时直接返回对象或数组；错误 body 是 JSON
字符串；`204` 完全没有 body。

健康检查：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"status":"alive"}
```

目标服务器列表为空时：

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

[]
```

创建目标服务器时，`201` 的 body 就是该服务器读取模型：

```http
HTTP/1.1 201 Created
Location: /v1/servers/c691ca6a-6c2a-4ece-b7bd-2e951eee7caa
Content-Type: application/json; charset=utf-8

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

删除成功时：

```http
HTTP/1.1 204 No Content
```

未能确认目标服务器 drain 时：

```http
HTTP/1.1 409 Conflict
Content-Type: application/json; charset=utf-8

"target_server_drain_failed"
```

## 目标服务器

### 写入模型

`POST /v1/servers` 和 `PUT /v1/servers/{serverId}` 使用相同 body：

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

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `endpoint` | string | hostname 或 IPv4；省略端口时为 `27015`。 |
| `requiresReservation` | boolean | 是否使用 reservation 暖服规则。 |
| `priority` | integer/null | 越高越优先；`null` 使用 `0`。允许负数。 |
| `maxConcurrentWarmups` | integer/null | 非预留服务器并发上限；`null` 使用 `36`。预留服务器强制为 `1`。 |
| `attemptWindowSeconds` | integer/null | 单次暖服窗口，`null` 使用 `720` 秒。 |
| `playerTarget` | integer/null | A2S 人数达到该值时停止暖服，`null` 使用 `6`。 |
| `enabled` | boolean/null | 是否参与调度，`null` 使用 `true`。 |
| `rconPassword` | string/null | 仅预留服务器可写，仅用于 reservation UDP 超时验证；读取响应不会返回它。 |

非预留服务器传入非空 `rconPassword` 返回 `400`，body 为
`"rcon_requires_reservation"`。将预留服务器改为非预留服务器会清除已保存的 RCON
凭据。

### 读取模型

`POST`、`PUT`、`GET /v1/servers/{serverId}` 返回单个对象；列表接口返回对象数组：

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

### 停用与删除

`PUT` 设为 `enabled: false` 和 `DELETE` 都会先禁止新调度，再请求停止该目标服务器的
全部 `active`/`uncertain` 暖服操作。

- 成功时分别返回 `200` 或 `204`。
- 无法确认任一 stop 时返回 `409`，body 为 `"target_server_drain_failed"`。
- `409` 后服务器已处于 disabled 状态，前端应刷新 `GET /v1/servers` 与
  `GET /v1/warmups`，并允许用户再次提交同一 disabled 更新以重试 drain。

其余常见错误为：参数错误 `400`、不存在 `404`。`DELETE` 不应在 `409` 后直接从列表中
乐观移除目标服务器。

## 暖服机

### 写入模型

`POST /v1/agents` 与 `PUT /v1/agents/{agentId}` 使用：

```json
{
  "name": "steam-account-01",
  "downloadRegion": "hongkong"
}
```

`name` 去除首尾空白后长度为 1-128，必须唯一。`downloadRegion` 可为 `null`。创建会同时
创建并启动容器；首次登录仍需通过返回的 `noVncPort` 访问 noVNC。

### 读取模型

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

`status` 当前可能为 `created`、`running`、`stopped` 或 `quarantined`。`quarantined`
表示 Core 无法安全确认某个暖服操作，不应自动重启或复用该 Agent。

`POST /start`、`/stop`、`/recreate` 返回同一读取模型。`recreate` 删除并重建容器，但保留
该 Agent 的 Steam 登录和账号配置卷；删除 Agent 也保留账号卷。名称重复或没有可用 noVNC
端口返回 `409`；字段错误为 `400`；未知 ID 为 `404`；Docker 调用失败可能为 `500`。

## 当前暖服状态

`GET /v1/warmups` 仅返回数据库内 `state` 为 `active` 或 `uncertain` 的尝试，没有历史接口。
此调用不会请求 Agent 或 A2S，适合前端轮询。

```json
[
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
]
```

| 字段 | 说明 |
| --- | --- |
| `mode` | `standard` 或 `reserved`。 |
| `state` | `active` 表示 Agent 已确认操作；`uncertain` 表示 Core 已持久化计划但未能确认启动结果。 |
| `phase` | `AwaitingFirstMember`、`Active` 或 `Selecting`。应按字符串展示，不要假定全部值。 |
| `remainingSeconds` | 到暖服窗口 deadline 的非负剩余秒数。 |

推荐每 5 秒刷新一次；在服务器或 Agent 写操作成功后立即刷新。空数组表示当前没有运行中暖服。

## Lobby 查询

`GET /v1/lobbies/{lobbyId}` 返回 Agent 读取到的实时 Steam lobby 快照：

```json
{
  "lobbyId": "109775242170052468",
  "ownerSteamId": "76561198000000000",
  "members": [
    {"steamId": "76561198000000000", "personaName": "Agent"}
  ],
  "metadata": {
    "Game:campaign": "L4D2C2",
    "Game:state": "game"
  },
  "observedAt": "2026-08-17T12:00:00+00:00"
}
```

非法 lobby ID 返回 `400`；没有健康 Agent 或 Agent 通信失败返回 `503`。该接口不会返回用于
查询的 Agent 身份，也不会改变 lobby 状态。

## 前端状态处理

- 首屏可并行读取 `/v1/servers`、`/v1/agents`、`/v1/warmups`；`/v1/lobbies/{id}` 仅在用户
  主动查看指定 lobby 时调用。
- 对 `401` 清理管理会话并返回登录/令牌配置流程；对 `409` 保留表单内容并刷新对应资源；对
  `500`/`503` 显示操作未确认，避免乐观修改本地状态。
- `rconPassword` 只能作为写入表单字段。任何服务器读取模型都没有该字段，前端不应尝试回填。
- 不存在手工创建大厅、手工开始暖服、写入 metadata 或查询暖服历史的公开 API。前端应以
  `/v1/warmups` 为当前调度状态的唯一来源。
