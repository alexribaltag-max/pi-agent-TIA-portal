param(
    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')),

    [Parameter(Mandatory = $false)]
    [string]$DeviceReference = '<project-name>/<device-name>',

    [Parameter(Mandatory = $false)]
    [string]$BlockReference = '<block-reference>'
)

if ($DeviceReference -like '<*>' -or $BlockReference -like '<*>') {
    throw "Replace -DeviceReference and -BlockReference with real values."
}

$ErrorActionPreference = 'Stop'

function Read-LineWithTimeout($Reader, $TimeoutMs) {
    $task = $Reader.ReadLineAsync()
    if (-not $task.Wait($TimeoutMs)) { throw 'timeout' }
    $task.Result
}

function Read-UntilEvent($Process, $ExpectedEvent, $TimeoutMs = 30000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $line = Read-LineWithTimeout $Process.StandardOutput 5000
        Write-Host $line
        try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'event' -and $obj.event -eq $ExpectedEvent) { return } } catch {}
    }
    throw 'event timeout'
}

function Read-UntilResponse($Process, $TimeoutMs = 60000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $line = Read-LineWithTimeout $Process.StandardOutput 5000
        Write-Host $line
        try { $obj = $line | ConvertFrom-Json; if ($obj.type -eq 'response' -or $obj.type -eq 'fatal') { return $obj } } catch {}
    }
    throw 'response timeout'
}

function Send-Command($Process, $Command) {
    Write-Host "`n>>> $Command"
    $Process.StandardInput.WriteLine($Command)
    $Process.StandardInput.Flush()
    Read-UntilResponse $Process
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
    Send-Command $p "GETPLCBLOCKGROUPS|$DeviceReference" | Out-Null
    Send-Command $p "GETPLCBLOCKINFO|$DeviceReference|$BlockReference" | Out-Null
    Send-Command $p 'EXIT' | Out-Null
}
finally {
    if (-not $p.HasExited) { try { $p.StandardInput.Close() } catch {}; $p.WaitForExit(5000) | Out-Null; if (-not $p.HasExited) { $p.Kill() } }
    $err = $p.StandardError.ReadToEnd(); if ($err) { Write-Host $err }
    $p.Dispose()
}
