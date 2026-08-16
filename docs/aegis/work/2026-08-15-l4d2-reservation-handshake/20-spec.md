# L4D2 Reservation 握手设计

## 架构

新增 `ReservationProtocol.cs` 作为纯协议所有者，负责字节编码、解码和 ICE 加密；新增
`ReservationCommand.cs` 负责 CLI 参数、UDP 生命周期和可诊断输出。`Program.Main` 只在第一个参数为
`reservation-vector` 或 `reserve-server` 时转发到新命令，其余既有调用保持原样。

## 数据流

1. 同一 UDP socket 发送 `FFFFFFFF 71 "reserve0000000\0"`。
2. 解析 `FFFFFFFF 41` 回包，读取 challenge、认证类型、server SteamID、secure 和 context。
3. 生成 8 字节 ICE key：两个 challenge XOR 值均按小端写入。
4. 生成 16 字节明文：`0xfeedbeef`、cookie、零 settings 长度。
5. 每 8 字节独立 ICE level 1 加密。
6. 发送 `FFFFFFFF 6E hostVersion payloadSize encryptedPayload`。
7. 解析 exact L4D2 的 `FFFFFFFF 70 hostVersion successBit`。成功回包可能由服务端后续状态机发送；
   最小空 settings 请求必须允许以服务器 `status` 作为接受证据。

## 错误处理

- 非 IPv4 endpoint、零 cookie、超时和错误 marker 返回非零退出码。
- challenge context 必须以 `reserve` 开头。
- payload 必须为正、最多 1024 字节且 8 字节对齐。
- live 命令不内置 RCON；响应超时时输出 `verification=server_status_required` 并返回非零退出码，
  服务器状态由外部只读验证完成。

## 兼容边界

- 现有 `<steam_api.dll> ...` 命令语法和行为不变。
- 新协议模块不加载 Steam API，也不调用 MatchFramework。
- 本阶段只验证 reservation cookie 接受，不声称完整 game settings 已实现。
