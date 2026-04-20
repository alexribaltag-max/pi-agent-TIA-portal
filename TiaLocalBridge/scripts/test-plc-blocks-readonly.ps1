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

function Read-LineWithTimeout {
    param(
        [System.IO.StreamReader]$Reader,
        [int]$TimeoutMs
    )

    $task = $Reader.ReadLineAsync()
    if (-not $task.Wait($TimeoutMs)) {
        throw "Timed out after $TimeoutMs ms while waiting for process output."
    }

    return $task.Result
}

function Read-UntilEvent {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$ExpectedEvent,
        [int]$TimeoutMs = 30000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = [Math]::Max(1000, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        $line = Read-LineWithTimeout -Reader $Process.StandardOutput -TimeoutMs $remaining
        if ($null -eq $line) { throw 'Process ended unexpectedly while waiting for startup event.' }
        Write-Host $line
        try {
            $obj = $line | ConvertFrom-Json
            if ($obj.type -eq 'event' -and $obj.event -eq $ExpectedEvent) { return }
        }
        catch {}
    }

    throw "Did not receive event '$ExpectedEvent' within timeout."
}

function Read-UntilResponse {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutMs = 120000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = [Math]::Max(1000, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        $line = Read-LineWithTimeout -Reader $Process.StandardOutput -TimeoutMs $remaining
        if ($null -eq $line) { throw 'Process ended unexpectedly while waiting for response.' }
        Write-Host $line
        try {
            $obj = $line | ConvertFrom-Json
            if ($obj.type -eq 'response' -or $obj.type -eq 'fatal') { return $obj }
        }
        catch {}
    }

    throw 'Timed out while waiting for response.'
}

function Send-Command {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Command,
        [int]$TimeoutMs = 120000
    )

    Write-Host ""
    Write-Host ">>> $Command"
    $Process.StandardInput.WriteLine($Command)
    $Process.StandardInput.Flush()
    return Read-UntilResponse -Process $Process -TimeoutMs $TimeoutMs
}

$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $BridgePath
$startInfo.WorkingDirectory = [System.IO.Path]::GetDirectoryName($BridgePath)
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo
$null = $process.Start()

try {
    Read-UntilEvent -Process $process -ExpectedEvent 'READY' -TimeoutMs 30000
    $null = Send-Command -Process $process -Command 'LIST' -TimeoutMs 60000
    $null = Send-Command -Process $process -Command "GETPLCBLOCKGROUPS|$DeviceReference" -TimeoutMs 60000
    $null = Send-Command -Process $process -Command "GETPLCBLOCKS|$DeviceReference" -TimeoutMs 60000
    $null = Send-Command -Process $process -Command "GETPLCBLOCKINFO|$DeviceReference|$BlockReference" -TimeoutMs 60000
    $null = Send-Command -Process $process -Command 'HELP' -TimeoutMs 60000
    $null = Send-Command -Process $process -Command 'EXIT' -TimeoutMs 10000
}
finally {
    if (-not $process.HasExited) {
        try { $process.StandardInput.Close() } catch {}
        $process.WaitForExit(5000) | Out-Null
        if (-not $process.HasExited) { $process.Kill() }
    }

    $stderr = $process.StandardError.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Write-Host ""
        Write-Host 'STDERR:'
        Write-Host $stderr
    }

    $process.Dispose()
}
