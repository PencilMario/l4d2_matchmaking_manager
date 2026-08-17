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

$steamInitPath = Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh'
if (-not (Test-Path -LiteralPath $steamInitPath)) {
    throw "Missing Steam supervisor initializer: $steamInitPath"
}

$steamInit = Get-Content -Raw -LiteralPath $steamInitPath
if ($steamInit -notmatch 'autostart=true') {
    throw 'The Steam supervisor initializer must enable the Steam service.'
}

if ($steamInit -notmatch 'STEAM_SHARED_LIBRARY_PATH' -or $steamInit -notmatch 'libraryfolders\.vdf') {
    throw 'The Steam supervisor initializer must configure the shared Steam library for each account.'
}

if ($steamInit -notmatch 'DisableShaderCache') {
    throw 'The Steam supervisor initializer must disable shader precaching for each account.'
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

if ($compose -notmatch 'STEAM_ARGS:\s*"-vgui -no-browser"') {
    throw 'Steam Desktop must start in small-screen mode without the browser UI.'
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

if ($compose -notmatch 'STEAM_DOWNLOAD_REGION:\s*\$\{STEAM_DOWNLOAD_REGION:-\}') {
    throw 'Steam Desktop must pass the optional download region to the account initializer.'
}

$agentSupervisor = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'l4d2-lobby-agent.ini')
if ($agentSupervisor -notmatch 'STEAM_DOWNLOAD_REGION') {
    throw 'The Agent supervisor must receive the optional account download region.'
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
