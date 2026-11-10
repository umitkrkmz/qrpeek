// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using QRPeek.Core;
using QRPeek.UI;
using Forms = System.Windows.Forms;

namespace QRPeek
{
    /// <summary>
    /// QRPeek lives in the notification area. This class owns the tray icon,
    /// the global hotkeys, watch mode and the scan flow; windows are created
    /// on demand.
    /// </summary>
    public partial class App : Application
    {
        private const string InstanceMutexName = @"Local\QRPeek.Instance";
        private const string ShowEventName = @"Local\QRPeek.Show";
        private const int ScanHotkeyId = 1;
        private const int SendHotkeyId = 2;

        private Mutex _instanceMutex;
        private EventWaitHandle _showEvent;
        private Forms.NotifyIcon _tray;
        private Action _balloonClick;
        private HotkeyManager _hotkeys;
        private ScreenWatcher _watcher;
        private MainWindow _main;
        private ResultWindow _results;
        private GeneratorWindow _generator;
        private SettingsWindow _settings;
        private SendWindow _send;
        private volatile bool _busy;
        private volatile bool _showingOwnQr;
        private bool _restoreMainAfterScan;

        internal static App Instance => (App)Current;

        internal AppSettings Settings { get; private set; }

        internal List<HistoryEntry> History { get; private set; } = new List<HistoryEntry>();

        internal event Action HistoryChanged;

        internal event Action HotkeysChanged;

        internal HotkeyPreset Hotkey => HotkeyPreset.Find(Settings.Hotkey, AppSettings.ScanKey);

        internal HotkeyPreset SendHotkey => HotkeyPreset.Find(Settings.SendHotkey, AppSettings.SendKey);

        internal bool HotkeyActive { get; private set; }

        internal bool SendHotkeyActive { get; private set; }

        /// <summary>GitHub build: a newer release found by the update check, else null.</summary>
        internal UpdateInfo AvailableUpdate { get; private set; }

        internal event Action UpdateChanged;

        private DispatcherTimer _updateTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _instanceMutex = new Mutex(true, InstanceMutexName, out bool firstInstance);
            if (!firstInstance)
            {
                // Already running: bring that one forward and leave.
                try { EventWaitHandle.OpenExisting(ShowEventName).Set(); }
                catch (WaitHandleCannotBeOpenedException) { }
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnUnhandled;
            AppDomain.CurrentDomain.UnhandledException += (s, a) => Storage.LogError(a.ExceptionObject as Exception);

            Settings = Storage.LoadSettings();
            S.Init(Settings.Language);
            WindowTheme.Apply(this, WindowTheme.SystemPrefersDark());
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            if (Settings.KeepHistory) History = LoadHistorySanitized();

            _hotkeys = new HotkeyManager();
            _hotkeys.Pressed += id =>
            {
                if (id == ScanHotkeyId) ScanScreen(fromWindow: false);
                else if (id == SendHotkeyId) ShowSend();
            };

            CreateTray();
            ApplyHotkeys();
            ApplyWatch();
            StartUpdateChecks();
            ListenForSecondInstance();

            // --tray: GitHub build started by the Run key. Store build: started by its StartupTask.
            bool startHidden = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) || Autostart.LaunchedAtStartup();
            if (!startHidden) ShowMain();
            if ((!HotkeyActive && !Hotkey.IsNone) || (!SendHotkeyActive && !SendHotkey.IsNone)) Notify(S.HotkeyTaken);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _watcher?.Dispose();
            _hotkeys?.Dispose();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            _showEvent?.Dispose();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }

        // ---------------------------------------------------------------- scan flow

        /// <summary>
        /// Captures all monitors and decodes every QR code. If nothing is found
        /// the frozen screenshot opens as a region selector, so the user can
        /// point at a code that was too small to find on its own.
        /// </summary>
        internal async void ScanScreen(bool fromWindow)
        {
            if (_busy) return;
            _busy = true;
            List<MonitorShot> shots = null;
            try
            {
                bool hidSomething = HideOwnWindows(hideMain: fromWindow);
                if (hidSomething) await Task.Delay(250); // let DWM repaint what was under our windows

                shots = ScreenCapture.CaptureAll();
                List<MonitorShot> captured = shots;
                List<string> texts = await Task.Run(() => captured.SelectMany(s => QrScanner.Decode(s.Image)).Distinct().ToList());

                if (texts.Count > 0)
                {
                    ShowResults(texts);
                }
                else
                {
                    RegionSelector.Show(shots, S.OverlayHintNotFound, OnRegionSelected);
                    shots = null; // the selector owns them now
                }
            }
            catch (Exception ex)
            {
                Storage.LogError(ex);
                RestoreMain();
            }
            finally
            {
                ScreenCapture.DisposeAll(shots);
                _busy = false;
            }
        }

        internal async void SelectRegion(bool fromWindow)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (HideOwnWindows(hideMain: fromWindow)) await Task.Delay(250);
                List<MonitorShot> shots = ScreenCapture.CaptureAll();
                RegionSelector.Show(shots, S.OverlayHint, OnRegionSelected);
            }
            catch (Exception ex)
            {
                Storage.LogError(ex);
                RestoreMain();
            }
            finally
            {
                _busy = false;
            }
        }

        private void OnRegionSelected(Bitmap crop)
        {
            if (crop == null)
            {
                RestoreMain();
                return;
            }
            ScanBitmap(crop, ownsBitmap: true);
        }

        internal void ScanClipboard()
        {
            Bitmap bmp = ImageUtil.FromClipboard();
            if (bmp == null)
            {
                Notify(S.ClipboardEmpty);
                return;
            }
            ScanBitmap(bmp, ownsBitmap: true);
        }

        internal void ScanFile(string path)
        {
            Bitmap bmp;
            try
            {
                bmp = ImageUtil.LoadFile(path);
            }
            catch (Exception ex) when (ImageUtil.IsImageLoadError(ex))
            {
                Notify(S.ImageOpenFailed);
                return;
            }
            ScanBitmap(bmp, ownsBitmap: true);
        }

        private async void ScanBitmap(Bitmap bmp, bool ownsBitmap)
        {
            try
            {
                List<string> texts = await Task.Run(() => QrScanner.Decode(bmp));
                ShowResults(texts);
            }
            finally
            {
                if (ownsBitmap) bmp.Dispose();
            }
        }

        internal void ShowResults(List<string> texts, bool fromHistory = false)
        {
            bool autoCopied = false;
            // Secrets (2FA keys, Wi-Fi passwords) are never copied on their own:
            // the clipboard is shared with every app and Windows' clipboard history.
            if (!fromHistory && texts.Count == 1 && Settings.CopyAutomatically && !QrContent.Parse(texts[0]).IsSensitive)
            {
                autoCopied = SafeClipboard.SetText(texts[0]);
            }

            RestoreMain();
            if (_results == null || !_results.IsLoaded) _results = new ResultWindow();
            _results.ShowItems(texts, autoCopied);

            _watcher?.MarkSeen(texts);
            if (!fromHistory) AddHistory(texts);
        }

        /// <summary>Hides our windows so they don't cover the screen being captured.</summary>
        private bool HideOwnWindows(bool hideMain)
        {
            bool hid = false;
            if (_results != null && _results.IsVisible) { _results.Hide(); hid = true; }
            // The generator stays: it often shows the very QR the user wants to test.
            if (hideMain && _main != null && _main.IsVisible)
            {
                _main.Hide();
                _restoreMainAfterScan = true;
                hid = true;
            }
            return hid;
        }

        private void RestoreMain()
        {
            if (_restoreMainAfterScan && _main != null)
            {
                _restoreMainAfterScan = false;
                _main.ShowActivated = false;
                _main.Show();
                _main.ShowActivated = true;
            }
        }

        // ---------------------------------------------------------------- history

        /// <summary>
        /// Loads history and strips secrets that older versions stored (Wi-Fi
        /// passwords, 2FA keys), rewriting the file if anything was removed.
        /// </summary>
        private static List<HistoryEntry> LoadHistorySanitized()
        {
            List<HistoryEntry> items = Storage.LoadHistory();
            bool changed = false;
            var clean = new List<HistoryEntry>();
            foreach (HistoryEntry h in items)
            {
                string safe = QrContent.ForHistory(h.Text);
                if (safe != h.Text) changed = true;
                if (safe == null) continue;
                if (clean.Any(c => c.Text == safe)) { changed = true; continue; }
                clean.Add(new HistoryEntry { Text = safe, Time = h.Time });
            }
            if (changed) Storage.SaveHistory(clean);
            return clean;
        }

        private void AddHistory(IEnumerable<string> texts)
        {
            if (!Settings.KeepHistory) return;
            foreach (string raw in texts.Reverse())
            {
                string t = QrContent.ForHistory(raw);
                if (t == null) continue;
                History.RemoveAll(h => h.Text == t);
                History.Insert(0, new HistoryEntry { Text = t, Time = DateTime.Now });
            }
            if (History.Count > Storage.MaxHistory) History.RemoveRange(Storage.MaxHistory, History.Count - Storage.MaxHistory);
            Storage.SaveHistory(History);
            HistoryChanged?.Invoke();
        }

        internal void ClearHistory()
        {
            History.Clear();
            Storage.DeleteHistory();
            HistoryChanged?.Invoke();
        }

        // ---------------------------------------------------------------- windows

        internal void ShowMain()
        {
            if (_main == null)
            {
                _main = new MainWindow();
                _main.Closing += (s, e) =>
                {
                    // Closing the window keeps QRPeek in the tray; Quit exits.
                    e.Cancel = true;
                    _main.Hide();
                    if (!Settings.TrayHintShown)
                    {
                        Settings.TrayHintShown = true;
                        Storage.SaveSettings(Settings);
                        Notify(HotkeyActive ? S.TrayHint(Hotkey.Display) : S.TrayHintNoHotkey);
                    }
                };
            }
            _restoreMainAfterScan = false;
            BringToFront(_main);
        }

        internal void ShowGenerator(string initialText = null)
        {
            if (_generator == null || !_generator.IsLoaded)
            {
                _generator = new GeneratorWindow();
                TrackOwnQr(_generator);
            }
            if (initialText != null) _generator.SetText(initialText);
            BringToFront(_generator);
        }

        /// <summary>Opens "send to phone" with whatever is on the clipboard (text, link, image or copied files).</summary>
        internal void ShowSend(IReadOnlyList<string> files = null)
        {
            if (_send == null || !_send.IsLoaded)
            {
                _send = new SendWindow();
                TrackOwnQr(_send);
            }
            if (files != null) _send.LoadFiles(files);
            else _send.LoadClipboard();
            BringToFront(_send);
        }

        internal void ShowSettings()
        {
            if (_settings == null || !_settings.IsLoaded) _settings = new SettingsWindow();
            BringToFront(_settings);
        }

        private static void BringToFront(Window w)
        {
            if (!w.IsVisible) w.Show();
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
            w.Topmost = true; // Activate alone doesn't win against a foreground app
            w.Topmost = false;
            w.Focus();
        }

        internal void Quit()
        {
            Shutdown();
        }

        // ---------------------------------------------------------------- watch mode

        /// <summary>Starts or stops watch mode to match the setting.</summary>
        internal void ApplyWatch()
        {
            if (Settings.WatchScreen && _watcher == null)
            {
                _watcher = new ScreenWatcher(() => _busy || _showingOwnQr);
                _watcher.Found += texts => Dispatcher.BeginInvoke(new Action(() => OnWatchFound(texts)));
            }
            else if (!Settings.WatchScreen && _watcher != null)
            {
                _watcher.Dispose();
                _watcher = null;
            }
            UpdateTrayMenu();
        }

        private void OnWatchFound(List<string> texts)
        {
            if (_watcher == null) return; // turned off meanwhile
            string message = texts.Count == 1
                ? S.WatchFoundOne(Describe(QrContent.Parse(texts[0])))
                : S.WatchFoundMany(texts.Count);
            WatchToast.Show(message, () => ShowResults(texts));
        }

        private static string Describe(QrContent c)
        {
            switch (c.Kind)
            {
                case ContentKind.Url: return c.Host;
                case ContentKind.Wifi: return "Wi-Fi " + c.Wifi.Ssid;
                case ContentKind.Otp: return S.KindOtp;
                case ContentKind.Invoice: return S.KindInvoice + " " + c.Invoice.Number;
                default:
                    string s = (c.Summary ?? "").Length > 0 ? c.Summary : c.Raw;
                    s = s.Replace('\r', ' ').Replace('\n', ' ');
                    return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
            }
        }

        /// <summary>Watch mode pauses while one of our own windows shows a QR code.</summary>
        private void TrackOwnQr(Window w)
        {
            w.IsVisibleChanged += (s, e) => UpdateOwnQr();
            w.StateChanged += (s, e) => UpdateOwnQr();
            w.Closed += (s, e) => UpdateOwnQr();
        }

        private void UpdateOwnQr()
        {
            bool Showing(Window w) => w != null && w.IsLoaded && w.IsVisible && w.WindowState != WindowState.Minimized;
            _showingOwnQr = Showing(_generator) || Showing(_send);
        }

        // ---------------------------------------------------------------- update check (GitHub build)

        /// <summary>First look 15 s after start, then every 3 hours whether a day has passed.</summary>
        private void StartUpdateChecks()
        {
            if (!UpdateChecker.Supported) return;
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _updateTimer.Tick += async (s, e) =>
            {
                _updateTimer.Interval = TimeSpan.FromHours(3);
                if (Settings.CheckUpdates && DateTime.UtcNow - Settings.LastUpdateCheck.ToUniversalTime() >= UpdateChecker.Interval)
                {
                    await CheckForUpdate(manual: false);
                }
            };
            _updateTimer.Start();
        }

        /// <summary>Asks GitHub; returns the latest release, or null if unreachable.</summary>
        internal async Task<UpdateInfo> CheckForUpdate(bool manual)
        {
            UpdateInfo info = await UpdateChecker.CheckAsync();
            Settings.LastUpdateCheck = DateTime.UtcNow;
            if (info != null && info.IsNewer)
            {
                AvailableUpdate = info;
                string v = info.Latest.ToString();
                if (!manual && Settings.NotifiedVersion != v)
                {
                    Settings.NotifiedVersion = v;
                    Notify(S.UpdateAvailable(v), () => OpenUrl(info.Url));
                }
            }
            else if (info != null)
            {
                AvailableUpdate = null;
            }
            SaveSettings();
            UpdateChanged?.Invoke();
            return info;
        }

        internal static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception ex) { Storage.LogError(ex); }
        }

        // ---------------------------------------------------------------- settings

        /// <summary>Registers both shortcuts. Check HotkeyActive / SendHotkeyActive for the outcome.</summary>
        internal void ApplyHotkeys()
        {
            HotkeyActive = _hotkeys.Register(ScanHotkeyId, Hotkey) && !Hotkey.IsNone;
            SendHotkeyActive = _hotkeys.Register(SendHotkeyId, SendHotkey) && !SendHotkey.IsNone;
            UpdateTrayMenu();
            HotkeysChanged?.Invoke();
        }

        internal void SaveSettings() => Storage.SaveSettings(Settings);

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool dark = WindowTheme.SystemPrefersDark();
                if (dark != WindowTheme.IsDark) WindowTheme.Apply(this, dark);
            }));
        }

        // ---------------------------------------------------------------- tray

        private void CreateTray()
        {
            _tray = new Forms.NotifyIcon
            {
                Text = S.AppName,
                Icon = LoadTrayIcon(),
                Visible = true,
            };
            _tray.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) ShowMain();
            };
            _tray.BalloonTipClicked += (s, e) =>
            {
                Action a = _balloonClick;
                _balloonClick = null;
                a?.Invoke();
            };
            _tray.BalloonTipClosed += (s, e) => _balloonClick = null;
            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            if (_tray == null || Settings == null) return;
            var menu = new Forms.ContextMenuStrip();

            var scan = new Forms.ToolStripMenuItem(S.ScanScreen, null, (s, e) => ScanScreen(fromWindow: false));
            if (HotkeyActive) scan.ShortcutKeyDisplayString = Hotkey.Display.Replace(" ", "");
            scan.Font = new Font(scan.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add(scan);
            menu.Items.Add(S.SelectRegion, null, (s, e) => SelectRegion(fromWindow: false));
            menu.Items.Add(S.TrayClipboard, null, (s, e) => ScanClipboard());
            var watch = new Forms.ToolStripMenuItem(S.TrayWatch, null, (s, e) =>
            {
                Settings.WatchScreen = !Settings.WatchScreen;
                SaveSettings();
                ApplyWatch();
            })
            { Checked = Settings.WatchScreen };
            menu.Items.Add(watch);
            menu.Items.Add(new Forms.ToolStripSeparator());

            var send = new Forms.ToolStripMenuItem(S.SendToPhone, null, (s, e) => ShowSend());
            if (SendHotkeyActive) send.ShortcutKeyDisplayString = SendHotkey.Display.Replace(" ", "");
            menu.Items.Add(send);
            menu.Items.Add(S.CreateQr, null, (s, e) => ShowGenerator());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(S.TrayOpen, null, (s, e) => ShowMain());
            menu.Items.Add(S.Settings, null, (s, e) => ShowSettings());
            menu.Items.Add(S.Quit, null, (s, e) => Quit());

            Forms.ContextMenuStrip old = _tray.ContextMenuStrip;
            _tray.ContextMenuStrip = menu;
            old?.Dispose();
        }

        private static Icon LoadTrayIcon()
        {
            var uri = new Uri("pack://application:,,,/Assets/QRPeek.ico");
            using (var stream = GetResourceStream(uri).Stream)
            {
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }

        /// <param name="onClick">Runs if the user clicks the notification.</param>
        internal void Notify(string message, Action onClick = null)
        {
            if (_tray == null) return;
            _balloonClick = onClick;
            _tray.ShowBalloonTip(5000, S.AppName, message, Forms.ToolTipIcon.None);
        }

        // ---------------------------------------------------------------- single instance

        private void ListenForSecondInstance()
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            var t = new Thread(() =>
            {
                try
                {
                    while (_showEvent.WaitOne())
                    {
                        Dispatcher.BeginInvoke(new Action(ShowMain));
                    }
                }
                catch (ObjectDisposedException) { }
            })
            {
                IsBackground = true,
                Name = "QRPeek single-instance listener",
            };
            t.Start();
        }

        private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Storage.LogError(e.Exception);
            MessageBox.Show(S.Error, S.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
