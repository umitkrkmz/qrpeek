# Installs the test-signed QRPeek package like a Store app and runs the Windows
# App Certification Kit (WACK) on it. Needs an elevated (Administrator) PowerShell.
#
# Before (normal PowerShell):
#   powershell -ExecutionPolicy Bypass -File packaging\msix\build_msix.ps1
#   powershell -ExecutionPolicy Bypass -File packaging\msix\sign_test.ps1
# Then (Administrator PowerShell):
#   powershell -ExecutionPolicy Bypass -File packaging\msix\run-wack.ps1
# When done testing, remove the test package and certificate again:
#   powershell -ExecutionPolicy Bypass -File packaging\msix\run-wack.ps1 -Cleanup
#
# Why not test the Developer Mode registration (Add-AppxPackage -Register of
# the layout folder)? WACK can't find such a package's root path ("The
# specified Windows Store App was not found") and every test fails. A signed,
# normally installed package is what the Store delivers, so it's also the
# more faithful test.
#
# Writes build\msix\wack-report.xml. Takes roughly 10-20 minutes; WACK opens and
# closes QRPeek itself several times, so don't use the PC heavily meanwhile.

param([switch]$Cleanup)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root 'build\msix'
$signed = Join-Path $out 'QRPeek-test-signed.msix'
$cer = Join-Path $out 'qrpeek-test.cer'
$report = Join-Path $out 'wack-report.xml'
$appcert = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run this from an elevated (Administrator) PowerShell.' }

$publisher = ([xml](Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml'))).Package.Identity.Publisher
Get-Process QRPeek -ErrorAction SilentlyContinue | Stop-Process -Force

if ($Cleanup) {
    Get-AppxPackage -Name 'umitkrkmz.QRPeek' | Remove-AppxPackage
    Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object { $_.Subject -eq $publisher } | Remove-Item
    Write-Host 'Removed the QRPeek test package and the test certificate from Trusted People.'
    Write-Host "(The signing certificate itself stays in CurrentUser\My for the next test; delete it in certmgr.msc if you like.)"
    Read-Host 'Kapatmak icin Enter / Press Enter to close'
    return
}

if (-not (Test-Path $signed) -or -not (Test-Path $cer)) { throw 'Run sign_test.ps1 first (normal PowerShell).' }
if (-not (Test-Path $appcert)) { throw 'appcert.exe (Windows App Certification Kit) not found' }

# 1) Trust the test certificate on this PC (only for installing the test package).
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

# 2) Replace any earlier copy (e.g. a Developer Mode registration) with the signed package.
Get-AppxPackage -Name 'umitkrkmz.QRPeek' | Remove-AppxPackage
Add-AppxPackage -Path $signed
$pkg = Get-AppxPackage -Name 'umitkrkmz.QRPeek' | Select-Object -First 1
Write-Host "Installed: $($pkg.PackageFullName)"
Write-Host "Location:  $($pkg.InstallLocation)"

# 3) WACK
if (Test-Path $report) { Remove-Item $report -Force }
& $appcert reset
& $appcert test -packagefullname $pkg.PackageFullName -reportoutputpath $report

$result = ([xml](Get-Content $report)).REPORT.OVERALL_RESULT
Write-Host "`nOverall result: $result"
Write-Host "Report: $report"
Write-Host 'QRPeek stays installed for manual testing; remove it later with -Cleanup.'
Read-Host 'Kapatmak icin Enter / Press Enter to close'
