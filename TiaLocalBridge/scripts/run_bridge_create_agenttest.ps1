param(
    [Parameter(Mandatory = $false)]
    [string]$TargetDirectory = '<target-project-directory>',

    [Parameter(Mandatory = $false)]
    [string]$ProjectName = '<project-name>',

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

if ($TargetDirectory -like '<*>' -or $ProjectName -like '<*>') {
    throw "Replace -TargetDirectory and -ProjectName with real values."
}

$inputText = @"
CREATE|$TargetDirectory|$ProjectName
LIST
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
