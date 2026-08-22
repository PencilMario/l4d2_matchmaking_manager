# Atomic Tasks

- [x] 规则：先写 `WarmupPauseWindowRulesTests` 失败测试，再实现时间解析、排序、Asia/Shanghai 判断和非法输入。
- [x] 持久化：新增 `CoreSettings.WarmupPauseWindowsJson`、EF 配置和 `202608230001_GlobalWarmupPauseWindows` 迁移。
- [ ] API：新增时间段 DTO、专用 GET/PUT、组合 GET 扩展、保存时立即排空和 409 契约。
- [ ] 调度：恢复与 tick 在 A2S/selector/start 前检查有效暂停状态，暂停时复用 `DrainAllAsync`。
- [ ] 前端 API：新增时间段模型、CoreClient/ApiService 方法和 snapshot client wiring 类型。
- [ ] 前端卡片：两套设置入口拆成四张同级卡片，实现多时间段新增、删除、校验和独立保存。
- [ ] 文档：更新 Core API 与前端 API 契约，记录时区、跨午夜、排空和兼容边界。
- [ ] 验证：目标测试先 RED 后 GREEN，最后运行完整 Core/Frontend 测试、构建和 diff 检查。
