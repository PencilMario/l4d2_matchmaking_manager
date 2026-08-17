# L4D2 Matchmaking Manager 管理前端设计

## 状态

已确认。本文定义面向运维人员的管理前端体验，以及为展示实时 A2S 信息所需的最小 Core API 增量；不实现前端或后端代码。

## 目标与边界

管理前端是 Core Controller 的认证桌面操作台。实现目标视口为宽度至少 `1280px`、高度至少 `720px`；本阶段不实现移动端布局、触摸交互或窄屏导航。它让运维人员在一个沉浸式的资源工作区中完成四件事：

1. 观察当前暖服队列和异常。
2. 配置、禁用和删除 Target Server。
3. 管理 Warm-up Agent 的生命周期。
4. 按需查询任意 Steam lobby。

Target Server 的显示名称必须优先使用实时 A2S 服名，而不是只显示 IP 或 hostname。A2S 玩家人数必须显示为当前人数与最大人数的组合，并且必须有可见的观测时间和不可用状态。

本文只扩展管理端可消费的 Core 公开 API。Agent Docker 内网 API、调度规则、Steam lobby 写入能力、暖服历史和手工创建大厅不在范围内。

## 设计输入

- [Core API](../../matchmaking-core-api.md)：部署、安全边界、Target Server、Warm-up Agent、Lobby 与暖服状态规则。
- [Frontend API](../../matchmaking-core-frontend-api.md)：现有 HTTP 契约、轮询、失败语义和写入限制。
- [项目术语](../../../CONTEXT.md)：Target Server、Warm-up Agent、Reservation Lobby 等统一定义。
- React Bits 仓库 `4e0e030193b563be6be33d928f77d0d01cefe237`：可复制的 React/TypeScript/CSS/Tailwind 组件，包括 `Hyperspeed`、`TargetCursor`、`Cubes`、`AnimatedList`、`Counter`、`Stepper`、`ClickSpark` 与 `GlassSurface`。

## 信息架构

应用采用 **Resource Workspace** 主框架。它不把首页做成营销式仪表盘，而是让最新运行状态、资源表与操作入口始终互相可达。

```text
全局工作区
|- Core 连接状态 / 最近刷新时间 / 全局刷新
|- 当前暖服
|  |- 活动与 uncertain 队列
|  `- Target Server 3D 拓扑带
|- Target Server
|  |- A2S 实时资源表
|  `- 创建、编辑、禁用、删除
|- Warm-up Agent
|  |- Agent 资源表
|  `- 创建、编辑、启动、停止、重建、删除
`- Lobby 查询
   `- 单次查询结果与 metadata/members 明细
```

全局顶栏显示 `Core online`、本轮刷新中的资源类型、上次成功刷新时间和一个带刷新图标的手工刷新命令。主导航使用 React Bits `LineSidebar` 或等效的桌面窄侧栏；本阶段只实现固定桌面导航，不提供窄屏折叠模式。

## 视觉方向与 React Bits 映射

界面为沉浸式夜间控制台，不限制使用重型 3D、背景效果或指针动画。动态视觉是产品风格的一部分，但不会替代文本、颜色和图标表达真实状态。

| 位置 | 组件与作用 | 约束 |
| --- | --- | --- |
| 全视口背景 | React Bits `Hyperspeed`，其实现基于 Three.js、`postprocessing` 和 WebGL。作为无边框的全屏动态场景置于数据层之后。 | 背景使用 `pointer-events: none`，不抢占表格、表单或确认操作。WebGL 不可用时回退为静态深色纹理。 |
| Target Server 拓扑带 | React Bits `Cubes`。每个可见 Target Server 是一个可交互单元，按 A2S 状态、玩家密度和暖服数变换表面颜色与倾角。 | 位于“当前暖服”与资源表之间的完整横向带，不嵌套在卡片中。单击只筛选资源表，不执行服务器操作。 |
| 资源工作区 | `GlassSurface` 形成一层稳定、半透明的工具平面；数据区维持固定列宽和高对比文字。 | 不使用浮动卡片堆叠；列表、表格和表单是各自独立的工具面。 |
| 暖服队列 | `AnimatedList` 与低幅度 `FadeContent`。新出现的 Warm-up Attempt 进入队列时才动画，现有行保持稳定位置。 | 排序和行高不能因动画改变，防止 5 秒刷新时视觉跳动。 |
| A2S 人数 | `Counter` 从旧值平滑过渡到 `playerCount / maxPlayers`。 | 不可用时显示 `-- / --`，不从旧值动画到新值，也不保留旧人数。 |
| 状态摘要 | `SpotlightCard` 用于活动暖服、需处理状态与在线 Target Server 三个摘要。 | 摘要为单层模块，不能包裹整个页面。 |
| 破坏性操作 | `Stepper` 承载确认阶段，`ClickSpark` 仅在最终确认成功或明确失败时提供局部反馈。 | `recreate`、禁用有活动暖服的服务器及删除均不允许乐观消失。 |
| 指针层 | `TargetCursor` 用于突出可选择的拓扑单元和资源行。 | 键盘焦点样式独立可见；只针对桌面精确指针设备验收。 |

主色为墨黑与炭黑，内容使用暖白；青色表示连接/可用，酸橙色表示调度活跃，琥珀色表示需关注，红色表示失败或隔离。动画强度可高，但 `prefers-reduced-motion` 必须停止连续场景和光标效果，保留完整功能与状态信息。

## 页面与交互

### 当前暖服

默认页是按状态优先的 Warm-up Attempt 工作区。首屏并列显示活动数、`uncertain` 数和 A2S 不可用 Target Server 数，之后是按 `targetEndpoint`、Agent、模式、阶段、lobby ID、剩余时间排序的队列。

- `active` 使用活跃标识；`uncertain` 始终显示“操作未确认”，不能提供自动重启或复用 Agent 的快捷动作。
- `phase` 按服务端字符串原样展示；界面不把枚举视为封闭集合。
- `lobbyId` 为 `null` 时显示“尚未返回”，而不是空白或假定失败。
- 选择一项会筛选下方 Target Server 拓扑和 Target Server 页，而不会新增 API 调用。

### Target Server

Target Server 是主资源表。每行将配置对象与 A2S Observation 按 `targetServerId` 连接，推荐列如下：

| 列 | 在线状态 | A2S 不可用或尚未采样 |
| --- | --- | --- |
| 服务器 | `serverName` 为主标题；`endpoint` 为次级等宽文本 | `endpoint` 为主标题；显示“A2S 不可用”或“等待首次观测” |
| 人数 | `playerCount / maxPlayers`，由 `Counter` 更新 | `-- / --` |
| A2S | 在线标志和 `observedAt` 相对时间 | 不可用/等待标志和本次失败或等待状态 |
| 调度 | enabled、reservation、priority、有效并发和活动暖服数 | 与在线状态无关，始终显示配置 |
| 操作 | 编辑、禁用/启用、删除 | 与在线状态无关，仍可配置或排障 |

按服务器行进入详情抽屉：上半部显示 A2S 服名、endpoint、人数与观测时间；下半部显示完整配置和关联暖服。编辑使用完整 `PUT` 模型，始终提交当前值加上用户修改后的值。`rconPassword` 是只写字段：只有预留服务器表单可见，永不由读取结果回填或缓存。

禁用或删除可能触发 drain。确认步骤明确提示“Core 将先停止该服务器的 active/uncertain 暖服”。收到 `409 "target_server_drain_failed"` 时，保留表单和服务器行，刷新服务器、A2S Observation 与暖服列表；服务器已 disabled，用户可再次提交禁用以重试 drain。

### Warm-up Agent

Agent 表列出名称、`status`、下载地区、仅本机可达的 noVNC 端口、更新时间和关联中的暖服数。

- `running` 显示停止与重建；`stopped` 显示启动与重建；`created` 显示启动；`quarantined` 明确说明“需人工调查”，隐藏自动启动和复用型快捷操作。
- `recreate` 进入两步确认，提示保留登录/账号数据卷、重建容器并可能重新显示 Steam 登录 UI。
- `DELETE` 成功后刷新整个列表；在 `500` 上不从表中移除该行。

### Lobby 查询

查询页只在用户提交非零十进制 lobby ID 时调用 `GET /v1/lobbies/{lobbyId}`。成功结果分为成员、owner、观测时间和 metadata 键值表。

`memberDataStatus=complete` 才显示成员人数和成员表；任何 metadata-only 状态都显示“成员数据未确认”，成员数为“不可用”，不能使用 `metadata["Members:numPlayers"]` 代替。`503` 的三个 Lobby 错误字符串以可操作提示展示，但不暴露查询 Agent 身份。

## A2S Observation API 增量

现有 `/v1/servers` 仅返回静态配置；`/v1/warmups` 是数据库快照且明确不触发 A2S。因此新增独立、认证的只读路由：

```text
GET /v1/servers/observations
Authorization: Bearer <CORE_API_TOKEN>
```

路由必须在 `/v1/servers/{serverId}` 之前映射，避免把字面量 `observations` 当作 UUID 参数。

成功时返回每个已配置 Target Server 一项，而非因一台服务器超时使整个工作区失败：

```json
[
  {
    "targetServerId": "c691ca6a-6c2a-4ece-b7bd-2e951eee7caa",
    "status": "online",
    "serverName": "L4D2 HK Versus #1",
    "playerCount": 2,
    "maxPlayers": 12,
    "observedAt": "2026-08-18T12:00:05+00:00"
  },
  {
    "targetServerId": "89b8a8a5-2da6-4519-b1d3-7f225d4e3573",
    "status": "unavailable",
    "serverName": null,
    "playerCount": null,
    "maxPlayers": null,
    "observedAt": "2026-08-18T12:00:05+00:00"
  }
]
```

允许的 `status` 是：

- `online`：这一项的 A2S_INFO 已成功读取，所有实时字段可用。
- `unavailable`：本轮 DNS/A2S 读取失败、超时或包无效；所有实时字段必须为 `null`，不得回显上一次成功值。
- `pending`：Target Server 刚加入或观察器尚未完成首轮采样；实时字段和 `observedAt` 均为 `null`。

Core 的 `A2sServerInfo` 需要从仅有 `PlayerCount` 扩展为 `ServerName`、`PlayerCount`、`MaxPlayers` 和 `ObservedAt`。Source A2S INFO 解析器已读取并跳过第一个 C-string 服名，且在当前人数之后已有最大人数 byte；扩展必须在该唯一解析位置取得这些字段，并为截断包维持既有失败语义。

### 观察器运行规则

新增内存态 `TargetServerObservationCollector`，每五秒以有界并发对所有已配置 Target Server 采样，并作为 UI Observation 快照的唯一 owner。它不写 PostgreSQL，不修改 Target Server 配置，也不参与 reservation lease。

WarmupScheduler 继续按既有规则从实时 A2S 执行调度决策，不能因为 UI 缓存而使用旧值。两个路径共享同一个 A2S 解析器和错误分类，但控制决策与展示快照分别拥有采样时机；这样浏览器轮询不会直接放大 UDP 查询，也不会把 UI 缓存升级为调度事实。

## 前端数据流

```text
首屏
  GET /v1/servers  -------+----> Target Server 配置表
  GET /v1/agents   -------+----> Agent 资源表
  GET /v1/warmups  -------+----> 暖服队列与关联计数
  GET /v1/servers/observations -> A2S 服名、人数、状态与观测时间

每 5 秒
  warmups + agents + observations

用户写操作成功
  刷新对应配置列表 + warmups + observations

Lobby 查询
  仅由显式提交触发；不加入全局轮询
```

浏览器经同源反向代理或 BFF 调用 Core。Bearer token 只在管理会话边界使用，不写入 URL、日志、分析事件或 React 状态持久化。

## 状态与失败处理

| 情形 | 前端行为 |
| --- | --- |
| `401` | 清理管理会话并进入登录/令牌配置流程。 |
| Target Server `409 target_server_drain_failed` | 保留表单，刷新 Target Server、暖服和 Observation；不从列表乐观删除。 |
| Agent 名称或 noVNC 端口 `409` | 表单内展示可恢复错误，不修改本地列表。 |
| `500` 或非 Lobby 的 `503` | 显示“操作未确认”，刷新相关资源后再允许显式重试；不把它当成字段校验。 |
| 单项 A2S `unavailable` | 该 Target Server 显示 `-- / --` 和不可用状态，其他 Target Server 继续显示自己的新样本。 |
| Lobby `503` | 根据 `lobby_data_unavailable`、`lobby_operation_preservation_failed` 或 `lobby_query_agent_unavailable` 显示查询失败，不清空上一次成功结果前先标注其已过期。 |
| `204 No Content` | 不解析 body；重新读取对应列表。 |

## 桌面视口与性能要求

- 桌面端为全视口 Three.js 场景加工具平面；只验收 `1280x720` 和 `1440x900` 两种桌面视口。
- 不创建移动端布局、触摸手势、窄屏折叠导航或移动端专用降级分支；低于最小视口属于不支持的部署条件。
- 动画和 WebGL 层独立于轮询状态。5 秒刷新只能更新数据组件，不能重挂载 `Hyperspeed` 或打断用户正在编辑的表单。
- 三维场景、光标层和大型列表需通过桌面 Playwright 截图以及 canvas 像素检查；验证背景非空、画面未遮挡文本、指针交互不阻塞按钮。

## 验收标准

1. 已配置 Target Server 在线时，资源行优先显示 A2S 服名、`endpoint` 和真实 `playerCount / maxPlayers`。
2. A2S 查询失败后，资源行显示不可用而不是旧服名或旧人数；其他服务器不受影响。
3. 浏览器持续轮询不直接发起 A2S UDP 请求；只读取 Core 的 Observation 快照。
4. 禁用/删除 drain 失败、Agent 隔离、Lobby metadata-only、401 和 204 行为均与现有 Frontend API 契约一致。
5. RCON 密码永不显示、回填、缓存或出现在日志中。
6. 在 `1280x720` 和 `1440x900` 桌面视口中，WebGL、3D 背景和光标效果启用时，文本、表格、表单、确认操作和键盘焦点仍可访问；WebGL 不可用或 reduced-motion 下完整退化。

## 非目标

- 手工创建或销毁 Steam lobby、覆盖调度器、写入 lobby metadata、暖服历史或公开 Agent 内网 API。
- 将 A2S Observation 写入 PostgreSQL、用它替换调度器实时判断，或将旧样本宣称为当前状态。
- 暴露 Docker、Steam 凭据、Agent 身份、账号卷、RCON 明文/密文或 Core Bearer token。
- 让背景、动画或指针特效成为状态判断或危险操作确认的唯一信号。

## 工作草案

### TaskIntentDraft

基于 Core 与 Frontend API 设计一个 React Bits 驱动、以 Resource Workspace 为主框架的管理端；它必须展示 A2S 服名与实时人数，并在保留既有安全和失败边界的前提下提供沉浸式 Three.js 视觉体验。

### BaselineReadSetHint

`docs/matchmaking-core-api.md`、`docs/matchmaking-core-frontend-api.md`、`CONTEXT.md`、现有 `SourceA2sClient` 与 `A2sServerInfo` 是本设计的权威基线。React Bits 仅为前端视觉实现来源，不改变 Core 或 Agent 的信任边界。

### ImpactStatementDraft

受影响层包括管理前端、Core A2S 解析/观察器和公开前端 API 文档。Core 的配置读取、调度用实时 A2S 判断、Agent 内网 API 与 Steam 凭据隔离保持不变；新的 Observation 端点是独立的展示读模型，不是控制或持久化状态的 owner。
