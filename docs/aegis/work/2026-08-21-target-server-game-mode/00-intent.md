# 目标服务器大厅模式

## 目标

为新增和编辑目标服务器增加一个可选的大厅模式预设。模式只允许 `coop`、`versus` 或留空；留空继续沿用当前大厅 metadata 的 `versus` 默认值。选择 `coop` 时，暖服节点创建大厅的 metadata 使用 `Members:numSlots=4`、`Game:Mode=coop`、`Game:sk_versus=19`。

## 事实、假设与未知

- 事实：目标服务器配置由 Core 的 `TargetServer` 实体、HTTP DTO 和 React 表单共同维护。
- 事实：Core 调度时通过 `AgentOperationRequest` 把目标地址和预留模式传给 Warm-up Agent。
- 事实：Warm-up Agent 的 `SteamNativeRuntime` 调用 `RealSessionSettings.CreateLobbyMetadata` 写入大厅 metadata；当前默认值是 8 个 metadata slots、`versus`、`sk_versus=35`。
- 假设：模式配置只改变新建大厅的 metadata；现有 reservation UDP payload 保持原有格式，避免扩大协议行为范围。
- 假设：空值在数据库和 API 响应中保持为 `null`，运行时由 metadata 生成器按 `versus` 默认值处理。

## 非目标

- 不允许用户输入任意字符串，也不新增自定义 metadata 编辑器。
- 不改变暖服调度优先级、并发、预留租约、A2S 观测或大厅生命周期规则。
- 不修改现有 reservation settings 编码；本次只覆盖 lobby metadata。

## 验收标准

1. 创建/更新目标服务器时，`gameMode` 省略、`null` 或空白会保存为 `null`；`coop` 和 `versus` 可保存；其他值返回 `400 invalid_game_mode`。
2. 目标服务器列表/详情返回 `gameMode`，旧记录返回 `null`。
3. 调度创建 Agent 操作时传递该目标的 `gameMode`。
4. Agent 生成 metadata 时，空值与 `versus` 保持现有值；`coop` 只覆盖三个指定键。
5. 前端新增/编辑表单使用非文本输入的下拉框，仅显示空白、`versus`、`coop` 预设，并正确提交 `null` 或所选值。
