param(
    [Parameter(Mandatory = $false)]
    [string[]]$Filters = @('1510SP-1 PN', 'ET200SP CPU 1510SP-1 PN', '1510SP'),

    [Parameter(Mandatory = $false)]
    [int]$MaxResults = 10,

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

$commands = New-Object System.Collections.Generic.List[string]
foreach ($filter in $Filters) {
    $commands.Add("SEARCHHWCATALOG|$filter|$MaxResults")
}
$commands.Add('EXIT')
$inputText = ($commands -join [Environment]::NewLine) + [Environment]::NewLine

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
