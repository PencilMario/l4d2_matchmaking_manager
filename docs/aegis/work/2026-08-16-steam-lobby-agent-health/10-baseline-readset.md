# 基线阅读集

- `research/SteamLobbyProbe/Program.cs`：当前动态加载 Steam API、ManualDispatch 与大厅列表回调的所有者。
- `research/SteamLobbyProbe/SteamLobbyProbe.csproj`：当前 `net8.0` / Windows x86 约束；Linux 发布需要移除固定 x86。
- `research/SteamLobbyProbe/README.md`：Steam Desktop 同会话运行和 AppID 550 证据边界。
- `research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj`：既有协议回归入口，首轮基线为 7/7 通过。
- `docs/aegis/work/2026-08-16-steam-lobby-agent-health/20-spec.md`：本次获批准的范围、持久化和 HTTP 合约。

事实：Probe 的默认路径会创建 lobby，不能用于健康检查；Linux `libsteam_api.so` ABI 尚未在目标主机验证。

假设：目标 Ubuntu 主机已经安装 Docker，且管理员可通过私网 noVNC 完成 Steam 首次登录并安装/挂载兼容的
L4D2 Linux `libsteam_api.so`。
