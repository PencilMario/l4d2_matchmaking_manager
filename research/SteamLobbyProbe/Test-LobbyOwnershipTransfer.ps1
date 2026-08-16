param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll',
    [UInt64]$LobbyId = [UInt64]'109775242109682143',
    [UInt64]$ExpectedCurrentOwnerSteamId = [UInt64]'76561199012457364',
    [UInt64]$NewOwnerSteamId = [UInt64]'76561199382197988',
    [string]$ExpectedConnectString = '106.54.197.3:24561'
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $PSScriptRoot 'publish-transfer-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath transfer-owner $LobbyId $NewOwnerSteamId 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "Lobby ownership transfer failed with exit code $probeExitCode.`n$output"
}

$expectedReservationId = $LobbyId.ToString()
$expectations = @(
    "(?m)^LobbyOwnership before lobby_id=$LobbyId owner=$ExpectedCurrentOwnerSteamId current_user=$ExpectedCurrentOwnerSteamId\r?$",
    "(?m)^LobbyData before key=server:connectstring value=$([regex]::Escape($ExpectedConnectString))\r?$",
    "(?m)^LobbyData before key=server:reservationid value=$expectedReservationId\r?$",
    "(?m)^SetLobbyOwner lobby_id=$LobbyId new_owner=$NewOwnerSteamId ok=True\r?$",
    "(?m)^LobbyOwnership after lobby_id=$LobbyId owner=$NewOwnerSteamId\r?$",
    "(?m)^LobbyData after key=server:connectstring value=$([regex]::Escape($ExpectedConnectString))\r?$",
    "(?m)^LobbyData after key=server:reservationid value=$expectedReservationId\r?$",
    '(?m)^MetadataPreserved connectstring=True reservationid=True\r?$',
    "(?m)^NewOwnerJoinURI=steam://joinlobby/550/$LobbyId/$NewOwnerSteamId\r?$"
)

foreach ($expectation in $expectations) {
    if ($output -notmatch $expectation) {
        throw "Missing expected output matching: $expectation`n$output"
    }
}

Write-Output "Lobby ownership transfer verified for lobby $LobbyId."
