# 单 Steam Lobby Agent 实施与部署总结

日期：2026-08-16
验证主机：`100.72.137.92`（`sirphomesv`）

## 交付范围

本轮只实现并部署了一个单账号 Steam Lobby Agent，用于验证已登录的 Steam Desktop 会话能否运行
`research/SteamLobbyProbe` 的只读健康检查。它不启动 `left4dead2`，不使用 Wine、Proton 或
`l4d2-client-runner`，也不创建、加入、离开或预留任何 lobby。

当前组件关系：

```text
Docker container
  Steam-Headless Desktop（Steam 登录会话）
  l4d2-lobby-agent HTTP 服务
    GET /healthz
    GET /v1/probe/status
      SteamLobbyProbe health-check（每次请求重新执行）
```

`/healthz` 只表示 HTTP 进程存活。`/v1/probe/status` 每次均启动新的 Probe 子进程，串行访问同一
Steam 账号，直接检查 Steam API、登录状态和大厅列表回调；不缓存健康结论或大厅状态。

## 部署结构

- 远端源码目录：`/home/sirp/l4d2-steam-lobby-agent`
- Compose 目录：`/home/sirp/l4d2-steam-lobby-agent/deploy/steam-lobby-agent`
- 运行容器：`steam-lobby-agent-steam-lobby-agent-1`
- 当前镜像摘要：`sha256:a92d2d591b9c745352e0133b88f4c6b7b63ec956c13f9df82f35529198d69596`
- Agent 仅监听 `127.0.0.1:8080`；Steam Desktop Web UI/noVNC 仅监听 `127.0.0.1:8083`。

Desktop 基础镜像采用 Steam-Headless 的 `josh5/steam-headless`，由其自身负责 Xfce、Xorg、Steam
生命周期与 Web UI。Agent 仅作为 supervisor 扩展加入，不再维护自制的 Xvfb、Fluxbox、VNC、websockify
或 Steam 启动脚本。

Steam 运行所需的 user namespace 已在主机配置：

```text
/etc/sysctl.d/90-steam-userns.conf
kernel.apparmor_restrict_unprivileged_userns=0
```

这解决了 Steam 的 `Steam now requires user namespaces to be enabled` 报错。未持久化配置代理；拉取镜像
期间仅通过指定命令临时使用 tailnet 代理，部署后没有 Docker、Compose 或容器级默认代理。

## 存储与账号隔离

每个未来 Agent 都必须保有独立 Steam 登录卷。当前卷为：

- `steam-lobby-agent_steam-data`：Steam 登录、Steam Guard、userdata 与客户端配置。
- `steam-lobby-agent_agent-config`：非敏感 Agent 配置。

共享的非凭据文件位于宿主机：

```text
/mnt/storage/l4d2-steam-library
```

其中包括共享 Steam 库内容以及供 x64 Probe 使用的运行时 API：

```text
/mnt/storage/l4d2-steam-library/agent-runtime/steamrt64/libsteam_api.so
```

容器中的对应路径为：

```text
/mnt/steam-library/agent-runtime/steamrt64/libsteam_api.so
```

远端 `.env` 已将 `STEAM_API_LIBRARY_PATH` 配置为该容器路径。共享库的安装、更新、校验和卸载必须串行，
避免多个 Agent 同时修改同一份 appmanifest 或 depot。升级或停止服务只能使用 `docker compose down`，绝不能
追加 `-v`，否则会删除 Steam 登录数据。

## Steam 配置

已完成 Steam 登录与 L4D2 内容下载。初始化脚本会在账号本地 Steam 配置中写入：

```text
ShaderCacheManager/DisableShaderCache = 1
```

即关闭 Shader Precaching。下载地区保留为可由未来核心控制面设置的接口/配置项，本轮没有把地区状态写入
数据库或作为 Agent 的持久运行状态。

## Linux API 兼容性修复

排查发现 L4D2 自带的 `bin/libsteam_api.so` 是 32 位 ELF，不能由 `linux-x64` 的 Probe 加载。最终使用已登录
Steam Desktop 的 Steam Runtime 64 位 `libsteam_api.so`，复制到共享库的 `agent-runtime/steamrt64`。

该运行时库不导出旧式的 `SteamAPI_Init` 和直接接口 getter。为兼容两种库实现，
`research/SteamLobbyProbe/Program.cs` 改为：

1. 优先使用传统 L4D2 API 导出。
2. 找不到时改用 `SteamAPI_InitFlat`，并将返回码 `0` 视为成功。
3. 再通过 `SteamInternal_FindOrCreateUserInterface` 获取 `SteamMatchMaking009`、`SteamUtils011` 和
   `SteamUser023`。

`src/L4d2LobbyAgent/Probe/ProbeCommandRunner.cs` 同时修复了两类失败处理：Probe 的可选 `appId` 或
`lobbyCount` 为 `null` 时返回结构化 HTTP 503，而非 Agent HTTP 500；Probe 在 JSON 前输出 Steam 回调诊断时，
解析最后一行 JSON。

## 最终验证

远端对同一已登录会话连续三次执行 `GET /v1/probe/status`，每次均返回 HTTP 200，核心结果一致：

```json
{
  "ready": true,
  "checks": {
    "steamDesktop": "ok",
    "steamApiInit": "ok",
    "appId": 550,
    "loggedOn": "ok",
    "manualDispatch": "ok",
    "lobbyListCallback": "ok",
    "lobbyCount": 50
  }
}
```

另行确认：

```text
shader-precaching=disabled
shared-steam-api=readable
probe_http=200
```

本地最终回归也已通过：

```text
dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --no-restore
12/12 passed

dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --no-restore
2/2 passed

powershell -NoProfile -ExecutionPolicy Bypass -File deploy/steam-lobby-agent/Test-ComposeContract.ps1
passed
```

## 当前限制与后续工作

- 此 Agent 只是后续暖服系统的底座健康检查，不包含核心控制面、子服务注册、数据库、NATS、reservation 握手、
  owner 离开后的 URI 重入或暖服调度。
- 对外只读大厅查询 API，以及其 login token + API key 鉴权，仍应由后续核心服务实现。它只能返回给定 Steam
  Lobby ID 的 Probe 查询结果，不应暴露 Agent 健康、账号状态、凭据或 Steam 原始输出。
- 动态状态必须持续即时查询，不可写入数据库作为权威值。
- 调查期间安装的 Steamworks SDK Redist（AppID `1007`）约 103 MB，未被最终方案使用；如需清理，应在 Steam
  Desktop 中正常卸载，不要手动删除 depot 文件。
