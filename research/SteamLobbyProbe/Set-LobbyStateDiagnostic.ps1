param(
    [Parameter(Mandatory = $true)]
    [UInt64]$LobbyId,
    [ValidateSet('lobby', 'game')]
    [string]$GameState = 'game',
    [UInt64]$ExpectedOwnerSteamId = [UInt64]'76561199382197988',
    [string]$SteamApiPath = 'C:\Program Files (x86)\Steam\steamapps\common\Left 4 Dead 2\bin\steam_api.dll'
)

$ErrorActionPreference = 'Stop'
$env:SteamAppId = '550'
Set-Location -LiteralPath (Split-Path -Parent (Split-Path -Parent $SteamApiPath))

$escapedSteamApiPath = $SteamApiPath.Replace('"', '""')
$source = @"
using System;
using System.Runtime.InteropServices;

public static class LobbyStateDiagnostic
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

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyData")]
    public static extern IntPtr GetLobbyData(IntPtr self, ulong lobbyId, [MarshalAs(UnmanagedType.LPStr)] string key);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_SetLobbyData")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SetLobbyData(
        IntPtr self,
        ulong lobbyId,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        [MarshalAs(UnmanagedType.LPStr)] string value);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyOwner")]
    public static extern ulong GetLobbyOwner(IntPtr self, ulong lobbyId);
}
"@

Add-Type -TypeDefinition $source
if (-not [LobbyStateDiagnostic]::Init()) {
    throw 'SteamAPI_Init=false'
}

try {
    $matchmaking = [LobbyStateDiagnostic]::Matchmaking()
    $requested = [LobbyStateDiagnostic]::RequestLobbyData($matchmaking, $LobbyId)
    Write-Output "RequestLobbyData=$requested"
    1..20 | ForEach-Object {
        [LobbyStateDiagnostic]::RunCallbacks()
        Start-Sleep -Milliseconds 100
    }

    function Get-GameState {
        $pointer = [LobbyStateDiagnostic]::GetLobbyData($matchmaking, $LobbyId, 'game:state')
        if ($pointer -eq [IntPtr]::Zero) {
            return ''
        }
        return [Runtime.InteropServices.Marshal]::PtrToStringAnsi($pointer)
    }

    $ownerBefore = [LobbyStateDiagnostic]::GetLobbyOwner($matchmaking, $LobbyId)
    Write-Output "OwnerBefore=$ownerBefore"
    Write-Output "StateBefore=$(Get-GameState)"
    if ($ownerBefore -ne $ExpectedOwnerSteamId) {
        throw "Expected owner $ExpectedOwnerSteamId, found $ownerBefore."
    }

    $set = [LobbyStateDiagnostic]::SetLobbyData($matchmaking, $LobbyId, 'game:state', $GameState)
    Write-Output "SetGameState=$set"
    if (-not $set) {
        throw 'SetLobbyData returned false.'
    }

    1..30 | ForEach-Object {
        [LobbyStateDiagnostic]::RunCallbacks()
        Start-Sleep -Milliseconds 100
    }
    Write-Output "OwnerAfter=$([LobbyStateDiagnostic]::GetLobbyOwner($matchmaking, $LobbyId))"
    Write-Output "StateAfter=$(Get-GameState)"
}
finally {
    [LobbyStateDiagnostic]::Shutdown()
}
