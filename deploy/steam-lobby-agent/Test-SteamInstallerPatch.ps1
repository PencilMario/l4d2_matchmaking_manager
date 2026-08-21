$ErrorActionPreference = 'Stop'

$dockerfile = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Dockerfile')

foreach ($requiredPattern in @(
    'patch_file="\$\(mktemp /tmp/steam-installer-patch\.XXXXXX\)"'
    'grep -Fq.*if command -v zenity.*\/usr\/games\/steam'
    'grep -Fq.*if ! "\$zenityish".*\/usr\/games\/steam'
    'index\(\$0,.*if !.*zenityish'
    'starts\+\+'
    'ends\+\+'
    'skipping != 0'
    'grep -Fq.*if command -v zenity.*patch_file'
    'grep -Fq.*if ! "\$zenityish".*patch_file'
    'grep -Fq.*bootstraplinux_ubuntu12_32\.tar\.xz.*patch_file'
    'grep -Fq.*deb-installer.*patch_file'
    'bash -n "\$\{patch_file\}"'
    'install -m 0755 "\$\{patch_file\}" \/usr\/games\/steam'
)) {
    if ($dockerfile -notmatch $requiredPattern) {
        throw "The Steam installer patch is missing required contract pattern: $requiredPattern"
    }
}

if ($dockerfile.Contains('/if command -v zenity/ {') -or
    $dockerfile.Contains('zenity --question')) {
    throw 'The patch must target the confirmation block through zenityish, not the zenity selector or a nonexistent literal command.'
}

if ($dockerfile -notmatch '(?s)grep -Fq.*if ! "\$zenityish".*\/usr\/games\/steam.*awk') {
    throw 'The patch must guard the expected confirmation block before transforming it.'
}

if ($dockerfile -notmatch '(?s)awk .*END \{.*starts != 1.*ends != 1.*skipping != 0') {
    throw 'The patch must fail closed unless exactly one confirmation block is removed.'
}

if ($dockerfile -notmatch '(?s)grep -Fq.*if ! "\$zenityish".*patch_file.*bash -n') {
    throw 'The patched script must be checked for removal of the confirmation block before installation.'
}

if ($dockerfile -notmatch '(?s)bash -n "\$\{patch_file\}".*install -m 0755') {
    throw 'The Agent image must apply a guarded, syntax-checked patch that removes the Steam installer confirmation.'
}
