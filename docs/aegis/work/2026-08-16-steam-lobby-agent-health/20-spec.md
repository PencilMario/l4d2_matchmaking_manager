# 单 Steam Lobby Agent 健康检查设计

## 目标

交付一个可在 Ubuntu Docker 主机运行的单账号 Agent，用来保留 Steam Desktop 的登录数据，并实时检查
`research/SteamLobbyProbe` 是否能在该会话中正常工作。首版不包含核心控制面、数据库、NATS、暖服编排、
reservation、RCON、L4D2 游戏客户端或第三方查询 API。

## 边界

Agent 只负责一个 Steam 账号，并只暴露本机或受信 Docker 网络内的健康接口。Steam Desktop 与 Probe 必须
使用同一个 Linux 用户和同一个 Steam 用户目录。Agent 不持有 Steam 密码；首次登录和 Steam Guard 由管理员
经受限 noVNC/X11 会话完成。

运行状态不写入数据库，也不保存为控制面的权威值。每次健康查询都重新执行 Probe 检查。每个 Agent 的持久化
volume 仅保存该账号的 Steam Desktop 登录、Steam Guard、userdata 与客户端配置，以及最小 Agent 配置；不保存健康
快照、lobby 资料或 reservation 状态。Steam AppID 内容、depot 与 appmanifest 放在一个受控的宿主机共享挂载，供
所有 Agent 使用，但其中不放置账号凭据。

## 组件

```text
Docker container
  s6/entrypoint supervisor
    Steam Desktop + Xvfb + optional noVNC
    l4d2-agent HTTP process
      SteamLobbyProbe Linux adapter
      GET /healthz
      GET /v1/probe/status
```

`l4d2-agent` 每次 `/v1/probe/status` 请求均在单一串行执行器中启动一次 Probe `health-check` 子进程。这样
健康结论直接来自研究工具本身，且 Agent 不保留 Steam API 状态；同一账号不会并发运行多个 Probe 进程。

## Linux 兼容门槛

现有 Probe 是 `net8.0`、`x86`，并显式加载 Windows `steam_api.dll`；它不能直接运行在 Ubuntu。首版必须将
Steam API 绑定与协议代码拆开：保留 `L4d2Protocol` 的平台无关逻辑，新增 Linux `libsteam_api.so` 绑定，并产出
`linux-x64` 可执行文件。不得使用 Wine 承载 Windows Steam 客户端或 DLL。

只有以下运行时证据全部成立，Agent 才报告 `ready`：

1. Steam Desktop 进程在运行，且 Probe 可以加载 `libsteam_api.so`。
2. `SteamAPI_Init` 成功，返回 AppID 550。
3. `SteamUser.BLoggedOn` 为 true，SteamID 非零。
4. Probe 能启用 ManualDispatch 并在限定时间内完成 `RequestLobbyList` 回调。

大厅列表为空不是失败；缺少回调、Steam 未登录、AppID 不为 550、ABI 加载失败或超时才是失败。检查不创建、修改、
加入或离开 lobby，因此可安全重复调用。

## HTTP 合约

`GET /healthz` 只反映 HTTP 进程存活，始终不触碰 Steam。

`GET /v1/probe/status` 触发一次新的只读 Probe 检查，返回：

```json
{
  "ready": true,
  "observedAt": "2026-08-16T12:00:00Z",
  "checks": {
    "steamDesktop": "ok",
    "steamApiInit": "ok",
    "appId": 550,
    "loggedOn": "ok",
    "manualDispatch": "ok",
    "lobbyListCallback": "ok"
  }
}
```

失败响应使用 HTTP `503`，仍返回机器可读的失败检查名和不含敏感信息的错误代码。接口不得返回 Steam 账号令牌、
cookie、密码、完整 Steam 安装路径或原始 Steam 回包。首版不要求 HTTP 鉴权，因为它只绑定受信 Docker 网络；
生产公开入口此前必须先接入核心的鉴权层。

## 容器与持久化

每个 Agent 使用独立命名 volume：`steam-data-<agent>` 挂载为 Steam 用户目录，保留登录、Steam Guard 授权、
userdata 与客户端配置；`agent-config-<agent>` 只存非敏感的配置。AppID 下载内容和 appmanifest 必须从一个已
存在的宿主机目录 bind-mount 到每个容器相同的库根路径，并由容器初始化写入账号本地的 Steam library 配置。容器
正常升级与重启不得删除这些 volume 或共享库。共享库的安装、更新、验证与卸载必须由单一维护操作串行执行；所有
Agent 使用同一镜像、同一共享库版本和独立账号目录。宿主机应给账号 volume 启用磁盘加密，并限制 Docker 管理权限。

noVNC 只用于首次登录或人工恢复，必须绑定 loopback、内网 VPN 或管理网，绝不发布到公网。Agent HTTP 端口也只
发布到受信网络。

## 错误处理与恢复

- Steam 尚未启动：返回 `steam_desktop_unavailable`；supervisor 按退避重试。
- Steam Guard、登录失效或未拥有 AppID 550：返回 `steam_not_logged_on` 或 `appid_mismatch`，要求管理员通过 noVNC 处理。
- Linux ABI 或架构不匹配：返回 `steam_api_load_failed`，不降级到 Wine。
- Steam 回调超时：返回 `lobby_list_timeout`，下一次请求重新检查，不缓存成功结果。
- 单次检查超时：取消本次 Probe 操作；串行执行器在清理后才接受下一次请求。

## 验收标准

本地自动化测试覆盖健康状态映射、超时、并发串行化、敏感字段过滤和 HTTP 503 语义。Docker 集成测试覆盖 Steam
未启动、Steam API 不可加载和伪造的成功 Probe adapter。真实 Ubuntu 验收使用已登录且拥有 L4D2 的账号，连续三次
请求 `/v1/probe/status` 均得到新的 `observedAt`、AppID 550、登录状态和 lobby-list 回调结果；重启容器后使用同一
volume 无需重新输入账号密码。

## 非目标

- 不创建、暖服、预留、绑定或管理任何游戏服务器。
- 不提供 lobby 详情查询、管理 UI、核心控制面或数据库。
- 不运行 `left4dead2`、Proton 或 L4D2 client runner。
- 不承诺 Steam Desktop 在所有无 GPU 主机上的行为；Ubuntu Steam API 真机验证是发布前门槛。

## 设计输入

- `research/SteamLobbyProbe/README.md`：Windows Steam Desktop 同会话及 ManualDispatch 已验证路径。
- `research/SteamLobbyProbe/SteamLobbyProbe.csproj`：当前 `net8.0` 与 `x86` Windows 约束。
- `research/L4d2Protocol`：已分离的 reservation/JoinData 平台无关协议代码。
