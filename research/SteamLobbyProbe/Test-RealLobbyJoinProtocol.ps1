param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll',
    [string]$ServerEndpoint = '202.105.108.88:27084'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'BinaryKeyValuesTestHelpers.ps1')

$publishDirectory = Join-Path $PSScriptRoot 'publish-real-join-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'
$ownerSteamId = [UInt64]'76561199012457364'
$requesterSteamId = [UInt64]'76561199382197988'
$lobbyId = [UInt64]'109775242121908184'
$requestHex = '000008C30053797353657373696F6E3A3A526571756573744A6F696E4461746100076964000110000154C0F6E40053657474696E677300004D656D6265727300026E756D4D616368696E65730000000001026E756D506C61796572730000000001026E756D536C6F74730000000001006D616368696E653000076964000110000154C0F6E4026E756D506C6179657273000000000107646C636D61736B000000000000000000017475766572003030303030303030000270696E67000000000000706C6179657230000778756964000110000154C0F6E4016E616D65005AE38082000067616D650002736B5F76657273757300000000090B0B0B0B0B0B0B'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath protocol-reply-real $requestHex $ServerEndpoint $lobbyId $ownerSteamId 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "Real protocol reply probe failed with exit code $probeExitCode.`n$output"
}

$replyMatch = [regex]::Match($output, '(?m)^ReplyJoinData recipient=(\d+) size=(\d+) raw=([0-9A-F]+)\r?$')
if (-not $replyMatch.Success) {
    throw "The probe did not emit a real ReplyJoinData payload.`n$output"
}

if ([UInt64]$replyMatch.Groups[1].Value -ne $requesterSteamId) {
    throw "Reply recipient did not match the RequestJoinData sender."
}

$replySize = [int]$replyMatch.Groups[2].Value
$replyHex = $replyMatch.Groups[3].Value
if ($replyHex.Length -ne $replySize * 2) {
    throw "Reply size $replySize did not match the encoded payload length $($replyHex.Length / 2)."
}

$parsed = ConvertFrom-BinaryKeyValuesHex -Hex $replyHex.Substring(8)
$expectedLeaves = @(
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/id'; Value = $requesterSteamId; Type = [UInt64] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/campaign'; Value = 'L4D2C2'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/chapter'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/MissionInfo/MissionFile'; Value = 'missions/campaign2.txt'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/Mode'; Value = 'versus'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/ModeInfo/workshopid'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/sk_versus'; Value = 35; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/state'; Value = 'game'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Game/vanilla'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/numMachines'; Value = 2; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/numPlayers'; Value = 2; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/numSlots'; Value = 8; Type = [int] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/machine0/id'; Value = $ownerSteamId; Type = [UInt64] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/machine0/player0/xuid'; Value = $ownerSteamId; Type = [UInt64] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/machine1/id'; Value = $requesterSteamId; Type = [UInt64] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Members/machine1/player0/xuid'; Value = $requesterSteamId; Type = [UInt64] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Options/Server'; Value = 'official'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/System/access'; Value = 'public'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/System/lock'; Value = ''; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/System/network'; Value = 'LIVE'; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Server/adronline'; Value = $ServerEndpoint; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Server/adrlocal'; Value = $ServerEndpoint; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Server/connectstring'; Value = $ServerEndpoint; Type = [string] }
    [pscustomobject]@{ Path = 'SysSession::ReplyJoinData/Settings/Server/reservationid'; Value = $lobbyId; Type = [UInt64] }
)

foreach ($expectedLeaf in $expectedLeaves) {
    Assert-BinaryKeyValuesValue $parsed.Index $expectedLeaf.Path $expectedLeaf.Value $expectedLeaf.Type
}

Write-Output "Real ReplyJoinData encoding verified for requester $requesterSteamId with owner $ownerSteamId."
