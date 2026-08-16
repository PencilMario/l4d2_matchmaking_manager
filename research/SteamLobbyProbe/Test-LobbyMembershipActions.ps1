param(
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll'
)

$ErrorActionPreference = 'Stop'

$publishDirectory = Join-Path $PSScriptRoot 'publish-membership-test'
$project = Join-Path $PSScriptRoot 'SteamLobbyProbe.csproj'
$probe = Join-Path $publishDirectory 'SteamLobbyProbe.exe'
$holderStdout = Join-Path $PSScriptRoot 'membership-holder.stdout.log'
$holderStderr = Join-Path $PSScriptRoot 'membership-holder.stderr.log'

& dotnet publish $project -c Release -r win-x86 --self-contained true -p:PublishSingleFile=false -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "SteamLobbyProbe publish failed with exit code $LASTEXITCODE."
}

$quotedSteamApiPath = '"' + $SteamApiPath + '"'
$holder = Start-Process `
    -FilePath $probe `
    -ArgumentList @($quotedSteamApiPath, 'create-lobby-hold', 'private', '8') `
    -RedirectStandardOutput $holderStdout `
    -RedirectStandardError $holderStderr `
    -PassThru `
    -WindowStyle Hidden

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    $holderOutput = ''
    $lobbyMatch = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $holderStdout) {
            $holderOutput = Get-Content -Raw -LiteralPath $holderStdout
            if ($null -eq $holderOutput) {
                $holderOutput = ''
            }
            $lobbyMatch = [regex]::Match(
                $holderOutput,
                '(?m)^CreateLobbyHold lobby_id=(\d+) type=0 seconds=8\r?$')
            if ($lobbyMatch.Success) {
                break
            }
        }

        if ($holder.HasExited) {
            $holderError = if (Test-Path -LiteralPath $holderStderr) {
                Get-Content -Raw -LiteralPath $holderStderr
            } else {
                ''
            }
            throw "Lobby holder exited before creating a lobby.`n$holderOutput`n$holderError"
        }

        Start-Sleep -Milliseconds 100
    }

    if (-not $lobbyMatch -or -not $lobbyMatch.Success) {
        throw "Timed out waiting for create-lobby-hold output.`n$holderOutput"
    }

    $lobbyId = [UInt64]$lobbyMatch.Groups[1].Value
    $ownerMatch = [regex]::Match($holderOutput, '(?m)^SteamUser BLoggedOn=True steam_id=(\d+)\r?$')
    if (-not $ownerMatch.Success) {
        throw "Lobby holder did not report its Steam ID.`n$holderOutput"
    }
    $steamId = [UInt64]$ownerMatch.Groups[1].Value

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $leaveOutput = (& $probe $SteamApiPath leave-lobby $lobbyId 1 2>&1 | Out-String)
    $leaveExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    if ($leaveExitCode -ne 0) {
        throw "leave-lobby failed with exit code $leaveExitCode.`n$leaveOutput"
    }

    $expectations = @(
        "(?m)^LobbyMembership before lobby_id=$lobbyId current_user=$steamId owner=$steamId member_count=1 valid_member_count=1 is_member=True members=$steamId\r?$",
        "(?m)^LeaveLobby requested lobby_id=$lobbyId settle_seconds=1\r?$",
        "(?m)^LobbyMembership after lobby_id=$lobbyId current_user=$steamId owner=$steamId member_count=1 valid_member_count=0 is_member=False members=<none>\r?$"
    )

    foreach ($expectation in $expectations) {
        if ($leaveOutput -notmatch $expectation) {
            throw "Missing expected output matching: $expectation`n$leaveOutput"
        }
    }

    Wait-Process -Id $holder.Id -Timeout 12
    $holderOutput = Get-Content -Raw -LiteralPath $holderStdout
    $holderExpectations = @(
        '(?m)^Keepalive complete seconds=8\r?$',
        '(?m)^LeaveLobby complete\r?$'
    )
    foreach ($expectation in $holderExpectations) {
        if ($holderOutput -notmatch $expectation) {
            $holderError = Get-Content -Raw -LiteralPath $holderStderr
            throw "Lobby holder did not complete cleanly; missing: $expectation`n$holderOutput`n$holderError"
        }
    }

    Write-Output "Lobby membership actions verified for lobby $lobbyId."
}
finally {
    if (-not $holder.HasExited) {
        Stop-Process -Id $holder.Id -Force
        Wait-Process -Id $holder.Id -ErrorAction SilentlyContinue
    }
}
