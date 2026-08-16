# 单主机暖服核心控制器：基线读取清单

| 资源 | 约束或证据 | 计划中的所有者 |
| --- | --- | --- |
| `CONTEXT.md` | 统一 Core Controller、Warm-up Agent、Reservation Admission Check 等术语。 | 所有代码和 API 文档。 |
| `20-spec.md` | 已确认架构、状态机、默认值、安全边界和非目标。 | 本任务的全部实现。 |
| `docs/steam-lobby-agent-api.md` | 当前 Agent 只有 health；控制端口不应向宿主机或公网发布。 | Agent HTTP 合约与部署文档。 |
| `docs/steam-lobby-automation.md` | 实时状态优先、单账号串行化、稳定事件判断和清理规则。 | Persistent Steam session actor。 |
| `research/SteamLobbyProbe/Program.cs` | 已验证的 Manual Dispatch、CreateLobby、metadata、member callback、LeaveLobby 与 reservation 调用。 | 抽取后的 Steam session runtime。 |
| `research/L4d2Protocol/RealSessionSettings.cs` | 当前真实 28 字段 profile 与 reservation settings。 | Campaign Profile 参数化。 |
| `deploy/steam-lobby-agent/91-enable-steam-supervisor.sh` | 每账号的 shared library、Shader Pre-Caching 配置。 | 下载地区、shader、自动更新配置。 |
| 本机 L4D2 VPK 解包 | C1-C14 title token、MissionFile、Author 和 SurvivorSet；C14 作者非 Valve。 | Agent metadata catalog。 |
| `D:\Steam\steamapps\appmanifest_550.acf` | `AutoUpdateBehavior="0"` 为默认自动更新；已检索到 Steam community 脚本将其改为 `"1"` 以仅在游戏启动时更新。 | Shared Game Library maintenance。 |

## 事实、假设与未知项

- 事实：Agent 与 L4d2Protocol 基线测试分别为 12/12 与 7/7；当前 health 实现每次启动短期 Probe，不能满足活动大厅中的任意状态查询。
- 事实：Agent 已验证可执行 reservation，但目标服务器没有 RCON 凭据；审计不可把 Agent 成功结果称为服务器侧独立验证。
- 假设：Docker 主机允许核心通过 `/var/run/docker.sock` 创建带项目标签的容器、卷和回环 noVNC 端口。
- 未知且不阻塞自动化：真实 Steam 的下载地区写入位置、Steam 客户端自身更新策略和完整首次游戏下载时间须在 Ubuntu 真机验收记录；AppID 550 的游戏自动更新行为用 `AutoUpdateBehavior="1"` 和不启动 L4D2 约束验证。

## 兼容边界

- `GET /healthz` 和 `GET /v1/probe/status` 保留路由、响应字段和失败码；其实现从短期子进程迁移至 persistent actor。
- 研究用 Probe CLI、健康测试与现有 standalone Agent Compose 保留；核心主路径不再以 `docker exec` 调用 Probe。
- Agent HTTP 在核心部署模式只在 Docker 内部网络监听；现有 standalone Compose 的回环 health 端口只用于诊断与兼容。
