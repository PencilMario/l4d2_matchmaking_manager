# 非预留服务器续暖调度设计

## 目标

当非预留目标服务器已有未完成的暖服时，空闲 Agent 应优先加入该服务器的暖服，直至服务器 A2S 人数达到 `PlayerTarget` 或该服务器本轮暖服窗口到期。该优先级高于新服务器的配置 `Priority`。

## 基线

- `WarmupSchedulerService` 当前先处理同目标重建请求，再按服务器 `Priority` 和同优先级持久化轮转选择普通启动目标。
- `WarmupAttempt.StartedAt` 目前由每次新启动写入；同目标重建会继承旧 attempt 的开始时间。
- 一个普通服务器可运行至多 `MaxConcurrentWarmups` 个 attempt；预留服务器的有效并发固定为 1。
- attempt 的 deadline 为 `StartedAt + AttemptWindowSeconds`，已满员或到 deadline 时由实时 A2S/调度决策结束。

## 方案

调度顺序调整为：

1. 已触发的同目标重建请求。
2. 非预留服务器的续暖候选。
3. 原有按 `Priority` 降序、同优先级持久化 round-robin 的新暖服候选。

续暖候选必须同时满足：

- 至少一个 `active` 或 `uncertain` attempt。
- `RequiresReservation == false`。
- 活跃 attempt 中最早的 `StartedAt` 加 `AttemptWindowSeconds` 仍晚于当前时间。
- 该服务器尚有 `MaxConcurrentWarmups` 剩余槽位。

续暖候选之间继续使用既有稳定轮转游标，以避免多个续暖服务器之间出现固定偏向；`Priority` 不参与该层排序。实际启动前继续执行 DNS/A2S 准入检查，满员、不可达或查询失败的候选跳过并继续选择下一候选。

## 服务器级时间窗

`StartedAt` 在本规则中代表目标服务器的一轮暖服开始时间，而非单个 Agent 的开始时间。续暖分配新增 attempt 时，必须把目标服务器当前活跃 attempt 的最早 `StartedAt` 传给新 attempt。这样并行 Agent 共享同一 12 分钟（或服务器配置的 `AttemptWindowSeconds`）窗口，不会因补充 Agent 而延长该服务器的暖服时限。

同目标重建继续继承该服务器的原始开始时间。A2S 达到 `PlayerTarget` 或共享 deadline 到期后，续暖资格消失；之后若 A2S 再次满足新建条件，则新一轮暖服使用新的开始时间。

## 兼容边界与非目标

- 预留服务器不参与续暖偏好，保留现有 reservation lease 和单并发规则。
- 不增加数据库实体或迁移；服务器级窗口由同一目标活跃 attempt 的最早 `StartedAt` 推导。
- 不改变大厅成员阶段、A2S 判定、批次启动上限或既有 priority 规则在无续暖候选时的行为。

## 验收与测试

- 低优先级的非预留续暖目标压过高优先级的新目标。
- 给续暖目标增加的 attempt 继承该服务器最早的 `StartedAt`。
- 已超时的续暖目标不再压过高优先级新目标。
- 预留目标不获得续暖偏好。
- 既有同优先级轮转、并发上限和重建优先级测试继续通过。
