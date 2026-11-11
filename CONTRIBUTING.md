# Katkı Rehberi / Contributing

**[🇹🇷 Türkçe](#türkçe) · [🇬🇧 English](#english)**

---

## Türkçe

Katkın için teşekkürler! Hata bildirimi, öneri, çeviri düzeltmesi ya da kod; hepsi değerli. Lütfen [Davranış Kuralları](CODE_OF_CONDUCT.md)'na uy. Güvenlik açıkları için issue değil, [SECURITY.md](SECURITY.md)'deki özel kanal kullanılır.

### Geliştirme ortamı

- Windows 10/11 ve [.NET SDK](https://dotnet.microsoft.com/download) 8 veya üstü. Visual Studio gerekmez (istersen kullanabilirsin).
- Uygulama **.NET Framework 4.8** hedefler; o Windows'ta zaten yüklüdür.

```bash
dotnet build -c Release
dotnet test
```

Çıktı: `src/QRPeek/bin/Release/net48/QRPeek.exe`.

### Mimari kurallar (PR'lar bunlara uymalı)

Ayrıntılar ve gerekçeler [AGENTS.md](AGENTS.md)'de. Özetle:

- **Hedef `net48` kalır.** Uygulamanın küçük kalması ve kurulum gerektirmemesi buna bağlı.
- **Yeni bağımlılık eklemeden önce sor.** Küçük ve GPL-3.0 ile uyumlu olmalı.
- **Her arayüz metni `S.cs` içinde, Türkçe ve İngilizce birlikte** yazılır.
- **QRPeek kendi başına internete çıkmaz.** Ağ kullanan yerler belli ve sınırlı (`LinkResolver`, `LanShare`, `LanReceive`, `UpdateChecker`); yenisini eklemeden önce tartışalım.
- **Bir QR kodu yalnızca güvenli bağlantı türlerini açabilir** ve sırlar (2FA, Wi-Fi şifresi) geçmişe ya da kendiliğinden panoya gitmez.
- **Üreticideki her stil okunabilir kalmalı** (`StyleTests`).

### Testler

`tests/QRPeek.Tests` (xUnit). Bir hatayı düzelttiğinde onu yakalayan bir test ekle; mümkünse düzeltmeyi geçici olarak geri alıp testin kırmızıya döndüğünü gör. Okuma (decode) ile ilgili değişikliklerde gerçek ekran görüntüleriyle de dene: sentetik görüntüler tarayıcıların ölçekleme bulanıklığını her zaman taklit edemiyor.

### Commit mesajları ve PR'lar

Kısa, "ne"den çok "neden" odaklı. PR açarken şablondaki kontrol listesini doldur. Yapay zeka araçları kullandıysan sorun değil; ama değişikliğin doğruluğundan sen sorumlusun.

### Yayın (bakımcı notu)

1. `CHANGELOG.md`'de `[Unreleased]` başlığını sürüm numarası ve tarihle değiştir (iki dilde).
2. `git tag vX.Y.Z` ve `git push origin vX.Y.Z`.
3. `release.yml` testleri çalıştırır, sürüm numarasını etiketten alarak derler, `QRPeek-portable.zip`'i ve SHA256 özetini GitHub Release'e koyar.
4. Microsoft Store: `packaging/msix` altındaki MSIX adımları (ayrıca belgelenecek).

---

## English

Thanks for contributing! Bug reports, ideas, translation fixes and code are all welcome. Please follow the [Code of Conduct](CODE_OF_CONDUCT.md). Security issues go through the private channel in [SECURITY.md](SECURITY.md), not an issue.

### Development setup

- Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later. Visual Studio is not required (use it if you like).
- The app targets **.NET Framework 4.8**, which already ships with Windows.

```bash
dotnet build -c Release
dotnet test
```

Output: `src/QRPeek/bin/Release/net48/QRPeek.exe`.

### Architectural rules (PRs need to follow these)

Details and reasons are in [AGENTS.md](AGENTS.md). In short:

- **The target stays `net48`.** That's what keeps the app small with nothing to install.
- **Ask before adding a dependency.** It must be small and GPL-3.0 compatible.
- **Every UI string lives in `S.cs`, in Turkish and English together.**
- **QRPeek doesn't go online on its own.** Network use is limited to known places (`LinkResolver`, `LanShare`, `LanReceive`, `UpdateChecker`); let's discuss before adding another.
- **A QR code may only open safe link types**, and secrets (2FA, Wi-Fi passwords) never go to history or to the clipboard on their own.
- **Every generator style must stay readable** (`StyleTests`).

### Tests

`tests/QRPeek.Tests` (xUnit). When you fix a bug, add a test that catches it; if you can, temporarily undo the fix and watch the test go red. For decoding changes, also try real screenshots: synthetic images can't always imitate browser scaling blur.

### Commit messages and PRs

Short, focused on "why" more than "what". Fill in the checklist in the PR template. Using AI tools is fine, but you are responsible for the change being correct.

### Cutting a release (maintainer note)

1. In `CHANGELOG.md`, replace `[Unreleased]` with the version and date (both languages).
2. `git tag vX.Y.Z` and `git push origin vX.Y.Z`.
3. `release.yml` runs the tests, builds with the version taken from the tag, and puts `QRPeek-portable.zip` and its SHA256 on the GitHub Release.
4. Microsoft Store: the MSIX steps under `packaging/msix` (to be documented).
