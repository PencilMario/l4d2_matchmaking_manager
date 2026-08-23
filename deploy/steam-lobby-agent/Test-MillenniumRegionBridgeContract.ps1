$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot 'millennium/steam-region-bridge'
$plugin = Join-Path $root 'plugin.json'
$backend = Join-Path $root 'backend/main.lua'
$frontend = Join-Path $root 'frontend/index.tsx'
$bundle = Join-Path $root '.millennium/Dist/index.js'
$initializer = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh')

foreach ($path in @($plugin, $backend, $bundle)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing Millennium Steam Region Bridge artifact: $path"
    }
}

$pluginJson = Get-Content -Raw -LiteralPath $plugin | ConvertFrom-Json
$backendContent = Get-Content -Raw -LiteralPath $backend
$frontendContent = Get-Content -Raw -LiteralPath $frontend
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

if ($backendContent -notmatch 'type\(params\) == "table"' -or
    $backendContent -notmatch 'tonumber\(params\)') {
    throw 'The bridge backend must accept both Millennium callable argument shapes when recording a region.'
}

if ($backendContent -notmatch 'local json = require\("json"\)' -or
    $backendContent -notmatch 'return json\.encode\(\{\s*targetRegionId' -or
    $backendContent -notmatch 'return json\.encode\(\{\s*success') {
    throw 'The bridge backend must JSON-encode object results because Millennium Lua RPC only transports scalar return values.'
}

if ($backendContent -notmatch 'steam-region-bridge-region' -or
    $backendContent -notmatch 'read_file' -or
    $backendContent -notmatch 'configured_value ~= nil' -or
    $backendContent -notmatch 'has_configured_file' -or
    $initializer -notmatch 'millennium_region_target_file' -or
    $initializer -notmatch 'steam-region-bridge-region' -or
    $backendContent -notmatch 'configured_home = "/home/default"' -or
    $backendContent -notmatch 'Steam Region Bridge config target') {
    throw 'The initializer and bridge backend must exchange the target region through an account-local Millennium config file.'
}

if ($bundleContent -notmatch 'SetSetting' -or $bundleContent -notmatch '64072' -or
    $bundleContent -notmatch 'vecValidDownloadRegions' -or
    $bundleContent -notmatch 'get_region_bridge_config' -or
    $bundleContent -notmatch 'record_region_state') {
    throw 'The bridge bundle must use SteamClient.Settings, wait for Steam regions, and read backend configuration.'
}

if ($bundleContent -notmatch 'SetSetting target=' -or
    $bundleContent -notmatch 'SetSetting observed=' -or
    $bundleContent -notmatch 'result=') {
    throw 'The deployed bridge bundle must log the Steam SetSetting result and the post-write observed region.'
}

if ($frontendContent -notmatch 'setResult = await' -or
    $frontendContent -notmatch 'setResult !== true' -or
    $frontendContent -notmatch 'currentRegionId !== targetRegionId') {
    throw 'The bridge must verify both the Steam SetSetting result and the post-write region state.'
}

if ($frontendContent -notmatch 'JSON\.parse' -or
    $frontendContent -notmatch 'targetRegionId') {
    throw 'The bridge must decode JSON-serialized Millennium callable results before reading targetRegionId.'
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

if ($initializer -notmatch 'chmod 0755 "\$\{millennium_install_directory\}"/\*') {
    throw 'The Millennium runtime must be executable by the non-root Steam user when UMASK is restrictive.'
}

if ($initializer -notmatch 'steam_runtime_root="\$\{USER_HOME:-/home/default\}/\.steam"' -or
    $initializer -match '"\$\{steam_root\}/ubuntu12_32/libXtst\.so\.6"' -or
    $initializer -match '"\$\{steam_root\}/ubuntu12_64/libXtst\.so\.6"') {
    throw 'Millennium bootstrap links must target Steam''s actual .steam/ubuntu12 runtime directories.'
}

if ($initializer -notmatch '"\$\{steam_root\}/ubuntu12_32/steam"') {
    throw 'The initializer must provide Millennium''s expected Steam executable path as an alias to the actual runtime binary.'
}
