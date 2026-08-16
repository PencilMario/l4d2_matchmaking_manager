$ErrorActionPreference = 'Continue'

$cdb = 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe'
$probe = Join-Path $PSScriptRoot 'NativeMatchFrameworkProbe.exe'

$output = (& $cdb -noshell -c 'sxe av; g; q' $probe 2>&1 | Out-String)

if ($output -match 'Access violation') {
    throw "MatchFramework initialization raised an access violation.`n$output"
}

if ($output -notmatch '(?m)^Init result=1\r?$') {
    throw "MatchFramework initialization did not return INIT_OK.`n$output"
}

Write-Output 'MatchFramework initialization returned INIT_OK.'
