param(
    [Parameter(Mandatory = $false)]
    [string]$TagFilePath = '<path-to-tag-list.txt>',

    [Parameter(Mandatory = $false)]
    [string]$DeviceReference = '<project-name>/<device-name>',

    [Parameter(Mandatory = $false)]
    [string]$TableReference = '<tag-table-reference>',

    [Parameter(Mandatory = $false)]
    [string]$BridgePath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Debug\TiaLocalBridge.exe'))
)

if ($TagFilePath -like '<*>' -or -not (Test-Path $TagFilePath)) {
    throw "Replace -TagFilePath with a valid tag list file path."
}

if ($DeviceReference -like '<*>' -or $TableReference -like '<*>') {
    throw "Replace -DeviceReference and -TableReference with real PLC target values."
}

$lines = Get-Content -Path $TagFilePath
$commands = New-Object System.Collections.Generic.List[string]
$commands.Add("GETPLCTAGTABLES|$DeviceReference")

foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "`t"
    if ($parts.Count -lt 3) {
        continue
    }

    $name = $parts[0].Trim()
    $type = $parts[1].Trim()
    $address = $parts[2].Trim()

    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($type) -or [string]::IsNullOrWhiteSpace($address)) {
        continue
    }

    $commands.Add("ADDPLCTAG|$DeviceReference|$TableReference|$name|$type|$address")
}

$commands.Add("GETPLCTAGS|$DeviceReference")
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
