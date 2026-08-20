$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot 'millennium/steam-region-bridge'
$plugin = Join-Path $root 'plugin.json'
$backend = Join-Path $root 'backend/main.lua'
$bundle = Join-Path $root '.millennium/Dist/index.js'
$initializer = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh')

foreach ($path in @($plugin, $backend, $bundle)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing Millennium Steam Region Bridge artifact: $path"
    }
}

$pluginJson = Get-Content -Raw -LiteralPath $plugin | ConvertFrom-Json
$backendContent = Get-Content -Raw -LiteralPath $backend
$bundleContent = Get-Content -Raw -LiteralPath $bundle
if ($pluginJson.name -ne 'steam-region-bridge' -or $pluginJson.backendType -ne 'lua') {
    throw 'The bridge must be a Lua Millennium plugin named steam-region-bridge.'
}

if ($backendContent -notmatch 'STEAM_DOWNLOAD_REGION_ID' -or
    $backendContent -notmatch 'STEAM_DOWNLOAD_REGION' -or
    $backendContent -notmatch 'STEAM_DOWNLOAD_REGION_STATUS_FILE' -or
    $backendContent -notmatch 'write_file') {
    throw 'The bridge backend must read the target environment and persist the actual region state.'
}

if ($bundleContent -notmatch 'SetSetting' -or $bundleContent -notmatch '64072' -or
    $bundleContent -notmatch 'vecValidDownloadRegions' -or
    $bundleContent -notmatch 'get_region_bridge_config' -or
    $bundleContent -notmatch 'record_region_state') {
    throw 'The bridge bundle must use SteamClient.Settings, wait for Steam regions, and read backend configuration.'
}

if ($initializer -match 'set_download_region' -or $initializer -match '"DownloadRegion"') {
    throw 'The account initializer must not write the legacy DownloadRegion VDF value.'
}

if ($initializer -notmatch 'STEAM_DOWNLOAD_REGION_ID' -or
    $initializer -notmatch 'install_millennium' -or
    $initializer -notmatch 'install_steam_region_bridge' -or
    $initializer -notmatch 'steam-region-bridge' -or
    $initializer -notmatch 'libmillennium_bootstrap_x86\.so' -or
    $initializer -notmatch 'libmillennium_bootstrap_hhx64\.so') {
    throw 'The initializer must install Millennium, install the bridge, and create the official bootstrap links.'
}

if ($initializer -notmatch 'millennium-v3\.4\.1-linux-x86_64\.tar\.gz' -or
    $initializer -notmatch '5f2f6f73915523a7b3f7ecc500dd3e6ed0e5c88a1b1db6584c40f173aa9d13d4') {
    throw 'The initializer must pin and verify the known Millennium v3.4.1 Linux archive.'
}
