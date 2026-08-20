# Task Intent

## Requested outcome

让部署在 `100.72.137.92` 的 Agent `phbbh21773----cmqy31466N` 正确安装 Millennium，
并将 Steam 下载区域设置为数字区域 `197`，同时恢复 VNC 连接和 Agent 就绪状态。

## Scope

- 修复 Millennium Lua RPC 与前端之间的对象结果传输。
- 将数字区域目标保存到账号专属 Millennium 配置文件。
- 更新远端 Agent 镜像中的插件目录并验证 Core、Agent、Steam 与 VNC。

## Non-goals

- 不删除 Steam 账号卷、登录凭据或历史日志。
- 不改变 Core 的 Agent 生命周期或 VNC 代理架构。

## Root-cause statement

Millennium Lua RPC 只可靠传输标量；backend 直接返回 Lua table，frontend 收到空结果，
因此没有读取到目标区域 `197`，界面继续显示 Steam 当前区域 `47`。
