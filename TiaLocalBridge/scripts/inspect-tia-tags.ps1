param(
    [Parameter(Mandatory = $false)]
    [string]$SiemensEngineeringDllPath = $env:SIEMENS_ENGINEERING_DLL_PATH
)

if ([string]::IsNullOrWhiteSpace($SiemensEngineeringDllPath)) {
    $SiemensEngineeringDllPath = '<path-to-Siemens.Engineering.dll>'
}

if ($SiemensEngineeringDllPath -like '<*>' -or -not (Test-Path $SiemensEngineeringDllPath)) {
    throw "Set -SiemensEngineeringDllPath or SIEMENS_ENGINEERING_DLL_PATH to a valid Siemens.Engineering.dll path."
}

$asm = [Reflection.Assembly]::LoadFrom($SiemensEngineeringDllPath)
$types = @(
    'Siemens.Engineering.SW.Tags.PlcTag',
    'Siemens.Engineering.SW.Tags.PlcTagTable',
    'Siemens.Engineering.SW.Tags.PlcTagComposition',
    'Siemens.Engineering.SW.Tags.PlcTagTableComposition',
    'Siemens.Engineering.SW.Tags.PlcTagTableGroup',
    'Siemens.Engineering.SW.Tags.PlcTagTableGroupComposition'
)

foreach ($typeName in $types) {
    $type = $asm.GetType($typeName)
    if ($null -eq $type) {
        Write-Output "TYPE NOT FOUND: $typeName"
        Write-Output ''
        continue
    }

    Write-Output "=== $($type.FullName) ==="
    $type.GetMembers([Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly') |
        Sort-Object MemberType, Name |
        ForEach-Object { "{0} {1}" -f $_.MemberType, $_.Name }
    Write-Output ''
}
