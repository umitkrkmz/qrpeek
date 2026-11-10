# QRPeek — Gizlilik Politikası / Privacy Policy

**[🇹🇷 Türkçe](#türkçe) · [🇬🇧 English](#english)**

Son güncelleme / Last updated: 2026-11-10

---

## Türkçe

QRPeek, kendi bilgisayarınızda çalışan; ekrandaki QR kodlarını okuyan ve QR kodu oluşturan bir Windows uygulamasıdır. Bu politika Microsoft Store sürümü ve GitHub sürümü için geçerlidir.

### Kısaca

- **Hesap yok, telemetri yok, reklam yok, analiz yok.** QRPeek'in, geliştiricinin işlettiği bir sunucusu yoktur; uygulama size ait hiçbir veriyi geliştiriciye göndermez.
- Ekran görüntüleri yalnızca bellekte işlenir; **kaydedilmez, hiçbir yere gönderilmez.**
- Tüm veriler **sizin cihazınızda** kalır.

### Ekran ve pano

- **Ekranı tara / Alan seç:** QRPeek, siz istediğinizde ekranın bir görüntüsünü alır, içindeki QR kodlarını bellekte çözer ve görüntüyü hemen atar.
- **Ekranı izleme modu** (varsayılan olarak kapalı, siz açarsanız): ekran 1,5 saniyede bir yalnızca bu bilgisayarda incelenir. Görüntüler saklanmaz; yalnızca bulunan kodun metni size gösterilir.
- **Pano:** okunan kod tek ise ve ayar açıksa panoya kopyalanır. Wi-Fi şifreleri ve 2FA anahtarları **asla otomatik kopyalanmaz**; bunları kendiniz kopyaladığınızda Windows pano geçmişine (Win+V) ve bulut eşitlemesine girmemeleri için işaretlenir.

### Cihazınızda tutulan veriler

Uygulama yalnızca çalışması için gerekenleri `%LOCALAPPDATA%\QRPeek` klasöründe saklar:

- **Ayarlar** (`settings.json`): kısayollar, dil, seçenekler.
- **Geçmiş** (`history.json`, ayarlardan kapatılabilir): okunan son 100 kodun metni. **2FA kodları hiç yazılmaz, Wi-Fi kodları şifresiz yazılır.** Geçmişi kapattığınızda dosya silinir.
- **Hata günlüğü** (`error.log`): yalnızca bir hata olursa, teknik ayrıntılarla.

Microsoft Store sürümünde bu klasör Windows'un uygulamaya ayırdığı yerdedir ve **uygulamayı kaldırdığınızda silinir**. Oluşturduğunuz QR görselleri ve dışa aktardığınız CSV dosyaları sizin seçtiğiniz yere kaydedilir.

### Uygulamanın kurduğu ağ bağlantıları

QRPeek kendi başına internete çıkmaz. Ağ yalnızca şu durumlarda kullanılır:

- **"Gerçek hedefi göster"** (bir bağlantı kartında siz bastığınızda): kısaltılmış bir bağlantının nereye gittiğini bulmak için o bağlantının sunucusuna istek gönderilir. Çerez gönderilmez, sayfa açılmaz. Her internet isteğinde olduğu gibi o sunucu IP adresinizi görebilir.
- **"Bağlantıyı aç"** (siz bastığınızda): bağlantı varsayılan tarayıcınızda açılır; o sitenin gizlilik politikası geçerlidir.
- **Sürüm kontrolü** (yalnızca GitHub sürümünde, ayarlardan kapatılabilir): günde en fazla bir kez GitHub'a (`api.github.com`) yeni sürüm olup olmadığı sorulur. İstekte kişisel bilgi yoktur. Microsoft Store sürümü bunu yapmaz; güncellemeleri Store getirir.
- **Telefona gönder** (yalnızca gönderme penceresi açıkken): dosyalar **yalnızca yerel ağınız üzerinden**, tahmin edilemeyen tek kullanımlık bir adresle paylaşılır. Bu adres en fazla 10 dakika geçerlidir ve internete açılmaz. Kısa metinler ve linkler ağ kullanılmadan doğrudan QR'a dönüştürülür.
- **Wi-Fi'a bağlan** (siz bastığınızda): QR'daki ağın profili Windows'a eklenir ve Windows bu ağa bağlanır.

### Paylaşım

Geliştirici hiçbir veri toplamaz; dolayısıyla satmaz, kiralamaz veya üçüncü taraflarla paylaşmaz. Microsoft Store sürümünü satın aldığınızda ödeme ve lisans işlemlerini Microsoft yürütür; bu bilgiler Microsoft'un gizlilik bildirimine tabidir ve geliştiriciyle kişisel olarak paylaşılmaz.

### Çocuklar

QRPeek çocuklara yönelik değildir ve kimseden bilerek kişisel veri toplamaz.

### Değişiklikler ve iletişim

Bu politika değişirse yeni hâli bu sayfada, tarihiyle yayımlanır. Sorularınız için: [GitHub üzerinden iletişim](https://github.com/umitkrkmz/qrpeek/issues/new/choose). Kaynak kodu açıktır; yukarıdakilerin hepsini [depoda](https://github.com/umitkrkmz/qrpeek) doğrulayabilirsiniz.

---

## English

QRPeek is a Windows app that runs on your own computer; it reads QR codes on your screen and creates QR codes. This policy covers the Microsoft Store version and the GitHub version.

### In short

- **No account, no telemetry, no ads, no analytics.** QRPeek has no server run by the developer; the app sends none of your data to the developer.
- Screen captures are processed in memory only; **they are never saved or sent anywhere.**
- All data stays **on your device**.

### Screen and clipboard

- **Scan screen / Select area:** when you ask, QRPeek takes a picture of the screen, decodes the QR codes in it in memory and discards the picture right away.
- **Watch mode** (off by default, only if you turn it on): the screen is checked every 1.5 seconds on this PC only. No pictures are kept; only the text of a code it finds is shown to you.
- **Clipboard:** when exactly one code is found and the setting is on, it is copied to the clipboard. Wi-Fi passwords and 2FA keys are **never copied automatically**; when you copy them yourself, they are marked so Windows clipboard history (Win+V) and cloud sync skip them.

### Data kept on your device

The app stores only what it needs to work, in `%LOCALAPPDATA%\QRPeek`:

- **Settings** (`settings.json`): shortcuts, language, options.
- **History** (`history.json`, can be turned off in Settings): the text of the last 100 codes read. **2FA codes are never written; Wi-Fi codes are written without the password.** Turning history off deletes the file.
- **Error log** (`error.log`): only if something goes wrong, with technical details.

In the Microsoft Store version this folder is the one Windows sets aside for the app and **is deleted when you uninstall it**. QR images you create and CSV files you export are saved where you choose.

### Network connections the app makes

QRPeek does not go online on its own. The network is used only in these cases:

- **"Show real destination"** (when you press it on a link card): a request is sent to that link's server to find out where a shortened link leads. No cookies are sent and the page is not opened. As with any internet request, that server can see your IP address.
- **"Open link"** (when you press it): the link opens in your default browser; that site's privacy policy applies.
- **Version check** (GitHub version only, can be turned off in Settings): at most once a day, GitHub (`api.github.com`) is asked whether a newer version exists. The request carries no personal information. The Microsoft Store version doesn't do this; the Store delivers updates.
- **Send to phone** (only while the send window is open): files are shared **over your local network only**, through an unguessable one-off address that is valid for at most 10 minutes and is not exposed to the internet. Short text and links are turned into a QR directly, with no network at all.
- **Connect to Wi-Fi** (when you press it): the network profile from the QR is added to Windows and Windows connects to it.

### Sharing

The developer collects no data, so it is not sold, rented or shared with third parties. When you buy the Microsoft Store version, Microsoft handles payment and licensing; that information is covered by Microsoft's privacy statement and is not shared with the developer personally.

### Children

QRPeek is not directed at children and does not knowingly collect personal data from anyone.

### Changes and contact

If this policy changes, the new version is published on this page with its date. Questions: [contact via GitHub](https://github.com/umitkrkmz/qrpeek/issues/new/choose). The source code is open; you can verify all of the above in the [repository](https://github.com/umitkrkmz/qrpeek).
