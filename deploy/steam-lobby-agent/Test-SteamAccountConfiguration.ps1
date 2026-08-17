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

if ($initializer -notmatch 'STEAM_LOGIN_UI_MODE' -or $initializer -notmatch 'loginusers\.vdf') {
    throw 'The initializer must select the Steam UI mode from the persistent account login state.'
}

if ($initializer -notmatch 'auto\|always\|never') {
    throw 'The initializer must retain explicit auto, always and never Steam UI mode choices for recovery.'
}

if ($initializer -notmatch '-vgui -no-browser') {
    throw 'The initializer must retain the small-screen UI for first-time Steam login.'
}

if ($initializer -notmatch '-silent -no-browser') {
    throw 'The initializer must suppress the Steam UI after account login is complete.'
}

$compose = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'docker-compose.yml')
if ($compose -match 'STEAM_ARGS:') {
    throw 'The compose file must not pin Steam to UI mode after login has completed.'
}

if ($compose -notmatch 'STEAM_LOGIN_UI_MODE:\s*\$\{STEAM_LOGIN_UI_MODE:-auto\}') {
    throw 'The compose file must expose auto UI selection with an operator recovery override.'
}
