# Signs a COPY of build\msix\QRPeek.msix with a local test certificate, so the
# package can be installed like a Store app (into C:\Program Files\WindowsApps)
# and checked with the Windows App Certification Kit.
#
#   powershell -ExecutionPolicy Bypass -File packaging\msix\sign_test.ps1
#
# Output (all git-ignored, under build\msix):
#   QRPeek-test-signed.msix   the signed copy, for local testing only
#   qrpeek-test.cer           the certificate's public part (run-wack.ps1 trusts it)
#
# The Store gets the UNSIGNED build\msix\QRPeek.msix: Microsoft signs it with
# the Store's own certificate. This test certificate never leaves this PC.
# Its subject must equal <Identity Publisher> in AppxManifest.xml, otherwise
# signing fails (the package publisher has to match the signer).
# No administrator rights needed: the certificate lives in CurrentUser\My.

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root 'build\msix'
$msix = Join-Path $out 'QRPeek.msix'
$signed = Join-Path $out 'QRPeek-test-signed.msix'
$cer = Join-Path $out 'qrpeek-test.cer'
if (-not (Test-Path $msix)) { throw 'build\msix\QRPeek.msix not found; run build_msix.ps1 first.' }

$publisher = ([xml](Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml'))).Package.Identity.Publisher
$friendly = 'QRPeek local test signing'

# Reuse the certificate from an earlier run if it's still valid.
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $publisher -and $_.FriendlyName -eq $friendly -and $_.NotAfter -gt (Get-Date).AddDays(7) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    # Code signing EKU (1.3.6.1.5.5.7.3.3) and "not a CA" basic constraints:
    # what MSIX signing requires of a certificate.
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
        -FriendlyName $friendly -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(1) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    Write-Host "Created test certificate $($cert.Thumbprint)"
}
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

$signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' | Sort-Object FullName | Select-Object -Last 1
if (-not $signtool) { throw 'signtool.exe (Windows SDK) not found' }
Copy-Item $msix $signed -Force
& $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint /s My $signed | Out-Null
if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }

Write-Host "Signed: $signed"
Write-Host "Certificate: $cer  ($publisher, valid until $($cert.NotAfter.ToString('yyyy-MM-dd')))"
Write-Host 'Next, from an elevated PowerShell: packaging\msix\run-wack.ps1'
