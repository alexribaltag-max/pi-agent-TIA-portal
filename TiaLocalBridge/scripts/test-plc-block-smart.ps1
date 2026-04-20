param(
    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')),

    [Parameter(Mandatory = $false)]
    [string]$DeviceReference = '<project-name>/<device-name>',

    [Parameter(Mandatory = $false)]
    [string]$ExportRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\exports-smart')),

    [Parameter(Mandatory = $false)]
    [string[]]$BlockReferences = @('<block-reference-1>', '<block-reference-2>', '<block-reference-3>', '<block-reference-4>')
)

if ($DeviceReference -like '<*>' -or ($BlockReferences | Where-Object { $_ -like '<*>' })) {
    throw "Replace -DeviceReference and -BlockReferences with real values."
}

$ErrorActionPreference = 'Stop'

if (Test-Path $ExportRoot) {
    Remove-Item $ExportRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $ExportRoot | Out-Null

$commands = @()
foreach ($blockReference in $BlockReferences) {
    $commands += "EXPORTPLCBLOCKSMART|$DeviceReference|$blockReference|$ExportRoot"
}
$commands += 'HELP'
$commands += 'EXIT'

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

    foreach ($command in $commands) {
        Write-Host "`n>>> $command"
        $p.StandardInput.WriteLine($command)
        $p.StandardInput.Flush()
        $null = Read-UntilResponse $p 180000
    }
}
finally {
    if (-not $p.HasExited) {
        try { $p.StandardInput.Close() } catch {}
        $p.WaitForExit(5000) | Out-Null
        if (-not $p.HasExited) { $p.Kill() }
    }

    $stderr = $p.StandardError.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Write-Host ""
        Write-Host 'STDERR:'
        Write-Host $stderr
    }

    $p.Dispose()
}
