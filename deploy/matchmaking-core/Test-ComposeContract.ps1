$ErrorActionPreference = 'Stop'

$composePath = Join-Path $PSScriptRoot 'docker-compose.yml'
if (-not (Test-Path -LiteralPath $composePath)) {
    throw "Missing compose file: $composePath"
}

$compose = Get-Content -Raw -LiteralPath $composePath
if ($compose -notmatch '/var/run/docker.sock:/var/run/docker.sock') {
    throw 'Only Core may mount the Docker socket.'
}

if ($compose -notmatch 'postgres:') {
    throw 'Core deployment requires PostgreSQL.'
}

if ($compose -match '5432:5432') {
    throw 'PostgreSQL must not publish a host port.'
}

if ($compose -notmatch 'CORE_API_TOKEN_FILE') {
    throw 'Core must read the management token from a secret file.'
}

if ($compose -notmatch 'CORE_RCON_ENCRYPTION_KEY_FILE') {
    throw 'Core must read the RCON encryption key from a secret file.'
}

if ($compose -notmatch 'CORE_SHARED_LIBRARY_HOST_PATH') {
    throw 'Core must receive the shared Steam library host path for managed Agents.'
}

if ($compose -notmatch 'CORE_AGENT_STEAM_API_LIBRARY_PATH') {
    throw 'Core must receive the Agent-visible Steam API library path for managed Agents.'
}

if ($compose -notmatch 'CORE_AGENT_STEAM_LOGIN_UI_MODE') {
    throw 'Core must receive the managed Agent Steam login UI mode.'
}

if ($compose -notmatch 'CORE_SCHEDULER_MAX_STARTS_PER_TICK') {
    throw 'Core must receive the bounded scheduler batch start limit.'
}

if ($compose -notmatch 'CORE_AGENT_IMAGE') {
    throw 'Core must receive the managed Agent image tag.'
}

if ($compose -cmatch '(?m)^\s+CORE_API_TOKEN:\s*') {
    throw 'Core must not place the management token directly in Compose environment values.'
}

if ($compose -cmatch '(?m)^\s+CORE_RCON_ENCRYPTION_KEY:\s*') {
    throw 'Core must not place the RCON encryption key directly in Compose environment values.'
}
