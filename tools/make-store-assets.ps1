# QRPeek - Microsoft Store images, written to build\store-assets (git-ignored):
#   shots\<lang>-1..6.png   1920x1080 screenshots (tr, en)
#   box-1080x1080.png       1:1 box art
#   poster-720x1080.png     2:3 poster art
#   tile-300x300.png ...    Store display tiles (300, 150, 71)
#
# Input: window captures in build\store-assets\captures\<lang>\
#   main.png result.png wifi.png invoice.png generator.png send.png
#   receive-qr.png receive-offer.png (0.2: waiting for a device, then the approval card)
# taken from QRPeek with demo data only (no personal data, no other brands).
# The windows are placed 1:1 (never scaled) so their text stays sharp.
#
# Usage: powershell -ExecutionPolicy Bypass -File tools\make-store-assets.ps1

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$assets = Join-Path $root 'build\store-assets'
$captures = Join-Path $assets 'captures'
$shots = Join-Path $assets 'shots'
New-Item -ItemType Directory -Force $shots | Out-Null
Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadFrom((Join-Path $root 'src\QRPeek\bin\Release\net48\QRCoder.dll')) | Out-Null

# The logo drawing functions from make-icon.ps1, without running that script
# (it would rewrite the app's icon files).
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'make-icon.ps1'), [ref]$null, [ref]$null)
foreach ($f in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    if ($f.Name -in 'New-RoundedRect', 'Draw-Icon') { Invoke-Expression $f.Extent.Text }
}

function Color([string]$hex, [int]$a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAliasGridFit'; $g.PixelOffsetMode = 'HighQuality'
    $rect = New-Object System.Drawing.RectangleF 0, 0, $w, $h
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (Color '#0B1222'), (Color '#1D3FB8'), 35.0
    $g.FillRectangle($bg, $rect)
    # soft glow behind the content
    $glow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glow.AddEllipse([single]($w * 0.35), [single]($h * 0.25), [single]($w * 0.8), [single]($h * 1.0))
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush $glow
    $pgb.CenterColor = Color '#3B82F6' 90; $pgb.SurroundColors = @((Color '#3B82F6' 0))
    $g.FillPath($pgb, $glow)
    return @{ Bmp = $bmp; G = $g }
}

function Draw-Shadowed($g, [System.Drawing.Image]$img, [int]$x, [int]$y) {
    for ($i = 18; $i -ge 1; $i -= 3) {
        $p = New-RoundedRect ([single]($x - $i + 4)) ([single]($y - $i + 10)) ([single]($img.Width + 2 * $i - 8)) ([single]($img.Height + 2 * $i - 8)) ([single](10 + $i))
        $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#000000' 14)), $p)
    }
    # Windows 11 windows have rounded corners: clip them, so the desktop
    # pixels the capture picked up outside the corners don't show.
    $clip = New-RoundedRect ([single]$x) ([single]$y) ([single]$img.Width) ([single]$img.Height) 8
    $g.SetClip($clip)
    $g.DrawImage($img, $x, $y, $img.Width, $img.Height) # 1:1, stays sharp
    $g.ResetClip()
}

# Vertical position that centers something of height h below the caption.
function Mid([int]$h) { return [int](650 - $h / 2) }

function Draw-Caption($g, [string]$title, [string]$sub) {
    $f1 = New-Object System.Drawing.Font 'Segoe UI Semibold', 50, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $f2 = New-Object System.Drawing.Font 'Segoe UI', 26, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString($title, $f1, [System.Drawing.Brushes]::White, 110, 92)
    $g.DrawString($sub, $f2, (New-Object System.Drawing.SolidBrush (Color '#C7D2FE')), 114, 168)
}

function Open([string]$lang, [string]$name) { [System.Drawing.Image]::FromFile((Join-Path $captures "$lang\$name.png")) }

function Save($c, [string]$path) { $c.Bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $c.G.Dispose(); $c.Bmp.Dispose() }

# A web page with a QR code on it, for the first screenshot. Drawn as a plain
# Windows browser window (tab, Windows caption buttons on the right, address
# bar): QRPeek is a Windows app, so nothing here may look like macOS, and it
# mimics no real browser's branding.
function Draw-DemoPage($g, [int]$x, [int]$y, [string]$lang) {
    $w = 700; $h = 600
    $icons = New-Object System.Drawing.Font 'Segoe Fluent Icons', 13, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $small = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $muted = New-Object System.Drawing.SolidBrush (Color '#5F6875')
    $dark = New-Object System.Drawing.SolidBrush (Color '#1B1F24')

    # shadow + window (Windows 11: small corner radius)
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#000000' 60)), (New-RoundedRect ($x + 6) ($y + 14) $w $h 10))
    $g.FillPath([System.Drawing.Brushes]::White, (New-RoundedRect $x $y $w $h 8))

    # tab strip with the caption buttons on the right
    $g.SetClip((New-RoundedRect $x $y $w $h 8))
    $g.FillRectangle((New-Object System.Drawing.SolidBrush (Color '#E8EBEF')), $x, $y, $w, 42)
    $g.FillPath([System.Drawing.Brushes]::White, (New-RoundedRect ($x + 10) ($y + 8) 220 34 8))
    $g.FillRectangle([System.Drawing.Brushes]::White, $x + 10, $y + 30, 220, 12)
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#2563EB')), (New-RoundedRect ($x + 22) ($y + 17) 16 16 4))
    $g.DrawString((@{ tr = 'Kafe Menüsü'; en = 'Café Menu' })[$lang], $small, $dark, $x + 46, $y + 15)
    $g.DrawString([string][char]0xE711, $icons, $muted, $x + 206, $y + 19)          # tab close
    $g.DrawString([string][char]0xE710, $icons, $muted, $x + 246, $y + 15)          # new tab
    $cx = $x + $w - 46 * 3
    foreach ($glyph in 0xE921, 0xE922, 0xE8BB) {                                    # minimize, maximize, close
        $g.DrawString([string][char]$glyph, $icons, $dark, $cx + 16, $y + 14); $cx += 46
    }

    # toolbar: back, forward, refresh, address field with a lock
    $g.FillRectangle([System.Drawing.Brushes]::White, $x, $y + 42, $w, 46)
    $bx = $x + 16
    foreach ($glyph in 0xE72B, 0xE72A, 0xE72C) { $g.DrawString([string][char]$glyph, $icons, $muted, $bx, $y + 58); $bx += 34 }
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#F1F3F6')), (New-RoundedRect ($x + 120) ($y + 50) ($w - 140) 30 15))
    $g.DrawString([string][char]0xE72E, $icons, $muted, $x + 134, $y + 58)
    $g.DrawString('example.com/menu', $small, $muted, $x + 154, $y + 55)
    $g.DrawLine((New-Object System.Drawing.Pen (Color '#E2E6EC'), 1), $x, $y + 88, $x + $w, $y + 88)
    $g.ResetClip()

    # page content
    $head = New-Object System.Drawing.Font 'Segoe UI Semibold', 30, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString((@{ tr = 'Kafe Menüsü'; en = 'Café Menu' })[$lang], $head, $dark, $x + 40, $y + 115)
    foreach ($i in 0..5) { $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#E2E6EC')), (New-RoundedRect ($x + 40) ($y + 180 + 34 * $i) (250 - 30 * ($i % 3)) 14 7)) }
    $gen = New-Object QRCoder.QRCodeGenerator
    $png = (New-Object QRCoder.PngByteQRCode ($gen.CreateQrCode('http://bit.ly/qrpeek-demo', [QRCoder.QRCodeGenerator+ECCLevel]::M))).GetGraphic(8)
    $qr = [System.Drawing.Image]::FromStream((New-Object IO.MemoryStream (, $png)))
    $g.InterpolationMode = 'NearestNeighbor'
    $g.DrawImage($qr, $x + 360, $y + 150, 290, 290)
    $g.InterpolationMode = 'HighQualityBicubic'
    $label = (@{ tr = 'Menü için okutun'; en = 'Scan for the menu' })[$lang]
    $lw = $g.MeasureString($label, $small).Width
    $g.DrawString($label, $small, $muted, [single]($x + 505 - $lw / 2), $y + 450)
}

function Draw-Badge($g, [string]$text, [int]$cx, [int]$cy) {
    $f = New-Object System.Drawing.Font 'Segoe UI Semibold', 22, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $sz = $g.MeasureString($text, $f)
    $p = New-RoundedRect ([single]($cx - $sz.Width / 2 - 18)) ([single]($cy - 24)) ([single]($sz.Width + 36)) 48 24
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color '#2563EB')), $p)
    $g.DrawString($text, $f, [System.Drawing.Brushes]::White, [single]($cx - $sz.Width / 2), [single]($cy - $sz.Height / 2))
}

$text = @{
    tr = @(
        @('Ekrandaki QR kodunu tek kısayolla okuyun', 'Kamera ya da telefon gerekmez · Bağlantının gerçek hedefi ve uyarılar'),
        @('Telefondan bilgisayara dosya alın', 'Telefona uygulama kurmadan · Siz onaylamadan hiçbir şey alınmaz'),
        @('Wi-Fi ve e-Fatura kodları anlaşılır kartlarda', "Tek tıkla Wi-Fi'a bağlanın · Faturayı Excel'e aktarın"),
        @('Linkleri ve dosyaları telefonunuza gönderin', 'Ctrl + Alt + W · Yerel ağ üzerinden, buluta yükleme yok'),
        @('Logolu, desenli QR kodları oluşturun', 'Her kod tasarlarken okunarak doğrulanır · PNG ve SVG'),
        @('Küçük, hızlı, gizlilik dostu', 'Bildirim alanında bekler, istediğinizde hazır')
    )
    en = @(
        @('Read any QR code on your screen with one shortcut', 'No camera or phone needed · See where a link really goes'),
        @('Get files from your phone onto the PC', 'Nothing to install on the phone · Nothing arrives until you approve'),
        @('Wi-Fi and e-invoice codes as clear cards', 'Join Wi-Fi in one click · Export invoices to Excel'),
        @('Send links and files to your phone', 'Ctrl + Alt + W · Over your local network, nothing uploaded'),
        @('Create QR codes with logos and patterns', 'Every code is verified as you design it · PNG and SVG'),
        @('Small, fast and private', 'Waits in the notification area, ready when you are')
    )
}
$bullets = @{
    tr = @('Hesap yok, reklam yok, telemetri yok', 'Ekran görüntüleri hiçbir yere gönderilmez', 'Türkçe ve İngilizce, açık ve koyu tema', 'Açık kaynak (GPL-3.0)')
    en = @('No account, no ads, no telemetry', 'Screen captures are never sent anywhere', 'Turkish and English, light and dark theme', 'Open source (GPL-3.0)')
}

foreach ($lang in 'tr', 'en') {
    $t = $text[$lang]

    # 1) page with a QR + the result card
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[0][0] $t[0][1]
    Draw-DemoPage $c.G 150 (Mid 600) $lang
    $r = Open $lang 'result'; Draw-Shadowed $c.G $r 1200 (Mid $r.Height); $r.Dispose()
    Draw-Badge $c.G 'Ctrl + Alt + Q' 1025 650
    Save $c (Join-Path $shots "$lang-1.png")

    # 2) receive from phone: the QR, then the approval card (new in 0.2)
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[1][0] $t[1][1]
    $a = Open $lang 'receive-qr'; $b = Open $lang 'receive-offer'
    Draw-Shadowed $c.G $a (960 - $a.Width - 40) (Mid $a.Height); Draw-Shadowed $c.G $b (960 + 40) (Mid $b.Height)
    $a.Dispose(); $b.Dispose()
    Save $c (Join-Path $shots "$lang-2.png")

    # 3) Wi-Fi + e-invoice cards
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[2][0] $t[2][1]
    $a = Open $lang 'wifi'; $b = Open $lang 'invoice'
    Draw-Shadowed $c.G $a (960 - $a.Width - 40) (Mid $a.Height); Draw-Shadowed $c.G $b (960 + 40) (Mid $b.Height)
    $a.Dispose(); $b.Dispose()
    Save $c (Join-Path $shots "$lang-3.png")

    # 4) send to phone + main window
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[3][0] $t[3][1]
    $a = Open $lang 'main'; $b = Open $lang 'send'
    Draw-Shadowed $c.G $a (960 - $a.Width - 40) (Mid $a.Height); Draw-Shadowed $c.G $b (960 + 40) (Mid $b.Height)
    $a.Dispose(); $b.Dispose()
    Save $c (Join-Path $shots "$lang-4.png")

    # 5) generator
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[4][0] $t[4][1]
    $a = Open $lang 'generator'; Draw-Shadowed $c.G $a ([int]((1920 - $a.Width) / 2)) (Mid $a.Height); $a.Dispose()
    Save $c (Join-Path $shots "$lang-5.png")

    # 6) main window + privacy points
    $c = New-Canvas 1920 1080; Draw-Caption $c.G $t[5][0] $t[5][1]
    $a = Open $lang 'main'; Draw-Shadowed $c.G $a 330 (Mid $a.Height); $a.Dispose()
    $f = New-Object System.Drawing.Font 'Segoe UI', 32, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $y = 650 - 2 * 90 + 20
    foreach ($line in $bullets[$lang]) {
        $c.G.FillEllipse((New-Object System.Drawing.SolidBrush (Color '#60A5FA')), 920, $y + 14, 14, 14)
        $c.G.DrawString($line, $f, [System.Drawing.Brushes]::White, 955, $y)
        $y += 90
    }
    Save $c (Join-Path $shots "$lang-6.png")
}

# Box art (1:1) and poster (2:3): logo + name, no language-specific text.
foreach ($spec in @(@('box-1080x1080.png', 1080, 1080, 520, 300), @('poster-720x1080.png', 720, 1080, 420, 360))) {
    $c = New-Canvas $spec[1] $spec[2]
    $logo = Draw-Icon $spec[3]
    $lx = [int](($spec[1] - $spec[3]) / 2); $ly = $spec[4] - 100
    $c.G.DrawImage($logo, $lx, $ly, $spec[3], $spec[3]); $logo.Dispose()
    $f = New-Object System.Drawing.Font 'Segoe UI Semibold', ([single]($spec[1] * 0.09)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $sz = $c.G.MeasureString('QRPeek', $f)
    $c.G.DrawString('QRPeek', $f, [System.Drawing.Brushes]::White, [single](($spec[1] - $sz.Width) / 2), [single]($ly + $spec[3] + 30))
    Save $c (Join-Path $assets $spec[0])
}

# Store display tiles (300/150/71): the logo itself on transparency, drawn at
# each size (the package only carries 150 px and smaller, which would blur at 300).
foreach ($size in 300, 150, 71) {
    $tile = Draw-Icon $size
    $tile.Save((Join-Path $assets "tile-${size}x${size}.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $tile.Dispose()
}

Get-ChildItem $assets -Recurse -File -Filter *.png | Where-Object { $_.FullName -notmatch '\\captures\\' } |
    ForEach-Object { Write-Host ($_.FullName.Substring($assets.Length + 1)) }
