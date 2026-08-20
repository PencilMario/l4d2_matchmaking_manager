$ErrorActionPreference = 'Stop'

$composePath = Join-Path $PSScriptRoot 'docker-compose.yml'
if (-not (Test-Path -LiteralPath $composePath)) {
    throw "Missing compose file: $composePath"
}

$compose = Get-Content -Raw -LiteralPath $composePath
$dockerfilePath = Join-Path $PSScriptRoot 'Dockerfile'
if (-not (Test-Path -LiteralPath $dockerfilePath)) {
    throw "Missing Dockerfile: $dockerfilePath"
}

$dockerfile = Get-Content -Raw -LiteralPath $dockerfilePath
if ($dockerfile -notmatch 'FROM josh5/steam-headless') {
    throw 'The image must extend the maintained Steam Headless desktop image.'
}

if ($dockerfile -notmatch '60-configure_gpu_driver\.sh') {
    throw 'The lobby-only image must remove the upstream GPU driver initializer.'
}

if ($dockerfile -notmatch 'desktop-apps-updated') {
    throw 'The lobby-only image must skip the upstream Firefox and ProtonUp bootstrap.'
}

if ($dockerfile -notmatch '91-enable-steam-supervisor\.sh') {
    throw 'The image must enable Steam through the upstream supervisor after initialization.'
}

if ($dockerfile -notmatch 'millennium/steam-region-bridge') {
    throw 'The Agent image must include the managed Steam Region Bridge plugin source.'
}

if ($dockerfile -notmatch 'libssl3t64:i386') {
    throw 'The Agent image must include the 32-bit OpenSSL runtime required by Millennium.'
}

if ($dockerfile -notmatch 'COPY src/L4d2Matchmaking.Contracts/L4d2Matchmaking.Contracts\.csproj src/L4d2Matchmaking.Contracts/') {
    throw 'The Docker build must copy shared Contracts before restoring the Agent and Probe projects.'
}

if ($dockerfile -notmatch 'COPY src/L4d2Matchmaking.Contracts/ src/L4d2Matchmaking.Contracts/') {
    throw 'The Docker build must copy shared Contracts source before publishing the Agent and Probe projects.'
}

$steamInitPath = Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh'
if (-not (Test-Path -LiteralPath $steamInitPath)) {
    throw "Missing Steam supervisor initializer: $steamInitPath"
}

$steamInit = Get-Content -Raw -LiteralPath $steamInitPath
if ($steamInit -notmatch 'autostart=true') {
    throw 'The Steam supervisor initializer must enable the Steam service.'
}

if ($steamInit -notmatch 'chmod=0660' -or $steamInit -notmatch 'chown=root:sudo') {
    throw 'The Steam supervisor socket must grant the Agent service group-only control access.'
}

if ($steamInit -match 'chmod=0666') {
    throw 'The Steam supervisor socket must not be writable by every container user.'
}

if ($steamInit -notmatch 'STEAM_SHARED_LIBRARY_PATH' -or $steamInit -notmatch 'libraryfolders\.vdf') {
    throw 'The Steam supervisor initializer must configure the shared Steam library for each account.'
}

if ($steamInit -notmatch 'DisableShaderCache') {
    throw 'The Steam supervisor initializer must disable shader precaching for each account.'
}

if ($steamInit -notmatch 'steam_region_status_file' -or $steamInit -notmatch 'STEAM_DOWNLOAD_REGION_STATUS_FILE') {
    throw 'The Steam initializer must define the account-local Millennium region state file.'
}

if ($compose -notmatch 'steam-data:') {
    throw 'Missing persistent Steam volume.'
}

if ($compose -notmatch '127.0.0.1:8083:8083') {
    throw 'Steam Desktop Web UI must bind to loopback.'
}

if ($compose -notmatch '127.0.0.1:8080:8080') {
    throw 'Agent HTTP must bind to loopback.'
}

if ($compose -notmatch 'apparmor=unconfined') {
    throw 'Steam Desktop requires an AppArmor profile that permits user namespaces.'
}

if ($compose -notmatch 'seccomp=unconfined') {
    throw 'Steam Desktop requires a seccomp profile that permits user namespaces.'
}

if ($compose -notmatch 'STEAM_LOGIN_UI_MODE:\s*\$\{STEAM_LOGIN_UI_MODE:-auto\}') {
    throw 'Steam Desktop must default to automatic first-login UI selection.'
}

if ($compose -match 'STEAM_ARGS:') {
    throw 'Steam Desktop must not pin the Steam UI after account login is complete.'
}

if ($steamInit -notmatch 'loginusers\.vdf' -or
    $steamInit -notmatch '-vgui -no-browser' -or
    $steamInit -notmatch '-silent -no-browser') {
    throw 'Steam Desktop must show the small-screen UI only until account login is complete.'
}

if ($compose -notmatch 'NVIDIA_VISIBLE_DEVICES:\s*""') {
    throw 'The non-NVIDIA deployment must disable the upstream Flatpak NVIDIA mount path.'
}

if ($compose -notmatch 'LIBGL_ALWAYS_SOFTWARE:\s*"1"') {
    throw 'The lobby-only desktop must use software rendering.'
}

if ($compose -notmatch 'steam-data:/home/default') {
    throw 'Steam Desktop must persist its supported home directory.'
}

if ($compose -notmatch 'STEAM_SHARED_LIBRARY_HOST_PATH') {
    throw 'Steam app content must use an explicit shared host-library mount.'
}

if ($compose -notmatch 'STEAM_SHARED_LIBRARY_PATH:\s*/mnt/steam-library') {
    throw 'Steam Desktop must receive the shared library path inside the container.'
}

if ($compose -notmatch 'STEAM_DOWNLOAD_REGION_ID:\s*\$\{STEAM_DOWNLOAD_REGION_ID:-\}') {
    throw 'Steam Desktop must pass the numeric download region target to the account initializer.'
}

if ($compose -notmatch 'STEAM_DOWNLOAD_REGION:\s*\$\{STEAM_DOWNLOAD_REGION:-\}') {
    throw 'Steam Desktop must retain the legacy download region environment for compatibility.'
}

$agentSupervisor = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'l4d2-lobby-agent.ini')
if ($agentSupervisor -notmatch 'STEAM_DOWNLOAD_REGION_ID' -or
    $agentSupervisor -notmatch 'STEAM_DOWNLOAD_REGION_STATUS_FILE') {
    throw 'The Agent supervisor must receive the numeric region target and Millennium state path.'
}

if ($compose -notmatch 'target:\s*/mnt/steam-library') {
    throw 'The shared Steam library must mount at its own library-root path.'
}

if ($compose -notmatch 'create_host_path:\s*false') {
    throw 'The shared Steam library host path must exist before deployment.'
}

if ($compose -match 'steam-data:/home/steam') {
    throw 'A fresh Steam volume must only use the maintained image home directory.'
}

if ($compose -match 'left4dead2') {
    throw 'The compose service must not start the L4D2 client.'
}
