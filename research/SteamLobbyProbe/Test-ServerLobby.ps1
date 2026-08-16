param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll',
    [string]$ServerEndpoint = '202.105.108.88:27084',
    [ValidateSet('lobby', 'game')]
    [string]$GameState = 'lobby'
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $PSScriptRoot 'publish-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath server $ServerEndpoint private 1 $GameState 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "Server lobby probe failed with exit code $probeExitCode.`n$output"
}

$lobbyMatch = [regex]::Match($output, '(?m)^LobbyCreated result=1 lobby_id=(\d+) callback_ok=True\r?$')
if (-not $lobbyMatch.Success) {
    throw "The probe did not create a Steam lobby.`n$output"
}
$lobbyId = $lobbyMatch.Groups[1].Value

$ownerMatch = [regex]::Match($output, '(?m)^SteamUser BLoggedOn=True steam_id=(\d+)\r?$')
if (-not $ownerMatch.Success) {
    throw "The probe did not report a logged-on Steam owner.`n$output"
}
$ownerSteamId = $ownerMatch.Groups[1].Value

$escapedEndpoint = [regex]::Escape($ServerEndpoint)
$expectations = @(
    "(?m)^SetLobbyData key=game:state value=$GameState ok=True\r?$",
    "(?m)^GetLobbyData key=game:state value=$GameState\r?$",
    "(?m)^SetLobbyData key=server:connectstring value=$escapedEndpoint ok=True\r?$",
    "(?m)^SetLobbyData key=server:adronline value=$escapedEndpoint ok=True\r?$",
    "(?m)^SetLobbyData key=server:adrlocal value=$escapedEndpoint ok=True\r?$",
    "(?m)^SetLobbyData key=server:reservationid value=$lobbyId ok=True\r?$",
    "(?m)^GetLobbyData key=server:connectstring value=$escapedEndpoint\r?$",
    "(?m)^GetLobbyData key=server:adronline value=$escapedEndpoint\r?$",
    "(?m)^GetLobbyData key=server:adrlocal value=$escapedEndpoint\r?$",
    "(?m)^GetLobbyData key=server:reservationid value=$lobbyId\r?$",
    "(?m)^SetLobbyGameServer lobby_id=$lobbyId ip=$escapedEndpoint steam_server_id=0\r?$",
    "(?m)^GetLobbyGameServer ok=True lobby_id=$lobbyId ip=$escapedEndpoint steam_server_id=0\r?$",
    "(?m)^JoinURI=steam://joinlobby/550/$lobbyId/$ownerSteamId\r?$",
    "(?m)^ConnectLobbyArg=\+connect_lobby $lobbyId\r?$",
    '(?m)^DispatchCallback id=509 size=24 raw=[0-9A-F]{48}\r?$',
    '(?m)^Keepalive complete seconds=1\r?$',
    '(?m)^LeaveLobby complete\r?$'
)

foreach ($expectation in $expectations) {
    if ($output -notmatch $expectation) {
        throw "Missing expected output matching: $expectation`n$output"
    }
}

Write-Output "Server lobby contract verified for lobby $lobbyId."
