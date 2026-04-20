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
$type = $asm.GetType('Siemens.Engineering.SW.Blocks.PlcBlock')
foreach ($m in $type.GetMethods([Reflection.BindingFlags]'Public,Instance,DeclaredOnly') | Where-Object { $_.Name -like 'Export*' }) {
    $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ', '
    Write-Output ("{0} {1}({2})" -f $m.ReturnType.FullName, $m.Name, $params)
}
