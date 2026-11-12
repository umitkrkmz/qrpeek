# Microsoft Store listing — QRPeek

The text pasted into Partner Center (QRPeek → Store listings, Properties,
Pricing, Submission options). Kept here so the next submission starts from
the last one. Rules that shaped it (Microsoft Store Policies):

- **10.1.1** — the title is just "QRPeek": no descriptive words, no other
  product's name.
- **10.1.3** — at most 7 search terms per language, relevant, no prices, no
  other products' names.
- Every claim must hold for the **Store build**: no in-app version check
  (the Store updates it), "Start with Windows" goes through Windows' Startup
  apps settings.
- Wi-Fi joining can make Windows ask for location access; the privacy policy
  explains it (section "Location permission").

---

## Properties

| Field | Value |
|---|---|
| Category | Utilities & tools (subcategory: none) |
| Privacy policy URL | https://github.com/umitkrkmz/qrpeek/blob/master/docs/PRIVACY.md |
| Website | https://github.com/umitkrkmz/qrpeek |
| Support contact | https://github.com/umitkrkmz/qrpeek/issues/new/choose |
| System requirements | Windows 10 version 2004 (19041) or later on x64; Windows 11 on ARM64; about 2 MB of disk space; no internet connection needed |

## Pricing and availability

- Base price: **$2.49**, all markets (from 0.3.0; $1.99 in 0.1–0.2). Partner Center only offers fixed tiers ($1.99, $2.49, $2.99…).
- Turkey: **₺75** (from 0.3.0; ₺60 before; market-specific price; Partner Center TRY prices go in steps of ₺5).
- A price change only affects new buyers; it can be lowered later or put on sale (Sale pricing).
- No free trial (enforcing it would need a Store license check in the app). The GitHub build stays free.

## Submission options — restricted capability (runFullTrust, ≤ 500 characters)

> QRPeek is a desktop (Win32, .NET Framework) app. It registers system-wide hotkeys, captures the screen to decode QR codes, shows a tray icon and overlay windows on every monitor, joins Wi-Fi networks through the native WLAN API, and briefly runs a local-network server to send files to, and receive user-approved files from, the user's phone. This is not possible in an AppContainer. It installs no drivers or services, never elevates, and runs no code from outside the package.

## Submission options — publishing and notes for certification

- Publishing hold: **"Don't publish this submission until I select Publish now"**, so the Store release and the GitHub release (`v0.1.0`, `v0.2.0`…) go out the same day. GitHub is pushed only after the Store version is live.
- Notes for certification (testers only, English):

> No account or sign-in needed. To test: open any web page or image that shows a QR code and press Ctrl+Alt+Q (or click "Scan screen"); a card with the decoded content appears. Ctrl+Alt+W shows the clipboard text as a QR code. "Send to phone" serves files over the local network only, so Windows Firewall may ask for permission. "Receive from phone" (tray menu, or "Receive files" in the send window) shows a QR code; open it on a phone or another device on the same network, pick files there, and they are saved to Downloads\QRPeek only after you press Accept on the PC. "Connect" on a Wi-Fi card may make Windows ask for location access (Windows 11 24H2 and later). New in 0.3: "Select area" over text that has no QR code (for example a receipt on a web page) shows the text, read on the device with the Windows.Media.Ocr API; if it looks like a receipt, a card with its fields appears (nothing is uploaded). The app keeps running in the notification area after its window is closed; use Settings > Quit QRPeek to exit.

---

## Türkçe (tr-tr)

**Kısa açıklama** (≤ 270 karakter)

> Ekrandaki QR kodlarını kamera ya da telefon olmadan okuyun, yeni QR kodları oluşturun. Tek kısayolla tüm ekranları tarar, bağlantının gerçek hedefini gösterir, QR'ı olmayan makbuzları okur, telefonunuzla dosya alıp verir. Hesap yok, reklam yok, telemetri yok.

**Açıklama**

```
QRPeek, ekranınızda gördüğünüz QR kodlarını kamera ya da telefon kullanmadan okur: bir web sayfasında, PDF'te, toplantı sunumunda, videoda veya bir görselde. Aynı zamanda logolu ve desenli QR kodları da oluşturur.

Ctrl + Alt + Q'ya basın; QRPeek tüm monitörleri tarar ve ekrandaki kodların hepsini bulur. Kod çok küçükse ekran donar, kodun etrafını seçersiniz. Tarayıcıda küçülmüş yoğun kodlar (örneğin e-Arşiv faturaları) da okunur. Birden çok ekranınız varsa kodun hangi ekranda olduğu da yazar.

Okunan her kod anlaşılır bir karta dönüşür:

• Bağlantılar: hiçbir şey kendiliğinden açılmaz. Önce gideceği site gösterilir; şifresiz bağlantı, IP adresi, kısaltılmış link, benzer görünümlü alan adı ve "@" hilesi için uyarı verilir. "Gerçek hedefi göster", kısaltılmış bir linkin nereye gittiğini tarayıcı açmadan bulur.
• Wi-Fi: QR'daki ağa tek tıkla bağlanın ya da şifreyi kopyalayın. Şifre siz isteyene kadar gizli kalır.
• 2FA kurulum kodları: gizli anahtar maskelenir, geçmişe yazılmaz, panoya kendiliğinden kopyalanmaz.
• e-Fatura / e-Arşiv: satıcı ve alıcı VKN, tarih, KDV ve ödenecek tutar düzenli bir kartta; tek tıkla Excel'e (CSV) aktarın.
• E-posta, telefon, SMS, kişi kartı (.vcf) ve konum kodları.

Telefona gönder (Ctrl + Alt + W): kopyaladığınız link ya da metin anında büyük bir QR olur, telefonun kamerasıyla okutursunuz. Dosyalar, fotoğraflar ve uzun metinler yalnızca yerel ağınız üzerinden, tek kullanımlık ve 10 dakika geçerli bir bağlantıyla gider; buluta hiçbir şey yüklenmez.

Telefondan al: telefon ya da tablet QR'ı okutur ve dosyaları seçer; bilgisayarda listeyi görürsünüz ve siz onaylamadan hiçbir şey alınmaz. Cihaza uygulama kurmak gerekmez, dosyalar yalnızca yerel ağdan gelir, hiçbiri kendiliğinden açılmaz.

QR oluşturucu: metin, bağlantı ya da Wi-Fi için; renk, desen (kare, yuvarlak, nokta), köşe işaretleri ve ortaya QRPeek logosu ya da kendi logonuz. QRPeek her kodu siz tasarlarken okuyarak doğrular, böylece çalışmayan bir kodu yazdırmazsınız. PNG, SVG ya da panoya.

QR yoksa yazı ve makbuzlar: seçtiğiniz alanda QR kodu yoksa içindeki yazı Windows'un kendi yazı tanıma özelliğiyle, bu bilgisayarda okunur; düzeltip kopyalarsınız. Yazı bir makbuz ya da faturaysa satıcı, belge no, tarih, kalemler, KDV ve toplam ayrı alanlarda gelir. İstediğiniz alanları işaretleyip özet olarak kopyalayın, Excel'e aktarın, CSV kaydedin ya da her telefonun okuyabileceği bir özet QR yapın. Kişisel bilgiler işaretsiz gelir; ürün anahtarı ve kart numarası maskelenir ve hiçbir çıktıya girmez.

İsteğe bağlı ekranı izleme modu: sunumda, toplantıda ya da videoda bir QR belirince sağ altta küçük bir uyarı çıkar.

Gizliliğiniz sizde: hesap yok, reklam yok, telemetri yok. Ekran görüntüleri yalnızca bellekte işlenir; kaydedilmez, gönderilmez. Wi-Fi'a bağlanırken Windows konum izni isteyebilir; QRPeek bunu yalnızca bağlantıyı doğrulamak için kullanır, konumunuzu okumaz.

Küçük ve hızlı: bildirim alanında sessizce bekler, istenirse Windows ile başlar. Türkçe ve İngilizce arayüz, açık ve koyu tema.

Açık kaynak: QRPeek GPL-3.0 lisanslı açık kaynak bir projedir ve kaynak kodu GitHub'da herkese açıktır (github.com/umitkrkmz/qrpeek). Store sürümü aynı uygulamadır; tek tıkla kurulur, güncellemeleri Store'dan kendiliğinden gelir. Satın alarak projenin gelişmesine destek olursunuz.
```

**Bu sürümdeki yenilikler**

> 0.3.0 — Yeni: QR'ı olmayan makbuzlar. Seçtiğiniz alanda QR yoksa yazı Windows'un kendi yazı tanımasıyla bu bilgisayarda okunur; makbuz ve faturalar seçilebilir alanlı bir karta dönüşür (özet, Excel, CSV, özet QR). Başka diller için düzenlenebilir fatura sözlüğü, bölümlü yeni Ayarlar. Düzeltme: kısaltılmış bir bağlantıyı açan site otomatik denetimi engellediğinde "Yönlendirme yok" yerine bu açıkça söylenir.

(0.2.0 sürümünde: "Yeni: Telefondan al. Telefon ya da tabletten bilgisayara fotoğraf ve dosya; cihaza uygulama kurmadan, yalnızca sizin onayınızla. Dosyalar yerel ağdan gelir ve İndirilenler\QRPeek klasörüne kaydedilir, hiçbiri kendiliğinden açılmaz. Ayrıca: birden çok ekranda kodun hangi ekranda bulunduğu gösterilir; ana pencerede "Telefona gönder / al" düğmesi.")

(0.1.0'da, ilk başvuruda, boş bırakılmıştı; Partner Center böyle istiyor.)

**Ürün özellikleri** (en fazla 20, her biri ≤ 200 karakter)

1. Tek kısayolla tüm ekranlardaki QR kodlarını aynı anda okuma, kamera gerekmez
2. QR yoksa ekrandaki yazıyı Windows'un kendi yazı tanımasıyla okuma ve kopyalama
3. Kod bulunamazsa ekranı dondurup alan seçme
4. Tarayıcıda küçülmüş yoğun kodları (e-Arşiv faturası) okuma
5. Bağlantıyı açmadan gerçek hedefini görme
6. Şüpheli bağlantılar için uyarılar
7. QR'daki Wi-Fi ağına tek tıkla bağlanma, şifre varsayılan olarak gizli
8. 2FA kurulum kodlarını güvenle gösterme
9. e-Fatura / e-Arşiv QR'ını okuma ve Excel'e (CSV) aktarma
10. Kopyalanan link, metin, dosya ve fotoğrafları telefona gönderme (yerel ağ, bulut yok)
11. Telefondan ya da tabletten dosya alma: uygulama kurmadan, yalnızca sizin onayınızla
12. Makbuz kartı: alanları seçip özet, Excel, CSV ya da özet QR olarak alma
13. Logolu, renkli ve desenli QR kodu oluşturma; her kod otomatik doğrulanır
14. PNG ve SVG olarak kaydetme, panoya kopyalama
15. Görselden okuma: sürükle-bırak ya da Ctrl+V
16. İsteğe bağlı ekranı izleme modu
17. Bildirim alanında çalışma, isteğe bağlı Windows ile başlama
18. Türkçe ve İngilizce arayüz, açık ve koyu tema
19. Hesap yok, reklam yok, telemetri yok
20. Açık kaynak (GPL-3.0)

**Arama terimleri** (en fazla 7)

1. qr okuyucu
2. qr kod okuma
3. qr kod oluşturucu
4. ekrandan qr okuma
5. wifi qr
6. qr code reader
7. qr code generator

**Telif hakkı ve marka bilgisi**

> © 2026 umitkrkmz. QRPeek, GPL-3.0-or-later lisanslı açık kaynak yazılımdır.

**Ek lisans koşulları**

> Bu uygulama GNU General Public License v3.0 (veya sonrası) ile lisanslanmıştır: https://github.com/umitkrkmz/qrpeek/blob/master/LICENSE — İçerdiği üçüncü taraf bileşenlerin (ZXing.Net, Apache-2.0; QRCoder, MIT) lisansları uygulama klasöründeki THIRD-PARTY-NOTICES.txt dosyasındadır.

---

## English (en-us)

**Short description** (≤ 270 characters)

> Read QR codes on your screen without a camera or phone, and create new ones. One shortcut scans every screen, shows where a link really goes, reads receipts that have no QR code, and swaps files with your phone. No account, no ads, no telemetry.

**Description**

```
QRPeek reads the QR codes you see on your screen without a camera or phone: on a web page, in a PDF, in a meeting presentation, in a video or in an image. It also creates QR codes, with logos and patterns.

Press Ctrl + Alt + Q and QRPeek scans every monitor and finds every code on screen. If a code is too small, the screen freezes and you select the area around it. Dense codes shrunk by the browser (such as Turkish e-invoices) are read too. With more than one screen, it also says which screen the code was on.

Every code becomes a clear card:

• Links: nothing opens on its own. You see the real destination first, with warnings for unencrypted links, bare IP addresses, URL shorteners, look-alike domains and the "@" trick. "Show real destination" finds where a shortened link goes without opening a browser.
• Wi-Fi: join the network in the code with one click, or copy the password. The password stays hidden until you ask.
• 2FA setup codes: the secret key is masked, never written to history and never copied to the clipboard on its own.
• Turkish e-invoices (e-Fatura / e-Arşiv): tax IDs, date, VAT and amount due in a tidy card; export to Excel (CSV) in one click.
• Email, phone, SMS, contact card (.vcf) and location codes.

Send to phone (Ctrl + Alt + W): a link or text you copied becomes a big QR code; point your phone's camera at it. Files, photos and long text travel only over your local network, through a one-off link valid for 10 minutes; nothing is uploaded to the cloud.

Receive from phone: a phone or tablet scans the QR code and picks files; the PC shows you the list and nothing is received until you approve it. Nothing to install on the device, files come over your local network only, and nothing opens on its own.

QR generator: for text, links or Wi-Fi; color, pattern (square, rounded, dots), corner markers, and the QRPeek logo or your own in the middle. QRPeek reads every code back as you design it, so you never print a code that doesn't scan. PNG, SVG or clipboard.

No QR? Text and receipts: if the area you pick has no QR code, its text is read on this PC with the text recognition built into Windows; fix it and copy it. If the text is a receipt or invoice, seller, number, date, items, tax and total come as separate fields. Tick the ones you want and copy a summary, export to Excel, save a CSV or make a summary QR any phone can read. Personal details start unticked; product keys and card numbers are masked and never get into any output.

Optional watch mode: when a QR code appears in a presentation, meeting or video, a small pop-up shows up bottom-right.

Your privacy stays yours: no account, no ads, no telemetry. Screen captures are processed in memory only, never saved or sent. When joining Wi-Fi, Windows may ask for location access; QRPeek uses it only to confirm the connection and never reads your location.

Small and fast: waits quietly in the notification area, and can start with Windows. Turkish and English interface, light and dark theme.

Open source: QRPeek is an open-source project under GPL-3.0, with its source code public on GitHub (github.com/umitkrkmz/qrpeek). The Store version is the same app, installed with one click and updated automatically through the Store. Buying it supports the project.
```

**What's new in this version**

> 0.3.0 — New: receipts with no QR code. If the area you pick has no QR code, its text is read on this PC with Windows' own text recognition; receipts and invoices become a card with pickable fields (summary, Excel, CSV, summary QR). An editable receipt-terms list for other languages, and a new sectioned Settings window. Fixed: when a site blocks the automatic check of a shortened link, it now says so instead of "No redirects".

(In 0.2.0: "New: Receive from phone. Photos and files from a phone or tablet to the PC, with nothing to install on the device and only with your approval. Files come over your local network, are saved to Downloads\QRPeek and never open on their own. Also: with more than one screen, the result says which screen a code was on; the main window has a "Phone: send / receive" button.")

(Left empty in 0.1.0, the first submission, as Partner Center asks.)

**Product features** (up to 20, each ≤ 200 characters)

1. Read every QR code on every screen at once with one shortcut, no camera needed
2. No QR? Read and copy the text on screen with Windows' own text recognition
3. If nothing is found, freeze the screen and select the area
4. Read dense codes shrunk by the browser (e.g. e-invoices)
5. See a link's real destination without opening it
6. Warnings for suspicious links
7. Join the Wi-Fi network in a QR code with one click; password hidden by default
8. Show 2FA setup codes safely
9. Read Turkish e-invoice (e-Fatura / e-Arşiv) codes and export them to Excel (CSV)
10. Send copied links, text, files and photos to your phone (local network, no cloud)
11. Receive files from a phone or tablet: nothing to install, only with your approval
12. Receipt card: pick fields and get a summary, Excel, CSV or a summary QR
13. Create QR codes with logos, colors and patterns; every code is verified
14. Save as PNG or SVG, copy to the clipboard
15. Read from images: drag and drop or Ctrl+V
16. Optional watch mode
17. Runs in the notification area, can start with Windows
18. Turkish and English interface, light and dark theme
19. No account, no ads, no telemetry
20. Open source (GPL-3.0)

**Search terms** (up to 7)

1. qr code reader
2. qr code scanner
3. qr code generator
4. screen qr scanner
5. wifi qr code
6. qr okuyucu
7. qr kod oluşturucu

**Copyright and trademark info**

> © 2026 umitkrkmz. QRPeek is open-source software licensed under GPL-3.0-or-later.

**Additional license terms**

> This app is licensed under the GNU General Public License v3.0 (or later): https://github.com/umitkrkmz/qrpeek/blob/master/LICENSE — Licenses of the third-party components it includes (ZXing.Net, Apache-2.0; QRCoder, MIT) are in THIRD-PARTY-NOTICES.txt in the app folder.

---

## Images

| Item | Size | Status |
|---|---|---|
| Screenshots (per language) | 1920×1080 PNG, 7 each (0.3.0): read a QR on a page + link card, a receipt with no QR + receipt card, receive from phone (QR + approval card), Wi-Fi + e-invoice cards, send to phone + main window, generator with logo, main window + privacy points | done (`build/store-assets/shots/tr-1..7.png`, `en-1..7.png`, not in git; in Partner Center, replace the old 6 and keep this order) |
| 1:1 box art (Store logo) | 1080×1080 PNG | done (`build/store-assets/box-1080x1080.png`) |
| 2:3 poster art | 720×1080 PNG | done (`build/store-assets/poster-720x1080.png`) |
| Store display tiles | 300×300, 150×150, 71×71 PNG | done (`build/store-assets/tile-*.png`) |

Made by `tools/make-store-assets.ps1` from window captures in `build/store-assets/captures/<lang>/` (main, result, receipt, wifi, invoice, generator, send, receive-qr, receive-offer, plus `receipt-page.png`). Windows are placed 1:1, never scaled.

Screenshot rule: **no real personal data and no other brands' logos.** Demo content only (example.com links, a made-up Wi-Fi network, a sample e-invoice, a made-up receipt), taken from an isolated copy of QRPeek's data folder.
