# QRPeek - draws the app icon (no design tools needed) and writes:
#   src/QRPeek/Assets/QRPeek.png  (256 px, used in the UI)
#   src/QRPeek/Assets/QRPeek.ico  (16..256 px, exe + tray)
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1

param([string]$OutDir = (Join-Path $PSScriptRoot "..\src\QRPeek\Assets"))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$size
    $u = $s / 16.0   # design grid: 16 x 16 units

    # Background tile
    $bg = New-RoundedRect (0.5 * $u) (0.5 * $u) (15 * $u) (15 * $u) (3.4 * $u)
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 0x3B, 0x82, 0xF6)), ([System.Drawing.Color]::FromArgb(255, 0x1D, 0x3F, 0xB8)), 45.0
    $g.FillPath($grad, $bg)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $tile = [System.Drawing.Color]::FromArgb(255, 0x2B, 0x5F, 0xD9)
    $tileBrush = New-Object System.Drawing.SolidBrush $tile

    # Three QR finder patterns: white square, tile-colored ring, white center
    foreach ($pos in @(@(3, 3), @(9, 3), @(3, 9))) {
        $x = $pos[0] * $u; $y = $pos[1] * $u
        $outer = New-RoundedRect $x $y (4 * $u) (4 * $u) (0.9 * $u)
        $g.FillPath($white, $outer)
        $ring = New-RoundedRect ($x + 0.8 * $u) ($y + 0.8 * $u) (2.4 * $u) (2.4 * $u) (0.5 * $u)
        $g.FillPath($tileBrush, $ring)
        $center = New-RoundedRect ($x + 1.4 * $u) ($y + 1.4 * $u) (1.2 * $u) (1.2 * $u) (0.3 * $u)
        $g.FillPath($white, $center)
    }

    # The "peek": a magnifier lens over the fourth corner
    $penW = [single]([Math]::Max(1.2, 1.1 * $u))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penW
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawEllipse($pen, (9.2 * $u), (9.2 * $u), (3.6 * $u), (3.6 * $u))
    $g.DrawLine($pen, (12.3 * $u), (12.3 * $u), (13.6 * $u), (13.6 * $u))
    if ($size -ge 32) {
        $dot = New-RoundedRect (10.4 * $u) (10.4 * $u) (1.2 * $u) (1.2 * $u) (0.3 * $u)
        $g.FillPath($white, $dot)
    }

    $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

# PNG for the UI
$big = Draw-Icon 256
$big.Save((Join-Path $OutDir "QRPeek.png"), [System.Drawing.Imaging.ImageFormat]::Png)

# Multi-size ICO with PNG-compressed entries (supported since Windows Vista)
$sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
$images = @()
foreach ($sz in $sizes) { $images += , (Get-PngBytes (Draw-Icon $sz)) }

$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $len = $images[$i].Length
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$len); $w.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $OutDir "QRPeek.ico"), $ico.ToArray())
Write-Host "Wrote QRPeek.png and QRPeek.ico to $OutDir"
