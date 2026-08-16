param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll',
    [Parameter(Mandatory = $true)]
    [UInt64]$LobbyId,
    [UInt64]$ExpectedOwnerSteamId = [UInt64]'76561199012457364'
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $PSScriptRoot 'publish-join-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath join-lobby-hold $LobbyId 1 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "join-lobby-hold failed with exit code $probeExitCode.`n$output"
}

$steamIdMatch = [regex]::Match($output, '(?m)^SteamUser BLoggedOn=True steam_id=(\d+)\r?$')
if (-not $steamIdMatch.Success) {
    throw "join-lobby-hold did not report the current Steam ID.`n$output"
}
$currentSteamId = [UInt64]$steamIdMatch.Groups[1].Value

$expectations = @(
    "(?m)^JoinLobbyHold lobby_id=$LobbyId response=1 seconds=1\r?$",
    "(?m)^LobbyMembership joined lobby_id=$LobbyId current_user=$currentSteamId owner=$ExpectedOwnerSteamId member_count=1 valid_member_count=1 is_member=True members=$currentSteamId\r?$",
    '(?m)^Keepalive complete seconds=1\r?$',
    '(?m)^LeaveLobby complete\r?$'
)

foreach ($expectation in $expectations) {
    if ($output -notmatch $expectation) {
        throw "Missing expected output matching: $expectation`n$output"
    }
}

Write-Output "Lobby join action verified for lobby $LobbyId."
