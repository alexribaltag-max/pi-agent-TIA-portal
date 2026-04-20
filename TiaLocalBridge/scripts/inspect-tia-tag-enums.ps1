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
    'Siemens.Engineering.ExportOptions',
    'Siemens.Engineering.ImportOptions',
    'Siemens.Engineering.DocumentInfoOptions'
)
foreach ($typeName in $types) {
    $type = $asm.GetType($typeName)
    if ($null -eq $type) { Write-Output "TYPE NOT FOUND: $typeName"; continue }
    Write-Output "=== $($type.FullName) ==="
    [Enum]::GetNames($type) | ForEach-Object { Write-Output $_ }
    Write-Output ''
}
