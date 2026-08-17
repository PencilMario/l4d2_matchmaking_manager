param(
    [Parameter(Mandatory)]
    [string]$SharedLibraryPath
)

$ErrorActionPreference = 'Stop'

$manifest = Join-Path $SharedLibraryPath 'steamapps/appmanifest_550.acf'
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Missing AppID 550 fixture: $manifest"
}

$fixture = Get-Content -Raw -LiteralPath $manifest
if ($fixture -notmatch '"AutoUpdateBehavior"\s+"0"') {
    throw 'The fixture must model Steam default AppID 550 automatic updates.'
}

$initializer = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh')
if ($initializer -notmatch 'set_l4d2_update_policy' -or
    $initializer -notmatch '"AutoUpdateBehavior" "1"' -or
    $initializer -notmatch 'STEAM_DOWNLOAD_REGION') {
    throw 'The initializer must set AppID 550 to update-on-launch and persist the optional region account-locally.'
}

if ($initializer -notmatch '"DisableShaderCache" "1"') {
    throw 'The initializer must disable shader precaching in the account-local Steam configuration.'
}

$compose = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'docker-compose.yml')
if ($compose -notmatch 'STEAM_ARGS:\s*"-vgui -no-browser"') {
    throw 'The Agent must default Steam to the small-screen UI without the browser process.'
}
