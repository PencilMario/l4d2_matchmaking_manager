# L4D 暖服调度语义

Core 每五秒读取目标配置、Agent 健康状态和 A2S 观察，然后为可用服务器创建 L4D Lobby。
Agent 的内部合约见 [steam-lobby-agent-api.md](steam-lobby-agent-api.md)。

## 选择目标

- 选择所有已启用的服务器；`requiresReservation=true` 的目标使用预留流程。
- A2S 必须成功返回可解析的 A2S_INFO；返回的 AppID 不作为拒绝目标的条件。
- A2S 玩家数达到 `playerTarget` 时停止继续暖服；缺省目标为 6。
- 同优先级服务器使用持久化游标轮询；优先级越高越先调度。
- 每个目标最多同时持有 `maxConcurrentWarmups` 个标准 Lobby，缺省为 36。
- 单次尝试缺省 720 秒；有外部成员后，连续 30 秒无新成员会重建 Lobby。

## L4D Lobby

当前标准模板来自 2026-08-21 捕获的真实 AppID 500 公共 Lobby：

- `Game:campaign=Farm`
- `Game:MissionInfo:DisplayTitle=#L4D360UI_Campaign_Farm`
- `Game:mode=coop`
- `Members:numSlots=4`
- `Options:Server=dedicated`
- `System:network=LIVE`

选择 `versus` 时使用 8 个槽位并写入 `Game:mode=versus`；省略模式时默认 `coop`。

## 预约功能边界

reservation/RCON 代码路径在本分支保持启用：预留目标的并发固定为 1，Core 会把解密后的
RCON 密码仅传给对应的 reserved operation。该路径尚未在 L4D AppID 500 服务器上完成真机
验证，因此应视为待验证功能，而不是已禁用功能。

## 恢复与维护

Core 重启时以 Agent 和 Steam 的实时状态为准，不根据旧数据库记录猜测 Lobby 是否仍存在。
共享库维护锁生效时不启动新任务。多个 Agent 不能并发安装、校验或更新 AppID 500 内容。
