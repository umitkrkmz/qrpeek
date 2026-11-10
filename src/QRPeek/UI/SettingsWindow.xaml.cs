// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using QRPeek.Core;

namespace QRPeek.UI
{
    public partial class SettingsWindow : Window
    {
        internal const string SourceUrl = "https://github.com/umitkrkmz/qrpeek";

        /// <summary>Also the privacy policy URL given to the Microsoft Store.</summary>
        internal const string PrivacyUrl = SourceUrl + "/blob/master/docs/PRIVACY.md";

        private readonly string _languageAtStart;

        public SettingsWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);

            AppSettings s = App.Instance.Settings;
            _languageAtStart = s.Language;

            BuildHotkeyChoices(HotkeyChoices, AppSettings.ScanKey, App.Instance.Hotkey.Id, "Hotkey");
            BuildHotkeyChoices(SendHotkeyChoices, AppSettings.SendKey, App.Instance.SendHotkey.Id, "SendHotkey");
            RefreshHotkeyErrors();

            AutoCopy.IsChecked = s.CopyAutomatically;
            KeepHistory.IsChecked = s.KeepHistory;
            Watch.IsChecked = s.WatchScreen;

            LoadStartupState();

            // Update check exists only in the GitHub build; the Store updates by itself.
            CheckUpdates.Visibility = UpdateChecker.Supported ? Visibility.Visible : Visibility.Collapsed;
            UpdateRow.Visibility = UpdateChecker.Supported ? Visibility.Visible : Visibility.Collapsed;
            CheckUpdates.IsChecked = s.CheckUpdates;
            if (App.Instance.AvailableUpdate != null) ShowUpdate(App.Instance.AvailableUpdate);

            LangAuto.IsChecked = s.Language != "tr" && s.Language != "en";
            LangTr.IsChecked = s.Language == "tr";
            LangEn.IsChecked = s.Language == "en";

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = S.Version(v.Major + "." + v.Minor + "." + v.Build);
        }

        private void BuildHotkeyChoices(Panel host, char key, string currentId, string group)
        {
            foreach (HotkeyPreset p in HotkeyPreset.For(key))
            {
                var rb = new RadioButton
                {
                    Content = p.Display,
                    Tag = p,
                    GroupName = group,
                    Style = (Style)FindResource("Pill"),
                    IsChecked = p.Id == currentId,
                };
                rb.Click += (s, e) =>
                {
                    App app = App.Instance;
                    if (group == "Hotkey") app.Settings.Hotkey = p.Id;
                    else app.Settings.SendHotkey = p.Id;
                    app.ApplyHotkeys();
                    app.SaveSettings();
                    RefreshHotkeyErrors();
                };
                host.Children.Add(rb);
            }
        }

        private void RefreshHotkeyErrors()
        {
            App app = App.Instance;
            HotkeyError.Visibility = !app.HotkeyActive && !app.Hotkey.IsNone ? Visibility.Visible : Visibility.Collapsed;
            SendHotkeyError.Visibility = !app.SendHotkeyActive && !app.SendHotkey.IsNone ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnWatchChanged(object sender, RoutedEventArgs e)
        {
            App app = App.Instance;
            app.Settings.WatchScreen = Watch.IsChecked == true;
            app.SaveSettings();
            app.ApplyWatch();
        }

        private void OnOptionChanged(object sender, RoutedEventArgs e)
        {
            App app = App.Instance;
            app.Settings.CopyAutomatically = AutoCopy.IsChecked == true;
            bool keep = KeepHistory.IsChecked == true;
            if (app.Settings.KeepHistory && !keep) app.ClearHistory(); // turning history off also forgets it
            app.Settings.KeepHistory = keep;
            app.SaveSettings();
        }

        private async void LoadStartupState()
        {
            Startup.IsEnabled = false;
            try { ShowStartupState(await Autostart.GetAsync()); }
            catch (Exception ex) { Storage.LogError(ex); }
            finally { Startup.IsEnabled = true; }
        }

        private async void OnStartupChanged(object sender, RoutedEventArgs e)
        {
            bool wanted = Startup.IsChecked == true;
            Startup.IsEnabled = false;
            try
            {
                ShowStartupState(await Autostart.SetAsync(wanted));
            }
            catch (Exception ex)
            {
                Storage.LogError(ex);
                try { ShowStartupState(await Autostart.GetAsync()); } catch (Exception ex2) { Storage.LogError(ex2); }
            }
            finally
            {
                Startup.IsEnabled = true;
            }
        }

        private void ShowStartupState(AutostartState state)
        {
            Startup.IsChecked = state == AutostartState.On;
            StartupNote.Text = state == AutostartState.BlockedByUser ? S.StartupBlockedByUser
                             : state == AutostartState.BlockedByPolicy ? S.StartupBlockedByPolicy
                             : "";
            StartupNote.Visibility = StartupNote.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnCheckUpdatesChanged(object sender, RoutedEventArgs e)
        {
            App.Instance.Settings.CheckUpdates = CheckUpdates.IsChecked == true;
            App.Instance.SaveSettings();
        }

        private async void OnCheckNow(object sender, RoutedEventArgs e)
        {
            CheckNowButton.IsEnabled = false;
            UpdateDownload.Visibility = Visibility.Collapsed;
            UpdateStatus.Text = S.UpdateChecking;
            UpdateInfo info = await App.Instance.CheckForUpdate(manual: true);
            CheckNowButton.IsEnabled = true;
            if (info == null) UpdateStatus.Text = S.UpdateFailed;
            else if (info.IsNewer) ShowUpdate(info);
            else UpdateStatus.Text = S.UpToDate(UpdateChecker.Current.ToString());
        }

        private void ShowUpdate(UpdateInfo info)
        {
            UpdateStatus.Text = S.UpdateAvailable(info.Latest.ToString());
            UpdateDownload.Visibility = Visibility.Visible;
        }

        private void OnUpdateDownload(object sender, RoutedEventArgs e)
        {
            if (App.Instance.AvailableUpdate != null) App.OpenUrl(App.Instance.AvailableUpdate.Url);
        }

        private void OnLanguage(object sender, RoutedEventArgs e)
        {
            string lang = (string)((RadioButton)sender).Tag;
            App.Instance.Settings.Language = lang;
            App.Instance.SaveSettings();
            LangNote.Visibility = lang != _languageAtStart ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSource(object sender, RoutedEventArgs e) => Launch(SourceUrl);

        // Opens the section in the app's language (GitHub anchors: "## Türkçe" -> #türkçe).
        private void OnPrivacy(object sender, RoutedEventArgs e) => Launch(PrivacyUrl + (S.IsTurkish ? "#türkçe" : "#english"));

        private void OnNotices(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
            Launch(File.Exists(path) ? path : SourceUrl);
        }

        private static void Launch(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception ex) { Storage.LogError(ex); }
        }

        private void OnQuit(object sender, RoutedEventArgs e) => App.Instance.Quit();

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
