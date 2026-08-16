param(
    [Parameter(Mandatory = $true)]
    [int]$X,
    [Parameter(Mandatory = $true)]
    [int]$Y,
    [ValidateRange(1, 3)]
    [int]$ClickCount = 1
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class DesktopClick
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

if (-not [DesktopClick]::SetCursorPos($X, $Y)) {
    throw 'SetCursorPos failed.'
}

for ($index = 0; $index -lt $ClickCount; $index++) {
    [DesktopClick]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [DesktopClick]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}
