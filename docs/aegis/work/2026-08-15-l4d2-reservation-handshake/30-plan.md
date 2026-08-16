# L4D2 Reservation Handshake Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:test-driven-development and
> aegis:verification-before-completion. Steps use checkbox syntax for tracking.

**Goal:** 实现并实测 L4D2 UDP reservation 握手。

**Architecture:** 纯协议与 UDP 命令分离；协议向量可离线测试，live 命令复用同一实现。

**Tech Stack:** C#/.NET 8、PowerShell、UDP、ICE level 1。

**Baseline / Authority Refs:** `10-baseline-readset.md` 中列出的 L4D2 反编译与同源代码。

**Compatibility Boundary:** 不改变既有 Steam lobby、JoinData 和 MatchFramework 行为。

**Verification:** `Test-ReservationProtocol.ps1`、全部既有协议测试、live UDP 结果语义、RCON `status`。

---

### Task 1: 固定协议契约

**Files:**
- Create: `research/SteamLobbyProbe/Test-ReservationProtocol.ps1`

**Verification:**
- 运行测试，预期在实现前因 `reservation-vector` 命令不存在而失败。
- 实现后固定向量必须等于 `04944D6A637033FE8CD590BB4FE70B99`。

### Task 2: 实现 ICE 与协议编码

**Files:**
- Create: `research/SteamLobbyProbe/IceCipher.cs`
- Create: `research/SteamLobbyProbe/ReservationProtocol.cs`

**Verification:**
- 固定 challenge `0x12345678`、cookie `0x0186000047CF0FD8` 的 key、明文、密文和完整 packet 全部匹配测试。
- challenge 与 reservation status 样本回包解析结果匹配测试。

### Task 3: 实现 UDP 诊断命令

**Files:**
- Create: `research/SteamLobbyProbe/ReservationCommand.cs`
- Modify: `research/SteamLobbyProbe/Program.cs`

**Verification:**
- `reserve-server 202.105.108.88:27084 0x0186000047CF0FD8` 输出 challenge 和 reservation request；
  若 exact L4D2 未即时发送 success bit，明确输出 response timeout 和外部验证要求。
- 本地环回 UDP fixture 覆盖接受、拒绝、host version 不匹配、challenge 超时、reservation 超时和畸形回包。
- 超时、错误回包和无效参数返回非零退出码。

### Task 4: 授权服务器验收与文档

**Files:**
- Modify: `research/SteamLobbyProbe/README.md`
- Modify: `docs/aegis/work/2026-08-15-l4d2-reservation-handshake/50-evidence.md`

**Verification:**
- 请求前 RCON 状态为 `0 humans`、`unreserved`。
- 请求后状态显示 cookie `186000047cf0fd8`，即使 UDP success bit 未在超时窗口内到达。
- 运行相关回归测试与 Release build。
