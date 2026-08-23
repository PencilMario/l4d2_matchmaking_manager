# 玩家进入统计原子任务

每项任务均按 Red -> Green -> Refactor 执行；不得先写生产代码再补测试。任务顺序遵循共享契约、采集链路、接收存储、查询服务和界面消费关系。

## Wave 1: Contract and capture

- [x] `C1` 为 `AgentOperationRequest.EntryStatisticsContext`、事件快照和批次响应写 JSON/边界测试。
- [x] `C2` 为成功/失败 `SendLobbyChatMsg` 写 Probe 捕获测试并确认 RED。
- [x] `C3` 实现可选 sink 和成功发送后的单次事件创建；确认旧协议测试仍 GREEN。
- [x] `C4` 为 4096 有界队列、100 条批次、2 秒超时、一次重试写 Agent 测试。
- [x] `C5` 实现 Agent sink/uploader/配置禁用语义，确认 Agent HTTP 回归 GREEN。

## Wave 2: Core write path

- [x] `S1` 为 EF 实体、字段长度、主键和维度时间索引写模型测试。
- [x] `S2` 添加 `PlayerEntryEvents` migration 并运行迁移发现/模型测试。
- [x] `S3` 为独立 token 哈希、scheme 隔离和跨 Agent 拒绝写鉴权测试。
- [x] `S4` 为首次接收、相同 EventId 重复、冲突 EventId、字段/时间/批次验证写 ingestion 测试。
- [x] `S5` 实现 Core 接收端点并验证 401/202/400/409 语义。
- [x] `S6` 为创建/重建容器 token/origin 注入及旧 Agent 兼容写测试并实现。
- [x] `S7` 为调度上下文快照写客户端/调度测试，实现 null mode -> versus。

## Wave 3: Query and UI

- [x] `Q1` 为时间范围、枚举和趋势桶限制写 RED 查询测试。
- [x] `Q2` 为上海跨日/跨周、零桶、每日 24 桶和实际小时分母写 RED 测试。
- [x] `Q3` 为 Agent/区域/大厅/模式/服务器聚合和删除快照写 RED 测试。
- [x] `Q4` 实现 Core 统计 DTO、PostgreSQL 聚合、零桶补齐和 retention hosted service。
- [x] `Q5` 为 API 查询参数、上海本地输入和页面状态写 frontend RED 测试。
- [x] `Q6` 实现 mounted App/Sidebar 统计入口、SVG 图表、表格和重试状态。

## Wave 4: Closure

- [x] `D1` 更新精确的 API、部署、环境变量和运维文档。
- [x] `V1` 运行所有 .NET 测试、前端测试、构建、migration/model 检查和 diff 检查。
- [x] `V2` 请求 advisory code review，修复 Critical/Important 问题。
- [x] `V3` 复跑完整验证，填写证据、未验证项、残余风险和置信度。

## Acceptance checklist

- [x] 只有有效成功 `ReplyJoinData` 计数；重复响应重复计数；不保存 Steam ID。
- [x] standard/reserved、all/coop/versus、未指定模式归 versus 均可筛选。
- [x] 最近 24 小时/7 天/30 天和自定义范围；auto/hour/day/week；上海 0–23 点规律。
- [x] 平均频率以总事件数除实际范围小时数，空闲小时计入分母。
- [x] 区域只来自显式 Agent 配置；未配置是默认；改名/删除保留历史快照。
- [x] 180 天原始保留；清理不阻塞接收/调度；失败、超时、队列满和重启可丢事件。
- [x] 上报 token 与管理 token 隔离；缺少上报配置只禁用统计链路。
- [x] 现有暖服、协议、管理页面和主用户旅程无回归。
