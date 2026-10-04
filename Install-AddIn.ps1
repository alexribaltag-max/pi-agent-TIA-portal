if (-Not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Restarting script with Administrator privileges..."
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    Exit
}

$ErrorActionPreference = "Stop"

$addinPath = Join-Path $PSScriptRoot "TiaPiAddin\bin\Debug\net48\TiaPiAddin.addin"
$tiaPortalPath = "C:\Program Files\Siemens\Automation\Portal V21\AddIns"

if (-Not (Test-Path $addinPath)) {
    Write-Host "TiaPiAddin.addin not found in $addinPath. Please build the add-in first."
    Read-Host "Press Enter to exit..."
    Exit 1
}

if (-Not (Test-Path $tiaPortalPath)) {
    Write-Host "Creating AddIns directory at $tiaPortalPath..."
    New-Item -ItemType Directory -Force -Path $tiaPortalPath
}

Write-Host "Copying TiaPiAddin.addin to $tiaPortalPath..."
Copy-Item -Path $addinPath -Destination $tiaPortalPath -Force

Write-Host "Successfully installed the TIA Portal add-in. Please restart TIA Portal."
Read-Host "Press Enter to exit..."
