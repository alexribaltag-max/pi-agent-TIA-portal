param(
    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

$ErrorActionPreference = 'Stop'

function Read-LineWithTimeout($Reader, $TimeoutMs) { $task = $Reader.ReadLineAsync(); if (-not $task.Wait($TimeoutMs)) { throw 'timeout' }; $task.Result }
function Read-UntilEvent($Process, $ExpectedEvent) { while ($true) { $line = Read-LineWithTimeout $Process.StandardOutput 30000; Write-Host $line; try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'event' -and $obj.event -eq $ExpectedEvent) { return } } catch {} } }
function Read-UntilResponse($Process) { while ($true) { $line = Read-LineWithTimeout $Process.StandardOutput 30000; Write-Host $line; try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'response' -or $obj.type -eq 'fatal') { return $obj } } catch {} } }
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
try {
  Read-UntilEvent $p 'READY'
  foreach ($cmd in @('HELP','EXIT')) {
    Write-Host "`n>>> $cmd"
    $p.StandardInput.WriteLine($cmd)
    $p.StandardInput.Flush()
    $null = Read-UntilResponse $p
  }
}
finally {
  if (-not $p.HasExited) { try { $p.StandardInput.Close() } catch {}; $p.WaitForExit(5000) | Out-Null; if (-not $p.HasExited) { $p.Kill() } }
  $stderr = $p.StandardError.ReadToEnd(); if ($stderr) { Write-Host $stderr }
  $p.Dispose()
}
