$ErrorActionPreference = 'Stop'

$script = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'l4d2-steam-status.sh')
$launcher = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'l4d2-steam-status.desktop')
$dockerfile = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Dockerfile')

if ($script -notmatch 'bootstrap_log\.txt' -or
    $script -notmatch 'supervisorctl status steam' -or
    $script -notmatch 'probe/status' -or
    $script -notmatch 'while true' -or
    $script -notmatch 'read -r -t 2' -or
    $script -notmatch 'print_state' -or
    $script -notmatch 'ready=true：允许进入调度候选集') {
    throw 'The Steam status script must refresh update progress, Steam process status and Agent readiness until dismissed.'
}

if ($launcher -notmatch '(?m)^Name=Steam 更新状态' -or
    $launcher -notmatch 'Exec=xfce4-terminal --hold --command=/usr/local/bin/l4d2-steam-status' -or
    $launcher -notmatch '(?m)^Categories=Game;' -or
    $launcher -notmatch '(?m)^Terminal=false') {
    throw 'The Applications launcher must open the read-only Steam status script in XFCE Terminal.'
}

if ($dockerfile -notmatch 'l4d2-steam-status\.sh /usr/local/bin/l4d2-steam-status' -or
    $dockerfile -notmatch 'l4d2-steam-status\.desktop /usr/share/applications/l4d2-steam-status\.desktop') {
    throw 'The Agent image must install the Steam status script and Applications launcher.'
}
