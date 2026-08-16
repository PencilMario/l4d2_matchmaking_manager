# 真实 Settings 与 JoinData 设计

## 设计结论

新增一个共享的 L4D2 session settings 所有者。它从已验证的真实 reserved lobby 快照构造 typed KeyValues，
并向 reservation 和 ReplyJoinData 提供不同视图：

```text
RealLobbySettings profile
        |
        +--> Reservation Settings
        |      Game + Members aggregates + Options + System
        |
        +--> ReplyJoinData Settings
               Game + dynamic Members + Options + System + Server
```

现有 `LobbyJoinProtocol` 不再私有拥有唯一一份 KeyValues writer；writer/parser 中可共享的协议代码提取为独立模块。
`LobbyJoinProtocol` 仍然拥有 Request/Reply 消息语义，`ReservationProtocol` 仍然拥有 UDP payload 和 ICE。

## 真实模板

模板使用真实 reserved lobby 的 28 字段快照。层级和名称保持 L4D2 观察到的大小写：

```text
Settings
  Game
    campaign = "L4D2C2"
    chapter = 1
    difficulty = "normal"
    dlcrequired = 0
    maxrounds = 3
    MissionInfo
      addon = 0
      Author = "Valve"
      builtin = 1
      DisplayTitle = "#L4D360UI_CampaignName_C2"
      InfectedOnly = 0
      MissionFile = "missions/campaign2.txt"
      SurvivorSet = 2
      Version = 1
      Website = "http://store.steampowered.com"
      workshopid = 0
    Mode = "versus"
    ModeInfo
      addon = 0
      workshopid = 0
    sk_versus = 35
    state = "game"
    vanilla = 1
  Members
    numMachines = 1
    numPlayers = 1
    numSlots = 8
  Options
    Server = "official"
  System
    access = "public"
    lock = ""
    network = "LIVE"
```

数字字段编码为 KeyValues int，文本和空字符串编码为 KeyValues string。模板不包含 `Server` 节点。

## Reservation 数据流

1. helper 创建 Steam lobby，取得实际 lobby CSteamID。
2. helper 以真实模板生成 reservation settings bytes。
3. UDP reservation 使用实际 lobby CSteamID 作为 cookie，并写入非零 settings length。
4. settings bytes 跟随现有 magic/cookie/length 进入同一个 ICE payload。
5. 服务器接受后，通过 exact UDP response 或只读服务器 status 验证 cookie。
6. 只有 reservation 已获得接受证据后，helper 才把 lobby 保持为可供客户端加入的游戏态。

旧 `reserve-server` 默认空 settings 行为保留，用于协议诊断。真实模板只由新的 `server-reserved` 高层模式选择，
避免改变已有脚本的参数和固定向量。

## JoinData 数据流

1. 真实客户端加入 helper-owned lobby 并发送 `SysSession::RequestJoinData`。
2. parser 验证顶层请求 ID、machine ID 和 player XUID 一致。
3. Reply 使用真实模板中的 `Game`、`Options` 和 `System`。
4. `Members` 根据 owner 和请求者动态构造：
   - `numMachines` 和 `numPlayers` 与实际输出 machine 数一致。
   - `numSlots` 保持真实模板值 `8`。
   - 请求者姓名、TU version 和 DLC mask 从 RequestJoinData 读取。
5. Reply 专属 `Server` 节点写入实际 endpoint 和实际 lobby CSteamID。
6. 客户端应使用 `connectstring` 连接 `202.105.108.88:27084`，而不是旧样本地址。

## CLI 与生命周期

为保留兼容性，现有命令不改变默认行为。新增明确的高层模式：

```text
SteamLobbyProbe.exe <steam_api.dll> server-reserved <ip:port> [lobbyType] [keepaliveSeconds]
```

该模式负责创建 lobby、写入真实 metadata、执行带真实 settings 的 reservation、处理 RequestJoinData 和清理 lobby。
它不写 `Server:*` lobby metadata，也不调用 `SetLobbyGameServer`，与真实 reserved lobby 的只读结果保持一致。

如果服务器没有即时发送 UDP success，模式输出 `verification=server_status_required` 并继续保持 lobby，但测试操作者必须先用
外部只读 status 确认 cookie，再让真实客户端加入。程序不内置 RCON，也不记录密码。

## 错误处理

- 模板序列化后必须能由同一 parser 完整读回，且没有尾随未消费数据。
- reservation settings 长度必须与实际 bytes 一致，并满足总 payload 上限和 ICE 对齐要求。
- RequestJoinData 缺少必要身份字段时不回复。
- endpoint、cookie、host version 或 response 不合法时沿用现有非零退出码语义。
- live 测试前目标服务器必须为 `0 humans` 且 `unreserved`；否则只报告状态，不发送 reservation。

## 兼容与退休边界

- 保留旧 `server` 模式作为不要求 reservation 的对照路径。
- 保留旧 `reserve-server` 空 settings 路径作为最小协议诊断。
- `LobbyJoinProtocol` 中私有 writer 在共享 writer 覆盖现有固定向量后退休，避免两套 binary KeyValues 编码所有者。
- 旧的手工精简 Reply settings 仅保留给 legacy `server`/`protocol-reply` 对照路径；`server-reserved` 使用真实模板。
  当 legacy 对照路径不再需要时，再删除精简 profile。machine/player 动态生成逻辑由两个 profile 共享。

## 验证标准

### 本地自动化

- 真实模板编码后逐字段解析回原值和原类型。
- reservation 明文中的 settings length 为非零且精确等于编码长度。
- 固定 challenge 下生成新的带 settings ICE 向量，并由独立解密/解析检查结构。
- 现有空 settings reservation 固定向量不变。
- 现有 RequestJoinData 样本生成 Reply 后，Game/System/Options 匹配真实模板，Members 动态值正确，Server 地址和 cookie 正确。
- 现有 `server`、`protocol-reply` 和两个协议测试保持通过。

### Live 验收

- helper 创建新 lobby，并使用该 lobby ID 完成带真实 settings 的 reservation。
- `202.105.108.88:27084` status 显示相同 cookie。
- 真实客户端加入 lobby 后收到 ReplyJoinData，控制台显示连接实际 endpoint。
- 服务器 status/A2S 显示该真实客户端进入；客户端不再出现 `failed to handle reservation request`。

## 证据边界

服务器接受带 settings 的 cookie 与客户端成功入服必须分别取证。仅看到 `reserved` 不能声称客户端已加入；
仅看到客户端开始 UDP connect 也不能声称服务器已接受 lobby reservation。
