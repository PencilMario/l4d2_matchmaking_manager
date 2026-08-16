param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll'
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $PSScriptRoot 'publish-server-reserved-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath server-reserved 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

if ($probeExitCode -eq 0 -or $output -notmatch '(?m)server-reserved mode requires an IPv4 endpoint in the form ip:port') {
    throw "Missing server-reserved endpoint validation.`n$output"
}

Write-Output 'server-reserved CLI validation verified.'
