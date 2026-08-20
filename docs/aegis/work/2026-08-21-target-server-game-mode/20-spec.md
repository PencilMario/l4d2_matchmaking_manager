# 目标服务器大厅模式设计

## 方案

在 `TargetServer` 上增加 nullable `GameMode` 字段，JSON 名称为 `gameMode`。Core 在创建和更新入口统一规范化：空白变为 `null`，仅接受 `coop` 或 `versus`，其他值抛出 `invalid_game_mode`。响应原样返回规范化值，因此旧记录和未填写表单都可明确显示为未指定。

暖服计划把 `TargetServer.GameMode` 放到 `AgentOperationRequest.GameMode` init-only 属性。Agent 只把该字段用于创建 lobby 时的 metadata：先按当前 `RealSessionSettings` 生成完整 versus metadata，再在 `coop` 时覆盖 `Members:numSlots`、`Game:Mode` 和 `Game:sk_versus`。`versus` 与空值不覆盖任何字段；reservation settings 二进制 payload 仍使用当前默认生成路径。

前端在两个现有目标服务器表单实现中增加 `<select>`，选项为“未指定”、`versus`、`coop`，不提供自由文本输入。提交时将“未指定”转换为 `null`；编辑时从服务端回填当前值。详情展示规范化后的模式或“未指定”。

## 数据流

```text
TargetServerForm/select
  -> POST/PUT /v1/servers { gameMode }
  -> TargetServerService.Normalize
  -> TargetServer.GameMode (nullable)
  -> WarmupSchedulerService
  -> AgentOperationRequest.GameMode
  -> SteamNativeRuntime
  -> RealSessionSettings.CreateLobbyMetadata(profile, gameMode)
  -> Steam lobby metadata
```

## 错误处理与兼容性

- API 层将非法模式映射为现有 `400` 文本错误，不创建或修改记录。
- 旧数据库记录的 nullable 列读取为 `null`，不需要数据迁移脚本回填。
- Agent 内部请求保留可选字段；旧 Core/Agent 组合未传字段时仍生成旧 versus metadata。
- 本次不改变实际 reservation settings 编码、Steam lobby 生命周期或并发调度。

## 测试设计

- Core endpoint 回归：创建时保存 `coop`、空值保持 `null`、非法值返回 400；更新回填模式。
- Core scheduler 回归：计划启动请求携带目标模式。
- Protocol 回归：空值/versus 保持 8/versus/35，coop 产生 4/coop/19。
- Agent contract 回归通过现有 operation 测试与全项目编译验证可选参数兼容。
- 前端组件回归：下拉框选 `coop` 后提交 `gameMode: 'coop'`，未选择提交 `null`；运行 Vitest、TypeScript build。

## 影响边界

- 变更 owner：TargetServer 配置、调度请求和 lobby metadata 生成器各自只负责一段明确转换。
- 保留 owner：`CreateReservationSettings` 继续负责 reservation payload；不会新增第二套 reservation fallback。
- 非目标：自定义模式、其他 L4D2 metadata、部署脚本和数据库数据回填。
