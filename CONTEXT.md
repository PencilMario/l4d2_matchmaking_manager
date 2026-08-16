# Project Context: L4D2 Matchmaking Manager

## Language

| Term | Definition | Avoid |
|------|------------|-------|
| Core Controller | 管理目标服务器与暖服机，并按照暖服规则调度大厅生命周期的控制服务。 | 主服务、主机、游戏服 |
| Warm-up Agent | 一个持有独立 Steam 账号、创建并持有 L4D2 大厅的 Docker 容器。 | 机器人、服务器、Steam 客户端 |
| Target Server | 被暖服流程指定的 L4D2 游戏服务器，以主机名或 IPv4 地址和游戏端口标识。 | 暖服机、Agent |
| Reservation Lobby | 为要求 reservation 的目标服务器创建、并在首位真实玩家进入前持续持有的大厅。 | 普通大厅、房间预留 |
| Session Metadata Profile | 用于描述 L4D2 大厅游戏会话的完整 metadata 键值集合，其中的地图字段必须来自同一官方战役。 | 随意 metadata、服务器信息 |
| Campaign Profile | 与一项官方战役绑定的完整地图 metadata 组合，包含 campaign 标识、标题、任务文件、作者和幸存者组。 | 单独地图字段、随机键值 |
| Official Campaign Pool | 从 L4D2 官方战役 C1 至 C14 中随机选择、并提供一致地图字段的候选集合。 | 随机地图、地图名 |
| Concurrent Warm-up Limit | 一个不要求 reservation 的目标服务器同时允许持有暖服大厅的 Agent 数量，默认值为 36。 | Agent 总数、服务器人数上限 |
| Warm-up Attempt Window | 某个 Agent 针对一个目标服务器持续执行暖服状态机的最大时长，默认值为 12 分钟。 | 大厅空闲倒计时、调度间隔 |
| Player Target | 由 A2S 观测到、达到后即停止为目标服务器暖服的服务器玩家人数阈值，默认值为 6。 | Steam 大厅成员数、暖服机数量 |
| Server Priority | 目标服务器参与调度时的整数优先级，数值越高越先被分配，默认值为 0，允许负数。 | 列表位置、服务器人数 |
| Reservation Admission Check | 预留服务器在创建大厅前进行的 A2S 空服检查，观测到任意玩家即跳过本轮。 | 人数目标检查、大厅成员检查 |

## Relationships

- A **Core Controller** manages many **Target Servers** and **Warm-up Agents**.
- A **Warm-up Agent** holds one **Reservation Lobby** or normal warm-up lobby at a time.
- Each lobby carries one **Session Metadata Profile** with one **Campaign Profile**, selected from the **Official Campaign Pool**.
- A **Target Server** defines one **Concurrent Warm-up Limit** and one **Warm-up Attempt Window**.
- A **Target Server** also defines one **Player Target**.
- A **Target Server** has one **Server Priority**.
- A **Reservation Lobby** starts only after its **Reservation Admission Check** finds no players.

## Flagged Ambiguities

- "暖服机" -> **Warm-up Agent** (2026-08-16)
- "预留大厅" -> **Reservation Lobby** (2026-08-16)
