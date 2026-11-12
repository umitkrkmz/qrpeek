<div align="center">

# QRPeek

[![Tests](https://github.com/umitkrkmz/qrpeek/actions/workflows/tests.yml/badge.svg)](https://github.com/umitkrkmz/qrpeek/actions/workflows/tests.yml)
[![Latest release](https://img.shields.io/github/v/release/umitkrkmz/qrpeek)](https://github.com/umitkrkmz/qrpeek/releases/latest)
[![License: GPL-3.0-or-later](https://img.shields.io/badge/license-GPL--3.0--or--later-blue)](LICENSE)

Ekrandaki QR kodlarını **kamera ya da telefon olmadan** okuyan, QR kodu oluşturan küçük bir Windows aracı.

A small Windows tool that reads QR codes **on your screen**, with no camera or phone, and creates them.

**📖 [Türkçe kılavuz](docs/README.tr.md) · [English guide](docs/README.en.md)** · [Changelog](CHANGELOG.md) · [Gizlilik / Privacy](docs/PRIVACY.md)

<a href="https://apps.microsoft.com/detail/9nx5t90f6dqk?referrer=appbadge&mode=direct"><img src="https://get.microsoft.com/images/en-us%20dark.svg" width="180" alt="Download from the Microsoft Store"></a>

**⬇ [QRPeek-portable.zip](https://github.com/umitkrkmz/qrpeek/releases/latest/download/QRPeek-portable.zip)** · GitHub, ücretsiz / free

<table align="center">
  <tr>
    <td valign="top"><img src="docs/images/main-tr.png" alt="QRPeek ana pencere" width="300"></td>
    <td valign="top"><img src="docs/images/receipt-tr.png" alt="QR'sız makbuz: yazı tanıma ve makbuz kartı" width="300"></td>
  </tr>
  <tr>
    <td valign="top"><img src="docs/images/result-tr.png" alt="Bağlantı kartı ve uyarılar" width="300"></td>
    <td valign="top"><img src="docs/images/receive-tr.png" alt="Telefondan al: bilgisayarda onay" width="300"></td>
  </tr>
</table>

<p>
  <img src="docs/images/qr-github.png" width="160" alt="QR code linking to this repository">
  <br>
  <sub>QRPeek kuruluysa <b>Ctrl + Alt + Q</b>'ya basın, bu kod ekrandan okunsun.</sub>
  <br>
  <sub>With QRPeek installed, press <b>Ctrl + Alt + Q</b> to read this code right off the screen.</sub>
  <br>
  <sub>Bu kod QRPeek'in kendi üreticisiyle yapıldı. · Made with QRPeek's own generator.</sub>
</p>

</div>

## Türkçe

- **Tek kısayol, tüm ekranlar:** `Ctrl + Alt + Q` ekrandaki bütün kodları bulur; küçük kodlar için alan seçersiniz.
- **Güvenli bağlantılar:** hiçbir şey kendiliğinden açılmaz; gerçek hedef ve şüpheli link uyarıları önce gösterilir.
- **Akıllı kartlar:** Wi-Fi'a tek tıkla bağlanma, 2FA kodu (maskeli), e-Fatura / e-Arşiv (CSV), kişi, konum…
- **Telefonla aktarım:** link, metin ve dosyaları telefona gönderin ya da telefondan alın; yalnızca yerel ağ, yalnızca sizin onayınızla.
- **QR yoksa yazı ve makbuz:** yazı Windows'un kendi OCR'ıyla bu bilgisayarda okunur; makbuzlar seçilebilir alanlı bir karta, CSV'ye ya da özet QR'a dönüşür.
- **QR oluşturucu:** renk, desen, logo; her kod tasarlanırken okunarak doğrulanır. PNG, SVG.
- **Küçük ve gizli:** kurulum gerektirmez, hesap, reklam ve telemetri yok. Türkçe ve İngilizce, açık ve koyu tema.

**GitHub sürümü ücretsizdir** ve öyle kalacak. **[Microsoft Store](https://apps.microsoft.com/detail/9NX5T90F6DQK) sürümü** aynı uygulamadır (ücretli): tek tıkla kurulum, kendiliğinden güncelleme; satın almak projeye destek olur. Ayrıntılar, kısayollar ve tüm özellikler: **[Türkçe kılavuz](docs/README.tr.md)**.

## English

- **One shortcut, every screen:** `Ctrl + Alt + Q` finds every code on screen; for tiny ones you select an area.
- **Safe links:** nothing opens on its own; the real destination and warnings for suspicious links come first.
- **Smart cards:** join Wi-Fi in one click, 2FA codes (masked), Turkish e-invoices (CSV), contacts, locations…
- **Phone transfers:** send links, text and files to your phone or receive them from it; local network only, and only with your approval.
- **No QR? Text and receipts:** text is read on this PC with the OCR built into Windows; receipts become a card with pickable fields, a CSV or a summary QR.
- **QR generator:** colors, patterns, logos; every code is read back as you design it. PNG, SVG.
- **Small and private:** nothing to install, no account, ads or telemetry. Turkish and English, light and dark theme.

**The GitHub build is free** and will stay free. **The [Microsoft Store](https://apps.microsoft.com/detail/9NX5T90F6DQK) version** is the same app (paid), with a one-click install and automatic updates; buying it supports the project. Details, shortcuts and every feature: **[English guide](docs/README.en.md)**.

## Kaynaktan derleme / Build from source

```bash
dotnet build -c Release
dotnet test
```

.NET SDK 8+ gerekir, Visual Studio gerekmez. · Needs the .NET SDK 8 or later; Visual Studio is not required.

[CONTRIBUTING.md](CONTRIBUTING.md) · [Code of Conduct](CODE_OF_CONDUCT.md) · [SECURITY.md](SECURITY.md)

## Lisans / License

QRPeek is free software under the [GNU General Public License v3.0 or later](LICENSE). Third-party libraries (ZXing.Net, Apache-2.0; QRCoder, MIT) are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

The **QRPeek name and logo are not covered by the GPL**. Forks are welcome under the GPL, but please publish them under a different name and icon.

QRPeek adı ve logosu GPL kapsamında değildir; çatallamalara açığız, ancak lütfen farklı bir ad ve simgeyle yayınlayın.
