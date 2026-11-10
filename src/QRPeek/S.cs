// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace QRPeek
{
    /// <summary>
    /// Every user-facing string, Turkish and English side by side so neither
    /// language can fall behind. XAML uses {x:Static local:S.Name}.
    /// Call Init once at startup, before any window is created.
    /// </summary>
    public static class S
    {
        private static bool _tr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

        public static bool IsTurkish => _tr;

        /// <param name="language">"auto", "tr" or "en".</param>
        public static void Init(string language)
        {
            if (language == "tr") _tr = true;
            else if (language == "en") _tr = false;
            else _tr = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        }

        private static string T(string tr, string en) => _tr ? tr : en;

        public const string AppName = "QRPeek";

        // Main window
        public static string Tagline => T("Ekrandaki QR kodlarını okuyun, yeni QR kodları oluşturun", "Read QR codes on your screen, create new ones");
        public static string ScanScreen => T("Ekranı tara", "Scan screen");
        public static string ScanScreenHint => T("Tüm ekranlardaki QR kodlarını bulur", "Finds QR codes on all your screens");
        public static string SelectRegion => T("Alan seç", "Select area");
        public static string FromImage => T("Görselden oku", "From image");
        public static string CreateQr => T("QR oluştur", "Create QR");
        public static string DropHint => T("Bir görseli buraya bırakın ya da Ctrl+V ile yapıştırın", "Drop an image here or paste it with Ctrl+V");
        public static string History => T("Geçmiş", "History");
        public static string HistoryEmpty => T("Henüz okunan bir kod yok.", "No codes read yet.");
        public static string HistoryOff => T("Geçmiş kapalı (Ayarlar).", "History is off (Settings).");
        public static string ClearHistory => T("Temizle", "Clear");
        public static string Settings => T("Ayarlar", "Settings");
        public static string WelcomeTitle => T("Hoş geldin! QRPeek'i en hızlı şöyle kullanırsın:", "Welcome! The quickest way to use QRPeek:");
        public static string WelcomeScan => T("Ekrandaki QR kodlarını okur; hangi uygulamada olursan ol.", "Reads the QR codes on your screen, whatever app you're in.");
        public static string WelcomeSend => T("Kopyaladığın link, metin, görsel ya da dosyayı telefona gönderir.", "Sends the link, text, image or file you copied to your phone.");
        public static string WelcomeTray => T("Pencereyi kapatınca QRPeek bildirim alanında çalışmaya devam eder; kısayollar her zaman hazırdır. Ayarlardan değiştirebilirsin.",
                                              "Closing the window keeps QRPeek running in the notification area, so the shortcuts always work. You can change them in Settings.");
        public static string WelcomeOk => T("Anladım", "Got it");

        // Results
        public static string NothingFound => T("QR kodu bulunamadı", "No QR code found");
        public static string NothingFoundHint => T("Kod çok küçük, çok yoğun ya da kısmen kapalı olabilir. Sayfayı yakınlaştırıp (Ctrl + fare tekerleği) ya da kodun etrafını seçerek tekrar deneyin.",
                                                   "The code may be too small, too dense or partly covered. Zoom in on the page (Ctrl + mouse wheel) or select the area around the code and try again.");
        public static string FoundCount(int n) => n == 1 ? T("1 QR kodu bulundu", "1 QR code found") : string.Format(T("{0} QR kodu bulundu", "{0} QR codes found"), n);
        public static string Copy => T("Kopyala", "Copy");
        public static string Copied => T("Kopyalandı", "Copied");
        public static string AutoCopied => T("Panoya kopyalandı", "Copied to clipboard");
        public static string OpenLink => T("Bağlantıyı aç", "Open link");
        public static string Open => T("Aç", "Open");
        public static string GoesTo => T("Gideceği site", "Goes to");
        public static string Connect => T("Bağlan", "Connect");
        public static string Connecting => T("Bağlanıyor…", "Connecting…");
        public static string Connected => T("Bağlandı", "Connected");
        public static string ConnectFailed => T("Bağlanılamadı. Ağ menzil dışında ya da şifre yanlış olabilir. Şifreyi kopyalayıp elle de bağlanabilirsiniz.",
                                                "Couldn't connect. The network may be out of range or the password may be wrong. You can also copy the password and connect manually.");
        public static string NoWifiAdapter => T("Bu bilgisayarda kullanılabilir bir Wi-Fi bağdaştırıcısı bulunamadı.", "No usable Wi-Fi adapter was found on this PC.");
        public static string CopyPassword => T("Şifreyi kopyala", "Copy password");
        public static string SaveVcf => T("Kişiyi kaydet (.vcf)", "Save contact (.vcf)");
        public static string Close => T("Kapat", "Close");

        public static string KindText => T("Metin", "Text");
        public static string KindUrl => T("Bağlantı", "Link");
        public static string KindWifi => "Wi-Fi";
        public static string KindEmail => T("E-posta", "Email");
        public static string KindPhone => T("Telefon", "Phone");
        public static string KindSms => "SMS";
        public static string KindContact => T("Kişi", "Contact");
        public static string KindLocation => T("Konum", "Location");

        public static string WarnHttp => T("Bağlantı şifreli değil (https yerine http).", "This link is not encrypted (http, not https).");
        public static string WarnIpHost => T("Alan adı yerine çıplak bir IP adresi kullanıyor.", "It uses a bare IP address instead of a domain name.");
        public static string WarnIdn => T("Alan adında Latin dışı harfler var; tanıdık bir siteyi taklit ediyor olabilir.", "The domain has non-Latin letters and may be imitating a familiar site.");
        public static string WarnUserInfo => T("Adreste '@' var; asıl site @ işaretinden sonraki kısımdır.", "The address contains '@'; the real site is the part after it.");
        public static string WarnShortener => T("Kısaltılmış bağlantı: gerçek hedef ancak açınca belli olur.", "Shortened link: the real destination only shows once you open it.");

        // Overlay
        public static string OverlayHint => T("QR kodun etrafını sürükleyerek seçin  ·  Esc: iptal", "Drag around the QR code  ·  Esc: cancel");
        public static string OverlayHintNotFound => T("Otomatik bulunamadı. QR kodun etrafını sürükleyerek seçin  ·  Esc: iptal",
                                                      "Nothing found automatically. Drag around the QR code  ·  Esc: cancel");

        // Generator
        public static string GenTabText => T("Metin / Bağlantı", "Text / Link");
        public static string GenTabWifi => "Wi-Fi";
        public static string GenTextLabel => T("Bağlantı veya metin", "Link or text");
        public static string WifiNetwork => T("Ağ adı", "Network name");
        public static string WifiPassword => T("Şifre", "Password");
        public static string WifiSecurity => T("Güvenlik", "Security");
        public static string WifiHidden => T("Gizli ağ", "Hidden network");
        public static string SecNone => T("Şifresiz", "None");
        public static string GenColor => T("Renk", "Color");
        public static string GenEcc => T("Hata düzeltme", "Error correction");
        public static string EccL => T("Düşük (L)", "Low (L)");
        public static string EccM => T("Orta (M)", "Medium (M)");
        public static string EccQ => T("Yüksek (Q)", "High (Q)");
        public static string EccH => T("En yüksek (H)", "Highest (H)");
        public static string GenSize => T("Dışa aktarma boyutu", "Export size");
        public static string GenPattern => T("Desen", "Pattern");
        public static string PatSquare => T("Kare", "Square");
        public static string PatRounded => T("Yuvarlak", "Rounded");
        public static string PatDots => T("Nokta", "Dots");
        public static string GenEyes => T("Köşe işaretleri", "Corner markers");
        public static string EyeCircle => T("Daire", "Circle");
        public static string GenLogo => T("Ortaya logo", "Center logo");
        public static string LogoNone => T("Yok", "None");
        public static string LogoApp => T("QRPeek logosu", "QRPeek logo");
        public static string LogoCustom => T("Kendi logom…", "My own logo…");
        public static string ChangeLogo => T("Değiştir…", "Change…");
        public static string LogoSmall => T("Küçük", "Small");
        public static string LogoMedium => T("Orta", "Medium");
        public static string LogoLarge => T("Büyük", "Large");
        public static string LogoEccNote => T("Logo kodun bir kısmını kapattığı için en yüksek hata düzeltme (H) kullanılır.",
                                              "A logo covers part of the code, so the highest error correction (H) is used.");
        public static string CheckOk => T("Okunabilir: QRPeek bu kodu doğruladı", "Readable: QRPeek verified this code");
        public static string CheckFail => T("QRPeek bu kodu okuyamadı. Logoyu küçültün ya da daha sade bir desen seçin.",
                                            "QRPeek couldn't read this code. Make the logo smaller or pick a simpler pattern.");
        public static string Checking => T("Okunabilirlik denetleniyor…", "Checking readability…");
        public static string CopyImage => T("Görseli kopyala", "Copy image");
        public static string SavePng => T("PNG kaydet", "Save PNG");
        public static string SaveSvg => T("SVG kaydet", "Save SVG");
        public static string GenEmpty => T("Önizleme burada görünecek", "The preview appears here");
        public static string GenTooLong => T("Bu metin bir QR koduna sığmayacak kadar uzun.", "This text is too long for a QR code.");
        public static string Saved => T("Kaydedildi", "Saved");

        // Settings
        public static string SetHotkey => T("Ekranı tarama kısayolu", "Scan screen shortcut");
        public static string HotkeyNone => T("Kapalı", "Off");
        public static string HotkeyTaken => T("Bu kısayolu başka bir uygulama kullanıyor. Başka birini seçin.", "Another app is using this shortcut. Pick a different one.");
        public static string SetAutoCopy => T("Tek kod bulununca panoya otomatik kopyala", "Copy to the clipboard automatically when one code is found");
        public static string SetHistory => T("Okunan kodların geçmişini tut (yalnızca bu bilgisayarda)", "Keep a history of read codes (on this PC only)");
        public static string SetStartup => T("Windows açılınca arka planda başlat", "Start in the background when Windows starts");
        public static string StartupBlockedByUser => T("Windows ile başlatma Görev Yöneticisi'nde (Başlangıç uygulamaları) kapatılmış; yalnızca oradan açılabilir.",
                                                       "Starting with Windows was turned off in Task Manager (Startup apps); it can only be turned back on there.");
        public static string StartupBlockedByPolicy => T("Bu bilgisayarın yönetici ilkesi Windows ile başlatmaya izin vermiyor.", "This PC's administrator policy doesn't allow starting with Windows.");
        public static string SetCheckUpdates => T("Yeni sürümleri denetle (günde bir kez GitHub'a sorar)", "Check for new versions (asks GitHub once a day)");
        public static string CheckUpdatesNow => T("Güncellemeleri denetle", "Check for updates");
        public static string UpdateChecking => T("Denetleniyor…", "Checking…");
        public static string UpToDate(string v) => string.Format(T("QRPeek güncel (v{0}).", "QRPeek is up to date (v{0})."), v);
        public static string UpdateAvailable(string v) => string.Format(T("Yeni sürüm var: v{0}", "A new version is available: v{0}"), v);
        public static string UpdateFailed => T("GitHub'a ulaşılamadı, daha sonra tekrar deneyin.", "Couldn't reach GitHub; try again later.");
        public static string Download => T("İndir", "Download");
        public static string SetLanguage => T("Dil", "Language");
        public static string LangAuto => T("Sistem dili", "System language");
        public static string LangRestart => T("Dil değişikliği QRPeek yeniden açılınca geçerli olur.", "The language change applies when QRPeek is reopened.");
        public static string About => T("Hakkında", "About");
        public static string AboutText => T("QRPeek ücretsiz ve açık kaynaklıdır (GPL-3.0-or-later). Her şey bu bilgisayarda işlenir, hiçbir veri toplanmaz. QRPeek internete yalnızca “Gerçek hedefi göster”e bastığınızda ve (GitHub sürümünde, ayar açıksa) günde bir kez yeni sürüm denetimi için çıkar; “Telefona gönder” sadece yerel ağınızı kullanır.",
                                            "QRPeek is free and open source (GPL-3.0-or-later). Everything is processed on this PC and no data is collected. QRPeek only goes online when you press “Show real destination” and (GitHub build, if enabled) once a day to check for a new version; “Send to phone” uses your local network only.");
        public static string SourceCode => T("Kaynak kod", "Source code");
        public static string ThirdParty => T("Üçüncü taraf lisansları", "Third-party licenses");
        public static string PrivacyPolicy => T("Gizlilik politikası", "Privacy policy");
        public static string Version(string v) => T("Sürüm ", "Version ") + v;
        public static string Quit => T("QRPeek'ten çık", "Quit QRPeek");

        // Tray and notices
        public static string TrayOpen => T("QRPeek'i aç", "Open QRPeek");
        public static string TrayClipboard => T("Panodaki görseli oku", "Read image on clipboard");
        public static string TrayHint(string hotkey) => string.Format(T("QRPeek arka planda çalışıyor. Ekranı taramak için {0}.", "QRPeek keeps running in the background. Press {0} to scan your screen."), hotkey);
        public static string TrayHintNoHotkey => T("QRPeek arka planda çalışıyor. Bildirim alanındaki simgeden ulaşabilirsiniz.", "QRPeek keeps running in the background. Find it in the notification area.");
        public static string ClipboardEmpty => T("Panoda görsel yok.", "There is no image on the clipboard.");
        public static string ImageOpenFailed => T("Görsel açılamadı.", "Couldn't open the image.");
        public static string ImageFilter => T("Görseller", "Images") + "|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.ico";
        // Send to phone (clipboard QR, LAN file sharing)
        public static string SendToPhone => T("Telefona gönder", "Send to phone");
        public static string SendScanHint => T("Telefonun kamerasıyla okutun", "Scan with your phone's camera");
        public static string SendEmpty => T("Panoda gönderilecek bir şey yok. Bir dosyayı buraya sürükleyin ya da seçin; veya bir metin, link, görsel kopyalayıp tekrar deneyin.",
                                            "Nothing to send on the clipboard. Drag a file here or choose one; or copy some text, a link or an image and try again.");
        public static string PickFiles => T("Dosya seç…", "Choose files…");
        public static string LanHint => T("Telefon bu bilgisayarla aynı Wi-Fi ağında olmalı. Windows izin sorarsa “Erişime izin ver”i seçin.",
                                          "The phone must be on the same Wi-Fi as this PC. If Windows asks, choose “Allow access”.");
        public static string LanWaiting => T("Telefonun bağlanması bekleniyor…", "Waiting for the phone…");
        public static string LanSending(int pct) => string.Format(T("Gönderiliyor… %{0}", "Sending… {0}%"), pct);
        public static string LanDone(string name) => string.Format(T("Gönderildi: {0}", "Sent: {0}"), name);
        public static string LanExpires(string mmss) => string.Format(T("{0} sonra kapanacak", "Closes in {0}"), mmss);
        public static string LanExpired => T("Paylaşım süresi doldu. Yeniden göndermek için dosyayı tekrar seçin.", "Sharing has ended. Choose the file again to resend.");
        public static string LanOffline => T("Bu bilgisayar bir ağa bağlı değil; dosya gönderilemiyor.", "This PC isn't connected to a network, so files can't be sent.");
        public static string LanFiles(int n, string size) => string.Format(T("{0} dosya · {1}", "{0} files · {1}"), n, size);
        public static string LanDownload => T("İndir", "Download");
        public static string LanPageHint => T("Dosyalar QRPeek ile doğrudan bilgisayardan geliyor; internete yüklenmedi.", "Sent straight from the PC with QRPeek; nothing was uploaded to the internet.");
        public static string TextTooLongForQr => T("Metin bir QR'a sığmayacak kadar uzun, .txt dosyası olarak gönderiliyor.", "The text is too long for a QR code, so it's sent as a .txt file.");
        public static string ClipboardFileName => T("pano", "clipboard");
        public static string SetSendHotkey => T("Panodakini telefona gönderme kısayolu", "Send clipboard to phone shortcut");

        // Link resolver
        public static string ShowRealTarget => T("Gerçek hedefi göster", "Show real destination");
        public static string Resolving => T("Yönlendirmeler izleniyor…", "Following redirects…");
        public static string ResolveNote => T("Bağlantının sunucusuna tek bir istek gönderilir; sayfa açılmaz.", "Sends a request to the link's server; the page itself is not opened.");
        public static string RealTarget => T("Gerçek hedef", "Real destination");
        public static string RedirectPath => T("Yönlendirme yolu", "Redirect path");
        public static string NoRedirect => T("Yönlendirme yok: bağlantı doğrudan bu siteye gidiyor.", "No redirects: the link goes straight to this site.");
        public static string OpenRealTarget => T("Gerçek hedefi aç", "Open real destination");
        public static string ResolveFailed(string why) => string.Format(T("Ulaşılamadı ({0}).", "Couldn't reach it ({0})."), why);
        public static string ResolveTimeout => T("zaman aşımı", "timed out");
        public static string ResolveNoDns => T("alan adı bulunamadı", "domain not found");
        public static string ResolveNoConnect => T("bağlantı kurulamadı", "connection failed");
        public static string ResolveTls => T("güvenli bağlantı kurulamadı", "secure connection failed");

        // 2FA
        public static string KindOtp => T("2FA kurulum kodu", "2FA setup code");
        public static string OtpWarning => T("Bu, bir hesabın iki adımlı doğrulama anahtarı. Telefonunuzdaki Authenticator uygulamasıyla okutun ve kimseyle paylaşmayın. Geçmişe kaydedilmedi.",
                                             "This is a two-factor key for an account. Scan it with the authenticator app on your phone and don't share it. It was not saved to history.");
        public static string OtpIssuer => T("Hizmet", "Service");
        public static string OtpAccount => T("Hesap", "Account");
        public static string OtpSecret => T("Gizli anahtar", "Secret key");
        public static string Show => T("Göster", "Show");
        public static string Hide => T("Gizle", "Hide");
        public static string CopySecret => T("Anahtarı kopyala", "Copy key");
        public static string PasswordNotStored => T("Güvenlik için şifre geçmişe kaydedilmez.", "For safety, the password isn't kept in history.");

        // e-Fatura
        public static string KindInvoice => T("e-Fatura", "e-Invoice (Turkey)");
        public static string InvNumber => T("Fatura no", "Invoice no");
        public static string InvDate => T("Tarih", "Date");
        public static string InvSeller => T("Satıcı VKN/TCKN", "Seller tax ID");
        public static string InvBuyer => T("Alıcı VKN/TCKN", "Buyer tax ID");
        public static string InvScenario => T("Senaryo", "Scenario");
        public static string InvType => T("Tip", "Type");
        public static string InvCurrency => T("Para birimi", "Currency");
        public static string InvSubtotal => T("Mal/hizmet toplamı", "Goods/services total");
        public static string InvVat => T("KDV", "VAT");
        public static string InvTaxIncluded => T("Vergiler dahil", "Tax included");
        public static string InvPayable => T("Ödenecek", "Amount due");
        public static string SaveCsv => T("CSV kaydet", "Save CSV");
        public static string ExportInvoices => T("Faturaları dışa aktar (CSV)", "Export invoices (CSV)");
        public static string CsvFilter => T("CSV (Excel)|*.csv", "CSV (Excel)|*.csv");

        // Watch mode
        public static string SetWatch => T("Ekranı izle: bir QR kodu belirince bildir", "Watch the screen: notify me when a QR code appears");
        public static string SetWatchNote => T("Ekran 1,5 saniyede bir yalnızca bu bilgisayarda incelenir; görüntü kaydedilmez, hiçbir yere gönderilmez.",
                                               "The screen is checked every 1.5 s on this PC only; nothing is saved or sent anywhere.");
        public static string TrayWatch => T("Ekranı izle", "Watch screen");
        public static string WatchFoundOne(string what) => string.Format(T("Ekranda QR kodu: {0}", "QR code on screen: {0}"), what);
        public static string WatchFoundMany(int n) => string.Format(T("Ekranda {0} QR kodu var", "{0} QR codes on screen"), n);
        public static string WatchClick => T("Görmek için tıklayın.", "Click to view.");

        public static string Error =>T("Beklenmeyen bir hata oluştu. Ayrıntılar error.log dosyasına yazıldı.", "Something went wrong. Details were written to error.log.");
    }
}
