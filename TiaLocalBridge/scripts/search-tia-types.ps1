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
$patterns = @('DocumentExportResult','DocumentImportResultForBlocks','SWImportOptions','ImportDocumentOptions')
foreach ($pattern in $patterns) {
    Write-Output "=== $pattern ==="
    $asm.GetTypes() | Where-Object { $_.FullName -like "*$pattern*" } | ForEach-Object { $_.FullName }
    Write-Output ''
}
