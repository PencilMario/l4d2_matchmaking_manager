$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot 'millennium/steam-ui-memory-saver'
$plugin = Join-Path $root 'plugin.json'
$backend = Join-Path $root 'backend/main.lua'
$frontend = Join-Path $root 'frontend/index.tsx'
$bundle = Join-Path $root '.millennium/Dist/index.js'
$initializer = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '91-enable-steam-supervisor.sh')
$dockerfile = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Dockerfile')

foreach ($path in @($plugin, $backend, $frontend, $bundle)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing Steam UI Memory Saver artifact: $path"
    }
}

$pluginJson = Get-Content -Raw -LiteralPath $plugin | ConvertFrom-Json
$backendContent = Get-Content -Raw -LiteralPath $backend
$frontendContent = Get-Content -Raw -LiteralPath $frontend
$bundleContent = Get-Content -Raw -LiteralPath $bundle

if ($pluginJson.name -ne 'steam-ui-memory-saver' -or $pluginJson.backendType -ne 'lua') {
    throw 'The Steam UI Memory Saver must be a Lua Millennium plugin named steam-ui-memory-saver.'
}

if ($backendContent -notmatch 'STEAM_LOGIN_UI_MODE' -or
    $backendContent -notmatch 'l4d2-agent-ready' -or
    $backendContent -notmatch 'json\.encode' -or
    $backendContent -notmatch 'millennium\.ready') {
    throw 'The memory saver backend must gate activation on the persistent no-VNC readiness state and expose a scalar-safe config.'
}

if ($frontendContent -notmatch 'routerHook\.addPatch' -or
    $frontendContent -notmatch "'/library'" -or
    $frontendContent -notmatch "'/library/downloads'" -or
    $frontendContent -notmatch 'EUIMode\.Desktop' -or
    $frontendContent -notmatch 'React\.createElement') {
    throw 'The memory saver frontend must replace the desktop Library route contents with a lightweight React component.'
}

if ($bundleContent -notmatch 'routerHook' -or
    $bundleContent -notmatch 'library/downloads' -or
    $bundleContent -notmatch 'get_steam_ui_memory_saver_config') {
    throw 'The deployed memory saver bundle must contain the route patches and backend readiness check.'
}

if ($initializer -notmatch 'STEAM_UI_MEMORY_SAVER_SOURCE' -or
    $initializer -notmatch 'steam-ui-memory-saver' -or
    $initializer -notmatch 'install_steam_ui_memory_saver') {
    throw 'The Steam initializer must install and enable the Steam UI Memory Saver plugin.'
}

if ($dockerfile -notmatch 'millennium/steam-ui-memory-saver') {
    throw 'The Agent image must include the Steam UI Memory Saver plugin source.'
}

Write-Output 'Steam UI Memory Saver contract passed.'
