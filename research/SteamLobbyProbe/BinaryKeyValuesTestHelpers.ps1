function Read-BinaryKeyValuesCString {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Data,
        [Parameter(Mandatory)]
        [ref]$Offset
    )

    $start = $Offset.Value
    while ($Offset.Value -lt $Data.Length -and $Data[$Offset.Value] -ne 0) {
        $Offset.Value++
    }

    if ($Offset.Value -ge $Data.Length) {
        throw "Binary KeyValues string at offset $start is missing its NUL terminator."
    }

    $length = $Offset.Value - $start
    $value = [Text.Encoding]::UTF8.GetString($Data, $start, $length)
    $Offset.Value++
    return $value
}

function Read-BinaryKeyValuesNumber {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Data,
        [Parameter(Mandatory)]
        [ref]$Offset,
        [Parameter(Mandatory)]
        [ValidateSet(4, 8)]
        [int]$Size,
        [switch]$Unsigned,
        [switch]$LittleEndian
    )

    if ($Offset.Value + $Size -gt $Data.Length) {
        throw "Binary KeyValues number at offset $($Offset.Value) is truncated."
    }

    $bytes = [byte[]]::new($Size)
    [Array]::Copy($Data, $Offset.Value, $bytes, 0, $Size)
    if ([BitConverter]::IsLittleEndian -and -not $LittleEndian) {
        [Array]::Reverse($bytes)
    }
    $Offset.Value += $Size

    if ($Size -eq 4) {
        return [BitConverter]::ToInt32($bytes, 0)
    }
    if ($Unsigned) {
        return [BitConverter]::ToUInt64($bytes, 0)
    }
    return [BitConverter]::ToInt64($bytes, 0)
}

function Read-BinaryKeyValuesList {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Data,
        [Parameter(Mandatory)]
        [ref]$Offset,
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$ParentPath,
        [Parameter(Mandatory)]
        [hashtable]$Index,
        [switch]$LittleEndian
    )

    while ($Offset.Value -lt $Data.Length) {
        $type = $Data[$Offset.Value]
        $Offset.Value++
        if ($type -eq 11) {
            return
        }

        $name = Read-BinaryKeyValuesCString -Data $Data -Offset $Offset
        $path = if ($ParentPath) { "$ParentPath/$name" } else { $name }

        switch ($type) {
            0 {
                $Index[$path] = $true
                Read-BinaryKeyValuesList -Data $Data -Offset $Offset -ParentPath $path -Index $Index -LittleEndian:$LittleEndian
            }
            1 { $Index[$path] = Read-BinaryKeyValuesCString -Data $Data -Offset $Offset }
            2 { $Index[$path] = Read-BinaryKeyValuesNumber -Data $Data -Offset $Offset -Size 4 -LittleEndian:$LittleEndian }
            3 { $Index[$path] = Read-BinaryKeyValuesNumber -Data $Data -Offset $Offset -Size 4 -LittleEndian:$LittleEndian }
            4 { $Index[$path] = Read-BinaryKeyValuesNumber -Data $Data -Offset $Offset -Size 4 -LittleEndian:$LittleEndian }
            6 { $Index[$path] = Read-BinaryKeyValuesNumber -Data $Data -Offset $Offset -Size 4 -LittleEndian:$LittleEndian }
            7 { $Index[$path] = Read-BinaryKeyValuesNumber -Data $Data -Offset $Offset -Size 8 -Unsigned -LittleEndian:$LittleEndian }
            8 {
                if ($Offset.Value -ge $Data.Length) {
                    throw "Binary KeyValues byte at offset $($Offset.Value) is truncated."
                }
                $Index[$path] = [int]$Data[$Offset.Value]
                $Offset.Value++
            }
            9 { $Index[$path] = [int]0 }
            10 { $Index[$path] = [int]1 }
            default { throw "Unsupported Binary KeyValues type $type at path '$path'." }
        }
    }

    throw "Binary KeyValues list '$ParentPath' is missing its end marker."
}

function ConvertFrom-BinaryKeyValuesHex {
    param(
        [Parameter(Mandatory)]
        [string]$Hex,
        [switch]$LittleEndian
    )

    $data = [Convert]::FromHexString($Hex)
    $offset = 0
    $index = @{}
    Read-BinaryKeyValuesList -Data $data -Offset ([ref]$offset) -ParentPath '' -Index $index -LittleEndian:$LittleEndian
    if ($offset -ne $data.Length) {
        throw "Binary KeyValues parser consumed $offset of $($data.Length) bytes."
    }

    return [pscustomobject]@{
        Data = $data
        Index = $index
        BytesConsumed = $offset
    }
}

function Assert-BinaryKeyValuesValue {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Index,
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$ExpectedValue,
        [Parameter(Mandatory)]
        [Type]$ExpectedType
    )

    if (-not $Index.ContainsKey($Path)) {
        throw "Binary KeyValues path '$Path' was not present."
    }

    $actual = $Index[$Path]
    if ($actual.GetType() -ne $ExpectedType) {
        throw "Binary KeyValues path '$Path' had type $($actual.GetType().FullName), expected $($ExpectedType.FullName)."
    }
    if ($actual -ne $ExpectedValue) {
        throw "Binary KeyValues path '$Path' was '$actual', expected '$ExpectedValue'."
    }
}
