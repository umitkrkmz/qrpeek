# QRPeek - draws the README's QR code with QRPeek's own renderer
# (rounded pattern, rounded corner markers, QRPeek logo in the middle).
# Build first:  dotnet build -c Release
# Usage:        powershell -ExecutionPolicy Bypass -File tools/make-readme-qr.ps1

param(
    [string]$Url = "https://github.com/umitkrkmz/qrpeek",
    [string]$Out = (Join-Path $PSScriptRoot "..\docs\images\qr-github.png")
)
$ErrorActionPreference = "Stop"
$root = Join-Path $PSScriptRoot ".."
$exe = Join-Path $root "src\QRPeek\bin\Release\net48\QRPeek.exe"
if (-not (Test-Path $exe)) { throw "Build first: dotnet build -c Release" }

Add-Type -AssemblyName System.Drawing
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $exe))
[Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $exe) "QRCoder.dll")) | Out-Null
$flags = [Reflection.BindingFlags]"Public,NonPublic,Static,Instance"

$styleType = $asm.GetType("QRPeek.Core.QrStyle")
$style = [Activator]::CreateInstance($styleType, $true)
$styleType.GetProperty("DarkHex").SetValue($style, "#1E3A8A")
$styleType.GetProperty("Modules").SetValue($style, [Enum]::Parse($asm.GetType("QRPeek.Core.ModuleStyle"), "Rounded"))
$styleType.GetProperty("Eyes").SetValue($style, [Enum]::Parse($asm.GetType("QRPeek.Core.EyeStyle"), "Rounded"))
$logo = New-Object System.Drawing.Bitmap (Join-Path $root "src\QRPeek\Assets\QRPeek.png")
$styleType.GetProperty("Logo").SetValue($style, $logo)
$styleType.GetProperty("LogoScale").SetValue($style, 0.24)

$gen = $asm.GetType("QRPeek.Core.QrGenerator")
$ecc = [QRCoder.QRCodeGenerator+ECCLevel]::H
$png = $gen.GetMethod("Png", $flags).Invoke($null, @($Url, $ecc, $style, 600))
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
[IO.File]::WriteAllBytes($Out, $png)

# Read it back with QRPeek's scanner before trusting it.
$bmp = New-Object System.Drawing.Bitmap $Out
$args1 = [object[]]::new(1); $args1[0] = [System.Drawing.Bitmap]$bmp
$found = $asm.GetType("QRPeek.Core.QrScanner").GetMethod("Decode", $flags).Invoke($null, $args1)
$bmp.Dispose(); $logo.Dispose()
if (-not $found.Contains($Url)) { throw "Generated QR did not decode back to $Url" }
Write-Host "Wrote $Out (verified: decodes to $Url)"
