# Steam Lobby Agent Deployment

This Compose service extends the maintained `josh5/steam-headless` Steam Desktop image, pinned to a tested image digest. Its Xfce desktop and Web UI expose only loopback ports. The `steam-data` volume is the Steam account's persistent home directory; do not use `docker compose down -v` unless intentionally removing its login data.

Steam application content is deliberately outside that account volume. Every lobby agent mounts the same existing host directory at `/mnt/steam-library`; initialization writes that path to the account-local Steam library configuration. The shared directory contains `steamapps` depots and manifests but no Steam account credentials. Each account keeps a separate `steam-data-<agent>` volume for login, Steam Guard, userdata and client configuration.

Shader Pre-Caching is disabled in each account-local Steam configuration (`DisableShaderCache=1`). An optional `STEAM_DOWNLOAD_REGION` is also written only to that account's `config.vdf`. Neither setting is stored in the shared game library.

After AppID 550 is installed, initialization changes its shared manifest to `AutoUpdateBehavior=1` (update only when launched). Lobby agents never launch the game client, so ordinary Agent startup does not trigger a game update. The Core's future shared-library maintenance lease must serialize installation, validation, update and uninstall work before this value is restored. This does not establish that Steam client self-updates are disabled; validate that separately on the Ubuntu deployment host.

## Host prerequisite

Steam requires unprivileged user namespaces. On Ubuntu hosts that enable AppArmor's user-namespace restriction, an administrator must allow them before starting the service:

```text
printf '%s\n' 'kernel.apparmor_restrict_unprivileged_userns=0' | sudo tee /etc/sysctl.d/90-steam-userns.conf >/dev/null
sudo sysctl --system
```

This is a host-wide security relaxation. Limit Docker administration access, keep the Compose service's ports on loopback, and review the setting when Steam or the host policy changes.

## First Login

Before the first start, create an `.env` from `.env.example` and create the configured shared library directory. The test host uses `/mnt/storage/l4d2-steam-library`:

```text
cp .env.example .env
sudo install -d -o 1000 -g 1000 -m 0750 /mnt/storage/l4d2-steam-library
```

The directory must already exist; Compose refuses to create it automatically. On the Docker host, start the service with `docker compose up -d --build`. From an administrator workstation, create a private SSH tunnel:

```text
ssh -L 8083:127.0.0.1:8083 <user>@<host>
```

Open `http://127.0.0.1:8083/`, select **Connect**, then sign in to Steam and complete Steam Guard. Before a successful login, the Agent starts Steam with the small-screen `-vgui -no-browser` UI. After Steam records a most-recent account in the private `loginusers.vdf`, later starts use `-silent -no-browser`, removing the Steam window and browser process. The noVNC endpoint remains loopback-only for recovery; set `STEAM_LOGIN_UI_MODE=always` in `.env` and recreate the service when Steam Guard or reauthentication needs a visible window. Install AppID 550 only to obtain its Linux Steam API library; the service never launches the game executable. Locate the installed `libsteam_api.so`, set `STEAM_API_LIBRARY_PATH` in `.env`, optionally set `STEAM_DOWNLOAD_REGION`, then recreate the service with `docker compose up -d`. The restart applies the AppID 550 update policy after the manifest exists.

For additional agents, reuse the same `STEAM_SHARED_LIBRARY_HOST_PATH` and image, but give each service a unique `steam-data-<agent>` volume. Do not run simultaneous install, validate, update or uninstall operations against the shared library.

## Health Check

The agent process endpoint is local to the Docker host:

```text
curl http://127.0.0.1:8080/healthz
curl http://127.0.0.1:8080/v1/probe/status
```

The second request uses the Agent's persistent Steam session actor. It reports `503` until Steam Desktop is running, logged in, uses AppID 550 and completes a lobby-list callback. Its response contains no Steam credentials, native library path or raw Steam data.

## Rollback

Use `docker compose down` to stop the Agent while retaining Steam login data. Do not append `-v` unless the account's persistent data should be deleted.
