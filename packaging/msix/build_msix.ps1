# Builds the Microsoft Store package: build\msix\QRPeek.msix.
#
#   powershell -ExecutionPolicy Bypass -File packaging\msix\build_msix.ps1
#
# Steps: Release build -> copy the app into build\msix\layout -> manifest with
# the version from QRPeek.csproj (+ ".0", the Store's 4th part) -> logos cut
# from the app's .ico -> resources.pri -> makeappx pack.
# Everything under build\ is git-ignored. Needs the Windows SDK (makeappx,
# makepri) and the .NET SDK.
#
# Local try without uploading (Developer Mode on):
#   Add-AppxPackage -Register build\msix\layout\AppxManifest.xml
# (an unsigned .msix itself can't be installed; the Store signs the upload).

param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root 'build\msix'
$layout = Join-Path $out 'layout'
$bin = Join-Path $root 'src\QRPeek\bin\Release\net48'

$sdk = 'C:\Program Files (x86)\Windows Kits\10\bin'
$makeappx = Get-ChildItem "$sdk\*\x64\makeappx.exe" | Sort-Object FullName | Select-Object -Last 1
$makepri = Get-ChildItem "$sdk\*\x64\makepri.exe" | Sort-Object FullName | Select-Object -Last 1
if (-not $makeappx -or -not $makepri) { throw 'makeappx.exe / makepri.exe (Windows SDK) not found' }

# 1) Release build
if (-not $NoBuild) {
    & dotnet build (Join-Path $root 'src\QRPeek\QRPeek.csproj') -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
}

# 2) Version: QRPeek.csproj <Version> (3 parts) + ".0"
$csproj = [xml](Get-Content (Join-Path $root 'src\QRPeek\QRPeek.csproj'))
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "unexpected <Version> '$version' in QRPeek.csproj (expected X.Y.Z)" }
$msixVersion = "$version.0"

# 3) Layout: only what the app needs at runtime
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null
Copy-Item (Join-Path $bin 'QRPeek.exe'), (Join-Path $bin 'QRPeek.exe.config'), (Join-Path $bin '*.dll'),
          (Join-Path $bin 'LICENSE.txt'), (Join-Path $bin 'THIRD-PARTY-NOTICES.txt') $layout

$manifest = [xml](Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml'))
$manifest.Package.Identity.Version = $msixVersion
$manifest.Save((Join-Path $layout 'AppxManifest.xml'))

# 4) Logos from the app's .ico. Every frame in QRPeek.ico is PNG-compressed,
# which System.Drawing.Icon can't decode properly, so the frames are cut out
# of the file by hand and read as plain PNGs (same approach as MediaGrab).
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $layout 'Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
$ico = [IO.File]::ReadAllBytes((Join-Path $root 'src\QRPeek\Assets\QRPeek.ico'))
$frames = @{}
for ($i = 0; $i -lt [BitConverter]::ToUInt16($ico, 4); $i++) {
    $entry = 6 + 16 * $i
    $size = if ($ico[$entry] -eq 0) { 256 } else { [int]$ico[$entry] }
    $len = [BitConverter]::ToUInt32($ico, $entry + 8)
    $offset = [BitConverter]::ToUInt32($ico, $entry + 12)
    $frames[$size] = [System.Drawing.Image]::FromStream((New-Object IO.MemoryStream(, $ico[$offset..($offset + $len - 1)])))
}
if (-not $frames[256]) { throw 'QRPeek.ico has no 256px frame' }

function Save-Logo([string]$name, [int]$w, [int]$h) {
    # The .ico's own frame when it has one at this exact size (drawn for small
    # sizes), else the 256px frame scaled, centered in a w x h canvas.
    $side = [Math]::Min($w, $h)
    $src = if ($frames[$side]) { $frames[$side] } else { $frames[256] }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, [int](($w - $side) / 2), [int](($h - $side) / 2), $side, $side)
    $bmp.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
Save-Logo 'Square150x150Logo.png' 150 150
Save-Logo 'Square44x44Logo.png' 44 44
Save-Logo 'StoreLogo.png' 50 50
Save-Logo 'Wide310x150Logo.png' 310 150
# Start menu, taskbar and Settings ask for exact pixel sizes; "unplated" =
# drawn as-is, without a tile square behind it. Windows only finds these
# variants through resources.pri (built below).
foreach ($t in 16, 24, 32, 48, 256) {
    Save-Logo "Square44x44Logo.targetsize-$t.png" $t $t
    Save-Logo "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t $t
    Save-Logo "Square44x44Logo.targetsize-${t}_altform-lightunplated.png" $t $t
}
foreach ($f in $frames.Values) { $f.Dispose() }

# 5) resources.pri: the index of those logo variants (manifest + Assets only)
$priRoot = Join-Path $out 'pri'
if (Test-Path $priRoot) { Remove-Item $priRoot -Recurse -Force }
New-Item -ItemType Directory -Force $priRoot | Out-Null
Copy-Item (Join-Path $layout 'AppxManifest.xml') $priRoot
Copy-Item $assets $priRoot -Recurse
$priConfig = Join-Path $out 'priconfig.xml'
& $makepri.FullName createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed ($LASTEXITCODE)" }
& $makepri.FullName new /pr $priRoot /cf $priConfig /mn (Join-Path $priRoot 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri new failed ($LASTEXITCODE)" }

# 6) Pack
$msix = Join-Path $out 'QRPeek.msix'
& $makeappx.FullName pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

$kb = [int]((Get-Item $msix).Length / 1KB)
Write-Host "Built $msix  (version $msixVersion, $kb KB)"
