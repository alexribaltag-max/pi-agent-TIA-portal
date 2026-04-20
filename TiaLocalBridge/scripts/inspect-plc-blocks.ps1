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
    'Siemens.Engineering.SW.Blocks.PlcBlock',
    'Siemens.Engineering.SW.Blocks.PlcBlockComposition',
    'Siemens.Engineering.SW.Blocks.PlcBlockSystemGroup',
    'Siemens.Engineering.SW.Blocks.PlcBlockUserGroup',
    'Siemens.Engineering.SW.Blocks.PlcBlockUserGroupComposition',
    'Siemens.Engineering.SW.Blocks.CodeBlock',
    'Siemens.Engineering.SW.Blocks.DataBlock',
    'Siemens.Engineering.SW.Blocks.InstanceDB',
    'Siemens.Engineering.SW.Blocks.FB',
    'Siemens.Engineering.SW.Blocks.FC',
    'Siemens.Engineering.SW.Blocks.OB'
)
foreach ($typeName in $types) {
    $type = $asm.GetType($typeName)
    if ($null -eq $type) {
        Write-Output "TYPE NOT FOUND: $typeName"
        Write-Output ''
        continue
    }

    Write-Output "=== $($type.FullName) ==="
    Write-Output ("BaseType: {0}" -f ($type.BaseType.FullName))

    foreach ($p in $type.GetProperties([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Sort-Object Name) {
        Write-Output ("Property {0} : {1}" -f $p.Name, $p.PropertyType.FullName)
    }

    foreach ($m in $type.GetMethods([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Sort-Object Name) {
        $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        Write-Output ("{0} {1}({2})" -f $m.ReturnType.Name, $m.Name, $params)
    }

    Write-Output ''
}
