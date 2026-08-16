$ErrorActionPreference = 'Continue'

$cdb = 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe'
$probe = Join-Path $PSScriptRoot 'NativeMatchFrameworkProbe.exe'

$output = (& $cdb -noshell -c 'sxe av; g; q' $probe 2>&1 | Out-String)

if ($output -match 'Access violation') {
    throw "CreateSession probe raised an access violation.`n$output"
}

if ($output -notmatch '(?m)^CreateSession called=true\r?$') {
    throw "CreateSession was not invoked by the probe.`n$output"
}

if ($output -notmatch '(?m)^GetMatchSession pointer=0x(?!00000000)[0-9A-Fa-f]{8}\r?$') {
    throw "CreateSession did not produce a match session.`n$output"
}

if ($output -notmatch '(?m)^Steam lobby query completed=true\r?$') {
    throw "The probe did not report a Steam lobby query result.`n$output"
}

if ($output -notmatch '(?m)^Steam lobby match=(true|false|unavailable)\r?$') {
    throw "The probe did not report a Steam lobby match state.`n$output"
}

Write-Output 'CreateSession produced a non-null match session.'
