# 单主机暖服核心控制器：原子任务

- [x] 建立共享 HTTP contracts、核心解决方案与测试工程。
- [x] 将真实 metadata 参数化为 C1-C14 完整 campaign profile catalog。
- [x] 从研究 Probe 抽取每账号单一 Manual Dispatch session actor。
- [x] 将 Agent HTTP 改接 actor，并增加幂等操作与任意 lobby 查询 API。
- [x] 固化共享库、下载地区、Shader Pre-Caching 与 AppID 550 自动更新策略。
- [x] 建立 Core 的配置、认证、PostgreSQL 实体和迁移。
- [x] 提供受认证的目标服务器管理 API。
- [x] 提供 Agent Docker 生命周期管理与内部 Agent client。
- [x] 实现 A2S 客户端和 Agent 读取驱动的 lobby 查询代理。
- [x] 实现可注入时钟的暖服状态机及全部定时/并发规则。
- [x] 实现持久化调度、预留租约、恢复和后台服务。
- [ ] 提供核心 Compose、更新部署文档并执行自动化与真机验收。
- [x] 已登录 Agent 的 auto/never 路径停用 VNC supervisor，并验证 Steam actor 继续就绪。
