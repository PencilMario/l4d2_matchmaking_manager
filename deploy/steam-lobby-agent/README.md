# Steam Lobby Agent 镜像

本目录构建供 Core 管理的单 Steam 账号 Agent 镜像。正式运行由
[`deploy/matchmaking-core`](../matchmaking-core) 中的 Core 创建容器；不要为每个正式
Agent 单独维护 Compose 服务。

每个 Core 管理的 Agent 使用独立的 `steam-data-<id>` 和 `agent-config-<id>` Docker
volume，但共享同一个宿主机游戏库，并在容器中挂载为 `/mnt/steam-library`。共享库只能
放 Steam App 内容和运行时，不能保存登录凭据或 Steam Guard 数据。

## 构建镜像

从仓库根目录执行：

```sh
docker build -f deploy/steam-lobby-agent/Dockerfile -t l4d2-steam-lobby-agent:local .
```

Core 默认使用该 tag；通过 `CORE_AGENT_IMAGE` 可改用受控的私有镜像 tag。

## 首次登录与资源策略

使用 Core 创建 Agent 后，它会返回一个仅回环绑定的 `noVncPort`。在 Docker 主机本机访问
`http://127.0.0.1:<noVncPort>/`，或从管理工作站通过 SSH 将该端口转发到本机，再完成
Steam 登录和 Steam Guard。

首次登录时 Steam 使用小屏幕 `-vgui -no-browser` UI。账号的 `loginusers.vdf` 出现
`MostRecent=1` 后，后续启动使用 `-silent -no-browser`，移除常规 Steam 窗口和浏览器
进程。需要重新登录时，将 Core `.env` 中的
`CORE_AGENT_STEAM_LOGIN_UI_MODE=always`，重启 Core 后重建目标 Agent；完成后恢复为
`auto`。独立诊断 Compose 则使用自身的 `STEAM_LOGIN_UI_MODE=always`。

每个账号的 Steam 配置均写入 `DisableShaderCache=1`。可在 Core 创建/更新 Agent 时设置
`downloadRegion`；更新后需调用 Agent 的 `recreate` 路由，令新容器取得新的环境变量。
共享 AppID 550 manifest 会设置 `AutoUpdateBehavior=1`，即仅游戏启动时更新；暖服 Agent
不会启动 L4D2 客户端。这是本部署的自动更新边界：它关闭的是 L4D2 的后台/常规更新，不
保证或阻止 Steam 客户端自身的 bootstrap 更新。后者没有可跨 Steam 版本验证的稳定配置，
且客户端更新可能影响 Steam API 兼容性；如需限制它，必须在实际 Ubuntu Steam 客户端上
另行验证后再作为运维策略实施。

## 独立 Compose 的边界

本目录的 `docker-compose.yml` 仅用于镜像/Steam 首次配置诊断。它会把 8080 和 8083
绑定至宿主机回环地址，不属于 Core 的正常业务路径。Core 创建的 Agent 只发布 noVNC
端口，Agent HTTP 保持在 Docker 内网。内部 API 见
[steam-lobby-agent-api.md](../../docs/steam-lobby-agent-api.md)。

Ubuntu 宿主机若启用了 AppArmor 用户命名空间限制，需要管理员按上游 Steam 镜像要求配置
`kernel.apparmor_restrict_unprivileged_userns=0`。这是宿主机级安全放宽，应限制 Docker
管理权限并定期复核。
