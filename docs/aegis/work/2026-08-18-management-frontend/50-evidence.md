# 计划阶段证据

## 2026-08-18：设计与实施计划已就绪

### 已验证的证据

- `git show --check 6a91a71`：管理前端与 A2S Observation 设计文档提交无空白错误。
- `git show --check f91f41e`：桌面-only 范围修订、任务意图、基线读集、实施计划和原子任务已提交。
- 设计文档相对链接解析到 `docs/matchmaking-core-api.md`、`docs/matchmaking-core-frontend-api.md` 与 `CONTEXT.md`。
- 计划包含 10 个实现任务和 10 个任务级验证段；占位词扫描未发现未完成工作标记。
- 暂存检查确认 `.aegis/` 本地视觉评审产物未纳入两个文档提交。

### 未验证项

- 尚未编译或修改任何 Core/Agent 源码。
- 尚未创建 `frontend/` 或安装 Node 依赖。
- 尚未运行 A2S、HTTP、Vitest、Playwright、WebGL canvas 或真实目标服务器验收。

### ResumeStateHint

从 `30-plan.md` Task 1 开始。先创建隔离 worktree，读取 `00-intent.md`、`10-baseline-readset.md`、`30-plan.md`、`40-atomic-tasks.md` 和本文件；确认 `git status` 与 `f91f41e` 一致后，按 TDD 顺序执行 A2S INFO parser 扩展。

## 2026-08-18：Task 1 A2S INFO 解析器

### 已验证的证据

- 新 worktree `J:\GithubRep\l4d2_matchmaking_manager\.worktrees\management-frontend` 从 `a6f5762` 创建，分支为 `feature/management-frontend`。
- 首次 `--no-restore` 调用未产生测试产物，因而未作为测试证据；正常 restore/build 后，新增断言以 `CS1061`（缺少 `ServerName`/`MaxPlayers`）预期红灯。
- `dotnet test src\L4d2MatchmakingCore\tests\L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~SourceA2sClientTests --verbosity minimal`：2/2 通过。
- `dotnet test src\L4d2MatchmakingCore\tests\L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~WarmupSchedulerServiceTests --verbosity minimal`：18/18 通过。

### DriftCheckDraft

- Scope：仅扩展 canonical A2S INFO 字段并迁移其测试构造，不涉及 scheduler 决策、公开路由或持久化。
- Compatibility：`ISourceA2sClient.GetInfoAsync`、challenge flow 和现有 `PlayerCount` 读取不变；新增数据由 parser 唯一 owner 提供。
- Retirement：parser 不再丢弃 A2S 服名和最大人数；scheduler DNS 重复代码尚未变更。
- Decision：`continue`，进入 Task 2。

## 2026-08-18：Task 2 resolver 与 Observation collector

### 已验证的证据

- `A2sEndpointResolverTests` 首次以缺少类型的 `CS0103` 预期红灯，完成后为 3/3 通过（localhost IPv4、普通 DNS 失败返回 null、取消传播）。
- `TargetServerObservationCollectorTests` 首次以缺少类型的 `CS0246` 预期红灯，完成后为 4/4 通过（online、timeout/DNS/无效包 unavailable、未采样为空、后续失败清除旧 live 值）。
- collector 测试诊断出手工 `AddDbContext` lambda 每个 scope 生成不同 InMemory name/root；仅测试 helper 改为共享 name/root 后恢复同一数据库。生产 collector 和 store 未因该问题改变。
- `WarmupSchedulerServiceTests` 为 18/18 通过；完整 Core 测试为 73 通过、3 个 PostgreSQL 条件测试跳过。

### DriftCheckDraft

- Scope：resolver 取代 scheduler 的两处重复 IPv4 选择；collector 只写进程内展示快照，五秒 loop 仅在非 Testing 环境启动。
- Compatibility：scheduler 继续独立执行实时 A2S 判断；collector 不写数据库、不创建 lease、不调用 Agent API，失败样本清空 live 字段。
- Retirement：两处 inline DNS owner 已删除；没有新增调度 fallback。
- Decision：`continue`，进入 Task 3 HTTP read projection。

## 2026-08-18：Task 3 Observation HTTP API

### 已验证的证据

- `TargetServerObservationEndpointTests` 先以缺少 response DTO 的 `CS0246` 预期红灯，再以 6/6 通过验证 Bearer 401、空数组、pending、online、unavailable 与字面量路由。
- 现有 `CoreAuthenticationTests` 1/1 和 `TargetServerEndpointTests` 9/9 均通过。
- 完整 Core 测试为 79 通过、3 个 PostgreSQL 条件测试跳过；`dotnet build L4d2MatchmakingManager.sln --no-restore` 在先 restore 后为 0 警告、0 错误。
- 两份 API 文档已定义 Observation schema、三种状态、null live-field 规则、五秒前端轮询与 route ordering。

### DriftCheckDraft

- Scope：新增认证、只读的 `/v1/servers/observations`，每台配置服务器恰有一行，service 只依赖 DbContext 与 snapshot store。
- Compatibility：既有 `/v1/servers` shapes 与 scheduler owner 不变；HTTP route 不调用 A2S 或 Agent API。
- Retirement：不存在被保留的旧 HTTP read path。
- Decision：`continue`，进入 Task 4。

## 2026-08-18：Task 4 React/Vite shell

### 已验证的证据

- React Bits revision `4e0e030193b563be6be33d928f77d0d01cefe237` 已用 detached checkout 确认，10 个 TypeScript/CSS 组件复制到 `frontend/src/components/react-bits/` 并附来源及 MIT + Commons Clause notice。
- 前端 smoke 测试先以缺少 `App` 的 Vite resolve error 预期红灯，再为 1/1 通过。
- `npm run build` 和 `npm run test -- --run` 均通过。上游源码的两个未使用声明仅做无行为改变的严格 TypeScript 兼容性清理。
- 初次 npm install 被上游 `postprocessing@6.36.0` peer range 阻止；按固定 revision 的 lockfile 升到兼容 `three@0.180.0` 的 `postprocessing@6.37.8` 后成功生成 lockfile。

### 风险

- npm audit 报告 5 个上游依赖问题（1 moderate、3 high、1 critical）。未运行 `npm audit fix --force`，避免在视觉依赖树中引入未经验证的大版本替换。

### DriftCheckDraft

- Scope：独立 `frontend/` 目录，Vite 仅在开发时代理 `/healthz` 和 `/v1`；桌面最小视口为 1280x720。
- Compatibility：Core 未暴露新公网端口，浏览器不会直接调用 Agent/A2S；重型可视组件尚未挂载到数据刷新路径。
- Retirement：不保留 runtime component fetch；上游源码在固定 revision 内 vendored。
- Decision：`continue`，进入 Task 5。

## 2026-08-18：Task 5 client/state 与 Task 6 shell

### 已验证的证据

- `core-client` 测试先以缺少模块红灯，再以 4/4 通过 token header、direct response、409 JSON string、204 与 Observation bulk request。
- snapshot hook 测试先以缺少模块红灯；首次实现触发 697 次调用，根因是默认 options 对象引用不稳定。解构为 primitive/function dependencies 后 client/state 测试为 5/5 通过。
- shell 测试先以缺少模块红灯；jsdom WebGL 失败后将 VisualStage 限制为仅在 `WebGLRenderingContext` 存在时挂载，静态 fallback 保持数据层测试可用。shell 测试为 1/1 通过且无 `act` warning。
- 当前前端 production build 通过。Vite 报告单个 JS chunk 849.67 kB（gzip 259.47 kB），重型 Hyperspeed 依赖应在下一 visual pass 动态切分。

### DriftCheckDraft

- Scope：token 仅驻留 React 内存；401 回到 token entry。读取请求由单一 abortable hook 以 `Promise.all` 并行发起，五秒循环不会重挂载 VisualStage。
- Compatibility：Core client 不包装响应，不缓存 RCON，不把 Observation 拆成服务器 N+1 requests。WebGL 退化为静态背景，不阻挡数据平面。
- Retirement：不存在各 view 自主轮询或 runtime React Bits fetch。
- Decision：`continue`，进入主资源视图。

### DriftCheckDraft

- Scope：桌面管理前端与加性 A2S Observation API；移动端仍明确排除。
- Compatibility：现有 Core 路由、调度器实时决策和凭据隔离保持不变。
- Retirement：仅计划中的 DNS 解析重复代码会在 Task 2 收敛；尚未发生实际迁移。
- Decision：`continue`；随后按用户选择的 inline execution 继续实现。

## 2026-08-18：Task 13 Warm-up 与 Target Server 展示面

### 已验证的证据

- `TargetServerTable.test.tsx` 先以缺少模块红灯，再以在线服名/人数、unavailable/pending 空 live 字段及可配置入口验证 2/2 通过。
- `WarmupWorkspace.test.tsx` 先以缺少模块红灯，再以活动/uncertain/A2S 失败摘要、服务端 phase 原文、未返回 lobby 和拓扑筛选验证 2/2 通过。
- `TargetServerDrawer.test.tsx` 先以缺少模块红灯，再以实时 A2S 与配置区分离、关联暖服及关闭语义验证 2/2 通过。
- `WorkspaceShell.test.tsx` 验证默认暖服视图、拓扑切换至筛选后的 A2S 资源表和行级抽屉，2/2 通过。
- 聚焦验证：`npm run test -- --run src/features/workspace/WorkspaceShell.test.tsx src/features/servers/TargetServerDrawer.test.tsx src/features/servers/TargetServerTable.test.tsx src/features/warmups/WarmupWorkspace.test.tsx`，4 个文件、8 个测试全部通过。

### DriftCheckDraft

- Scope：实现桌面 Target Server 的 A2S 展示、暖服工作区及只读详情抽屉；未引入服务器写操作。
- Compatibility：Observation 只展示实时字段，配置始终来自 Target Server；A2S unavailable/pending 均不回填旧 live 值，WarmupScheduler/Agent/Core 所有权不变。
- Retirement：Workspace placeholder 已被实际 Warm-up 与 Target Server 视图取代；没有新增轮询或二级 A2S 请求。
- Decision：`continue`，进入 Target Server 完整 PUT 表单和 drain/delete 语义。

## 2026-08-18：Task 14-18 完成、最终回归与视觉验收

### 已验证的证据

- `npm ci`：安装锁定的 191 个包成功；npm audit 报告 5 个上游依赖问题（1 moderate、3 high、1 critical），本任务未执行强制升级。
- `npm run test -- --run`：12 个文件、24 个测试全部通过。覆盖 CoreClient 默认浏览器 fetch 上下文、Token/Observation 请求、Target Server PUT/RCON/drain、Agent quarantined/重建删除、Lobby 非零十进制校验与 503 stale 结果。
- `npm run build`：TypeScript 与 Vite 构建成功；主包 433.56 kB，Hyperspeed 动态包 653.37 kB，只有预期的 chunk size warning，无编译错误。
- `npx playwright test --trace on`：Chromium 单 worker 下 `desktop-1280`（1280x720，20.9 秒）和 `desktop-1440`（1440x900，30.0 秒）均通过。旅程覆盖登录、暖服 phase 原文、在线服名/2/12、unavailable/pending、拓扑筛选、409 drain 恢复、Lobby complete/metadata-only、视觉层 z-index、pointer-events 与文本溢出。
- Playwright trace 的 1440 截图已目视确认底部 Hyperspeed 道路正常绘制；`ffmpeg signalstats` 对 800x220 视觉区域统计 `YMAX=217`、`SATMAX=73`，证明截图存在非空且有色 3D 内容。运行时 canvas 使用 `alpha: true` 且不保留 WebGL drawing buffer，因此 E2E 使用可见/非零尺寸/截图断言，未修改渲染器仅为测试回读而开启 `preserveDrawingBuffer`。
- `dotnet test src\\L4d2MatchmakingCore\\tests\\L4d2MatchmakingCore.Tests.csproj --no-restore --verbosity minimal`：79 通过、3 个 PostgreSQL 条件测试跳过。
- `dotnet build L4d2MatchmakingManager.sln --no-restore`：0 个警告、0 个错误。
- `git diff --check`：无输出、无空白错误。

### 调试与修复记录

- Playwright 首次登录失败的根因是 `CoreClient` 将原生 `fetch` 作为未绑定方法调用；先加入失败回归，再改为 `globalThis.fetch(...args)` 委托，消除 Chromium `Illegal invocation`。
- E2E mock 改为按结构化 `URL.pathname + method` 分发 Observation、集合和单资源 PUT；Vitest include 限定为 `src`，避免收集 Playwright spec；Playwright web server 使用 TCP `port: 4174`，避免 HTTP 400 readiness 误判和遗留服务复用。

### DriftCheckDraft

- Scope：所有批准的 Core Observation 与桌面管理前端功能已实现；移动端、真实 Steam/A2S 生产验收仍是明确非目标。
- Compatibility：现有 Core 路由/响应、调度器实时 A2S 决策、认证、RCON 只写、Agent 隔离和 reservation lease 行为保持不变。
- Retirement：重复 scheduler DNS owner 已收敛到 `A2sEndpointResolver`；旧的 parser 丢弃字段、前端 placeholder、各视图独立轮询和 runtime React Bits fetch 均未保留。
- Decision：`continue` 到分支审查/集成；没有未解决的实现阻塞。

### Evidence Boundary

- Used：本文件中的计划摘要、工作树源文件与新鲜命令输出；Playwright trace 截图及其 `ffmpeg` 像素统计。
- Not loaded：完整 npm audit 报告、完整 Playwright trace 二进制和真实生产 Core/Steam 日志。
- Confidence：B；自动化覆盖核心用户旅程与桌面视觉层，真实目标服务器和生产反向代理仍需部署环境验收。
