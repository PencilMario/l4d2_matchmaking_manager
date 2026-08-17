# Desktop 管理前端与 A2S 观测原子任务

- [x] 1. 扩展 `A2sServerInfo` 字段并增加服名/最大人数/截断包测试。
- [x] 2. 更新所有 `A2sServerInfo` 测试构造并运行调度器回归。
- [x] 3. 创建 `A2sEndpointResolver`，迁移调度器两处 IPv4 解析并验证行为不变。
- [x] 4. 创建 Observation snapshot/store/collector，加入五秒有界并发采样和不可用替换语义。
- [x] 5. 注册 collector（Testing 环境不启动）并通过 Core 全量测试。
- [x] 6. 创建 `/v1/servers/observations` DTO、投影服务和认证 endpoint。
- [x] 7. 增加 endpoint 的 pending/online/unavailable/401/route-precedence 测试。
- [x] 8. 更新两份 Core/Frontend API 文档并执行 `dotnet build`。
- [x] 9. 创建 `frontend/` Vite/TypeScript/React 工程、依赖锁定、开发代理和测试配置。
- [x] 10. 复制固定 React Bits revision 的视觉组件并建立桌面 tokens/层级。
- [x] 11. 实现 Core typed client、token 会话、五秒快照轮询与错误规范化。
- [x] 12. 实现 `Hyperspeed`、`Cubes`、`TargetCursor`、`LineSidebar` 与 Core status shell。
- [x] 13. 实现 Warm-up Workspace 和 Target Server A2S 资源表/详情抽屉。
- [x] 14. 实现 Target Server 完整 PUT 表单、RCON 只写和 drain/delete Stepper。
- [x] 15. 实现 Warm-up Agent 表、状态动作、quarantined 边界和重建/删除确认。
- [x] 16. 实现 Lobby 查询、metadata-only/member-complete 展示和 503 stale 结果。
- [x] 17. 添加前端单元测试、Core mock fixture 和 1280x720/1440x900 Playwright journey。
- [x] 18. 完成 API/前端 README、Aegis 索引和最终 Core/frontend/build/e2e 验证。
