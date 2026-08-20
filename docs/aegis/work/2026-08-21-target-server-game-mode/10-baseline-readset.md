# 基线读取集

## 代码与契约

- `src/L4d2MatchmakingCore/Data/Entities.cs`：`TargetServer` 持久化实体。
- `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`：实体列约束与 EF 模型。
- `src/L4d2MatchmakingCore/Servers/TargetServerDtos.cs`、`TargetServerService.cs`、`TargetServerEndpoints.cs`：目标服务器 API 的 canonical owner、校验和响应。
- `src/L4d2MatchmakingCore/Scheduling/WarmupSchedulerService.cs`：把目标配置转换为 `AgentOperationRequest` 的暖服调度边界。
- `src/L4d2Matchmaking.Contracts/AgentContracts.cs`：Core 与 Agent 的操作请求契约。
- `research/L4d2Protocol/RealSessionSettings.cs`：当前 versus lobby metadata 的唯一生成器。
- `src/L4d2LobbyAgent/Steam/SteamNativeRuntime.cs`：实际创建 Steam lobby 并写入 metadata 的 owner。
- `frontend/src/components/TargetServerModal.tsx`、`frontend/src/services/api.ts`、`frontend/src/types/index.ts`：当前 App 使用的目标服务器表单、请求适配和展示模型。
- `frontend/src/features/servers/TargetServerForm.tsx`、`frontend/src/api/models.ts`：并行 workspace 实现使用的目标服务器表单和类型，需保持编译与行为一致。

## 基线行为

- `TargetServer` 没有模式列；旧记录默认由 `RealSessionSettings` 生成 versus metadata。
- `AgentOperationRequest` 只有 warm-up operation mode、目标 IPv4/端口和可选 RCON 密码。
- `RealSessionSettings.CreateLobbyMetadata` 从 reservation settings 展平出 `Members:numSlots=8`、`Game:Mode=versus`、`Game:sk_versus=35`。
- 当前 active frontend 是 `App -> TargetServersView -> TargetServerModal`；`features/servers` 是同仓库的 workspace 实现和测试覆盖。

## 兼容边界

- 新增数据库列必须 nullable，不回填旧记录；API 的新增字段也必须 nullable。
- `AgentOperationRequest` 保留现有 positional constructor，并以 nullable init-only `GameMode` 属性扩展 JSON 契约，保持现有 constructor 调用可编译。
- `CreateLobbyMetadata` 的现有无模式调用保持原输出；reservation settings 编码不因 lobby mode 改变。
- 未修改的工作区文件（当前已有 Steam status、Core lobby query 等改动）不在本任务范围内。
