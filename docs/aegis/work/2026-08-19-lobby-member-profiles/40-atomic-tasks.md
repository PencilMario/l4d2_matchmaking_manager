# 大厅成员 Steam 资料解析原子任务

- [x] 1. 为 `LobbyMemberSnapshot` 增加可空 `AvatarUrl`，为 `CoreSettings` 增加 Steam Web API 密文列和 migration。
- [x] 2. 扩展全局设置 DTO、服务和 endpoint，使用现有 AES-GCM 保护 Key，GET 只返回 configured 状态。
- [x] 3. 为设置加密、更新、清除和不回显行为补充 Core 测试，并先观察失败。
- [x] 4. 创建 `ISteamProfileService` 和 `SteamProfileService`，实现最多 100 个 ID 批量请求、15 分钟成功缓存和 1 分钟失败缓存。
- [x] 5. 用 fake HTTP handler 覆盖资料映射、批量、缓存、超时和 HTTP 失败回退。
- [x] 6. 在 `LobbyQueryService` 中仅为完整成员数据调用资料解析并合并昵称/头像，保留 Agent 失败语义。
- [x] 7. 为大厅 endpoint 增加资料合并、去重、metadata-only 跳过和外部失败仍成功的测试。
- [x] 8. 扩展前端设置模型和界面，增加密码输入、已配置状态和清除按钮，不回显 Key。
- [x] 9. 扩展前端大厅成员模型和视图，显示固定尺寸头像或默认用户图标并保留 Steam64 ID。
- [x] 10. 运行 Core、契约、Agent 与前端完整测试，执行前端生产构建和 diff 检查。
- [x] 11. 检查 API Key 不进入 GET 响应、日志、前端状态或测试快照，记录残余线上 API 风险。
