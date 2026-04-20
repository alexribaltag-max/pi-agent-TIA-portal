param(
    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')),

    [Parameter(Mandatory = $false)]
    [string]$ProjectPath = '<path-to-project.apXX>',

    [Parameter(Mandatory = $false)]
    [string]$ProjectName = '<project-name>'
)

if ($ProjectPath -like '<*>' -or $ProjectName -like '<*>') {
    throw "Replace -ProjectPath and -ProjectName with real project values."
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
    $lines = @()

    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = [Math]::Max(1000, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        $line = Read-LineWithTimeout -Reader $Process.StandardOutput -TimeoutMs $remaining
        if ($null -eq $line) {
            throw 'Process ended unexpectedly while waiting for startup event.'
        }

        $lines += $line
        Write-Host $line

        try {
            $obj = $line | ConvertFrom-Json
            if ($obj.type -eq 'event' -and $obj.event -eq $ExpectedEvent) {
                return
            }
        }
        catch {
        }
    }

    throw "Did not receive event '$ExpectedEvent' within timeout."
}

function Read-UntilTerminalResponse {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutMs = 600000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    $lines = @()

    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = [Math]::Max(1000, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        $line = Read-LineWithTimeout -Reader $Process.StandardOutput -TimeoutMs $remaining
        if ($null -eq $line) {
            throw 'Process ended unexpectedly while waiting for command response.'
        }

        $lines += $line
        Write-Host $line

        try {
            $obj = $line | ConvertFrom-Json
            if ($obj.type -eq 'response' -or $obj.type -eq 'fatal') {
                return $obj
            }
        }
        catch {
        }
    }

    throw 'Timed out while waiting for command response.'
}

function Send-Command {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Command,
        [int]$TimeoutMs = 600000
    )

    Write-Host ""
    Write-Host ">>> $Command"
    $Process.StandardInput.WriteLine($Command)
    $Process.StandardInput.Flush()
    return Read-UntilTerminalResponse -Process $Process -TimeoutMs $TimeoutMs
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

    $openResponse = Send-Command -Process $process -Command "OPEN|$ProjectPath" -TimeoutMs 600000
    $listResponse = Send-Command -Process $process -Command 'LIST' -TimeoutMs 60000
    $devicesResponse = Send-Command -Process $process -Command "GETDEVICES|$ProjectName" -TimeoutMs 60000

    $deviceReferences = @()
    if ($devicesResponse.status -eq 'success') {
        $devicesText = [string]$devicesResponse.result
        foreach ($match in [regex]::Matches($devicesText, '\[Reference=([^\],]+(?:/[^\],]+)*),\s*Type=')) {
            $reference = $match.Groups[1].Value.Trim()
            if (-not [string]::IsNullOrWhiteSpace($reference)) {
                $deviceReferences += $reference
            }
        }
    }

    $deviceReferences = $deviceReferences | Select-Object -Unique

    if ($deviceReferences.Count -gt 0) {
        $firstDeviceReference = $deviceReferences[0]
        $null = Send-Command -Process $process -Command "GETDEVICESJSON|$firstDeviceReference" -TimeoutMs 60000
        $null = Send-Command -Process $process -Command "GETDEVICEITEMS|$firstDeviceReference" -TimeoutMs 60000
    }

    $plcDeviceReference = $null
    $hmiDeviceReference = $null

    foreach ($deviceReference in $deviceReferences) {
        $deviceJsonResponse = Send-Command -Process $process -Command "GETDEVICESJSON|$deviceReference" -TimeoutMs 60000
        if ($deviceJsonResponse.status -ne 'success') {
            continue
        }

        $result = $deviceJsonResponse.result
        if (-not $plcDeviceReference -and $result.device.hasPlcSoftware) {
            $plcDeviceReference = $deviceReference
        }

        if (-not $hmiDeviceReference -and $result.device.hasHmiSoftware) {
            $hmiDeviceReference = $deviceReference
        }

        if ($plcDeviceReference -and $hmiDeviceReference) {
            break
        }
    }

    if ($plcDeviceReference) {
        $null = Send-Command -Process $process -Command "GETPLCTAGS|$plcDeviceReference" -TimeoutMs 60000
    }

    if ($hmiDeviceReference) {
        $null = Send-Command -Process $process -Command "GETHMITAGS|$hmiDeviceReference" -TimeoutMs 60000
    }

    $null = Send-Command -Process $process -Command 'EXIT' -TimeoutMs 10000
}
finally {
    if (-not $process.HasExited) {
        try {
            $process.StandardInput.Close()
        }
        catch {
        }

        $process.WaitForExit(5000) | Out-Null
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }

    $stderr = $process.StandardError.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Write-Host ""
        Write-Host 'STDERR:'
        Write-Host $stderr
    }

    $process.Dispose()
}
