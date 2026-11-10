# Changelog

Bu dosya QRPeek'teki önemli değişiklikleri sürüm bazında listeler.
This file lists notable changes to QRPeek, by release.

---

## [Türkçe](#türkçe) · [English](#english)

---

## Türkçe

### [Unreleased] — 0.1.0 (ilk sürüm)

**Ekrandan okuma**
- Tek kısayolla (`Ctrl + Alt + Q`, değiştirilebilir) tüm monitörleri tarama; ekrandaki birden fazla kodu bir kerede bulma; koyu zemin üzerindeki açık renkli kodlar.
- Kod bulunamazsa ekran donar ve alan seçilir; küçük seçimler okunmadan önce büyütülür.
- Tarayıcının küçülttüğü yoğun kodlar (ör. e-Arşiv faturası, modül başına ~2,3 piksel): köşe karelerinden ızgara kurup her modülü tam merkezinden örnekleyen ek bir okuyucu. Gerçek fatura ekran görüntülerinde kodun etrafındaki 600 rastgele seçimin 600'ü okundu.
- Görselden okuma: sürükle-bırak, `Ctrl + V`, dosya seçme, bildirim alanından "Panodaki görseli oku".
- İsteğe bağlı ekranı izleme modu: 1,5 saniyede bir, değişmeyen ekranı atlayarak; her kodu 10 dakikada bir bildirir; Rahatsız Etmeyin'de de görünen kendi uyarı kutusuyla.

**Sonuç kartları**
- Bağlantı: gerçek alan adı ve http, IP adresi, kısaltıcı, benzer görünümlü alan adı ve `@` hilesi uyarıları; "Gerçek hedefi göster" (çerezsiz, en fazla 10 yönlendirme).
- Wi-Fi: Windows Wi-Fi API'siyle bağlanma (kayıtlı ağın şifresini günceller, gerçekten bağlanınca "Bağlandı" der), şifreyi kopyalama.
- 2FA (`otpauth://`): maskeli anahtar; geçmişe yazılmaz, otomatik kopyalanmaz.
- e-Fatura / e-Arşiv: fatura no, tarih, VKN'ler, tutarlar (iki sayı biçimi de okunur: `11373.33` ve `13.648,00`); tek fatura ya da geçmişteki tüm faturalar için Excel uyumlu CSV.
- E-posta, telefon, SMS, kişi (.vcf kaydet), konum, metin.
- Tek kod bulunursa otomatik panoya kopyalama (sırlar hariç).

**Telefona gönder (`Ctrl + Alt + W`)**
- Kısa metin ve linkler doğrudan QR olur.
- Dosya, panodaki görsel ve uzun metinler yerel ağdan gider: 128 bit gizli adresli, salt okunur, 10 dakika geçerli tek kullanımlık sunucu; indirme ilerlemesi bilgisayarda görünür.

**QR oluşturucu**
- Metin/bağlantı ve Wi-Fi (WPA/WPA2, WPA3, WEP, şifresiz, gizli ağ); 6 renk; desen (kare, yuvarlak, nokta); köşe işaretleri (kare, yuvarlak, daire); ortaya logo (yok, QRPeek logosu, kendi logon; logo varken hata düzeltme H).
- Tasarlanırken QRPeek'in kendi okuyucusuyla okunabilirlik denetimi.
- PNG (512/1024/2048 px), SVG (logo gömülü), görseli kopyalama.

**Uygulama**
- Bildirim alanında çalışır, tek kopya; ilk açılışta kısayolları anlatan kart.
- Geçmiş (son 100, yalnızca bu bilgisayarda, kapatılabilir); Wi-Fi şifresi ve 2FA anahtarı yazılmaz. Kopyalanan sırlar Windows pano geçmişine girmez.
- Windows ile başlat: GitHub sürümünde Run anahtarı, Store sürümünde paket StartupTask'ı (paket içinde henüz denenmedi).
- GitHub sürümünde günde en fazla bir kez sürüm kontrolü (kapatılabilir); yalnızca bu deponun sürüm sayfası açılır.
- Türkçe ve İngilizce, Windows'un açık/koyu temasını izler; yaklaşık 1 MB, .NET Framework 4.8.

**Bilinen kısıtlar**
- Çoklu monitör, farklı ölçekleme ve ARM64 bilgisayarlar henüz test edilmedi.

---

## English

### [Unreleased] — 0.1.0 (first release)

**Reading from the screen**
- Scan all monitors with one shortcut (`Ctrl + Alt + Q`, configurable); several codes at once; light-on-dark codes.
- If nothing is found, the screen freezes for an area selection; small selections are enlarged before decoding.
- Dense codes shrunk by the browser (e.g. Turkish e-Arşiv invoices at ~2.3 px per module): an extra decoder builds the grid from the finder squares and samples every module at its exact center. On real invoice screenshots, 600 of 600 random selections around the code were read.
- Read from images: drag and drop, `Ctrl + V`, file picker, tray "Read image on clipboard".
- Optional watch mode: every 1.5 s, skipping unchanged screens; each code is reported once per 10 minutes, with QRPeek's own pop-up that also shows under Do Not Disturb.

**Result cards**
- Link: the real domain and warnings for http, IP hosts, shorteners, look-alike domains and the `@` trick; "Show real destination" (no cookies, at most 10 redirects).
- Wi-Fi: joins through the Windows Wi-Fi API (updates a saved network's password, says "Connected" only once really connected), copy password.
- 2FA (`otpauth://`): masked key; never written to history, never auto-copied.
- e-Fatura / e-Arşiv: invoice no, date, tax IDs, totals (both number styles: `11373.33` and `13.648,00`); Excel-friendly CSV for one invoice or all invoices in history.
- Email, phone, SMS, contact (save .vcf), location, text.
- Auto-copy when exactly one code is found (secrets excluded).

**Send to phone (`Ctrl + Alt + W`)**
- Short text and links become a QR directly.
- Files, clipboard images and long text go over the local network: a one-off, read-only server behind a 128-bit secret path, valid for 10 minutes; download progress shows on the PC.

**QR generator**
- Text/link and Wi-Fi (WPA/WPA2, WPA3, WEP, open, hidden); 6 colors; pattern (square, rounded, dots); corner markers (square, rounded, circle); center logo (none, QRPeek logo, your own; error correction H while a logo is on).
- Readability check with QRPeek's own scanner while designing.
- PNG (512/1024/2048 px), SVG (logo embedded), copy image.

**App**
- Lives in the notification area, single instance; a first-run card explains the shortcuts.
- History (last 100, on this PC only, can be turned off); Wi-Fi passwords and 2FA keys are never written. Copied secrets stay out of Windows clipboard history.
- Start with Windows: the Run key in the GitHub build, the package StartupTask in the Store build (not yet tried inside a package).
- GitHub build: version check at most once a day (can be turned off); only this repository's release page is opened.
- Turkish and English, follows the Windows light/dark theme; about 1 MB, .NET Framework 4.8.

**Known limitations**
- Multiple monitors, mixed scaling and ARM64 PCs are untested.
