# Desktop 管理前端与 A2S 观测任务意图

## TaskIntentDraft

实现已确认的桌面管理前端，并补齐 Core 的 A2S Observation 读模型，使 Target Server 行优先显示实时服名、当前人数/最大人数和观测状态。视觉实现使用 React Bits 与 Three.js `Hyperspeed`，主框架采用 Resource Workspace。

## 范围

- Core：扩展 A2S INFO 解码、增加内存态五秒观察器和认证的 `GET /v1/servers/observations`。
- Frontend：在全新 `frontend/` 目录创建 React/Vite/TypeScript 桌面应用，接入现有 Core API 与新增 Observation API。
- 视觉：`Hyperspeed`、`Cubes`、`TargetCursor`、`AnimatedList`、`Counter`、`Stepper`、`ClickSpark`、`GlassSurface` 和 `SpotlightCard`。
- 验收：仅桌面 `1280x720` 与 `1440x900`；移动端布局、触摸手势和窄屏导航不实现。

## 非目标

- 不改变 WarmupScheduler 的实时 A2S 决策边界。
- 不把 Observation 写入 PostgreSQL，不复用旧观测作为当前状态。
- 不开放手工创建 lobby、Agent 内网 API、Steam 凭据或 RCON 密码读取。
- 不在本计划中实现移动端。

## 风险提示

- Core 尚无 Observation collector；需要在不阻塞现有调度器的情况下增加独立内存态采样。
- 仓库没有前端工程和 Node 依赖清单；需要固定 React Bits 源码 revision 与前端依赖版本。
- WebGL/Three.js 场景可能影响桌面帧率或遮挡数据层，必须通过 canvas 像素与 Playwright 截图验证。
