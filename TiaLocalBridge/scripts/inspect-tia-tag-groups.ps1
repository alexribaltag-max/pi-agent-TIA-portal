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
    'Siemens.Engineering.SW.Tags.PlcTagTableUserGroupComposition',
    'Siemens.Engineering.SW.Tags.PlcTagUserConstantComposition',
    'Siemens.Engineering.SW.Tags.PlcTagTable'
)
foreach ($typeName in $types) {
    $type = $asm.GetType($typeName)
    if ($null -eq $type) { Write-Output "TYPE NOT FOUND: $typeName"; Write-Output ''; continue }
    Write-Output "=== $($type.FullName) ==="
    foreach ($m in $type.GetMethods([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Sort-Object Name) {
        $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        Write-Output ("{0} {1}({2})" -f $m.ReturnType.Name, $m.Name, $params)
    }
    Write-Output ''
}
