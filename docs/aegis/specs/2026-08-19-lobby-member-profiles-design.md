# 大厅成员 Steam 资料解析设计

## 目标

大厅查询返回成员 Steam64 ID 时，由 Core 使用 Steam Web API 解析公开 Steam 昵称和头像，并在前端成员列表展示。成员不是当前账号好友不影响解析。

## 架构与数据流

1. 全局设置 API 增加 Steam Web API Key 的设置入口。
2. Key 使用现有 `CORE_RCON_ENCRYPTION_KEY` 对应的 AES-GCM 保护器加密存储。
3. 读取全局设置只返回 `steamWebApiKeyConfigured`，不返回明文或密文。
4. `LobbyQueryService` 从 Agent 获得完整成员列表后，收集 Steam64 ID，调用 `ISteamUser/GetPlayerSummaries/v0002`，每批最多 100 个 ID。
5. Core 将返回的 `personaname` 和 `avatarmedium` 合并到成员快照；现有 `PersonaName` 字段继续复用，新增可空 `AvatarUrl`。
6. 前端显示头像和昵称；头像缺失时使用默认用户图标。

## 设置行为

- 设置页使用密码输入框，不回显已有 Key，只显示“已配置/未配置”。
- 输入新 Key 后保存会替换旧 Key。
- 提供独立清除操作，清除后停止外部资料查询。
- Steam Web API Key 无法解密或配置加密密钥缺失时，设置保存失败并返回明确错误；不得明文落库。

## 解析、缓存与失败处理

- 仅在 Key 已配置且成员数据完整时请求 Steam Web API。
- 使用内存缓存，成功资料缓存 15 分钟；失败结果短暂缓存 1 分钟，避免反复查询拖垮 API。
- 外部请求超时、限流、HTTP 错误、JSON 无效、资料私有或不存在时，不使大厅查询失败；对应成员保留 Steam64 ID，昵称和头像为空。
- Steam Web API 返回的 URL 只作为数据字段传递和展示，不接受用户提交的 URL 作为请求目标。
- 大厅查询仍以 Agent 的成员数据状态为准；外部资料补全失败不改变 `MemberDataStatus`。

## 兼容性边界与非目标

- Agent HTTP API 和 Steamworks 成员读取逻辑不变。
- 未配置 Key 时，现有大厅查询行为保持不变。
- 不在前端调用 Steam Web API，不向浏览器返回 Key。
- 不解析私有资料，不保证每个 Steam64 ID 都能获得昵称或头像。
- 不引入好友关系判断，也不要求成员是当前 Steam 账号好友。

## 验证

- Core 测试验证 Key 加密存储、读取不回显、更新和清除。
- Core 查询测试验证批量请求、资料合并、缓存和外部失败回退。
- 契约测试验证 `AvatarUrl` 可空兼容旧响应。
- 前端测试验证 Key 状态展示、密码输入、清除操作和成员头像/默认图标展示。
- 运行 Core 与前端完整测试及生产构建。

## 工作草案

### TaskIntentDraft

扩展全局设置和大厅查询，给非好友大厅成员补全公开 Steam 资料；风险集中在密钥保护、跨模块契约和外部 API 不稳定性。

### BaselineReadSetHint

重点基线为 `GlobalSettingsService`、`CoreSettings`、`RconCredentialProtector`、`LobbyQueryService`、`LobbyMemberSnapshot` 及前端设置/大厅成员视图。现有设置 API 已具备持久化入口，成员契约已具备昵称字段。

### ImpactStatementDraft

受影响层：Core 设置与数据库、Core 大厅查询、共享契约、前端设置和大厅成员展示。兼容性要求是 Key 未配置和资料解析失败时保持原查询成功路径；不改变 Agent 协议和 Steamworks 成员发现流程。
