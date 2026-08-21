# L4D2 暖服调度语义

Core 是大厅生命周期的唯一编排者。它从 PostgreSQL 读取静态目标配置、操作关联和租约，
但 Agent、Steam lobby 与 A2S 的实时观察始终高于数据库中的旧记录。Agent 的内部
HTTP 合约见 [steam-lobby-agent-api.md](steam-lobby-agent-api.md)。

## 选择目标

每五秒，Core 为每个运行中且健康的空闲 Agent 规划一个服务器，并按以下规则选择：

- 只考虑已启用的服务器。已有暖服且仍有容量的非预留服务器优先按当前暖服数降序
  填充，并列时按 `priority` 降序和稳定 ID 选择；这条集中规则不受新目标 priority
  影响。
- 已有目标达到并发上限、A2S 人数目标、DNS/A2S 检查失败或本轮窗口到期时，继续检查
  下一个可接收的已有普通目标。所有已有普通目标都不可接收时，才按 `priority` 降序
  选择新目标，同优先级的新目标依据持久化游标轮询。
- 非预留服务器的并发上限为其 `maxConcurrentWarmups`，缺省为 36；预留服务器固定为 1。
- Core 将 hostname 解析为 IPv4 后执行 A2S；没有 IPv4、A2S 失败或 A2S 人数已达到
  `playerTarget` 的候选会跳过，并继续考察下一个候选。
- 预留服务器在创建前若 A2S 玩家数大于 0，直接跳过，不创建大厅也不取得租约。

开始操作前，Core 先持久化 `uncertain` 操作和预留租约，再调用 Agent。启动、停止或
读取操作的网络失败会隔离该 Agent 并保留不确定状态/租约，避免同一 Steam 账号或
预留目标被并发复用。

## 预留服务器

预留目标最多一个活动操作。创建 reserved lobby 后：

1. Agent 持有大厅，最多等待 120 秒的第一个非 owner 成员。
2. 120 秒内无人加入时，Core 先要求 Agent 离开，再对同一服务器重建大厅。
3. 第一个成员进入后，每次观察到新的非 owner 成员都会重置 30 秒空窗计时。
4. 连续 30 秒无新成员、A2S 人数达到 `playerTarget`，或原始尝试窗口到期时，Core
   离开大厅、释放预留租约并回到全局调度。

同服务器重建不会重置该次暖服的 `attemptWindowSeconds`，默认 720 秒。预留完成结果是
Agent 对 Steam reservation 和 lobby 状态的观察；Core 未持有服务器 RCON 凭据，不把它
宣称为服务器侧 reservation cookie 的独立验证。

## 非预留服务器

每个空闲健康 Agent 可为同一目标创建一个 standard lobby，直至达到该目标的并发上限。
Agent 创建后立即将 lobby metadata 标记为 `Game:state=game`，并随机生成完整 C1-C14
官方战役 metadata。

在至少一个外部成员进入前，不启动 30 秒计时。出现成员后，连续 30 秒没有新成员时，
Core 离开旧 lobby 并对同一服务器重建；只有 A2S 人数达到目标或原始尝试窗口结束时，
才回到全局调度选择下一个目标。

## 恢复与异常

Core 启动时先逐项查询 `active` 和 `uncertain` 操作。Agent 明确回报 `active` 时保留
操作与预留租约；操作不存在或停止时才结束记录并释放对应租约。读取失败会隔离 Agent，
而不是根据旧数据库记录猜测 Steam 已离开大厅。

共享游戏库维护租约有效时，调度器不启动新的暖服操作。共享库的安装、校验、更新与卸载
必须在单一受控维护窗口内进行，不能由多个 Agent 并发执行。
