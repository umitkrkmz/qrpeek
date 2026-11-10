# Security Policy

**[🇹🇷 Türkçe](#türkçe) · [🇬🇧 English](#english)**

---

## Türkçe

### Desteklenen sürümler

Güvenlik düzeltmeleri yalnızca **en son sürüme** gelir (GitHub Releases ve Microsoft Store).

### Kapsam

QRPeek ekrandan **güvenilmeyen veri** okur: bir QR kodunu herkes hazırlayabilir. Bu yüzden şunlar önemlidir:

- **QR içeriğinin açtıkları:** bir QR kodu yalnızca `http(s)`, `mailto`, `tel` ve `sms` bağlantılarını, o da kullanıcı düğmeye bastığında açabilir. `file:`, `javascript:`, `ms-settings:` gibi başka bir şemanın açılması, bir şeyin kendiliğinden açılması ya da kendiliğinden panoya kopyalanan bir sırrın (2FA anahtarı, Wi-Fi şifresi) olması güvenlik açığıdır.
- **Telefona gönder (yerel ağ sunucusu):** yalnızca kullanıcının seçtiği dosyaları, yalnızca 128 bit rastgele gizli adresin arkasında, salt okunur ve en fazla 10 dakika sunar. Başka bir dosyaya erişilebilmesi (path traversal), gizli adres olmadan erişim, yükleme yapılabilmesi ya da sunucunun pencere kapandıktan sonra açık kalması güvenlik açığıdır.
- **Wi-Fi profili:** QR'daki ağ adı ve şifre Windows WLAN profil XML'ine yazılır; burada XML enjeksiyonu açıktır.
- **e-Fatura CSV'si:** QR içeriğinin Excel'de formül olarak çalışabilmesi (CSV injection) açıktır.
- **Geçmiş ve pano gizliliği:** 2FA anahtarlarının ya da Wi-Fi şifrelerinin `history.json`'a yazılması açıktır.
- **Sürüm kontrolü (GitHub sürümü):** "İndir" yalnızca `github.com/umitkrkmz/qrpeek/releases` altındaki adresleri açmalıdır; başka bir adresin açtırılabilmesi açıktır.
- **"Gerçek hedefi göster":** istek yalnızca kullanıcı bastığında, çerezsiz ve en fazla 10 yönlendirmeyle yapılmalıdır.

### Bir açık bulursanız

**Lütfen herkese açık bir issue açmayın.** Bunun yerine [GitHub Security Advisories](https://github.com/umitkrkmz/qrpeek/security/advisories/new) üzerinden özel olarak bildirin (depo → Security sekmesi → "Report a vulnerability").

Mümkünse etkilenen dosyayı, yeniden üretme adımlarını (örnek bir QR görseli çok işe yarar) ve olası etkiyi ekleyin. Bu tek kişilik, gönüllü bir bakım altındaki bir proje; kesin bir yanıt süresi taahhüt edemem, ama bildirilen her açığı ciddiye alıyorum ve en kısa sürede düzeltme yayınlamayı hedefliyorum.

### Kapsam dışı

- ZXing.Net'teki açıklar → [micjahn/ZXing.Net](https://github.com/micjahn/ZXing.Net)
- QRCoder'daki açıklar → [Shane32/QRCoder](https://github.com/Shane32/QRCoder)
- Kullanıcının kendisi açmayı seçtiği bir sitenin içeriği
- Windows Güvenlik Duvarı'nın "Telefona gönder" için izin istemesi (beklenen davranış)

---

## English

### Supported versions

Security fixes go to the **latest release** only (GitHub Releases and the Microsoft Store).

### Scope

QRPeek reads **untrusted data** from the screen: anyone can make a QR code. That makes these areas matter:

- **What QR content can open:** a QR code may only open `http(s)`, `mailto`, `tel` and `sms` links, and only when the user presses the button. Opening any other scheme (`file:`, `javascript:`, `ms-settings:` …), anything opening on its own, or a secret (2FA key, Wi-Fi password) being copied to the clipboard automatically is a vulnerability.
- **Send to phone (local network server):** serves only the files the user picked, only behind a 128-bit random secret path, read-only, for at most 10 minutes. Reaching any other file (path traversal), access without the secret, uploads, or the server staying up after the window closes is a vulnerability.
- **Wi-Fi profile:** the network name and password from a QR are written into a Windows WLAN profile XML; XML injection there is a vulnerability.
- **e-Fatura CSV:** QR content running as a formula in Excel (CSV injection) is a vulnerability.
- **History and clipboard privacy:** 2FA keys or Wi-Fi passwords ending up in `history.json` is a vulnerability.
- **Version check (GitHub build):** "Download" must only open addresses under `github.com/umitkrkmz/qrpeek/releases`; getting it to open anything else is a vulnerability.
- **"Show real destination":** the request must happen only when the user presses it, without cookies, with at most 10 redirects.

### If you find a vulnerability

**Please don't open a public issue.** Report it privately through [GitHub Security Advisories](https://github.com/umitkrkmz/qrpeek/security/advisories/new) instead (repo → Security tab → "Report a vulnerability").

If you can, include the affected file, steps to reproduce (a sample QR image helps a lot) and the potential impact. This is a one-person, volunteer-maintained project; I can't promise a fixed response time, but I take every report seriously and aim to ship a fix as soon as possible.

### Out of scope

- Vulnerabilities in ZXing.Net → [micjahn/ZXing.Net](https://github.com/micjahn/ZXing.Net)
- Vulnerabilities in QRCoder → [Shane32/QRCoder](https://github.com/Shane32/QRCoder)
- The content of a site the user chose to open
- Windows Firewall asking for permission for "Send to phone" (expected behavior)
