param(
    [Parameter(Mandatory = $false)]
    [string]$Filter = '1510SP-1 PN',

    [Parameter(Mandatory = $false)]
    [int]$MaxResults = 30,

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

$inputText = @"
SEARCHHWCATALOG|$Filter|$MaxResults
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
