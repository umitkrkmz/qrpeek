# AGENTS.md

For AI coding tools working in this repo. `README.md` (short) and the full guides `docs/README.tr.md` / `docs/README.en.md` are for users; keep the two guides in step when a feature changes.

## Ground rules

- **No commit or push without explicit approval at that moment.** An earlier approval doesn't carry over.
- **"Fixed" needs a test.** Prefer showing the test goes red without the fix.
- Destructive or irreversible operations are asked about first.

## Build / test

```bash
dotnet build -c Release
dotnet test
```

## Layout

- `src/QRPeek/Core/` - no UI: `ScreenCapture` (per-monitor GDI capture), `QrScanner` (ZXing, multi-code, inverted, upscaling for small crops), `OcrReader` + `OcrLayout` (Windows.Media.Ocr: the text of an area with no QR, rows/columns, digit fixes; never stored or auto-copied), `ReceiptTerms` (language-independent invoice label dictionary: picks the OCR language, glues split words, fixes label typos, never values), `ReceiptParser` + `ReceiptExport` (receipt card: label → value, tables, personal/secret fields; summary, CSV, Excel copy), `QrContent` (what a decoded text means + link warnings), `QrGenerator` (QRCoder data + style), `QrRenderer` (module/eye shapes, logo; one geometry for PNG and SVG), `WifiConnector` (wlanapi.dll: set profile, connect, wait for a real connection), `LanShare` (one-off local web server for send-to-phone, read-only), `LanReceive` + `ReceivedFiles` + `ReceivePage` (receive-from-phone: upload server, safe file saving, the phone's page), `LanHttp` (what both servers share), `LinkResolver` (follows redirects on demand), `ScreenWatcher` (watch mode), `InvoiceCsv` + `FlatJson` (e-Fatura), `SafeClipboard`, `Storage` (JSON in `%LOCALAPPDATA%\QRPeek`), `Hotkeys`, `Autostart`, `ImageUtil`, `Native`.
- `src/QRPeek/UI/` - WPF windows. `RegionSelector` is code-only (one overlay window per monitor).
- `src/QRPeek/App.xaml.cs` - tray icon, hotkey, the scan flow, single instance.
- `src/QRPeek/S.cs` - every user-facing string, Turkish and English side by side.
- `tests/QRPeek.Tests/` - xUnit, net48.

## Constraints (the design depends on these)

- **Target stays `net48`.** It ships with Windows 10 1903+ / 11, so the app stays at a few MB with nothing to install. A modern-.NET WPF build is 60+ MB. Don't retarget without asking.
- **New dependencies must be GPL-3.0 compatible and small.** Ask before adding one.
- **Every string goes through `S` with both `tr` and `en`.**
- **Nothing leaves the PC on its own.** No telemetry, no background network. The only network code is `LinkResolver` (runs when the user presses "Show real destination"), `LanShare` (local network only, while the send window is open), `LanReceive` (local network only, while the receive window is open) and `UpdateChecker` (GitHub build only, setting-controlled, at most once a day, opens only this repo's release links). Keep it that way.
- **`Autostart.StartupTaskId` must match the `<uap5:StartupTask TaskId>` in the MSIX manifest.** The Store build can't use the Run key.
- **Secrets stay out of history and the clipboard history.** `QrContent.ForHistory` decides what's stored (no 2FA, no Wi-Fi passwords); copies of secrets go through `SafeClipboard.SetText(..., sensitive: true)`. `HistoryPrivacyTests` guards this.
- **`LanShare` serves only the listed files behind the random token, read-only.** Never add directory listing, uploads or path-based file access to it.
- **`LanReceive` writes nothing the user didn't approve.** It exists only while the receive window is open (behind the random token, closes when idle); the first device holds the session and every later request needs its key; the phone sends a list first and uploads only after the user accepts that exact list on the PC; each upload must match its announced size; files go only into the receive folder under `ReceivedFiles.SafeName`, never overwrite, get the Zone.Identifier (internet) mark, and are never opened. Never add directory listing, reading files back, client-supplied paths, or auto-accept. `ReceiveTests` and `ReceivedFileNameTests` guard this.
- **The receipt card never lets a secret out.** Product keys and card numbers (Luhn-valid) are `ReceiptField.Secret`: shown masked, never tickable, and `ReceiptExport` leaves them out even if ticked. Personal fields (buyer, e-mail, payment) start unticked. `ReceiptParserTests` guards this.
- **`QrContent.OpenTarget` only ever holds http(s), mailto, tel or sms.** A QR code must never be able to launch `file:`, `javascript:`, `ms-settings:` or any other scheme. `ContentTests.Never_offers_to_open_anything_but_safe_schemes` guards this.
- **Every generator style must stay readable.** `StyleTests` decodes every pattern/eye combination and every logo size; add a case when you add a style.
- **Wi-Fi goes through wlanapi, not netsh.** netsh can't overwrite a network saved for all users and its "connect" doesn't confirm anything.
- **The app is per-monitor DPI aware (`app.manifest`).** Capture and overlays work in physical pixels; keep it that way or small QR codes get blurred.
- **Don't declare `System.Windows.Forms.ApplicationConfigurationSection` in `App.config` `<configSections>`.** It's built into machine.config, and declaring it breaks the config system at runtime (`DataContractJsonSerializer` fails).
