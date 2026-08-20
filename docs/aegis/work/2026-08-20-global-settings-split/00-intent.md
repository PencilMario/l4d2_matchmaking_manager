# Task Intent

## Requested outcome

将全局设置中的 VNC 代理和 Steam Web API Key 拆成两个独立设置项与独立保存链路，避免保存其中一项时覆盖另一项。

## Scope

- Core：新增 VNC 代理与 Steam Web API Key 的独立读取/更新端点。
- Frontend：旧版 `GlobalSettingsView` 与新版 `SettingsWorkspace` 均使用独立保存动作。
- Compatibility：保留现有 `/v1/settings` 读写接口供旧客户端使用。
- Tests/docs：补充跨层契约测试并更新 API 文档。

## Non-goals

- 不修改数据库列名或现有加密方式。
- 不改变 VNC 代理注入 Agent 容器的运行时行为。
- 不回显 Steam Web API Key。

## Risk hints

- 当前旧请求模型把代理和 API Key 放在同一 PUT body 中，部分更新可能清空另一项。
- 项目同时存在两套前端设置组件，必须同步修改。
