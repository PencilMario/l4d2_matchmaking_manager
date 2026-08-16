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
$requesterSteamId = [UInt64]'76561199382197988'
$lobbyId = [UInt64]'109775242105038129'
$requestHex = '000008C30053797353657373696F6E3A3A526571756573744A6F696E4461746100076964000110000154C0F6E40053657474696E677300004D656D6265727300026E756D4D616368696E65730000000001026E756D506C61796572730000000001026E756D536C6F74730000000001006D616368696E653000076964000110000154C0F6E4026E756D506C6179657273000000000107646C636D61736B000000000000000000017475766572003030303030303030000270696E67000000000000706C6179657230000778756964000110000154C0F6E4016E616D65005AE38082000067616D650002736B5F76657273757300000000090B0B0B0B0B0B0B'

function ConvertTo-AsciiHex([string]$Value) {
    return [BitConverter]::ToString([Text.Encoding]::UTF8.GetBytes($Value)).Replace('-', '')
}

function ConvertTo-BigEndianUInt64Hex([UInt64]$Value) {
    $bytes = [BitConverter]::GetBytes($Value)
    [Array]::Reverse($bytes)
    return [BitConverter]::ToString($bytes).Replace('-', '')
}

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe $SteamApiPath protocol-reply $requestHex $ServerEndpoint $lobbyId $requesterSteamId $GameState 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "Protocol reply probe failed with exit code $probeExitCode.`n$output"
}

$replyMatch = [regex]::Match($output, '(?m)^ReplyJoinData recipient=(\d+) size=(\d+) raw=([0-9A-F]+)\r?$')
if (-not $replyMatch.Success) {
    throw "The probe did not emit a ReplyJoinData payload.`n$output"
}

if ([UInt64]$replyMatch.Groups[1].Value -ne $requesterSteamId) {
    throw "Reply recipient did not match the RequestJoinData sender.`n$output"
}

$replySize = [int]$replyMatch.Groups[2].Value
$replyHex = $replyMatch.Groups[3].Value
if ($replyHex.Length -ne $replySize * 2) {
    throw "Reply size $replySize did not match the encoded payload length $($replyHex.Length / 2)."
}

$expectedReplyBase64 = 'AAAIwwBTeXNTZXNzaW9uOjpSZXBseUpvaW5EYXRhAAdpZAABEAABVMD25ABTZXR0aW5ncwAATWVtYmVycwACbnVtTWFjaGluZXMAAAAAAQJudW1QbGF5ZXJzAAAAAAECbnVtU2xvdHMAAAAACABtYWNoaW5lMAAHaWQAARAAAVTA9uQCbnVtUGxheWVycwAAAAABB2RsY21hc2sAAAAAAAAAAAABdHV2ZXIAMDAwMDAwMDAAAnBpbmcAAAAAAABwbGF5ZXIwAAd4dWlkAAEQAAFUwPbkAW5hbWUAWuOAggALCwsAZ2FtZQABbW9kZQB2ZXJzdXMAAW1hcABjMm0xX2hpZ2h3YXkAAXN0YXRlAGxvYmJ5AAJza192ZXJzdXMAAAAAAAsAU3lzdGVtAAFuZXR3b3JrAExJVkUAAWFjY2VzcwBwdWJsaWMAAW5ldGZsYWcAdGVhbWxvYmJ5AAsAT3B0aW9ucwABc2VydmVyAGRlZGljYXRlZAALAFNlcnZlcgABYWRyb25saW5lADIwMi4xMDUuMTA4Ljg4OjI3MDg0AAFhZHJsb2NhbAAyMDIuMTA1LjEwOC44ODoyNzA4NAABY29ubmVjdHN0cmluZwAyMDIuMTA1LjEwOC44ODoyNzA4NAAHcmVzZXJ2YXRpb25pZAABhgAARs2lMQsLCws='
$expectedReplyHex = [Convert]::ToHexString([Convert]::FromBase64String($expectedReplyBase64))
if ($replyHex -cne $expectedReplyHex) {
    throw "Reply payload did not match the golden hex.`nExpected: $expectedReplyHex`nActual:   $replyHex"
}

$expectations = @(
    ('^000008C300' + (ConvertTo-AsciiHex 'SysSession::ReplyJoinData') + '00'),
    ('07' + (ConvertTo-AsciiHex 'id') + '00' + (ConvertTo-BigEndianUInt64Hex $requesterSteamId)),
    ('00' + (ConvertTo-AsciiHex 'Settings') + '00'),
    ('01' + (ConvertTo-AsciiHex 'state') + '00' + (ConvertTo-AsciiHex $GameState) + '00'),
    ('00' + (ConvertTo-AsciiHex 'Server') + '00'),
    ('01' + (ConvertTo-AsciiHex 'adronline') + '00' + (ConvertTo-AsciiHex $ServerEndpoint) + '00'),
    ('01' + (ConvertTo-AsciiHex 'adrlocal') + '00' + (ConvertTo-AsciiHex $ServerEndpoint) + '00'),
    ('01' + (ConvertTo-AsciiHex 'connectstring') + '00' + (ConvertTo-AsciiHex $ServerEndpoint) + '00'),
    ('07' + (ConvertTo-AsciiHex 'reservationid') + '00' + (ConvertTo-BigEndianUInt64Hex $lobbyId)),
    ('02' + (ConvertTo-AsciiHex 'numSlots') + '00' + '00000008'),
    '0B$'
)

foreach ($expectation in $expectations) {
    if ($replyHex -notmatch $expectation) {
        throw "Reply payload did not match: $expectation`n$replyHex"
    }
}

Write-Output "ReplyJoinData encoding verified for requester $requesterSteamId."
