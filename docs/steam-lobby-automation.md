# L4D1 暖服调度语义

Core 每五秒读取目标配置、Agent 健康状态和 A2S 观察，然后为可用的 AppID 500 服务器创建
标准 L4D1 Lobby。Agent 的内部合约见 [steam-lobby-agent-api.md](steam-lobby-agent-api.md)。

## 选择目标

- 只选择已启用、`requiresReservation=false` 的服务器。
- A2S 必须成功返回 AppID `500`；非 L4D1、不可解析或不可用的服务器会被跳过。
- A2S 玩家数达到 `playerTarget` 时停止继续暖服；缺省目标为 4。
- 同优先级服务器使用持久化游标轮询；优先级越高越先调度。
- 每个目标最多同时持有 `maxConcurrentWarmups` 个标准 Lobby，缺省为 36。
- 单次尝试缺省 720 秒；有外部成员后，连续 30 秒无新成员会重建 Lobby。

## L4D1 Lobby

当前标准模板来自 2026-08-21 捕获的真实 AppID 500 公共 Lobby：

- `Game:campaign=Farm`
- `Game:MissionInfo:DisplayTitle=#L4D360UI_Campaign_Farm`
- `Game:mode=coop`
- `Members:numSlots=4`
- `Options:Server=dedicated`
- `System:network=LIVE`

选择 `versus` 时使用 8 个槽位并写入 `Game:mode=versus`；省略模式时默认 `coop`。

## 预约功能边界

上游的 reservation 代码和常量是 L4D2 研究成果，没有完成 L4D1 真机验证。本分支不会把它
当作可用功能：Core 与 Agent 都拒绝新的 reserved operation，错误码为
`l4d1_reservation_not_supported`。数据库中的旧字段保留是为了迁移和停用旧记录，不代表支持。

## 恢复与维护

Core 重启时以 Agent 和 Steam 的实时状态为准，不根据旧数据库记录猜测 Lobby 是否仍存在。
共享库维护锁生效时不启动新任务。多个 Agent 不能并发安装、校验或更新 AppID 500 内容。
