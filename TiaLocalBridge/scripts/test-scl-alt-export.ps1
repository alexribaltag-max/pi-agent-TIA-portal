param(
    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')),

    [Parameter(Mandatory = $false)]
    [string]$DeviceReference = '<project-name>/<device-name>',

    [Parameter(Mandatory = $false)]
    [string]$BlockReference = '<scl-block-reference>',

    [Parameter(Mandatory = $false)]
    [string]$ExportRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\exports-kinds')),

    [Parameter(Mandatory = $false)]
    [string]$BaseName = 'SCL_Block'
)

if ($DeviceReference -like '<*>' -or $BlockReference -like '<*>') {
    throw "Replace -DeviceReference and -BlockReference with real values."
}

$ErrorActionPreference = 'Stop'

function Read-LineWithTimeout($Reader, $TimeoutMs) {
    $task = $Reader.ReadLineAsync()
    if (-not $task.Wait($TimeoutMs)) { throw "timeout after $TimeoutMs ms" }
    $task.Result
}
function Read-UntilEvent($Process, $ExpectedEvent, $TimeoutMs = 30000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $line = Read-LineWithTimeout $Process.StandardOutput 30000
        Write-Host $line
        try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'event' -and $obj.event -eq $ExpectedEvent) { return } } catch {}
    }
    throw 'event timeout'
}
function Read-UntilResponse($Process, $TimeoutMs = 180000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $line = Read-LineWithTimeout $Process.StandardOutput 180000
        Write-Host $line
        try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'response' -or $obj.type -eq 'fatal') { return $obj } } catch {}
    }
    throw 'response timeout'
}
function Send-Command($Process, $Command, $TimeoutMs = 180000) {
    Write-Host "`n>>> $Command"
    $Process.StandardInput.WriteLine($Command)
    $Process.StandardInput.Flush()
    Read-UntilResponse $Process $TimeoutMs
}
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
    $docsDir = Join-Path $ExportRoot ($BaseName + '_docs')
    $null = Send-Command $p "GETPLCBLOCKINFO|$DeviceReference|$BlockReference"
    $null = Send-Command $p "EXPORTPLCBLOCKDOCS|$DeviceReference|$BlockReference|$docsDir|$BaseName"
    $null = Send-Command $p 'EXIT' 10000
}
finally {
    if (-not $p.HasExited) { try { $p.StandardInput.Close() } catch {}; $p.WaitForExit(5000) | Out-Null; if (-not $p.HasExited) { $p.Kill() } }
    $stderr = $p.StandardError.ReadToEnd(); if ($stderr) { Write-Host $stderr }
    $p.Dispose()
}
