# 验证证据

## 本地验证（2026-08-16）

- `dotnet test research/L4d2Protocol/tests/L4d2Protocol.Tests.csproj --nologo`：7/7 通过。
- `dotnet test research/SteamLobbyProbe/tests/SteamLobbyProbe.Tests.csproj --nologo`：2/2 通过。
- `dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj --nologo`：10/10 通过。
- `dotnet build research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release --nologo`：0 warning，0 error。
- `dotnet build src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release --nologo`：0 warning，0 error。
- 两个项目的 `linux-x64` self-contained `dotnet publish` 已通过。
- `pwsh -File deploy/steam-lobby-agent/Test-ComposeContract.ps1`：通过。

## 授权远端验证（100.72.137.92）

- 独立部署目录：`/home/sirp/l4d2-steam-lobby-agent`；未使用历史 Steam 数据目录。
- Compose 镜像 `l4d2-steam-lobby-agent:local` 已在主机完成构建并运行。
- 初版容器曾映射为 `127.0.0.1:8080` 和 `127.0.0.1:6080`；该容器与两个初版卷已经按用户指示删除，并由后续 `8083` Desktop 镜像部署取代。
- 初版持久卷 `steam-lobby-agent_steam-data`、`steam-lobby-agent_agent-config` 已按用户指示删除；后续部署创建了同名的新空卷，当前用于新的 Steam 登录会话。
- `GET /healthz` 返回 `200 {"status":"alive"}`。
- 在尚未配置 `STEAM_API_LIBRARY_PATH` 时，连续三次 `GET /v1/probe/status` 均返回 `503`、`failure: steam_api_library_not_configured` 与不同的 `observedAt`；初始化检查为 `unknown`，没有返回路径、令牌或原始 Steam 输出。
- 使用容器内不存在的 `/tmp/missing-steam-api.so` 直接执行 Probe，返回机器可读的 `steam_api_load_failed`，且 `checks.steamApiInit` 为 `failed`。

## 待完成的真实会话检查

Steam Desktop 已运行并显示 installer，但尚未在此新 volume 完成登录，也尚未配置 AppID 550 的 `libsteam_api.so`。完成 Steam Guard、安装 L4D2 Linux 内容并设置 `STEAM_API_LIBRARY_PATH` 后，需要连续三次检查 `/v1/probe/status`，每次预期 HTTP 200、`appId: 550`、`loggedOn: ok`、`manualDispatch: ok` 和 `lobbyListCallback: ok`。

回滚命令为 `docker compose -f deploy/steam-lobby-agent/docker-compose.yml down`；禁止附加 `-v`，否则会删除本 Agent 的 Steam 登录数据。

## 2026-08-16 Desktop image replacement investigation

- Symptom: the custom desktop container accepted a noVNC WebSocket connection and Steam processes were alive, but its entrypoint launched `/usr/games/steam -silent`; the managed desktop therefore had no visible Steam main window.
- Canonical desktop owner: `josh5/steam-headless:latest`, maintained by the Steam-Headless project. It provides a Debian/Xfce desktop, loopback-compatible Web UI, Steam lifecycle and a supervisor extension point.
- Repair: `deploy/steam-lobby-agent/Dockerfile` now extends the tested image digest `sha256:1803d3d1bb51899dcc40a41625442a33108f1d205332eb09aa8dc662cfae0bd5` and adds the HTTP Agent as a supervisor process; Compose clears `STEAM_ARGS`, exposes the upstream Web UI only on `127.0.0.1:8083`, and retains only a `/home/default` Steam volume.
- Retirement: the custom Xvfb, Fluxbox, x11vnc, websockify and hand-written entrypoint are removed from the Docker image. They must not be reintroduced while the upstream desktop image remains the owner.
- Host prerequisite: the remote Ubuntu host has `kernel.apparmor_restrict_unprivileged_userns=0` set in `/etc/sysctl.d/90-steam-userns.conf`; this is documented because Steam's sandbox requires user namespaces.
- User-directed cleanup: the initial deployment container and both `steam-lobby-agent_steam-data` and `steam-lobby-agent_agent-config` volumes were removed after confirming they were mounted by no other container. The local image was retained; unrelated remote services were untouched.

## 2026-08-16 replacement deployment evidence

- The tested upstream image was imported with a one-off `regctl` process through `100.106.239.85:7890`, then pinned by digest. The temporary binary and image tar were deleted after `docker load`; no proxy was added to Docker, Compose or the current container.
- The lobby-only extension disables upstream GPU-driver installation, the unrelated Firefox/ProtonUp bootstrap and the accidental NVIDIA Flatpak mount path. It uses software rendering and does not add `SYS_ADMIN`, GPU devices or a game process.
- The post-init script enables the upstream Steam supervisor after its normal configuration runs. The final remote supervisor check showed `steam`, `l4d2-lobby-agent`, `desktop`, `frontend`, `xorg` and `x11vnc` all `RUNNING`.
- The remote container has only `127.0.0.1:8080` and `127.0.0.1:8083` listeners. A local SSH tunnel to `8083` served the upstream SteamHeadless noVNC page, and the visible desktop contained the Steam installer `Install` dialog.
- The fresh current call to `/healthz` returned `200`. `/v1/probe/status` returned the expected `503 steam_api_library_not_configured` with `steamDesktop: ok`; no status cache was introduced.
- `runuser -u default -- unshare --user --map-root-user true` succeeded in the current container. The temporary apt proxy file was absent.
- Fresh local regression: protocol 7/7, Probe 2/2, Agent 10/10 and the Compose contract all passed.
