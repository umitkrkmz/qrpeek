// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using QRPeek.Core;

namespace QRPeek.UI
{
    public partial class MainWindow : Window
    {
        private const int HistoryShown = 30;

        public MainWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);

            App.Instance.HistoryChanged += RefreshHistory;
            App.Instance.HotkeysChanged += RefreshHotkey;
            App.Instance.UpdateChanged += RefreshUpdate;
            RefreshHistory();
            RefreshHotkey();
            RefreshUpdate();
            if (!App.Instance.Settings.WelcomeShown) ShowWelcome();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && !(e.OriginalSource is TextBox))
                {
                    e.Handled = true;
                    App.Instance.ScanClipboard();
                }
            };
            DragEnter += (s, e) => SetDropHighlight(e.Data.GetDataPresent(DataFormats.FileDrop));
            DragLeave += (s, e) => SetDropHighlight(false);
            Drop += OnDrop;
        }

        internal void RefreshHotkey()
        {
            App app = App.Instance;
            HotkeyBadge.Visibility = app.HotkeyActive ? Visibility.Visible : Visibility.Collapsed;
            HotkeyText.Text = app.Hotkey.Display;
            SendButton.ToolTip = app.SendHotkeyActive ? S.SendToPhone + " (" + app.SendHotkey.Display + ")" : null;
        }

        private void RefreshUpdate()
        {
            UpdateInfo u = App.Instance.AvailableUpdate;
            UpdateBanner.Visibility = u != null ? Visibility.Visible : Visibility.Collapsed;
            if (u != null) UpdateText.Text = S.UpdateAvailable(u.Latest.ToString());
        }

        private void OnDownloadUpdate(object sender, RoutedEventArgs e)
        {
            UpdateInfo u = App.Instance.AvailableUpdate;
            if (u != null) App.OpenUrl(u.Url);
        }

        /// <summary>Shows the configured shortcuts (not hard-coded ones) on first run.</summary>
        private void ShowWelcome()
        {
            App app = App.Instance;
            WelcomeRows.Children.Clear();
            WelcomeRows.RowDefinitions.Clear();
            if (app.HotkeyActive) AddWelcomeRow(app.Hotkey.Display, S.WelcomeScan);
            if (app.SendHotkeyActive) AddWelcomeRow(app.SendHotkey.Display, S.WelcomeSend);
            WelcomeCard.Visibility = Visibility.Visible;
        }

        private void AddWelcomeRow(string keys, string what)
        {
            int row = WelcomeRows.RowDefinitions.Count;
            WelcomeRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var key = new Border
            {
                Background = (Brush)FindResource("SurfaceHover"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 10, 8),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = keys, FontSize = 12, FontFamily = (FontFamily)FindResource("MonoFont"), TextWrapping = TextWrapping.NoWrap },
            };
            var text = new TextBlock { Text = what, FontSize = 13, Margin = new Thickness(0, 1, 0, 8) };
            Grid.SetRow(key, row);
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 1);
            WelcomeRows.Children.Add(key);
            WelcomeRows.Children.Add(text);
        }

        private void OnWelcomeOk(object sender, RoutedEventArgs e)
        {
            WelcomeCard.Visibility = Visibility.Collapsed;
            App.Instance.Settings.WelcomeShown = true;
            App.Instance.SaveSettings();
        }

        private List<InvoiceInfo> HistoryInvoices() =>
            App.Instance.History.Select(h => QrContent.Parse(h.Text)).Where(c => c.Kind == ContentKind.Invoice).Select(c => c.Invoice).ToList();

        private void RefreshHistory()
        {
            HistoryList.Children.Clear();
            App app = App.Instance;
            ClearButton.Visibility = app.History.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ExportButton.Visibility = HistoryInvoices().Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (app.History.Count == 0)
            {
                HistoryList.Children.Add(new TextBlock
                {
                    Text = app.Settings.KeepHistory ? S.HistoryEmpty : S.HistoryOff,
                    Style = (Style)FindResource("Muted"),
                    Margin = new Thickness(2, 4, 0, 0),
                });
                return;
            }

            for (int i = 0; i < app.History.Count && i < HistoryShown; i++)
            {
                HistoryList.Children.Add(HistoryRow(app.History[i]));
            }
        }

        private UIElement HistoryRow(HistoryEntry entry)
        {
            QrContent c = QrContent.Parse(entry.Text);
            string title = c.Kind == ContentKind.Invoice ? S.KindInvoice + " " + c.Invoice.Number
                : c.Kind == ContentKind.Wifi ? "Wi-Fi: " + c.Wifi.Ssid
                : !string.IsNullOrWhiteSpace(c.Summary) && c.Kind != ContentKind.Url ? c.Summary : entry.Text;
            title = title.Replace('\r', ' ').Replace('\n', ' ');

            var row = new DockPanel();
            var icon = new TextBlock { Text = ResultWindow.KindIcon(c.Kind), Style = (Style)FindResource("Icon"), FontSize = 14, Foreground = (Brush)FindResource("FgMuted"), Margin = new Thickness(0, 0, 10, 0) };
            DockPanel.SetDock(icon, Dock.Left);
            var time = new TextBlock { Text = FormatTime(entry.Time), Style = (Style)FindResource("Muted"), FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
            DockPanel.SetDock(time, Dock.Right);
            row.Children.Add(icon);
            row.Children.Add(time);
            row.Children.Add(new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13.5 });

            var b = new Button
            {
                Content = row,
                Style = (Style)FindResource("Ghost"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 7, 8, 7),
                // Wrapped and capped: a long one-line JSON (e-Fatura) otherwise ran across the whole screen.
                ToolTip = new TextBlock
                {
                    Text = entry.Text.Length > 300 ? entry.Text.Substring(0, 300) + "…" : entry.Text,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420,
                },
            };
            b.Click += (s, e) => App.Instance.ShowResults(new List<string> { entry.Text }, fromHistory: true);
            return b;
        }

        private static string FormatTime(DateTime t)
        {
            CultureInfo ci = S.IsTurkish ? new CultureInfo("tr-TR") : CultureInfo.CurrentCulture;
            return t.Date == DateTime.Today ? t.ToString("t", ci) : t.ToString("d MMM", ci);
        }

        private void SetDropHighlight(bool on)
        {
            DropFrame.Stroke = (Brush)FindResource(on ? "Accent" : "Line");
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            SetDropHighlight(false);
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                App.Instance.ScanFile(files[0]);
            }
        }

        private void OnScan(object sender, RoutedEventArgs e) => App.Instance.ScanScreen(fromWindow: true);

        private void OnSelectRegion(object sender, RoutedEventArgs e) => App.Instance.SelectRegion(fromWindow: true);

        private void OnCreate(object sender, RoutedEventArgs e) => App.Instance.ShowGenerator();

        private void OnSettings(object sender, RoutedEventArgs e) => App.Instance.ShowSettings();

        private void OnFromImage(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = S.ImageFilter };
            if (dlg.ShowDialog(this) == true) App.Instance.ScanFile(dlg.FileName);
        }

        private void OnClearHistory(object sender, RoutedEventArgs e) => App.Instance.ClearHistory();

        private void OnSend(object sender, RoutedEventArgs e) => App.Instance.ShowSend();

        private void OnExportInvoices(object sender, RoutedEventArgs e) =>
            ResultWindow.SaveInvoicesCsv(this, HistoryInvoices(), "e-faturalar-" + DateTime.Now.ToString("yyyy-MM-dd"));
    }
}
