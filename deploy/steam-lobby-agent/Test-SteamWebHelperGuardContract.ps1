param()

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$initializerPath = Join-Path $root '91-enable-steam-supervisor.sh'
$guardPath = Join-Path $root 'steamwebhelper-guard.sh'
$shimPath = Join-Path $root 'steamwebhelper-guard-shim.sh'
$supervisorPath = Join-Path $root 'steamwebhelper-guard.ini'
$dockerfilePath = Join-Path $root 'Dockerfile'

foreach ($path in @($initializerPath, $guardPath, $shimPath, $supervisorPath, $dockerfilePath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing Steam webhelper guard deployment file: $path"
    }
}

$initializer = Get-Content -Raw -LiteralPath $initializerPath
$guard = Get-Content -Raw -LiteralPath $guardPath
$shim = Get-Content -Raw -LiteralPath $shimPath
$supervisor = Get-Content -Raw -LiteralPath $supervisorPath
$dockerfile = Get-Content -Raw -LiteralPath $dockerfilePath

if ($initializer -notmatch 'set_steam_webhelper_guard_autostart' -or
    $initializer -notmatch '/etc/supervisor\.d/steamwebhelper-guard\.ini') {
    throw 'The initializer must own the Steam webhelper guard Supervisor autostart state.'
}

if ($initializer -notmatch 'restore_steam_webhelper_wrapper' -or
    $initializer -notmatch 'steamwebhelper_sniper_wrap\.sh\.valve-original') {
    throw 'The initializer must retain the original Valve wrapper for Steam startup.'
}

if ($initializer -notmatch '(?s)restore_steam_webhelper_wrapper\s*\n\s*sed -i "s\|\^command' -or
    $initializer -match 'if \[ "\$\{enable_steam_webhelper_guard\}" = false \]; then\s*restore_steam_webhelper_wrapper') {
    throw 'The initializer must restore the Valve wrapper before every Steam startup, including no-VNC guard mode.'
}

$alwaysBlock = [regex]::Match($initializer, "(?s)always\)\s*steam_arguments=''.*?;;").Value
$neverBlock = [regex]::Match($initializer, '(?s)never\)\s*steam_arguments="\$\{steam_no_vnc_arguments\}".*?;;').Value
$autoBlock = [regex]::Match($initializer, '(?s)auto\)\s*if \{ \[ -f "\$\{loginusers_file\}" \].*?;;').Value
if ($alwaysBlock -match 'enable_steam_webhelper_guard=true' -or
    $neverBlock -notmatch 'enable_steam_webhelper_guard=true' -or
    $autoBlock -notmatch '(?s)MostRecent.*?enable_steam_webhelper_guard=true.*?else.*?enable_steam_webhelper_guard=false') {
    throw 'Only the no-VNC never and logged-in auto paths may enable the Steam webhelper guard.'
}

if ($initializer -notmatch 'set_steam_webhelper_guard_autostart "\$\{enable_steam_webhelper_guard\}"') {
    throw 'The selected Steam UI mode must set the final Steam webhelper guard autostart state.'
}

if ($guard -notmatch 'http://127\.0\.0\.1:8080/v1/probe/status' -or
    $guard -notmatch "jq -e '\.ready == true'" -or
    $guard -notmatch 'while true') {
    throw 'The guard must wait until the local Agent readiness probe reports ready.'
}

if ($guard -notmatch 'STEAM_WEBHELPER_GUARD_GRACE_SECONDS:-60') {
    throw 'The guard must default to a 60-second post-readiness grace period.'
}

if ($guard -notmatch 'STEAM_CEF_GUARD_APPLIED_MARKER' -or
    $guard -notmatch 'date \+%s%3N' -or
    $guard -notmatch 'mark_guard_applied' -or
    $guard -notmatch '(?s)pkill -TERM -x steamwebhelper.*mark_guard_applied') {
    throw 'The guard must persist the timestamp after the Chromium shim becomes active.'
}

if ($guard -notmatch '(?s)if is_guard_shim; then.*?if \[ ! -f "\$\{applied_marker\}" \].*?guard_replaced=true') {
    throw 'An already-installed Chromium shim without a marker must start a fresh guard timestamp.'
}

if ($initializer -notmatch 'l4d2-cef-guard-applied' -or
    $initializer -notmatch 'clear_steam_cef_guard_marker') {
    throw 'The initializer must clear stale CEF guard state before each Steam startup.'
}

if ($guard -notmatch 'agent_is_ready\(\)' -or
    $guard -notmatch 'wait_for_stable_agent_ready\(\)' -or
    $guard -notmatch 'wait_for_agent_ready' -or
    $guard -notmatch 'sleep "\$\{grace_seconds\}"' -or
    $guard -notmatch 'if agent_is_ready; then' -or
    $guard -notmatch 'wait_for_stable_agent_ready\s*\n\s*while true') {
    throw 'The guard must wait through the grace period and recheck readiness before installing the shim.'
}

if ($guard -notmatch 'steamwebhelper_sniper_wrap\.sh\.valve-original' -or
    $guard -notmatch 'exec \./steamwebhelper') {
    throw 'The guard must retain and recognize the original Valve wrapper before replacing it.'
}

if ($guard -notmatch 'cmp -s' -or
    $guard -notmatch 'steamwebhelper-guard-shim\.sh' -or
    $guard -notmatch 'pkill -TERM -x steamwebhelper') {
    throw 'The guard must install the shim idempotently and terminate only the existing Chromium helper processes.'
}

if ($guard -match 'pkill -KILL' -or $guard -match 'pkill -f steamwebhelper') {
    throw 'The guard must not use broad or non-graceful process matching.'
}

if ($shim -notmatch 'exec /usr/bin/sleep infinity' -or
    $shim -match 'steamwebhelper') {
    throw 'The shim must be a persistent lightweight replacement and must not execute Chromium.'
}

if ($supervisor -notmatch '\[program:steamwebhelper-guard\]' -or
    $supervisor -notmatch 'autostart=false' -or
    $supervisor -notmatch 'autorestart=false' -or
    $supervisor -notmatch 'command=/usr/local/bin/steamwebhelper-guard') {
    throw 'The image must include a disabled-by-default, one-instance Steam webhelper guard Supervisor program.'
}

foreach ($copy in @(
        'COPY deploy/steam-lobby-agent/steamwebhelper-guard\.sh /usr/local/bin/steamwebhelper-guard',
        'COPY deploy/steam-lobby-agent/steamwebhelper-guard-shim\.sh /usr/local/lib/steamwebhelper-guard-shim\.sh',
        'COPY deploy/steam-lobby-agent/steamwebhelper-guard\.ini /etc/supervisor\.d/steamwebhelper-guard\.ini')) {
    if ($dockerfile -notmatch $copy) {
        throw "The Dockerfile must install the Steam webhelper guard asset: $copy"
    }
}

Write-Host 'Steam webhelper guard deployment contract passed.'
