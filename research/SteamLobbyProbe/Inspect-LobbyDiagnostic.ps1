param(
    [Parameter(Mandatory = $true)]
    [UInt64]$LobbyId,
    [string]$SteamApiPath = 'D:\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll',
    [ValidateRange(500, 10000)]
    [int]$CallbackPumpMilliseconds = 2500
)

$ErrorActionPreference = 'Stop'

if ([IntPtr]::Size -ne 4) {
    $x86PowerShell = "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
    if (-not (Test-Path -LiteralPath $x86PowerShell)) {
        throw "32-bit PowerShell was not found at $x86PowerShell."
    }

    & $x86PowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -LobbyId $LobbyId `
        -SteamApiPath $SteamApiPath `
        -CallbackPumpMilliseconds $CallbackPumpMilliseconds
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $SteamApiPath)) {
    throw "steam_api.dll was not found at $SteamApiPath."
}

$env:SteamAppId = '550'
$gameDirectory = Split-Path -Parent (Split-Path -Parent $SteamApiPath)
Set-Location -LiteralPath $gameDirectory

$escapedSteamApiPath = $SteamApiPath.Replace('"', '""')
$source = @"
using System;
using System.Runtime.InteropServices;

public static class LobbyReadOnlyDiagnostic
{
    private const string Dll = @"$escapedSteamApiPath";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_Init")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Init();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_Shutdown")]
    public static extern void Shutdown();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_RunCallbacks")]
    public static extern void RunCallbacks();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_SteamMatchmaking_v009")]
    public static extern IntPtr Matchmaking();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_RequestLobbyData")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool RequestLobbyData(IntPtr self, ulong lobbyId);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyDataCount")]
    public static extern int GetLobbyDataCount(IntPtr self, ulong lobbyId);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyDataByIndex")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool GetLobbyDataByIndex(
        IntPtr self,
        ulong lobbyId,
        int index,
        IntPtr key,
        int keySize,
        IntPtr value,
        int valueSize);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyOwner")]
    public static extern ulong GetLobbyOwner(IntPtr self, ulong lobbyId);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetNumLobbyMembers")]
    public static extern int GetNumLobbyMembers(IntPtr self, ulong lobbyId);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex")]
    public static extern ulong GetLobbyMemberByIndex(IntPtr self, ulong lobbyId, int memberIndex);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyGameServer")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool GetLobbyGameServer(
        IntPtr self,
        ulong lobbyId,
        out uint ip,
        out ushort port,
        out ulong serverId);
}
"@

Add-Type -TypeDefinition $source

function Read-Utf8Buffer {
    param(
        [Parameter(Mandatory = $true)]
        [IntPtr]$Pointer,
        [Parameter(Mandatory = $true)]
        [int]$Capacity
    )

    $bytes = New-Object byte[] $Capacity
    [Runtime.InteropServices.Marshal]::Copy($Pointer, $bytes, 0, $Capacity)
    $length = [Array]::IndexOf($bytes, [byte]0)
    if ($length -lt 0) {
        $length = $Capacity
    }
    return [Text.Encoding]::UTF8.GetString($bytes, 0, $length)
}

function Format-JsonString {
    param([AllowEmptyString()][string]$Value)
    return ConvertTo-Json -Compress -InputObject $Value
}

Write-Output 'Mode=read-only'
Write-Output 'ProcessArchitecture=x86'
Write-Output "SteamApiPath=$SteamApiPath"
Write-Output "LobbyId=$LobbyId"
Write-Output ("LobbyIdHex=0x{0:X16}" -f $LobbyId)

if (-not [LobbyReadOnlyDiagnostic]::Init()) {
    throw 'SteamAPI_Init=false'
}

try {
    $matchmaking = [LobbyReadOnlyDiagnostic]::Matchmaking()
    if ($matchmaking -eq [IntPtr]::Zero) {
        throw 'SteamAPI_SteamMatchmaking_v009 returned null.'
    }

    $requested = [LobbyReadOnlyDiagnostic]::RequestLobbyData($matchmaking, $LobbyId)
    Write-Output "RequestLobbyData=$requested"

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.ElapsedMilliseconds -lt $CallbackPumpMilliseconds) {
        [LobbyReadOnlyDiagnostic]::RunCallbacks()
        Start-Sleep -Milliseconds 50
    }
    [LobbyReadOnlyDiagnostic]::RunCallbacks()
    Write-Output "CallbackPumpMilliseconds=$($stopwatch.ElapsedMilliseconds)"

    [UInt64]$owner = [LobbyReadOnlyDiagnostic]::GetLobbyOwner($matchmaking, $LobbyId)
    Write-Output "Owner=$owner"

    $memberCount = [LobbyReadOnlyDiagnostic]::GetNumLobbyMembers($matchmaking, $LobbyId)
    Write-Output "MemberCount=$memberCount"
    for ($index = 0; $index -lt $memberCount; $index++) {
        [UInt64]$member = [LobbyReadOnlyDiagnostic]::GetLobbyMemberByIndex($matchmaking, $LobbyId, $index)
        Write-Output "Member[$index]=$member owner=$($member -eq $owner)"
    }

    $metadataCount = [LobbyReadOnlyDiagnostic]::GetLobbyDataCount($matchmaking, $LobbyId)
    Write-Output "MetadataCount=$metadataCount"
    $keyCapacity = 512
    $valueCapacity = 16384
    for ($index = 0; $index -lt $metadataCount; $index++) {
        $keyPointer = [Runtime.InteropServices.Marshal]::AllocHGlobal($keyCapacity)
        $valuePointer = [Runtime.InteropServices.Marshal]::AllocHGlobal($valueCapacity)
        try {
            [Runtime.InteropServices.Marshal]::Copy((New-Object byte[] $keyCapacity), 0, $keyPointer, $keyCapacity)
            [Runtime.InteropServices.Marshal]::Copy((New-Object byte[] $valueCapacity), 0, $valuePointer, $valueCapacity)

            $read = [LobbyReadOnlyDiagnostic]::GetLobbyDataByIndex(
                $matchmaking,
                $LobbyId,
                $index,
                $keyPointer,
                $keyCapacity,
                $valuePointer,
                $valueCapacity)

            if (-not $read) {
                Write-Output "Metadata[$index] read=False"
                continue
            }

            $key = Read-Utf8Buffer -Pointer $keyPointer -Capacity $keyCapacity
            $value = Read-Utf8Buffer -Pointer $valuePointer -Capacity $valueCapacity
            Write-Output "Metadata[$index] key=$(Format-JsonString $key) value=$(Format-JsonString $value)"
        }
        finally {
            [Runtime.InteropServices.Marshal]::FreeHGlobal($keyPointer)
            [Runtime.InteropServices.Marshal]::FreeHGlobal($valuePointer)
        }
    }

    [UInt32]$serverIp = 0
    [UInt16]$serverPort = 0
    [UInt64]$serverSteamId = 0
    $hasGameServer = [LobbyReadOnlyDiagnostic]::GetLobbyGameServer(
        $matchmaking,
        $LobbyId,
        [ref]$serverIp,
        [ref]$serverPort,
        [ref]$serverSteamId)

    $serverIpText = '{0}.{1}.{2}.{3}' -f `
        (($serverIp -shr 24) -band 0xFF), `
        (($serverIp -shr 16) -band 0xFF), `
        (($serverIp -shr 8) -band 0xFF), `
        ($serverIp -band 0xFF)
    Write-Output "GetLobbyGameServer=$hasGameServer ip=$serverIpText port=$serverPort steam_server_id=$serverSteamId"
}
finally {
    [LobbyReadOnlyDiagnostic]::Shutdown()
}
