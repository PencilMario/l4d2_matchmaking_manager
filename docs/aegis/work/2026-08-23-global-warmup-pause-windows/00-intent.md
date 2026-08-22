# Task Intent

## Requested outcome

新增全局“时间段暂停暖服和调度”设置，支持 Asia/Shanghai 下多个分钟级、可跨午夜的时间段；
时间段内停止并清空当前暖服任务，时间段外自动恢复调度。同时将每一项全局设置拆分为独立卡片。

## Confirmed behavior

- 已运行暖服任务在时间段开始时停止并清空。
- 时间段使用 Asia/Shanghai，分钟级 `HH:mm`，支持跨午夜和多个时间段。
- 采用 CoreSettings JSON 数组持久化。
- Warm-up Agent 容器保持运行。

## Scope

- Core 设置模型、迁移、专用 API、时间规则判断、scheduler 暂停和排空。
- 旧部署入口和新版工作台入口的设置卡片与 API wiring。
- 相关测试、API 文档和验证证据。

## Non-goals

- 不修改单个 Target Server 的调度配置或既有任务状态语义。
- 不添加按星期、用户时区或一次性暂停。
- 不停止、删除或重建 Warm-up Agent 容器。
