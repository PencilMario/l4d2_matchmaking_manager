# Warm-up Agent 玩家进入统计设计

## 目标

为管理控制台增加暖服大厅玩家进入统计。统计口径是 Agent 对有效大厅进入请求成功发送 `ReplyJoinData` 的次数：一次成功发送计一次，重复进入请求也分别计数；它不是去重玩家数，也不代表玩家已经连接目标游戏服务器。

统计以事件发生时间为准，展示上海时区（`Asia/Shanghai`）的时间趋势、整日 0–23 点进入规律、平均每小时进入，以及 Agent 和下载区域维度。

## 已确认的产品规则

- 时间范围支持最近 24 小时、最近 7 天、最近 30 天和自定义 `[from, to)`；默认最近 24 小时。
- 趋势粒度支持自动、小时、日、周。自动按范围选择：不超过 48 小时用小时，不超过 31 天用日，否则用周。
- 整日规律固定返回上海时间 00:00–23:00 共 24 个桶，空桶返回 0。
- 平均每小时进入 = 选定范围内总事件数 ÷ 实际范围小时数，范围内没有事件的小时仍计入分母。
- 大厅筛选支持全部、standard、reserved。
- 目标模式筛选支持全部、coop、versus；目标服务器未指定模式、历史旧事件或空值统一归入 versus。
- 可按 Agent、Target Server 筛选。Agent 无事件时仍显示当前 Agent 的 0 行；区域维度不根据当前配置虚构历史事件。
- 下载区域只接受 Agent 显式配置的快照。未配置区域统一为“默认”；运行时探测到的区域不写入统计事件。事件保存发生时的 Agent、区域、目标服务器和模式快照，之后改名、改区域或删除配置不改写历史事实。
- 统计原始事件保留 180 天，后台定期清理过期事件；清理失败不能阻塞上报、调度或大厅生命周期。

## 架构与数据流

```text
Steam Lobby Chat callback
  -> SendLobbyChatMsg == true
  -> non-blocking bounded queue (Agent)
  -> batch <= 100, timeout 2s, one transport/5xx retry
  -> Core internal endpoint + per-Agent reporting token
  -> PlayerEntryEvents (EventId idempotency + immutable snapshots)
  -> PostgreSQL filtering/grouping
  -> Shanghai bucket mapping and zero-fill
  -> management API -> mounted statistics page
```

Steam 回调线程只调用同步、非阻塞的 `TryEnqueue`，上报网络请求由 Agent 后台服务处理。队列满、超时、传输失败、5xx 和 Agent 重启允许丢事件；4xx 不重试。缺少上报配置只禁用统计链路，不影响原有暖服接口。

Core 的上报认证使用独立 scheme。每个 Agent 只拥有自己的 token，Core 只存 SHA-256 哈希；管理 bearer token 不可用于上报，上报 token 也不可访问管理 API。相同 `EventId` 和相同内容重复上报不重复落库，内容冲突返回 409。

统计读取首先在数据库完成时间范围和维度过滤，并按 UTC 小时、Agent/名称快照、区域分组，避免把 180 天内的所有原始事件载入 Core 内存。Core 根据固定上海时区把 UTC 小时组映射到趋势和整日桶，并补齐零桶。

## 接口与存储边界

- Agent 上报：`POST /v1/internal/player-entry-events`，仅接受 1–100 条事件。
- 管理查询：`GET /v1/statistics/player-entries`，可传 `from`、`to`、`granularity`、`lobbyType`、`targetMode`、`agentId`、`targetServerId`。
- 事件表不保存 Steam ID，只保存事件 ID、发生时间、操作/大厅标识、大厅类型、Agent 快照、显式下载区域快照、目标服务器快照和规范化模式。
- 事件表不建立到 Agent 或 Target Server 的外键，避免删除配置级联删除历史事实。

## 兼容性与非目标

- `AgentOperationRequest` 的旧位置参数和既有 JSON 保持兼容；没有统计上下文的旧操作不产生不完整事件。
- 既有 Steam 协议、暖服调度、管理认证、Agent 管理接口保持原行为。
- 不统计大厅当前成员数、不计算去重 UV、不推断真实进服成功率、不把运行时下载区域探测结果追溯写入历史事件。
- 不引入新的图表依赖；管理页使用现有 React/CSS/SVG，并提供表格文本以支持读取和空/错/加载状态。

## 验收标准

1. 只有成功的 `SendLobbyChatMsg` 产生事件；失败发送、无效请求、未持有活动大厅和缺少上下文不产生事件。
2. Agent、时间趋势、整日规律、standard/reserved、coop/versus（未指定归 versus）、Target Server、显式区域/默认区域均可查询和展示。
3. 频率分母使用实际时间范围；历史快照在 Agent/Target Server 改名或删除后仍可用于统计。
4. 上报 token 与管理 token 隔离，统计链路故障不影响暖服主流程。
5. 180 天清理、数据库聚合、迁移、API、现用管理页和完整回归验证均有证据记录。
