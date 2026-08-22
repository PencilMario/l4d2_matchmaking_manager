# 时间段暂停暖服和调度设计

## 目标

增加一个可持久化的全局时间段设置。在 `Asia/Shanghai` 的任意配置暂停时间段内，Core
停止并清空当前暖服任务，不执行新的暖服调度；时间段结束后自动恢复调度。管理前端的
每一项全局设置各自显示为独立卡片。

## 已确认的产品行为

- 时间段按 `Asia/Shanghai` 判断，精度为分钟。
- 时间段使用 `HH:mm` 起止值，起始时间包含、结束时间不包含。
- 支持跨午夜时间段，例如 `23:00–00:00`。
- 支持多个时间段；重叠时间段可以保存，命中任意时间段即暂停。
- 相同起止时间拒绝保存，避免误配置成全天暂停。
- 时间段进入时，停止并清空 `active`、`uncertain`、`restart_pending` 暖服任务。
- 停止不确定的任务保持原状态，暂停状态仍然生效，调度器后续 tick 重试排空。
- Warm-up Agent 容器不停止、不删除、不重建；已确认停止的 Agent 继续使用现有 Steam
  恢复流程。
- 时间段结束后从已经清空的任务状态恢复，不恢复旧任务。
- 既有“全局启用暖服和调度”手动开关继续保留；手动关闭或命中时间段，任一条件满足
  即暂停调度。
- 保存一个当前已生效的时间段时，配置先持久化，再在共享调度锁内立即尝试排空当前任务。
  排空失败返回 `409`，但新的暂停配置仍然保留。
- 从当前已生效的暂停配置切换到当前不生效的配置时，Core 先排空仍未确认的任务；排空
  失败返回 `409` 并保留旧的暂停配置，让后续 tick 继续重试，排空成功后才保存新配置。

## 方案与取舍

### 方案 A：`CoreSettings` JSON 数组（采用）

在现有单行 `CoreSettings` 中增加 `WarmupPauseWindowsJson` JSONB 列，内容为时间段数组。
Core 每次只读取一条全局设置记录，保存时以一次设置更新替换数组。该方案迁移和事务边界
最小，适合时间段只作为调度规则输入、不需要 SQL 单独查询的场景。

### 方案 B：独立时间段表

每个时间段一行，拥有排序字段和外键。关系模型更规范，也便于未来对单条时间段做审计，
但会增加实体、迁移、排序、删除和事务处理；当前没有单条记录 API 或查询需求。

### 方案 C：环境变量或配置文件

实现简单且不需要数据库迁移，但无法由管理页面持久化和修改，也无法保证控制服务重启后
使用同一份全局设置，因此不满足需求。

## Core 架构

### 持久化与 API

`CoreSettings` 新增非空 `WarmupPauseWindowsJson`，默认值为 `[]`。新增 DTO：

- `WarmupPauseWindow`：`Start`、`End` 两个 `HH:mm` 字符串。
- `UpdateWarmupPauseWindowsRequest`：`Windows` 数组。
- `WarmupPauseWindowsSettingsResponse`：`Windows`、当前 `Active` 状态和 `UpdatedAt`。

新增认证资源：

```http
GET /v1/settings/warmup-pause-windows
PUT /v1/settings/warmup-pause-windows
```

请求和响应示例：

```json
{
  "windows": [
    { "start": "00:00", "end": "08:00" },
    { "start": "23:00", "end": "00:00" }
  ]
}
```

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

组合 `GET /v1/settings` 增加 `warmupPauseWindows` 和 `warmupPauseWindowsActive` 两个
非敏感字段；旧组合 PUT 仍只处理 VNC 代理和 Steam Web API Key，不清除或覆盖时间段。

### 时间规则

新增纯规则组件负责解析、规范化和判断：

- 只接受严格的 `HH:mm`，小时 `00` 至 `23`，分钟 `00` 至 `59`。
- 空数组表示没有时间段暂停。
- `start < end` 使用同日区间 `[start, end)`。
- `start > end` 使用跨午夜区间 `[start, 24:00) ∪ [00:00, end)`。
- `start == end` 返回 `invalid_warmup_pause_window`。
- 保存前按起止分钟排序，但不合并用户配置的重叠区间。
- 当前时间由 UTC 转换为固定 UTC+8 的 `Asia/Shanghai` 时钟后比较；该时区没有夏令时，
  不依赖运行环境的操作系统时区名称。

### 调度数据流

1. `GlobalSettingsService` 读取并校验时间段，使用 `WarmupSchedulingGate` 与 scheduler
   的恢复/tick 互斥。
2. `WarmupSchedulerService.RecoverAsync` 和 `TickAsync` 在访问 A2S、健康节点选择或目标
   选择前，计算有效状态。
3. 有效状态为暂停时，调用已有 `WarmupAttemptDrainService.DrainAllAsync`，然后直接返回；
   不读取 A2S，不选择目标，不启动操作。
4. 排空失败不把任务改成完成，保留暂停状态；下一个五秒 tick 再次尝试。
5. 时间段结束或手动开关重新启用后，scheduler 按现有恢复和调度规则运行；已经完成清理的
   attempt 不会被重新创建。

## 前端设计

旧部署入口 `GlobalSettingsView` 和新版 `SettingsWorkspace` 都移除包住全部设置的外层设置
卡片，改为四个同级卡片：

1. **全局启用暖服和调度**：现有开关和关闭确认。
2. **时间段暂停暖服和调度**：多个 `input type="time"` 行，新增、删除、保存，显示
   `Asia/Shanghai` 和当前生效状态。
3. **VNC 代理**：现有独立代理表单。
4. **Steam Web API Key**：现有密码输入、替换和清除表单。

每个卡片维护自己的保存中状态和反馈；保存一个设置不会调用其他设置的更新接口。时间段
保存成功或触发排空后刷新暖服、节点和服务器快照。两套 API client 都增加时间段资源的
读取/更新方法，但旧组合读取仍用于一次性初始化。

## 错误处理

- 非法时间格式、空时间段、相同起止时间或非法请求结构返回 `400
  "invalid_warmup_pause_window"`。
- 当前暂停排空有未确认任务时返回 `409 "global_warmup_drain_failed"`，时间段配置仍保留。
- 既有手动开关的 `global_warmup_drain_failed` 和 `global_warmup_drain_pending` 行为保持不变。
- 前端保存失败保留当前编辑值；排空冲突提示检查任务状态后重试。

## 测试与验证

- Core 规则单测覆盖空配置、同日边界、跨午夜边界、多个区间、Asia/Shanghai 转换和非法输入。
- Core endpoint 测试覆盖默认值、专用资源读写、组合响应、旧 PUT 兼容、排空成功和冲突。
- scheduler 测试覆盖暂停期间跳过 A2S/selector/start、暂停期间排空、窗口结束后恢复。
- 两套设置组件测试覆盖四张卡片、时间段新增/删除/保存及 API 独立调用。
- API client 测试验证专用路径和请求体。
- 最终运行 `dotnet test L4d2MatchmakingManager.sln`、`npm test -- --run`、
  `npm run build` 和 `git diff --check`。

## 兼容边界与非目标

- 保留 `/v1/settings` 及其原有字段、Bearer 鉴权和五秒调度周期。
- 数据库迁移只新增列，不修改已有密文、任务状态或 Agent 容器生命周期。
- 不增加按单条时间段审计、用户级时区、每周不同日期规则或临时一次性暂停。
- 旧客户端看见新增 JSON 字段不会影响其已有读取；旧组合 PUT 不再是新时间段设置的写入 owner。

## 工作草稿

### TaskIntentDraft

交付可持久化的多时间段全局暖服/调度暂停，以及将四项全局设置拆为独立卡片；保持既有
手动暂停排空、API 兼容和 Agent 容器运行边界。

### BaselineReadSetHint

主要基线为 `CoreSettings`/`GlobalSettingsService`/`GlobalSettingsEndpoints`、
`WarmupSchedulerService`/`WarmupAttemptDrainService`、两套设置组件与 API client、
现有全局设置和调度测试、API 文档及 `CONTEXT.md`。

### ImpactStatementDraft

受影响层为 Core 数据模型/迁移、设置 API、调度 gate 与 scheduler、两套 React 设置 UI、
API 类型和文档。共享调度锁、现有任务状态语义、排空失败保留任务、容器不停止是必须保持
的约束；不涉及目标服务器设置和 Agent 容器生命周期改造。
