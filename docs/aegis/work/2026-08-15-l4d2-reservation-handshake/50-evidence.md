# 证据记录

## 2026-08-15 协议基线

- RCON `status`：`0 humans`、`unreserved`。
- 服务器版本：`2.2.4.3 10097`；connect challenge 回包中的 `GetHostVersion` 为 `2243`。
- challenge 请求：`FFFFFFFF71726573657276653030303030303000`。
- challenge 回包：
  `FFFFFFFF41C40B09030300000000001730FEDE21C7400101726573657276653030303030303000`。
- challenge：`0x03090BC4`；认证类型：`3`；context：`reserve0000000`。

## ICE 独立测试向量

输入：

```text
challenge = 0x12345678
cookie    = 0x0186000047CF0FD8
key       = 6A98CC4C54B2ACB8
plain     = EFBEEDFED80FCF470000860100000000
```

PyPI `ICECipher 1.0` 输出：

```text
encrypted = 04944D6A637033FE8CD590BB4FE70B99
packet    = FFFFFFFF6EC30800001000000004944D6A637033FE8CD590BB4FE70B99
```

该依赖只安装在临时目录，用作独立 oracle，没有加入项目依赖。

## TDD 证据

- 首次有效 RED：`reservation-vector` 尚未实现，现有 CLI 将其误作旧参数并在 `ParseLobbyType` 失败。
- 修正测试夹具后使用 self-contained `win-x86` 发布，排除了本机缺少 x86 .NET 8 runtime 的环境噪声。
- GREEN：`Test-ReservationProtocol.ps1` 输出 `L4D2 reservation protocol vectors verified.`。
- exact L4D2 `SendReservationStatus` 反编译确认回包为 10 字节：
  `FFFFFFFF 70 hostVersion successBit`，不是 Kisak 新版的 status/ValveDS/slots 格式。

## Live reservation

请求前 RCON `status`：

```text
players : 0 humans, 0 bots (12 max) (not hibernating) (unreserved)
```

实际 challenge 与请求：

```text
challenge = 0x03090BC4
request   = FFFFFFFF6EC308000010000000753BBB7473666073136FE3303EAAD11D
cookie    = 0x0186000047CF0FD8
```

客户端在 5 秒内未收到 `S2A_RESERVE_RESPONSE`，但请求后立即读取 RCON：

```text
players : 0 humans, 0 bots (12 max) (not hibernating) (reserved 186000047cf0fd8)
```

服务器权威状态证明请求已被接受。空 settings 没有触发完整 game settings 过渡，UDP success bit 是否延迟到后续状态机
仍是后续观察项，不影响 cookie 接受结论。

## 独立审查与修正

独立只读审查未发现 Critical 问题，并确认 ICE level 1 key schedule、8 字节分块、challenge/reservation
字节偏移、旧 CLI 分发兼容性和凭据边界均符合当前契约。审查提出的两个 Important 已处理：

- exact L4D2 reservation response 现在必须恰好为 10 字节，success byte 只能是 `0` 或 `1`；尾随字段和
  `success=3` 等非规范响应会被拒绝。
- `Test-ReservationProtocol.ps1` 增加本地环回 UDP fixture，覆盖接受、拒绝、host version 不匹配、
  challenge 超时、无效 challenge、reservation 超时、尾随字段和非规范 success byte。

TDD RED 证据：修正前 `invalid-trailing` fixture 返回退出码 `0`，并输出
`accepted=True raw=FFFFFFFF70C308000001AA`；严格解析修正后完整 fixture 集合通过。

## 最终验证

2026-08-15 新鲜验证：

- `dotnet build -c Release`：成功，`0` 个警告，`0` 个错误。
- `Test-ReservationProtocol.ps1`：`L4D2 reservation protocol vectors verified.`。
- 同一 PowerShell 进程连续运行两次 `Test-ReservationProtocol.ps1`：两次均通过。
- `Test-LobbyJoinProtocol.ps1`：`ReplyJoinData encoding verified`。
- 新增三个 C# 文件的定向 `dotnet format --verify-no-changes`：通过。
- 十六进制与十进制 challenge/cookie CLI 输入：输出逐字节一致。
- 排除构建产物后的项目文本凭据扫描：未发现授权 RCON 密码。

完整项目 `dotnet format --verify-no-changes` 仍只报告 `Program.cs:298-312` 的既有空白格式；该区域与本任务无关，
没有为收尾而引入无关格式改动。

本证据支持“授权服务器接受 cookie `0x0186000047CF0FD8` 的最小 reservation 请求”。它不支持
“binary KeyValues game settings 已完成”或“真实客户端已经通过该 lobby 流程进入服务器”；后两项仍属于下一阶段。
