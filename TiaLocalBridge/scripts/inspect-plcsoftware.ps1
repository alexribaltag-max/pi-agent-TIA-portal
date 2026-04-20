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
$type = $asm.GetType('Siemens.Engineering.SW.PlcSoftware')
Write-Output "=== $($type.FullName) ==="
foreach ($m in $type.GetMethods([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Sort-Object Name) {
    $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
    Write-Output ("{0} {1}({2})" -f $m.ReturnType.Name, $m.Name, $params)
}
Write-Output ''
foreach ($p in $type.GetProperties([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Sort-Object Name) {
    Write-Output ("Property {0} : {1}" -f $p.Name, $p.PropertyType.FullName)
}
