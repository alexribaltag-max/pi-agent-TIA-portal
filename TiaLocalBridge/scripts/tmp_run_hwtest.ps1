param(
    [Parameter(Mandatory = $false)]
    [string]$TargetDirectory = '<target-project-directory>',

    [Parameter(Mandatory = $false)]
    [string]$ProjectName = '<project-name>',

    [Parameter(Mandatory = $false)]
    [string]$TypeIdentifier = '<hardware-type-identifier>',

    [Parameter(Mandatory = $false)]
    [string]$DeviceName = '<device-name>',

    [Parameter(Mandatory = $false)]
    [string]$DeviceItemName = '<device-item-name>',

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

if ($TargetDirectory -like '<*>' -or $ProjectName -like '<*>' -or $TypeIdentifier -like '<*>' -or $DeviceName -like '<*>' -or $DeviceItemName -like '<*>') {
    throw "Replace the placeholder parameters before running this hardware test example."
}

$inputText = @"
CREATE|$TargetDirectory|$ProjectName
ADDDEVICE|$ProjectName|$TypeIdentifier|$DeviceName|$DeviceItemName
GETDEVICES|$ProjectName
GETDEVICEITEMS|$ProjectName/$DeviceName
EXIT
"@

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $BridgePath
$psi.WorkingDirectory = [System.IO.Path]::GetDirectoryName($BridgePath)
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$p = New-Object System.Diagnostics.Process
$p.StartInfo = $psi
$null = $p.Start()
$p.StandardInput.Write($inputText)
$p.StandardInput.Close()
$stdout = $p.StandardOutput.ReadToEnd()
$stderr = $p.StandardError.ReadToEnd()
$p.WaitForExit()

Write-Output $stdout
if ($stderr) {
  Write-Error $stderr
}
exit $p.ExitCode
