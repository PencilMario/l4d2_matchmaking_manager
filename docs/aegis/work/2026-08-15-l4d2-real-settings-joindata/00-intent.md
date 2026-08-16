# 任务意图

## 目标

使用真实 L4D2 客户端创建的 reserved lobby 数据，构造 binary KeyValues `Settings`，让同一份会话设置同时用于：

- 向 `202.105.108.88:27084` 发送带 settings 的 UDP reservation。
- 响应真实客户端的 `SysSession::RequestJoinData`。
- 把实际服务器地址和 lobby cookie 通过 `SysSession::ReplyJoinData` 交给客户端。

最终目标是验证真实 L4D2 客户端通过 helper 创建的 lobby 进入要求
`sv_allow_lobby_connect_only=1` 的目标服务器。

## 已批准范围

- 真实 metadata 快照是 canonical game/session 模板。
- `Game`、`System`、`Options` 在 reservation 和 ReplyJoinData 中复用。
- reservation 使用聚合 `Members` 数据，不包含 `Server` 节点。
- ReplyJoinData 根据实际请求者构造 machine/player，并增加 `Server` 节点。
- `connectstring` 使用真实目标地址；`reservationid` 使用 helper 创建的 lobby CSteamID。

## 非目标

- 不重放包含旧地址、旧成员和旧 lobby ID 的历史 ReplyJoinData blob。
- 不把 Steam metadata 的字符串序列直接当作 binary KeyValues。
- 不改变现有 `server`、`reserve-server`、`protocol-reply` 命令语义。
- 不修改服务器配置、插件或地图。
- RCON 密码不写入项目文件或新 CLI 参数。

## 风险

- Steam metadata API 把所有值暴露为字符串，但 binary KeyValues 必须恢复正确类型。
- 真实 lobby metadata 不包含 machine/player 子树；ReplyJoinData 必须继续从 RequestJoinData 获取请求者信息。
- 服务器可能已 reserved、有玩家或处于不可测试状态；live 测试前必须只读检查。
