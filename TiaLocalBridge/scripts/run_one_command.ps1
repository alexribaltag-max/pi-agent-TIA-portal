param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Command,

    [Parameter(Mandatory = $false)]
    [int]$TimeoutSeconds = 30,

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')),

    [Parameter(Mandatory = $false)]
    [string]$WorkingDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe')))
)

if (-not (Test-Path $BridgePath)) {
    throw "Bridge executable not found: $BridgePath"
}

if ($TimeoutSeconds -le 0) {
    throw "TimeoutSeconds must be greater than zero."
}

$inputText = "$Command`r`nEXIT`r`n"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $BridgePath
$psi.WorkingDirectory = $WorkingDirectory
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

$stdoutTask = $p.StandardOutput.ReadToEndAsync()
$stderrTask = $p.StandardError.ReadToEndAsync()

if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
    try {
        $p.Kill()
    }
    catch {
    }

    throw "Bridge command timed out after $TimeoutSeconds second(s): $Command"
}

$p.WaitForExit()
$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()

if ($stdout) {
    Write-Output $stdout
}

if ($stderr) {
    Write-Error $stderr
}

exit $p.ExitCode
