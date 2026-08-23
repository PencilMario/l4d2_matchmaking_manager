# 部署与更新指示

本文档面向在本地 Windows 工作站上，通过 SSH 更新 `100.72.137.92` 上本项目的维护者。
API 字段、状态和错误码以 [Core 前端 API 契约](matchmaking-core-frontend-api.md) 为准；本文档
只说明发布、更新、验收和回滚操作。

## 1. 部署基线

| 项目 | 当前值 |
| --- | --- |
| SSH 别名 | `sirphomesv` |
| SSH 用户/地址 | `sirp@100.72.137.92:22` |
| Core 源码目录 | `/mnt/storage/l4d2-matchmaking-core` |
| Core Compose | `/mnt/storage/l4d2-matchmaking-core/deploy/matchmaking-core/docker-compose.yml` |
| Core 环境文件 | `/mnt/storage/l4d2-matchmaking-core/.env` |
| Core API | 远端回环 `http://127.0.0.1:18080` |
| 前端目录 | `/home/sirp/l4d2-matchmaking-frontend` |
| 前端 Compose | `/home/sirp/l4d2-matchmaking-frontend/deploy/docker-compose.yml` |
| 前端环境文件 | `/home/sirp/l4d2-matchmaking-frontend/deploy/.env` |
| 管理页面 | `http://100.72.137.92:8084` |
| Docker 网络 | `l4d2-matchmaking` |
| 共享 Steam 游戏库 | 宿主机 `/mnt/storage/l4d2-steam-library`，容器 `/mnt/steam-library` |
| Core Compose 项目名 | `matchmaking-core` |
| 前端 Compose 项目名 | `deploy` |

截至 2026-08-23，本机已核对的运行版本示例为：

- Core 镜像：`l4d2-matchmaking-core:codex-0d8cad8`
- Warm-up Agent 镜像：`l4d2-steam-lobby-agent:codex-0d8cad8`
- 前端 release：`release-20260823-0d8cad8`
- 现场备份：`/mnt/storage/.l4d2-matchmaking-core-backup-20260823-0d8cad8`

上面的版本号只用于识别当前现场。后续更新必须为每次发布生成新的不可变 tag 和新的前端
release 目录，不要覆盖旧版本。

## 2. 重要安全边界

- 不要执行 `docker compose down -v`，也不要删除 `matchmaking-core_postgres-data`、
  `steam-data-*` 或 `agent-config-*` 卷；这些卷包含数据库和 Steam 账号状态。
- 正式 Warm-up Agent 由 Core 通过 Docker socket 创建和管理，不要用独立的
  `deploy/steam-lobby-agent/docker-compose.yml` 替代它们，也不要手工 `docker rm` 正式 Agent。
- 更新已有 Agent 必须逐个调用 `POST /v1/agents/{agentId}/recreate`。该接口会排空该 Agent 的
  暖服任务，但保留 Steam 登录、Steam Guard 和账号配置卷。
- 共享 Steam 游戏库是所有 Agent 共用的资源。安装、校验、更新或卸载 AppID 550 和 Steam
  runtime 时，只允许一个维护操作同时进行，并先暂停暖服调度。
- Token、PostgreSQL 密码、RCON 加密密钥、Steam Web API Key、Steam 登录信息和私钥只能留在
  受限的远端文件或交互式输入中。不要把它们写进仓库、发布包、命令输出或本指示文档。
- Core API、Agent HTTP 和 noVNC 端口只应绑定回环地址；公网只暴露前端的 `8084`。
- 代理只按单次命令临时使用，不要写入系统默认配置或持久环境变量。通过 tailnet 访问时，若确实
  需要 HTTP 代理，使用 `http://100.106.239.85:7890`；本机直连时使用
  `http://127.0.0.1:7890`。

## 3. 本地发布前检查

在仓库根目录 `J:\GithubRep\l4d2_matchmaking_manager` 执行。发布内容来自当前 Git 提交，
因此先确认工作树干净，并确认当前提交就是要部署的提交。

```powershell
$ErrorActionPreference = 'Stop'

git status --short --branch
git log -1 --oneline

dotnet test L4d2MatchmakingManager.sln --no-build --no-restore --disable-build-servers --logger "console;verbosity=minimal"

Push-Location frontend
npm.cmd run test -- --run
npm.cmd run build
Pop-Location

pwsh -NoProfile -ExecutionPolicy Bypass -File .\deploy\matchmaking-core\Test-ComposeContract.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\deploy\steam-lobby-agent\Test-ComposeContract.ps1

git diff --check
```

若 Core 测试因为本地没有已构建产物而不能使用 `--no-build`，先执行一次 solution build，
再重新运行上面的测试命令。不要用未提交的文件作为发布内容；先提交，或明确采用其他受控的
打包方式并记录提交号。

## 4. 例行更新流程

例行更新分为“本地打包”“远端备份/停调度”“更新镜像”“串行重建 Agent”“前端切换”和
“验收”六个阶段。Core 和 Agent 代码更新时执行全部阶段；仅前端改动时可跳过 Core 镜像和
Agent 重建，但仍要执行前端 release 切换及同源验证。

### 4.1 本地生成发布包

在本地完成第 3 节验证后执行。此命令只归档已提交文件；`.env`、`secrets`、`bin`、`obj`、
`node_modules` 和本地构建缓存不会进入发布包。

```powershell
$ErrorActionPreference = 'Stop'

if (git status --porcelain) {
    throw '工作树不干净。请先提交或清理变更，再生成发布包。'
}

$Revision = (git rev-parse --short=7 HEAD).Trim()
$ReleaseId = "$(Get-Date -Format 'yyyyMMdd-HHmmss')-$Revision"
$CoreImage = "l4d2-matchmaking-core:codex-$Revision"
$AgentImage = "l4d2-steam-lobby-agent:codex-$Revision"
$FrontendRelease = "release-$ReleaseId"
$PackageRoot = Join-Path ([System.IO.Path]::GetTempPath()) "l4d2-matchmaking-$ReleaseId"

New-Item -ItemType Directory -Force -Path $PackageRoot | Out-Null
if (-not (Test-Path -LiteralPath 'frontend\dist\index.html')) {
    throw '缺少 frontend\dist\index.html。请先在 frontend 目录运行 npm.cmd run build。'
}

$SourceArchive = Join-Path $PackageRoot "l4d2-$ReleaseId-source.tar"
$FrontendArchive = Join-Path $PackageRoot "l4d2-$ReleaseId-frontend-dist.tar"

git archive --format=tar --output="$SourceArchive" HEAD
tar.exe -cf $FrontendArchive -C frontend dist

scp $SourceArchive $FrontendArchive "sirphomesv:/tmp/"

Write-Output "ReleaseId=$ReleaseId"
Write-Output "CoreImage=$CoreImage"
Write-Output "AgentImage=$AgentImage"
Write-Output "FrontendRelease=$FrontendRelease"
```

记下命令最后输出的 `ReleaseId`、`CoreImage`、`AgentImage` 和 `FrontendRelease`。后续远端
Shell 代码中的同名变量必须替换为这些值；不要复用旧 release 名称。

### 4.2 远端预检查、暂停调度并备份

先连接远端：

```powershell
ssh sirphomesv
```

以下代码在远端 Ubuntu Shell 中执行。将 `RELEASE_ID` 替换为本次发布的值；代码不会打印
Token 或其他秘密内容。

```sh
set -eu

RELEASE_ID='20260823-153000-0d8cad8'
CORE='/mnt/storage/l4d2-matchmaking-core'
FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
CORE_COMPOSE="$CORE/deploy/matchmaking-core/docker-compose.yml"
FRONTEND_COMPOSE="$FRONTEND/deploy/docker-compose.yml"
BACKUP="/mnt/storage/.l4d2-matchmaking-core-backup-$RELEASE_ID"

test "$CORE" = '/mnt/storage/l4d2-matchmaking-core'
test "$FRONTEND" = '/home/sirp/l4d2-matchmaking-frontend'
test -f "$CORE/.env"
test -f "$CORE/secrets/core-api-token"
test -f "$CORE/secrets/core-rcon-encryption-key"
test -d /mnt/storage/l4d2-steam-library
test -f "$CORE_COMPOSE"
test -f "$FRONTEND/deploy/.env"
test -f "$FRONTEND_COMPOSE"

docker network inspect l4d2-matchmaking >/dev/null
docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE_COMPOSE" config -q
docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND_COMPOSE" config -q

BASE='http://127.0.0.1:18080'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH_HEADER="Authorization: Bearer $TOKEN"

curl -fsS "$BASE/healthz" | jq -e '.status == "alive"' >/dev/null
SCHEDULING_BEFORE="$(curl -fsS -H "$AUTH_HEADER" \
  "$BASE/v1/settings/warmup-scheduling")"
printf '%s\n' "$SCHEDULING_BEFORE" > "$BACKUP/scheduling.before.json"
mkdir -m 700 "$BACKUP"

if [ "$(printf '%s\n' "$SCHEDULING_BEFORE" | jq -r '.enabled')" = 'true' ]; then
  curl -fsS -X PUT -H "$AUTH_HEADER" -H 'Content-Type: application/json' \
    --data '{"enabled":false}' "$BASE/v1/settings/warmup-scheduling" | jq .
fi

curl -fsS -H "$AUTH_HEADER" "$BASE/v1/warmups" | \
  jq -e 'type == "array" and length == 0' >/dev/null

mkdir -p "$BACKUP/source" "$BACKUP/secrets"
cp -a "$CORE/.env" "$BACKUP/.env"
cp -a "$CORE/secrets/." "$BACKUP/secrets/"
cp -a "$FRONTEND/site/current" "$BACKUP/frontend-current.before" 2>/dev/null || true
chmod -R go-rwx "$BACKUP"

CORE_COMPOSE_ARGS="--env-file $CORE/.env --project-name matchmaking-core -f $CORE_COMPOSE"
docker compose $CORE_COMPOSE_ARGS exec -T postgres \
  pg_dump -U matchmaking -d matchmaking -Fc > "$BACKUP/postgres-predeploy.dump"

docker compose $CORE_COMPOSE_ARGS ps > "$BACKUP/core-compose-ps.txt"
docker ps --filter label=com.l4d2.matchmaking.managed=true \
  --format '{{.Names}}\t{{.Image}}' > "$BACKUP/managed-agents.txt"

CORE_IMAGE_BEFORE="$(sed -n 's/^CORE_IMAGE=//p' "$CORE/.env")"
AGENT_IMAGE_BEFORE="$(sed -n 's/^CORE_AGENT_IMAGE=//p' "$CORE/.env")"
docker image inspect "$CORE_IMAGE_BEFORE" > "$BACKUP/core-image-inspect.json"
docker image inspect "$AGENT_IMAGE_BEFORE" > "$BACKUP/agent-image-inspect.json"

tar --exclude='.git' --exclude='.env' --exclude='secrets' \
  --exclude='bin' --exclude='obj' --exclude='node_modules' --exclude='dist' \
  -czf "$BACKUP/source.tar.gz" -C "$CORE" .

printf '备份已创建：%s\n' "$BACKUP"
```

上面命令中 `mkdir "$BACKUP"` 必须早于写入 `scheduling.before.json`。如果按代码执行，
请将这两行顺序调整为先创建备份目录，再写入该文件：

```sh
mkdir -m 700 "$BACKUP"
SCHEDULING_BEFORE="$(curl -fsS -H "$AUTH_HEADER" "$BASE/v1/settings/warmup-scheduling")"
printf '%s\n' "$SCHEDULING_BEFORE" > "$BACKUP/scheduling.before.json"
```

这是故意单独列出的修正：备份目录不存在时，原示例的第一段会在写文件处停止。以后直接
采用修正后的顺序。若暂停调度返回 `409`，不要继续更新；先查看 `/v1/warmups` 和 Core 日志，
确认所有任务已排空后再重试。

### 4.3 同步源码和前端静态文件

仍在远端 Shell 中执行。将归档文件名替换为第 4.1 节实际输出的文件名。

```sh
set -eu

RELEASE_ID='20260823-153000-0d8cad8'
SOURCE_ARCHIVE="l4d2-$RELEASE_ID-source.tar"
FRONTEND_ARCHIVE="l4d2-$RELEASE_ID-frontend-dist.tar"
CORE='/mnt/storage/l4d2-matchmaking-core'
FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
STAGE="/mnt/storage/.l4d2-matchmaking-stage-$RELEASE_ID"
FRONTEND_RELEASE="release-$RELEASE_ID"

mkdir -p "$STAGE/core" "$STAGE/frontend" \
  "$FRONTEND/site/releases/$FRONTEND_RELEASE"
tar -xf "/tmp/$SOURCE_ARCHIVE" -C "$STAGE/core"
tar -xf "/tmp/$FRONTEND_ARCHIVE" -C "$STAGE/frontend" --strip-components=1

test -f "$STAGE/core/deploy/matchmaking-core/docker-compose.yml"
test -f "$STAGE/frontend/index.html"

# 只同步源码；远端 .env 和 secrets 由排除规则保护。
rsync -a --delete \
  --exclude='.env' \
  --exclude='secrets/' \
  "$STAGE/core/" "$CORE/"

rsync -a --delete "$STAGE/frontend/" \
  "$FRONTEND/site/releases/$FRONTEND_RELEASE/"
ln -sfn "releases/$FRONTEND_RELEASE" "$FRONTEND/site/current"

test "$(readlink "$FRONTEND/site/current")" = "releases/$FRONTEND_RELEASE"
rm -f "/tmp/$SOURCE_ARCHIVE" "/tmp/$FRONTEND_ARCHIVE"
rm -rf "$STAGE"
```

`rsync --delete` 只允许对上面两个已经检查过的精确目录使用。它会删除远端源码目录中不在
当前 Git 归档里的文件，所以必须先完成备份；如果远端源码有未纳入 Git 的人工补丁，应先
停止并人工合并，不要直接执行同步。

### 4.4 构建并重启 Core

在远端执行。`CORE_IMAGE` 和 `AGENT_IMAGE` 必须使用第 4.1 节生成的 tag。

```sh
set -eu

RELEASE_ID='20260823-153000-0d8cad8'
CORE_IMAGE="l4d2-matchmaking-core:codex-${RELEASE_ID##*-}"
AGENT_IMAGE="l4d2-steam-lobby-agent:codex-${RELEASE_ID##*-}"
CORE='/mnt/storage/l4d2-matchmaking-core'
CORE_COMPOSE="$CORE/deploy/matchmaking-core/docker-compose.yml"

update_env_key() {
  key="$1"
  value="$2"
  if grep -q "^${key}=" "$CORE/.env"; then
    sed -i "s#^${key}=.*#${key}=${value}#" "$CORE/.env"
  else
    printf '%s=%s\n' "$key" "$value" >> "$CORE/.env"
  fi
}

update_env_key CORE_IMAGE "$CORE_IMAGE"
update_env_key CORE_AGENT_IMAGE "$AGENT_IMAGE"

cd "$CORE"
docker build -f deploy/steam-lobby-agent/Dockerfile -t "$AGENT_IMAGE" .
docker build -f deploy/matchmaking-core/Dockerfile -t "$CORE_IMAGE" .

docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE_COMPOSE" config -q
docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE_COMPOSE" up -d --no-build postgres core

curl -fsS http://127.0.0.1:18080/healthz | jq -e '.status == "alive"' >/dev/null
```

Core 启动时会自动应用数据库迁移。不要手工执行迁移 SQL，也不要在迁移失败时删除数据库
卷。Core 健康检查通过前不要重建 Agent。

### 4.5 串行重建已有 Warm-up Agent

Core 新容器只会使用新镜像创建后续 Agent；已经存在的 Agent 仍然使用旧容器镜像。完成
Core 健康检查后，在远端执行以下脚本。脚本按 API 返回顺序逐个重建，任一失败都会停止，
不会跳过失败 Agent。

```sh
set -eu

CORE='/mnt/storage/l4d2-matchmaking-core'
BASE='http://127.0.0.1:18080'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH_HEADER="Authorization: Bearer $TOKEN"
AGENTS_JSON="/tmp/l4d2-agents-before-recreate.json"

curl -fsS -H "$AUTH_HEADER" "$BASE/v1/agents" > "$AGENTS_JSON"
jq -e 'type == "array"' "$AGENTS_JSON" >/dev/null

jq -r '.[].id' "$AGENTS_JSON" | while IFS= read -r AGENT_ID; do
  [ -n "$AGENT_ID" ] || continue
  printf 'recreate %s\n' "$AGENT_ID"
  curl -fsS -X POST -H "$AUTH_HEADER" \
    "$BASE/v1/agents/$AGENT_ID/recreate" | jq '{id, status, ready}'
done

curl -fsS -H "$AUTH_HEADER" "$BASE/v1/agents" | \
  jq -e 'type == "array" and length > 0 and all(.[]; .status == "running" and .ready == true)' \
  >/dev/null

rm -f "$AGENTS_JSON"
```

如果生产环境暂时没有 Agent，最后的 `length > 0` 检查应改为只检查
`type == "array" and all(.[]; .status == "running" and .ready == true)`；首次创建并完成
Steam 登录后，必须使用默认检查确认至少一个 Agent 已就绪。

不要并行调用多个 `recreate`，不要用 `docker compose` 操作 Agent。新版本加入玩家进入统计
上报后，旧 Agent 也必须至少重建一次，才能取得新的独立 reporting token。

### 4.6 启动前端并恢复调度

前端使用版本化目录和 `current` 符号链接，切换 release 不覆盖旧文件。执行：

```sh
set -eu

FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
FRONTEND_COMPOSE="$FRONTEND/deploy/docker-compose.yml"

docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND_COMPOSE" up -d --no-build frontend
docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND_COMPOSE" exec -T frontend nginx -t
```

如果第 4.2 节记录的 `scheduling.before.json` 中 `enabled` 原本是 `true`，在 Core、Agent
和前端验证全部通过后再恢复：

```sh
CORE='/mnt/storage/l4d2-matchmaking-core'
BASE='http://127.0.0.1:18080'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH_HEADER="Authorization: Bearer $TOKEN"

curl -fsS -X PUT -H "$AUTH_HEADER" -H 'Content-Type: application/json' \
  --data '{"enabled":true}' "$BASE/v1/settings/warmup-scheduling" | jq .
```

恢复失败时必须保持调度关闭，记录错误并处理，不要用 Docker 重启循环掩盖问题。

## 5. 更新验收

以下命令在远端执行。它们验证 HTTP 存活、管理鉴权、Agent readiness、数据库迁移和索引、
玩家进入统计接口、前端同源代理以及镜像/release 标签。

### 5.1 Core、Agent 和数据库

```sh
set -eu

CORE='/mnt/storage/l4d2-matchmaking-core'
CORE_COMPOSE="$CORE/deploy/matchmaking-core/docker-compose.yml"
BASE='http://127.0.0.1:18080'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH_HEADER="Authorization: Bearer $TOKEN"

curl -fsS "$BASE/healthz" | jq -e '.status == "alive"' >/dev/null

AGENTS="$(curl -fsS -H "$AUTH_HEADER" "$BASE/v1/agents")"
printf '%s\n' "$AGENTS" | jq -e \
  'type == "array" and length > 0 and all(.[]; .status == "running" and .ready == true)' \
  >/dev/null

WARMUPS="$(curl -fsS -H "$AUTH_HEADER" "$BASE/v1/warmups")"
printf '%s\n' "$WARMUPS" | jq -e 'type == "array"' >/dev/null

COMPOSE_ARGS="--env-file $CORE/.env --project-name matchmaking-core -f $CORE_COMPOSE"
MIGRATIONS="$(docker compose $COMPOSE_ARGS exec -T postgres \
  psql -U matchmaking -d matchmaking -Atc \
  'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";')"
printf '%s\n' "$MIGRATIONS" | grep -Fxq '202608230001_GlobalWarmupPauseWindows'
printf '%s\n' "$MIGRATIONS" | grep -Fxq '202608230001_PlayerEntryEvents'

INDEXES="$(docker compose $COMPOSE_ARGS exec -T postgres \
  psql -U matchmaking -d matchmaking -Atc \
  "select indexname from pg_indexes where schemaname = 'public' and tablename = 'PlayerEntryEvents' and indexname like 'IX_PlayerEntryEvents_%' order by indexname;")"
for INDEX_NAME in \
  IX_PlayerEntryEvents_OccurredAtUtc \
  IX_PlayerEntryEvents_AgentId_OccurredAtUtc \
  IX_PlayerEntryEvents_TargetServerId_OccurredAtUtc \
  IX_PlayerEntryEvents_DownloadRegionSnapshot_OccurredAtUtc \
  IX_PlayerEntryEvents_TargetModeSnapshot_OccurredAtUtc \
  IX_PlayerEntryEvents_LobbyType_OccurredAtUtc; do
  printf '%s\n' "$INDEXES" | grep -Fxq "$INDEX_NAME"
done

STATS="$(curl -fsS -H "$AUTH_HEADER" "$BASE/v1/statistics/player-entries")"
printf '%s\n' "$STATS" | jq -e \
  'type == "object" and (.totalEntries | type == "number") and (.trend | type == "array")' \
  >/dev/null

docker compose $COMPOSE_ARGS ps
printf 'Core image: '
docker inspect matchmaking-core-core-1 --format '{{.Config.Image}}'
printf 'Managed Agent images:\n'
docker ps --filter label=com.l4d2.matchmaking.managed=true --format '{{.Image}}' | sort -u
```

玩家进入统计查询路径是复数的 `/v1/statistics/player-entries`，不是
`/v1/statistics/player-entry`。迁移会创建 `PlayerEntryEvents` 表、主键索引和上面列出的六个
查询索引；新迁移不回填旧事件。

### 5.2 前端同源代理和 release

```sh
set -eu

FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
BASE='http://100.72.137.92:8084'
CORE='/mnt/storage/l4d2-matchmaking-core'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH_HEADER="Authorization: Bearer $TOKEN"

curl -fsS "$BASE/healthz" | jq -e '.status == "alive"' >/dev/null
curl -fsS -H "$AUTH_HEADER" "$BASE/v1/agents" | \
  jq -e 'type == "array"' >/dev/null
test -f "$FRONTEND/site/current/index.html"
test "$(readlink "$FRONTEND/site/current")" = 'releases/release-20260823-153000-0d8cad8'

docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND/deploy/docker-compose.yml" ps
```

把最后一个 `test` 中的 release 名称替换为本次实际的 `FrontendRelease`。浏览器访问
`http://100.72.137.92:8084`，输入 Core Bearer token 后，确认首页能加载 Target Server、
Warm-up Agent、暖服状态和玩家进入统计；浏览器不应直接调用 Agent 内部 API。

## 6. 回滚流程

满足以下任一条件时停止继续发布并考虑回滚：Core `/healthz` 不存活、迁移失败、Agent 无法
全部恢复 `running/ready`、同源 `/v1` 返回 502/5xx、前端静态资源加载失败，或暖服行为出现
无法确认的状态。

### 6.1 先恢复运行版本

在远端执行。`BACKUP` 必须指向本次更新创建的、权限为 `0700` 的备份目录。

```sh
set -eu

RELEASE_ID='20260823-153000-0d8cad8'
BACKUP="/mnt/storage/.l4d2-matchmaking-core-backup-$RELEASE_ID"
CORE='/mnt/storage/l4d2-matchmaking-core'
FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
CORE_COMPOSE="$CORE/deploy/matchmaking-core/docker-compose.yml"
test -d "$BACKUP"
test -f "$BACKUP/.env"
test -f "$BACKUP/postgres-predeploy.dump"

# 恢复部署配置和秘密文件；不要把它们打印到终端。
cp -a "$BACKUP/.env" "$CORE/.env"
install -d -m 700 "$CORE/secrets"
cp -a "$BACKUP/secrets/." "$CORE/secrets/"
chmod -R go-rwx "$CORE/secrets"

docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE_COMPOSE" up -d --no-build postgres core
curl -fsS http://127.0.0.1:18080/healthz | jq -e '.status == "alive"' >/dev/null
```

此时 Core 使用备份 `.env` 中记录的旧 Core/Agent 镜像 tag。只回退镜像时，先按第 4.5 节
逐个调用 `recreate`，确认旧 Agent 镜像可以 `running/ready` 后，再恢复第 4.2 节记录的调度
开关。不要直接删除现有 Agent 容器或卷。

### 6.2 仅在需要时恢复数据库

如果旧镜像能够在当前数据库 schema 上正常启动且验收通过，优先不恢复数据库，以免丢失回滚
前产生的统计事件。若旧代码无法处理本次迁移后的数据库，才使用预部署 dump。该操作会丢弃
dump 之后写入的业务数据，执行前必须确认目标是本项目 PostgreSQL 数据库。

```sh
set -eu

CORE='/mnt/storage/l4d2-matchmaking-core'
BACKUP='/mnt/storage/.l4d2-matchmaking-core-backup-20260823-153000-0d8cad8'
CORE_COMPOSE="$CORE/deploy/matchmaking-core/docker-compose.yml"
COMPOSE_ARGS="--env-file $CORE/.env --project-name matchmaking-core -f $CORE_COMPOSE"

docker compose $COMPOSE_ARGS stop core
docker compose $COMPOSE_ARGS exec -T postgres \
  psql -U matchmaking -d matchmaking -v ON_ERROR_STOP=1 \
  -c 'drop schema public cascade; create schema public;'
docker compose $COMPOSE_ARGS exec -T postgres \
  pg_restore --exit-on-error -U matchmaking -d matchmaking \
  --no-owner --no-privileges < "$BACKUP/postgres-predeploy.dump"
docker compose $COMPOSE_ARGS up -d --no-build core
```

数据库恢复后重新执行第 5.1 节的迁移、表和索引检查；旧版本不应被要求存在本次更新新增的
迁移。恢复过程失败时保持 Core 停止，保留备份和日志，先处理数据库状态再启动调度。

### 6.3 回退前端 release

备份目录中的 `frontend-current.before` 保存了切换前的相对链接。确认内容只指向
`releases/` 下的既有目录后执行：

```sh
set -eu

BACKUP='/mnt/storage/.l4d2-matchmaking-core-backup-20260823-153000-0d8cad8'
FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
OLD_RELEASE="$(readlink "$BACKUP/frontend-current.before")"
case "$OLD_RELEASE" in
  releases/*) ;;
  *) echo '备份中的前端链接不是受支持的 releases 路径' >&2; exit 1 ;;
esac
test -f "$FRONTEND/site/$OLD_RELEASE/index.html"
ln -sfn "$OLD_RELEASE" "$FRONTEND/site/current"
docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND/deploy/docker-compose.yml" up -d --no-build frontend
```

回滚完成后重新检查 Core health、全部 Agent readiness、前端 `/healthz` 和同源 `/v1`，确认
无误后才恢复原本启用的调度。至少保留当前版本和上一个可用版本的镜像、前端 release 与
数据库备份，不要在验收前清理。

## 7. 常见故障处理

### Core 启动失败或迁移失败

```sh
CORE='/mnt/storage/l4d2-matchmaking-core'
docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE/deploy/matchmaking-core/docker-compose.yml" ps
docker compose --env-file "$CORE/.env" --project-name matchmaking-core \
  -f "$CORE/deploy/matchmaking-core/docker-compose.yml" logs --tail=200 core
```

先确认 `.env` 路径、`POSTGRES_PASSWORD`、两个 secret-file 路径和
`CORE_AGENT_STEAM_API_LIBRARY_PATH` 非空且可访问。不要把日志中的连接字符串、Token 或 Steam
登录输出复制到工单或聊天中。迁移错误未定位前不要删卷、重建 PostgreSQL 或手工改表。

### Agent 为 `running` 但 `ready=false`

`running` 只表示容器进程状态，调度要求 `/v1/agents` 同时报告 `ready=true`。检查：

```sh
CORE='/mnt/storage/l4d2-matchmaking-core'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
curl -fsS -H "Authorization: Bearer $TOKEN" \
  http://127.0.0.1:18080/v1/agents | jq '.[] | {id, name, status, ready}'
find /mnt/storage/l4d2-steam-library -name libsteam_api.so -type f -print
docker ps --filter label=com.l4d2.matchmaking.managed=true \
  --format '{{.Names}}\t{{.Image}}\t{{.Status}}'
```

如果是 `steam_api_load_failed` 或路径缺失，确认宿主机实际文件与容器路径的对应关系；当前
实例使用 `/mnt/steam-library/agent-runtime/steamrt64/libsteam_api.so`。修改路径后必须先重启
Core，再逐个 `recreate` Agent。

### `recreate` 返回冲突或失败

保持全局暖服调度关闭，查看：

```sh
CORE='/mnt/storage/l4d2-matchmaking-core'
TOKEN="$(cat "$CORE/secrets/core-api-token")"
AUTH="Authorization: Bearer $TOKEN"
curl -fsS -H "$AUTH" http://127.0.0.1:18080/v1/warmups | jq .
curl -fsS -H "$AUTH" http://127.0.0.1:18080/v1/agents | jq .
```

先处理仍在 `active`、`uncertain` 或 `restart_pending` 的暖服任务，再重试目标 Agent。不能
通过删除容器或账号卷绕过 drain；无法确认停止时保留 Agent 隔离状态并升级处理。

### 前端 502、空白页或 401

- 502：确认 Core 和前端都连接到外部 Docker 网络 `l4d2-matchmaking`，并检查前端 Compose
  项目名仍为 `deploy`。
- 空白页或资源 404：确认 `site/current/index.html` 存在，链接指向完整的
  `site/releases/release-*` 目录，且 Nginx 配置测试通过。
- 401：在页面中输入 Core Bearer token；Token 不应写入前端 `.env`、Nginx 配置或静态文件。
- 前端 `/healthz` 正常但 `/v1` 失败：先验证 Core 的回环 health 和鉴权 API，再检查
  `docker network inspect l4d2-matchmaking`，不要把 Core API 改为公网监听。

### 共享 Steam 游戏库异常

暂停暖服调度后，只保留一个维护 Agent 进行 Steam 安装或校验。完成后确认
`appmanifest_550.acf` 和 `libsteam_api.so` 存在，再按 Agent readiness 检查并逐个重建受影响
Agent。共享库只放游戏内容、Steam runtime 和 Steam API 依赖；账号登录、Steam Guard 和
`userdata` 必须留在独立 `steam-data-*` 卷中。

## 8. 首次部署

首次部署与例行更新的区别是需要准备远端目录、secret 文件、PostgreSQL 配置和首次 Steam
登录。先完成第 3、4.1 节的本地验证和发布包，再连接远端。

### 8.1 准备远端目录和 secret 文件

```sh
set -eu

CORE='/mnt/storage/l4d2-matchmaking-core'
mkdir -p "$CORE/secrets" /mnt/storage/l4d2-steam-library
chmod 700 "$CORE/secrets"
chmod 750 /mnt/storage/l4d2-steam-library

test -f "$CORE/secrets/core-api-token" || \
  openssl rand -hex 32 > "$CORE/secrets/core-api-token"
test -f "$CORE/secrets/core-rcon-encryption-key" || \
  openssl rand -base64 32 > "$CORE/secrets/core-rcon-encryption-key"
chmod 600 "$CORE/secrets/core-api-token" "$CORE/secrets/core-rcon-encryption-key"

cp "$CORE/deploy/matchmaking-core/.env.example" "$CORE/.env"
chmod 600 "$CORE/.env"
```

编辑 `$CORE/.env`，至少设置以下键；`POSTGRES_PASSWORD` 使用新的随机密码，不能复用其他
服务的密码。不要在文档、终端回显或 Git 中保存实际值。

```dotenv
CORE_API_TOKEN_FILE_HOST=/mnt/storage/l4d2-matchmaking-core/secrets/core-api-token
CORE_RCON_ENCRYPTION_KEY_FILE_HOST=/mnt/storage/l4d2-matchmaking-core/secrets/core-rcon-encryption-key
POSTGRES_PASSWORD=<unique-random-password>
CORE_SHARED_LIBRARY_HOST_PATH=/mnt/storage/l4d2-steam-library
CORE_AGENT_STEAM_API_LIBRARY_PATH=/mnt/steam-library/agent-runtime/steamrt64/libsteam_api.so
CORE_AGENT_STEAM_LOGIN_UI_MODE=auto
CORE_SCHEDULER_MAX_STARTS_PER_TICK=16
CORE_IMAGE=l4d2-matchmaking-core:codex-<revision>
CORE_AGENT_IMAGE=l4d2-steam-lobby-agent:codex-<revision>
CORE_AGENT_NETWORK=l4d2-matchmaking
CORE_AGENT_REPORTING_ORIGIN=http://core:8080
CORE_HTTP_PORT=18080
CORE_NOVNC_PORT_START=18083
CORE_NOVNC_PORT_END=18183
```

`CORE_AGENT_STEAM_API_LIBRARY_PATH` 是 Agent 容器内的路径，不是宿主机路径。若首次安装
Steam 后实际路径不同，在宿主机执行：

```sh
find /mnt/storage/l4d2-steam-library -name libsteam_api.so -type f -print
```

将宿主机路径相对于 `/mnt/storage/l4d2-steam-library` 的部分映射到
`/mnt/steam-library/...`，更新 `.env`，重启 Core，并在创建 Agent 后调用 `recreate` 使新路径
生效。

### 8.2 启动 Core、创建共享库和首次 Agent

先按第 4.3、4.4 节同步源码、构建两个镜像并启动 Core/PostgreSQL：

```sh
cd /mnt/storage/l4d2-matchmaking-core
docker build -f deploy/steam-lobby-agent/Dockerfile \
  -t "$(sed -n 's/^CORE_AGENT_IMAGE=//p' .env)" .
docker build -f deploy/matchmaking-core/Dockerfile \
  -t "$(sed -n 's/^CORE_IMAGE=//p' .env)" .
docker compose --env-file /mnt/storage/l4d2-matchmaking-core/.env \
  --project-name matchmaking-core \
  -f deploy/matchmaking-core/docker-compose.yml up -d --no-build postgres core
```

创建 Agent 后，通过 SSH 回环转发访问 noVNC 完成 Steam 登录和 AppID 550 安装；示例中
`18083` 仅代表 API 返回的实际 `noVncPort`：

```powershell
ssh -N -L 18083:127.0.0.1:18083 sirphomesv
```

浏览器访问 `http://127.0.0.1:18083/`。完成登录、Steam Guard、AppID 550 和共享 runtime
安装后，关闭 noVNC 转发，确认共享库中的 `libsteam_api.so`，更新 Core `.env` 并重建该
Agent。首次 Steam 账号配置和共享库边界详见
[Steam Lobby Agent README](../deploy/steam-lobby-agent/README.md)。

### 8.3 部署前端

在本地完成 `frontend/dist` 构建并上传后，在远端执行：

```sh
FRONTEND='/home/sirp/l4d2-matchmaking-frontend'
FRONTEND_RELEASE='release-20260823-153000-0d8cad8'
mkdir -p "$FRONTEND/site/releases/$FRONTEND_RELEASE"
tar -xf "/tmp/l4d2-20260823-153000-0d8cad8-frontend-dist.tar" \
  -C "$FRONTEND/site/releases/$FRONTEND_RELEASE" --strip-components=1
ln -sfn "releases/$FRONTEND_RELEASE" "$FRONTEND/site/current"
docker compose --env-file "$FRONTEND/deploy/.env" --project-name deploy \
  -f "$FRONTEND/deploy/docker-compose.yml" up -d --no-build frontend
```

首次部署完成后执行第 5 节全部验收；首次登录的 Agent 未达到 `ready=true` 前，不要打开
正式暖服调度。

## 9. 相关文档

- [Core 运维与 API](matchmaking-core-api.md)
- [Core 前端 API 契约](matchmaking-core-frontend-api.md)
- [Agent 内部 API](steam-lobby-agent-api.md)
- [Core Compose README](../deploy/matchmaking-core/README.md)
- [Frontend Compose README](../deploy/matchmaking-frontend/README.md)
- [Steam Lobby Agent README](../deploy/steam-lobby-agent/README.md)

