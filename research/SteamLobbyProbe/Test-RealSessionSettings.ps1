$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'BinaryKeyValuesTestHelpers.ps1')

$publishDirectory = Join-Path $PSScriptRoot 'publish-real-settings-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = (& $probe real-settings-vector 0x12345678 0x0186000047CF0FD8 2243 2>&1 | Out-String)
$probeExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($probeExitCode -ne 0) {
    throw "Real settings vector failed with exit code $probeExitCode.`n$output"
}

$sizeMatch = [regex]::Match($output, '(?m)^SettingsSize=(\d+)\r?$')
$rawMatch = [regex]::Match($output, '(?m)^SettingsRaw=([0-9A-F]+)\r?$')
$plainMatch = [regex]::Match($output, '(?m)^PlainPayload=([0-9A-F]+)\r?$')
$encryptedMatch = [regex]::Match($output, '(?m)^EncryptedPayload=([0-9A-F]+)\r?$')
$requestMatch = [regex]::Match($output, '(?m)^ReservationRequest=([0-9A-F]+)\r?$')
if (-not $sizeMatch.Success -or -not $rawMatch.Success -or
    -not $plainMatch.Success -or -not $encryptedMatch.Success -or -not $requestMatch.Success) {
    throw "The probe did not emit the complete settings reservation vector.`n$output"
}

$settingsSize = [int]$sizeMatch.Groups[1].Value
$settingsRaw = $rawMatch.Groups[1].Value
$plainPayload = [Convert]::FromHexString($plainMatch.Groups[1].Value)
$encryptedPayload = [Convert]::FromHexString($encryptedMatch.Groups[1].Value)
$reservationRequest = [Convert]::FromHexString($requestMatch.Groups[1].Value)
$parsed = ConvertFrom-BinaryKeyValuesHex -Hex $settingsRaw -LittleEndian
if ($settingsSize -ne $parsed.Data.Length) {
    throw "SettingsSize $settingsSize did not match the encoded payload length $($parsed.Data.Length)."
}
if ($parsed.BytesConsumed -ne $settingsSize) {
    throw "Parser consumed $($parsed.BytesConsumed) bytes, expected $settingsSize."
}

if ($plainPayload.Length -ne (($settingsSize + 16 + 7) -band -8)) {
    throw "PlainPayload had $($plainPayload.Length) bytes, expected an 8-byte aligned settings payload."
}
if ($encryptedPayload.Length -ne $plainPayload.Length) {
    throw "EncryptedPayload length $($encryptedPayload.Length) did not match PlainPayload length $($plainPayload.Length)."
}
$expectedEncryptedPayload = '04944D6A637033FEF3432B66703D4FFAC33E586D005B41530FDD7EF91A23FBBADC9E660788DF70E22642B9D34EEBCD3DA1E08D000850DC148041243C7EE35AB0F835CB0F9D48B1B776C2FEAEBE2AE36108905926E2391A37CF098A8CB56AD5472ABE0D984398FEFA5EB9EEC4CCE2BEDF1A0D6C925E49A57F86B1A289BCE9AB7F21A108EA18A793778500F1B675CA676945342DF20A6B8D6EBA5F100C1D31C29015FAA72B858B076ADD7C0FE0E855AAF3C48F1A2AD5764F57E7262FC760655F1060670352FCC4AFCE8DA23D9B7092C922622E1B5A008235063CF8B50DAAEEB51E8593849DB2816D41D8D5E50749F1E694C5114C3D949395FE0428FB7667FE5F50E6906892B90935586174447F0F1B6EE33C98BC648E5ED437963FFDA11FAEAA0B3719226EF00528EA9E57CF32D83CFE5058F31EDAF4D5DE5D184CF3FBB3E99D5956C77B588E6E2DBE0C8E6AA82B3FFF005B7C4CD501201BE180061DA8474B34AB6F7B637DCCBEA375E1310514FEFD991F4E6892A41A91CA1F31CABDCD58D4C3963828EA1B25E26CD8CB1C026E35DEA6E557D31CC91DE073F1586CAB95375F900472822EE2588BA7DE18D1B98995971CAF8B54DE275C2CB0044930D6500D156DA065EF167B5EA45D0C94EE747005312BF5EFEC9322FCB83B3794EE747005312BF5538977F02FAF8A0438C1420B50428FCC4487F14AA4817CE6B9B986B5342DF5AF35D5F190BEB9A71267EA26707571E497EC131F9B463C41071843FF0B1B15DF4DA5A4CABD518345B5693FA76A15546FA4BB72496F77221040'
if ($encryptedMatch.Groups[1].Value -cne $expectedEncryptedPayload) {
    throw "EncryptedPayload did not match the fixed ICE vector."
}
if ($reservationRequest.Length -ne 13 + $encryptedPayload.Length) {
    throw "ReservationRequest had $($reservationRequest.Length) bytes, expected $($encryptedPayload.Length + 13)."
}

$expectedPlainPrefix = [byte[]](0xEF, 0xBE, 0xED, 0xFE, 0xD8, 0x0F, 0xCF, 0x47, 0x00, 0x00, 0x86, 0x01, 0x27, 0x02, 0x00, 0x00)
if ([Convert]::ToHexString([byte[]]$plainPayload[0..15]) -cne [Convert]::ToHexString($expectedPlainPrefix)) {
    throw "PlainPayload did not contain the expected magic, cookie, or settings length prefix."
}
if ([Convert]::ToHexString([byte[]]$plainPayload[16..($settingsSize + 15)]) -cne $settingsRaw) {
    throw "PlainPayload settings bytes did not match SettingsRaw."
}
$paddingStart = $settingsSize + 16
if ($paddingStart -lt $plainPayload.Length -and
    ($plainPayload[$paddingStart..($plainPayload.Length - 1)] | Where-Object { $_ -ne 0 })) {
    throw "PlainPayload contained non-zero bytes after the settings data."
}

$expectedRequestPrefix = [byte[]](0xFF, 0xFF, 0xFF, 0xFF, 0x6E, 0xC3, 0x08, 0x00, 0x00, 0x38, 0x02, 0x00, 0x00)
if ([Convert]::ToHexString([byte[]]$reservationRequest[0..12]) -cne [Convert]::ToHexString($expectedRequestPrefix)) {
    throw "ReservationRequest did not contain the expected header, host version, or encrypted length."
}
if ([Convert]::ToHexString([byte[]]$reservationRequest[13..($reservationRequest.Length - 1)]) -cne [Convert]::ToHexString($encryptedPayload)) {
    throw "ReservationRequest encrypted payload did not match EncryptedPayload."
}

$expectedSettingsLeaves = @(
    [pscustomobject]@{ Path = 'Settings/Game/campaign'; Value = 'L4D2C2'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/chapter'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/difficulty'; Value = 'normal'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/dlcrequired'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/maxrounds'; Value = 3; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/addon'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/Author'; Value = 'Valve'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/builtin'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/DisplayTitle'; Value = '#L4D360UI_CampaignName_C2'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/InfectedOnly'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/MissionFile'; Value = 'missions/campaign2.txt'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/SurvivorSet'; Value = 2; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/Version'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/Website'; Value = 'http://store.steampowered.com'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/MissionInfo/workshopid'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/Mode'; Value = 'versus'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/ModeInfo/addon'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/ModeInfo/workshopid'; Value = 0; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/sk_versus'; Value = 35; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Game/state'; Value = 'game'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/Game/vanilla'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Members/numMachines'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Members/numPlayers'; Value = 1; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Members/numSlots'; Value = 8; Type = [int] }
    [pscustomobject]@{ Path = 'Settings/Options/Server'; Value = 'official'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/System/access'; Value = 'public'; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/System/lock'; Value = ''; Type = [string] }
    [pscustomobject]@{ Path = 'Settings/System/network'; Value = 'LIVE'; Type = [string] }
)
$actualLeafCount = @($parsed.Index.Values | Where-Object { $_ -isnot [bool] }).Count
if ($actualLeafCount -ne 28) {
    throw "Binary settings contained $actualLeafCount leaf fields, expected 28."
}
if ($expectedSettingsLeaves.Count -ne 28) {
    throw "The binary settings test declared $($expectedSettingsLeaves.Count) leaf expectations, expected 28."
}
foreach ($expectedLeaf in $expectedSettingsLeaves) {
    Assert-BinaryKeyValuesValue $parsed.Index $expectedLeaf.Path $expectedLeaf.Value $expectedLeaf.Type
}

if ($parsed.Index.ContainsKey('Settings/Server')) {
    throw 'Reservation settings unexpectedly contained Settings/Server.'
}

$expectedMetadata = [ordered]@{
    'Game:campaign' = 'L4D2C2'
    'Game:chapter' = '1'
    'Game:difficulty' = 'normal'
    'Game:dlcrequired' = '0'
    'Game:maxrounds' = '3'
    'Game:MissionInfo:addon' = '0'
    'Game:MissionInfo:Author' = 'Valve'
    'Game:MissionInfo:builtin' = '1'
    'Game:MissionInfo:DisplayTitle' = '#L4D360UI_CampaignName_C2'
    'Game:MissionInfo:InfectedOnly' = '0'
    'Game:MissionInfo:MissionFile' = 'missions/campaign2.txt'
    'Game:MissionInfo:SurvivorSet' = '2'
    'Game:MissionInfo:Version' = '1'
    'Game:MissionInfo:Website' = 'http://store.steampowered.com'
    'Game:MissionInfo:workshopid' = '0'
    'Game:Mode' = 'versus'
    'Game:ModeInfo:addon' = '0'
    'Game:ModeInfo:workshopid' = '0'
    'Game:sk_versus' = '35'
    'Game:state' = 'game'
    'Game:vanilla' = '1'
    'Members:numMachines' = '1'
    'Members:numPlayers' = '1'
    'Members:numSlots' = '8'
    'Options:Server' = 'official'
    'System:access' = 'public'
    'System:lock' = ''
    'System:network' = 'LIVE'
}
$metadataCountMatches = [regex]::Matches($output, '(?m)^LobbyMetadataCount=(\d+)\r?$')
if ($metadataCountMatches.Count -ne 1) {
    throw "The probe did not emit exactly one LobbyMetadataCount line.`n$output"
}
$metadataCount = [int]$metadataCountMatches[0].Groups[1].Value
$metadataMatches = [regex]::Matches($output, '(?m)^LobbyMetadata key=([^\r\n]+?) value=([^\r\n]*)\r?$')
$metadata = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
foreach ($match in $metadataMatches) {
    $key = $match.Groups[1].Value
    if ($metadata.ContainsKey($key)) {
        throw "Lobby metadata key '$key' was emitted more than once."
    }
    $metadata.Add($key, $match.Groups[2].Value)
}
if ($metadataCount -ne $expectedMetadata.Count) {
    throw "LobbyMetadataCount was $metadataCount, expected $($expectedMetadata.Count)."
}
if ($metadataMatches.Count -ne $metadataCount -or $metadata.Count -ne $metadataCount) {
    throw "The probe emitted $($metadataMatches.Count) metadata lines for declared count $metadataCount."
}
foreach ($key in $expectedMetadata.Keys) {
    if (-not $metadata.ContainsKey($key)) {
        throw "Lobby metadata key '$key' was not present."
    }
    if ($metadata[$key] -cne $expectedMetadata[$key]) {
        throw "Lobby metadata key '$key' was '$($metadata[$key])', expected '$($expectedMetadata[$key])'."
    }
}

$requesterSteamId = [UInt64]'76561199382197988'
$lobbyId = [UInt64]'109775242105038129'
$requestHex = '000008C30053797353657373696F6E3A3A526571756573744A6F696E4461746100076964000110000154C0F6E40053657474696E677300004D656D6265727300026E756D4D616368696E65730000000001026E756D506C61796572730000000001026E756D536C6F74730000000001006D616368696E653000076964000110000154C0F6E4026E756D506C6179657273000000000107646C636D61736B000000000000000000017475766572003030303030303030000270696E67000000000000706C6179657230000778756964000110000154C0F6E4016E616D65005AE38082000067616D650002736B5F76657273757300000000090B0B0B0B0B0B0B'
$ErrorActionPreference = 'Continue'
$trailingOutput = (& $probe $project protocol-reply ($requestHex + '00') '202.105.108.88:27084' $lobbyId $requesterSteamId lobby 2>&1 | Out-String)
$trailingExitCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($trailingExitCode -eq 0 -or $trailingOutput -match '(?m)^ReplyJoinData ') {
    throw "RequestJoinData with trailing bytes was accepted.`n$trailingOutput"
}

Write-Output "Real session settings encoding verified: size=$settingsSize fields=28 trailing_bytes=0; trailing RequestJoinData rejected."
