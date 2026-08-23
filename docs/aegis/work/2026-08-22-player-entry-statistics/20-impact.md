# 玩家进入统计影响说明

## 受影响 owner

- Probe：成功 `SendLobbyChatMsg` 后创建事件快照。
- Agent：非阻塞队列和后台 uploader。
- Core：独立 reporting auth、事件表、幂等接收、统计聚合和 retention。
- Scheduler：把 Agent/区域/Target Server/模式快照放入操作请求。
- Frontend：现用 App/Sidebar 中的统计入口和查询视图。

## 必须保持的兼容边界

- 旧 Agent 操作请求仍可反序列化；缺少统计上下文只是不记录统计。
- Steam callback、ReplyJoinData 发送、调度和管理 API 不等待统计网络请求。
- 管理 bearer token 不扩权；Agent reporting token 不可访问管理端点。
- 历史事件不依赖当前 Agent/Target Server 外键，当前配置变化不改写历史快照。

## 失败与回滚面

- 上报链路允许丢事件，失败不能使大厅操作失败。
- 统计表和 migration 可独立回滚；删除事件表会丢失最多 180 天统计数据，因此仅作为明确的数据迁移操作处理。
- 查询若遇数据库不可用由现有 Core 请求失败语义承接，不引入内存全量回退。
