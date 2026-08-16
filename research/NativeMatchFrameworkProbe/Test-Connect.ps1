$ErrorActionPreference = 'Continue'

$cdb = 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe'
$probe = Join-Path $PSScriptRoot 'NativeMatchFrameworkProbe.exe'

$output = (& $cdb -noshell -c 'sxe av; g; q' $probe 2>&1 | Out-String)

if ($output -match 'Access violation') {
    throw "Connect probe raised an access violation.`n$output"
}

if ($output -notmatch '(?m)^Connect=(true|false)\r?$') {
    throw "Connect probe did not return to the caller.`n$output"
}

Write-Output 'Connect probe completed without an access violation.'
