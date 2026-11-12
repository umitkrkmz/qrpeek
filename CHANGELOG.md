# Changelog

Bu dosya QRPeek'teki önemli değişiklikleri sürüm bazında listeler.
This file lists notable changes to QRPeek, by release.

---

## [Türkçe](#türkçe) · [English](#english)

---

## Türkçe

### [Unreleased] — 0.3.0

**Yeni**
- **QR yoksa yazıyı kopyala:** seçilen alanda ya da görselde (sürükle-bırak, `Ctrl + V`, dosya) QR kodu bulunamazsa içindeki yazı Windows'un yerleşik OCR'ıyla okunur ve düzenlenebilir bir kartta gösterilir; "Metni kopyala" ile alınır. Ek indirme yok, internet yok; dil Windows'ta yüklü dillere göre seçilir. Okunan yazı geçmişe yazılmaz, kendiliğinden kopyalanmaz. Aynı satırdaki bilgiler (ör. "Sipariş Toplamı ... USD 1.99") bir arada kalır, sütunlar sekmeyle ayrılır; Excel'e yapıştırınca hücrelere bölünür. Seçimin kenarına yapışık harfler ve ince tek satırlar da okunur. Bağlamdan rakam düzeltme ("II Kasım" → "11 Kasım"). "QR yap" düğmesi metni QR oluşturucuda açar. Ekran yazısı okunmadan önce en az 2 kat büyütülür (küçük gri tablo başlıkları, "$20.00" gibi tutarlar); başka bir OCR dili kazansa da isimlerdeki Türkçe harfler (ü, ş, ğ, İ…) korunur. Görüntü 3 kat büyüklükte de okunur ve yalnızca boş kalan yerler doldurulur (ör. soluk "Qty" başlığı). Windows OCR'ın tanımadığı ₺ ("b179,99" → "₺179,99"), sipariş numaralarındaki O/0, renkli ikonların harf sanılması ve bir okumanın bozup diğerinin doğru okuduğu altı çizili e-posta linkleri ile tutarlar düzeltildi; e-postalarda bilinen sağlayıcının (gmail.com…) adresi tercih edilir, "Arnount" gibi rn/m karışmaları başlıklarda düzelir. Tablodaki "20%" "2096" (ya da küçük yazıda "207/2") okunursa, aynı oran metnin başka yerinde doğru okunduysa ya da hücre "Vergi/Tax" sütunundaysa "20%" olarak düzelir; yıl ya da model numarası gibi sayılara dokunulmaz. Kartta kırmızı "Kontrol edin" uyarısı.
- **Fatura/makbuz sözlüğü:** OCR metni, faturaların standart başlıklarıyla (Invoice number, Amount paid, KDV, Ara toplam…) karşılaştırılır. Bölünen sözcükler birleşir ("Invoice n um ber" → "Invoice number"), başlıklardaki küçük harf hataları düzelir ("Total excludng tax" → "Total excluding tax"); isim, adres, numara gibi değerlere dokunulmaz. Windows'ta birden çok OCR dili varsa görüntü hepsiyle okunur, başlıkları en iyi tanıyan sonuç seçilir. Sözlük dil bağımsızdır; TR ve EN yerleşik gelir. Ayarlar → Fatura sözlüğü'nden başka diller ya da kendi başlıklarınız eklenir, düzenlenir, .txt olarak içe ve dışa aktarılır; yazarken bozuk satırlar ve yanlış alan adları gösterilir. "AI için komutu kopyala", istediğiniz bir AI'dan başka bir dil için liste istemek üzere hazır bir komut verir (QRPeek kendisi hiçbir yere bağlanmaz). AI yanıtı olduğu gibi yapıştırılabilir; kod bloğu ve madde işaretleri yok sayılır.
- **Makbuz kartı:** okunan yazı bir makbuz ya da faturaysa satıcı, belge no, tarih, kalemler, ara toplam, KDV, toplam, alıcı ve ödeme ayrı alanlarda gösterilir (fatura sözlüğüyle; başka diller kullanıcı terimleriyle). Değerler düzenlenebilir, onay kutularıyla seçilir; seçimler hatırlanır. "Özeti kopyala", "Excel için kopyala", "CSV kaydet" ve "Özet QR" yalnızca işaretli alanları kullanır. Alıcı, e-posta ve ödeme bilgisi işaretsiz gelir; ürün anahtarı ve kart numarası maskelenir, işaretlenemez ve hiçbir çıktıya girmez. Özet, resmî belge olmadığını belirtir. Birden çok belge no ya da tarih varsa ("Invoice number", "Receipt number") hepsi kendi etiketiyle gösterilir; "Oct 26–Nov 26, 2026" gibi dönemler Dönem alanına gelir. Makbuz ve tam metin iki sekmede, düğmeler hep görünür. QRPeek'in özet QR'ı taranınca (başka bir bilgisayarda, başka dilde yapılmış olsa da) yeniden makbuz kartı olarak açılır; CSV ve Excel düğmeleri hazır gelir. Elle yapılmış bir "özet" de aynı kurallardan geçer: ürün anahtarı alınmaz, kart numarası maskelenir.
- **Ayarlar bölümlere ayrıldı:** soldaki menüden Kısayollar, Genel, Dil, Yazı tanıma ve Hakkında; son açılan bölüm hatırlanır. Pencere hiçbir ekranda taşmaz (eskiden 1080p'de alt kısım görev çubuğunun altında kalıyordu).

**Düzeltmeler**
- "Gerçek hedefi göster", bir site otomatik denetimi engellediğinde (ör. Cloudflare bot denetimi, HTTP 403) yanlışlıkla "Yönlendirme yok" diyordu. Artık "Bu site otomatik denetimi engelledi, gerçek hedef doğrulanamadı" uyarısı çıkıyor; yönlendirmenin ortasında engellenirse yalnızca "Şuraya kadar izlenebildi" gösteriliyor.

**Belgeler**
- README kısaldı; tüm özellikler, kısayollar ve ayarlar artık ayrı kılavuzlarda: [Türkçe](docs/README.tr.md) ve [English](docs/README.en.md). Microsoft Store rozeti eklendi, ekran görüntüleri yenilendi (makbuz kartı, Ayarlar).

### [0.2.0] — 2026-11-11

**Telefondan al**
- Telefondan ya da tabletten bilgisayara fotoğraf ve dosya: cihaza uygulama kurmadan, QR'ı okutup tarayıcıdan seçerek. Bildirim alanında "Telefondan al", gönderme penceresinde bir düğme ya da isteğe bağlı kısayol.
- Bilgisayarda bağlanan cihaz (ör. "iPhone · Safari", IP) ve iki ekranda aynı görünen 4 haneli eşleşme kodu.
- Telefon önce yalnızca dosya listesini yollar; bilgisayarda **Kabul et / Reddet** sorulur, onay olmadan hiçbir şey yazılmaz. Program ya da betik dosyaları (.exe, .bat, .ps1…) için ayrıca uyarı.
- Oturum ilk bağlanan cihaza kilitlenir; QR'ı başka biri okutsa giremez. İşlem olmazsa 10 dakikada kapanır.
- Dosyalar İndirilenler\QRPeek'e (değiştirilebilir) kaydedilir: dosya adı temizlenir (klasör yolu, `:`, CON gibi adlar, ters yazı hileleri), hiçbir dosyanın üstüne yazılmaz, "internetten indirildi" olarak işaretlenir ve hiçbiri kendiliğinden açılmaz. Yarıda kalan aktarımın yarım dosyası silinir.
- Tek seferde en fazla 100 dosya ve toplam 4 GB; diskte yer yoksa kabul edilemez.

**Ekrandan okuma**
- Birden çok ekran varken sonuç kartı kodun nerede bulunduğunu söyler: "Ana ekranda", "Soldaki ekranda", "Sağdaki ekranda"…

**Arayüz**
- Ana penceredeki düğme "Telefona gönder / al" oldu.
- Gönderme penceresi içeriği panodan kendiliğinden aldığında bunu belirtir ("Panodan alındı").

**Diğer**
- Birden çok ekran ve ekranlar arasında farklı ölçek (%100 + %150, kablosuz bağlı tablet) denendi; tarama, görüntü netliği ve pencere yerleşimi sorunsuz. README'deki "denenmedi" notu kaldırıldı.
- "Yaklaşık 1 MB" ifadesi belgelerden kaldırıldı; QRPeek küçük kalıyor ama boyut sabit bir sayı değil.

### [0.1.0] — 2026-11-11 (ilk sürüm)

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
- Windows ile başlat: GitHub sürümünde Run anahtarı, Store sürümünde paket StartupTask'ı (Windows Ayarlar → Başlangıç ile iki yönlü).
- GitHub sürümünde günde en fazla bir kez sürüm kontrolü (kapatılabilir); yalnızca bu deponun sürüm sayfası açılır.
- Türkçe ve İngilizce, Windows'un açık/koyu temasını izler; yaklaşık 1 MB, .NET Framework 4.8.

**Bilinen kısıtlar**
- Çoklu monitör, farklı ölçekleme ve ARM64 bilgisayarlar henüz test edilmedi.

---

## English

### [Unreleased] — 0.3.0

**New**
- **No QR? Copy the text:** if the selected area or image (drag and drop, `Ctrl + V`, file) has no QR code, its text is read with the OCR built into Windows and shown in an editable card; "Copy text" takes it. Nothing to download, no internet; the language follows those installed in Windows. The text isn't written to history or copied on its own. Items on the same row (e.g. "Order total ... USD 1.99") stay together, with columns separated by tabs, so it pastes into Excel as cells. Letters touching the edge of the selection and thin single lines are read too. Digits fixed from context ("II Kasım" → "11 Kasım"). "Make QR" opens the text in the QR generator. Screen text is enlarged at least 2x before reading (small gray table headers, amounts like "$20.00"); when another OCR language wins, Turkish letters in names (ü, ş, ğ, İ…) are kept. The image is also read at 3x, filling only the gaps (e.g. a faint "Qty" header). Fixed: ₺, which Windows OCR doesn't know ("b179,99" → "₺179,99"), O/0 in order numbers, colored icons read as letters, and underlined e-mail links or amounts that one reading breaks while another reads them right; a known mail provider's address (gmail.com…) is preferred, and rn/m mix-ups such as "Arnount" are fixed in labels. A table's "20%" read as "2096" (or "207/2" in small type) is fixed when the same rate is read right elsewhere in the text or the cell is under a "Tax" column; numbers such as years or model numbers are left alone. A red "Check it" warning on the card.
- **Receipt term dictionary:** OCR text is checked against the standard labels of invoices (Invoice number, Amount paid, VAT, Subtotal…). Split words are joined ("Invoice n um ber" → "Invoice number"), small typos in labels are fixed ("Total excludng tax" → "Total excluding tax"); values such as names, addresses and numbers are never touched. With several OCR languages in Windows, the image is read with each and the reading that knows the most labels wins. The dictionary isn't tied to a language; Turkish and English are built in. Under Settings → Receipt terms you can add other languages or your own labels, edit them, and import or export them as .txt; bad lines and unknown field names are shown as you type. "Copy prompt for an AI" gives a ready-made prompt to ask an AI of your choice for another language's list (QRPeek itself doesn't connect anywhere). An AI's answer can be pasted as it is; code blocks and bullet points are ignored.
- **Receipt card:** when the text is a receipt or invoice, seller, number, date, items, subtotal, tax, total, buyer and payment are shown as separate fields (via the receipt terms; other languages via your own terms). Values can be edited and are picked with checkboxes; your picks are remembered. "Copy summary", "Copy for Excel", "Save CSV" and "Summary QR" use only the ticked fields. Buyer, e-mail and payment start unticked; product keys and card numbers are masked, can't be ticked and never get into any output. The summary says it's not an official document. Several numbers or dates ("Invoice number", "Receipt number") are all shown with their own labels; ranges like "Oct 26–Nov 26, 2026" go to Period. Receipt and full text are two tabs, with the buttons always in view. Scanning a QRPeek summary QR (even one made on another PC, in the other language) opens it as a receipt card again, with the CSV and Excel buttons. A hand-made "summary" goes through the same rules: product keys are dropped, card numbers masked.
- **Settings in sections:** Shortcuts, General, Language, Text recognition and About from a menu on the left; the last section is remembered. The window fits every screen (on 1080p its bottom used to go under the taskbar).

**Fixes**
- "Show real destination" said "No redirects" when a site blocked the automatic check (e.g. a Cloudflare bot check, HTTP 403). It now warns that the site blocked the check and the real destination couldn't be verified; if it's blocked midway, it only shows how far it got.

**Docs**
- The README is shorter; every feature, shortcut and setting is now in separate guides: [Türkçe](docs/README.tr.md) and [English](docs/README.en.md). Added the Microsoft Store badge and new screenshots (receipt card, Settings).

### [0.2.0] — 2026-11-11

**Receive from phone**
- Photos and files from a phone or tablet to the PC, with nothing to install on the device: scan the QR and pick the files in the browser. "Receive from phone" in the notification area, a button in the send window, or an optional shortcut.
- The PC shows which device connected (e.g. "iPhone · Safari", IP) and a 4-digit match code that appears on both screens.
- The phone first sends only the list of files; the PC asks **Accept / Reject** and nothing is written without approval. Extra warning for programs and scripts (.exe, .bat, .ps1…).
- The session locks to the first device that connects; anyone else scanning the QR can't join. It closes after 10 idle minutes.
- Files are saved to Downloads\QRPeek (changeable): names are cleaned up (folder paths, `:`, names like CON, right-to-left tricks), nothing is ever overwritten, files are marked as downloaded from the internet and nothing opens on its own. An interrupted transfer's partial file is deleted.
- At most 100 files and 4 GB in total at once; a list can't be accepted without enough disk space.

**Reading from the screen**
- With more than one screen, the result card says where the code was: "On the main screen", "On the left screen", "On the right screen"…

**Interface**
- The main window button is now "Phone: send / receive".
- The send window says when it picked up the clipboard by itself ("From your clipboard").

**Other**
- Tried with several screens and mixed scaling between them (100% + 150%, a wirelessly connected tablet): scanning, sharpness and window placement are fine. The "untested" note is gone from the README.
- Dropped "about 1 MB" from the docs; QRPeek stays small, but the size isn't a fixed number.

### [0.1.0] — 2026-11-11 (first release)

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
- Start with Windows: the Run key in the GitHub build, the package StartupTask in the Store build (stays in sync with Windows Settings → Startup).
- GitHub build: version check at most once a day (can be turned off); only this repository's release page is opened.
- Turkish and English, follows the Windows light/dark theme; about 1 MB, .NET Framework 4.8.

**Known limitations**
- Multiple monitors, mixed scaling and ARM64 PCs are untested.
