// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using QRPeek.Core;

namespace QRPeek.UI
{
    public partial class ResultWindow : Window
    {
        public ResultWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);
        }

        /// <param name="places">Which screen each code was on; null when there's only one screen or it wasn't a screen scan.</param>
        /// <param name="ocrText">When no QR code was found: the text OCR read in the same image (null = not tried / no OCR language).</param>
        internal void ShowItems(List<string> texts, bool autoCopied, IReadOnlyDictionary<string, ScreenPlace> places = null, string ocrText = null)
        {
            Items.Children.Clear();
            CopiedBadge.Visibility = autoCopied ? Visibility.Visible : Visibility.Collapsed;

            if (texts.Count == 0 && !string.IsNullOrWhiteSpace(ocrText))
            {
                Heading.Text = S.NoQrButText;
                Items.Children.Add(TextCard(ocrText));
            }
            else if (texts.Count == 0)
            {
                Heading.Text = S.NothingFound;
                Items.Children.Add(new TextBlock { Text = S.NothingFoundHint, Style = (Style)FindResource("Muted"), FontSize = 13.5 });
            }
            else
            {
                Heading.Text = S.FoundCount(texts.Count);
                foreach (string t in texts)
                {
                    ScreenPlace? where = places != null && places.TryGetValue(t, out ScreenPlace p) ? p : (ScreenPlace?)null;
                    Items.Children.Add(BuildCard(QrContent.Parse(t), where));
                }
            }

            bool place = !IsVisible;
            Show();
            if (place) Placement.OnCursorMonitor(this);
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }

        // ------------------------------------------------------------ cards

        private UIElement BuildCard(QrContent c, ScreenPlace? where = null)
        {
            var stack = new StackPanel();
            stack.Children.Add(KindLine(c.Kind, where));
            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };

            switch (c.Kind)
            {
                case ContentKind.Url:
                    UrlCard(c, stack, actions);
                    break;
                case ContentKind.Wifi:
                    WifiCard(c, stack, actions);
                    break;
                case ContentKind.Otp:
                    OtpCard(c, stack, actions);
                    break;
                case ContentKind.Invoice:
                    InvoiceCard(c, stack, actions);
                    break;
                default:
                    stack.Children.Add(RawText(c.Raw, mono: false));
                    if (c.OpenTarget != null)
                    {
                        Button open = ActionButton("", S.Open, primary: true);
                        open.Click += (s, e) => OpenTarget(c.OpenTarget);
                        actions.Children.Add(open);
                    }
                    if (c.Kind == ContentKind.Contact && c.Raw.TrimStart().StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
                    {
                        Button vcf = ActionButton("", S.SaveVcf, false);
                        vcf.Click += (s, e) => SaveVcf(c);
                        actions.Children.Add(vcf);
                    }
                    break;
            }

            if (c.Kind != ContentKind.Otp)
            {
                Button copy = ActionButton("", S.Copy, primary: actions.Children.Count == 0);
                copy.Click += (s, e) => CopyWithFeedback(copy, c.Raw, c.IsSensitive);
                actions.Children.Add(copy);
            }
            stack.Children.Add(actions);

            return new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 0, 10), Child = stack };
        }

        private void UrlCard(QrContent c, StackPanel stack, WrapPanel actions)
        {
            stack.Children.Add(HostLine(S.GoesTo, c.Host));
            stack.Children.Add(RawText(c.Raw, mono: true));
            foreach (string w in c.Warnings) stack.Children.Add(Warning(w));

            var resolvePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
            stack.Children.Add(resolvePanel);

            Button open = ActionButton("", S.OpenLink, primary: c.Warnings.Count == 0);
            open.Click += (s, e) => OpenTarget(c.OpenTarget);
            actions.Children.Add(open);

            Button resolve = ActionButton("", S.ShowRealTarget, false);
            resolve.ToolTip = S.ResolveNote;
            resolve.Click += async (s, e) =>
            {
                resolve.IsEnabled = false;
                SetButtonText(resolve, S.Resolving);
                ResolveResult r = await LinkResolver.ResolveAsync(new Uri(c.OpenTarget));
                actions.Children.Remove(resolve);
                ShowResolved(r, resolvePanel, actions);
            };
            actions.Children.Add(resolve);
        }

        /// <summary>How a column gap shows in the text card (the copy for Excel turns it back into a tab).</summary>
        private const string ColumnSpaces = "   ";

        /// <summary>
        /// No QR code, but text: an editable box (OCR makes mistakes) and a
        /// copy button. Never copied on its own and never written to history:
        /// the text may be anything on the user's screen.
        /// </summary>
        private UIElement TextCard(string text)
        {
            var stack = new StackPanel();
            var kind = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            kind.Children.Add(new TextBlock { Text = KindIcon(ContentKind.Text), Style = (Style)FindResource("Icon"), FontSize = 14, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 0, 8, 0) });
            kind.Children.Add(new TextBlock { Text = S.OcrKind, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            stack.Children.Add(kind);

            // Columns come tab-separated; a tab jumps to the next tab stop and looks
            // like a hole in most places, so show (and copy) three spaces instead.
            bool hasColumns = text.IndexOf('\t') >= 0;
            var box = new TextBox
            {
                Text = text.Replace("\t", ColumnSpaces),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 60,
                MaxHeight = 260,
                FontSize = 13.5,
                Padding = new Thickness(6, 4, 6, 4),
            };
            stack.Children.Add(box);
            // OCR is never perfect (1 vs I in codes, a lost "/"): say so where it can't be missed.
            var check = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var checkIcon = new TextBlock { Text = "", Style = (Style)FindResource("Icon"), FontSize = 13, Foreground = (Brush)FindResource("Bad"), Margin = new Thickness(0, 1, 7, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(checkIcon, Dock.Left);
            check.Children.Add(checkIcon);
            var checkText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = (Brush)FindResource("Bad") };
            checkText.Inlines.Add(new Run(S.OcrCheckLead) { FontWeight = FontWeights.SemiBold, TextDecorations = TextDecorations.Underline });
            checkText.Inlines.Add(new Run(" " + S.OcrCheck));
            check.Children.Add(checkText);
            stack.Children.Add(check);
            stack.Children.Add(new TextBlock { Text = S.OcrNote, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 0) });

            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button copy = ActionButton("", S.CopyText, primary: true);
            copy.Click += (s, e) => CopyWithFeedback(copy, box.Text, sensitive: false);
            actions.Children.Add(copy);
            if (hasColumns)
            {
                // Same text with the column gaps as tabs: pastes into Excel as cells (edits in the box included).
                Button excel = ActionButton("", S.CopyForExcel, primary: false);
                excel.ToolTip = S.CopyForExcelHint;
                excel.Click += (s, e) => CopyWithFeedback(excel, Regex.Replace(box.Text, " {3,}", "\t"), sensitive: false);
                actions.Children.Add(excel);
            }
            // The text as it is now in the box (fixes included) goes to the generator, which says if it's too long for a QR.
            Button qr = ActionButton("", S.MakeQr, primary: false);
            qr.Click += (s, e) => App.Instance.ShowGenerator(box.Text.Trim());
            actions.Children.Add(qr);
            stack.Children.Add(actions);

            return new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 0, 10), Child = stack };
        }

        private void ShowResolved(ResolveResult r, StackPanel panel, WrapPanel actions)
        {
            panel.Children.Clear();
            panel.Visibility = Visibility.Visible;

            bool refused = r.RefusedWith.HasValue;
            if (r.Chain.Count > 1)
            {
                string path = string.Join("  →  ", r.Chain.Select(u => u.Host));
                panel.Children.Add(new TextBlock { Text = S.RedirectPath + ": " + path, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 0, 0, 4) });
                // Refused midway: this is only how far the check got, not where a browser lands.
                panel.Children.Add(HostLine(refused ? S.ReachedSoFar : S.RealTarget, r.Final.IdnHost));
                panel.Children.Add(RawText(r.Final.AbsoluteUri, mono: true));
                foreach (string w in QrContent.Parse(r.Final.AbsoluteUri).Warnings) panel.Children.Add(Warning(w));

                Button openReal = ActionButton("", S.OpenRealTarget, false);
                openReal.Click += (s, e) => OpenTarget(r.Final.AbsoluteUri);
                if (!refused) actions.Children.Insert(Math.Min(1, actions.Children.Count), openReal);
            }
            if (refused)
            {
                panel.Children.Add(Warning(S.ResolveRefused(r.RefusedWith.Value)));
            }
            else if (r.Chain.Count == 1 && r.Error == null)
            {
                panel.Children.Add(new TextBlock { Text = S.NoRedirect, Style = (Style)FindResource("Muted") });
            }

            if (r.Error != null)
            {
                panel.Children.Add(new TextBlock { Text = S.ResolveFailed(r.Error), Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 4, 0, 0) });
            }
        }

        private void WifiCard(QrContent c, StackPanel stack, WrapPanel actions)
        {
            WifiInfo w = c.Wifi;
            var grid = Grid2();
            AddRow(grid, S.WifiNetwork, w.Ssid);
            TextBox password = null;
            string masked = new string('•', Math.Max(8, Math.Min(w.Password.Length, 16)));
            if (w.IsOpen) AddRow(grid, S.WifiPassword, S.SecNone);
            else if (w.PasswordMissing) AddRow(grid, S.WifiPassword, S.PasswordNotStored, muted: true);
            else password = AddRow(grid, S.WifiPassword, masked, mono: true); // hidden by default: someone may be looking
            string sec = w.IsOpen ? S.SecNone : (w.Security.ToUpperInvariant() == "SAE" ? "WPA3" : w.Security.ToUpperInvariant() == "WPA" ? "WPA/WPA2" : w.Security);
            AddRow(grid, S.WifiSecurity, sec);
            stack.Children.Add(grid);

            if (w.PasswordMissing) return; // from history: nothing to connect with
            actions.Children.Add(WifiConnectButton(w));
            if (password != null)
            {
                bool shown = false;
                Button toggle = ActionButton("", S.Show, false);
                toggle.Click += (s, e) =>
                {
                    shown = !shown;
                    password.Text = shown ? w.Password : masked;
                    SetButtonText(toggle, shown ? S.Hide : S.Show);
                };
                actions.Children.Add(toggle);

                Button pw = ActionButton("", S.CopyPassword, false);
                pw.Click += (s, e) => CopyWithFeedback(pw, w.Password, sensitive: true);
                actions.Children.Add(pw);
            }

            // Windows 11 24H2+ asks for location when an app reads Wi-Fi details;
            // say so before the user presses Connect, so the prompt isn't a surprise.
            stack.Children.Add(new TextBlock
            {
                Text = S.WifiLocationNote,
                Style = (Style)FindResource("Muted"),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
            });
        }

        private void OtpCard(QrContent c, StackPanel stack, WrapPanel actions)
        {
            OtpInfo o = c.Otp;
            stack.Children.Add(Warning(S.OtpWarning));
            var grid = Grid2();
            grid.Margin = new Thickness(0, 8, 0, 0);
            if (o.Issuer.Length > 0) AddRow(grid, S.OtpIssuer, o.Issuer);
            if (o.Account.Length > 0) AddRow(grid, S.OtpAccount, o.Account);
            string masked = new string('•', Math.Min(o.Secret.Length, 16));
            TextBox secret = AddRow(grid, S.OtpSecret, masked, mono: true);
            stack.Children.Add(grid);

            bool shown = false;
            Button toggle = ActionButton("", S.Show, false);
            toggle.Click += (s, e) =>
            {
                shown = !shown;
                secret.Text = shown ? Group(o.Secret) : masked;
                SetButtonText(toggle, shown ? S.Hide : S.Show);
            };
            actions.Children.Add(toggle);

            Button copy = ActionButton("", S.CopySecret, false);
            copy.Click += (s, e) => CopyWithFeedback(copy, o.Secret, sensitive: true);
            actions.Children.Add(copy);
        }

        private void InvoiceCard(QrContent c, StackPanel stack, WrapPanel actions)
        {
            InvoiceInfo i = c.Invoice;
            var grid = Grid2();
            AddRow(grid, S.InvNumber, i.Number, bold: true);
            AddRow(grid, S.InvDate, i.Date);
            AddRow(grid, S.InvSeller, i.SellerId, mono: true);
            if (i.BuyerId.Length > 0) AddRow(grid, S.InvBuyer, i.BuyerId, mono: true);
            string kind = string.Join(" · ", new[] { i.Scenario, i.Type }.Where(x => x.Length > 0));
            if (kind.Length > 0) AddRow(grid, S.InvScenario, kind);
            if (i.Subtotal.HasValue) AddRow(grid, S.InvSubtotal, Money(i.Subtotal, i.Currency));
            if (i.Vat.HasValue) AddRow(grid, S.InvVat, Money(i.Vat, i.Currency));
            if (i.TaxIncluded.HasValue) AddRow(grid, S.InvTaxIncluded, Money(i.TaxIncluded, i.Currency));
            if (i.Payable.HasValue) AddRow(grid, S.InvPayable, Money(i.Payable, i.Currency), bold: true);
            if (i.Ettn.Length > 0) AddRow(grid, "ETTN", i.Ettn, mono: true);
            stack.Children.Add(grid);

            Button csv = ActionButton("", S.SaveCsv, primary: true);
            csv.Click += (s, e) => SaveInvoicesCsv(this, new[] { i }, i.Number);
            actions.Children.Add(csv);
        }

        internal static void SaveInvoicesCsv(Window owner, IEnumerable<InvoiceInfo> invoices, string name)
        {
            string file = string.IsNullOrWhiteSpace(name) ? "e-fatura" : name;
            foreach (char ch in Path.GetInvalidFileNameChars()) file = file.Replace(ch, '_');
            var dlg = new SaveFileDialog { Filter = S.CsvFilter, FileName = file + ".csv" };
            if (dlg.ShowDialog(owner) != true) return;
            try
            {
                InvoiceCsv.Write(dlg.FileName, invoices);
            }
            catch (IOException ex)
            {
                // Typically: the file is open in Excel.
                MessageBox.Show(owner, ex.Message, S.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string Money(decimal? d, string currency)
        {
            CultureInfo ci = S.IsTurkish ? new CultureInfo("tr-TR") : CultureInfo.CurrentCulture;
            return d.Value.ToString("N2", ci) + " " + currency;
        }

        private static string Group(string secret)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < secret.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(secret[i]);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ building blocks

        private UIElement KindLine(ContentKind k, ScreenPlace? where = null)
        {
            var line = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            if (where.HasValue)
            {
                // "On the left screen": only with more than one monitor.
                var place = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                place.Children.Add(new TextBlock { Text = "", Style = (Style)FindResource("Icon"), FontSize = 12, Foreground = (Brush)FindResource("FgMuted"), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                place.Children.Add(new TextBlock { Text = S.OnScreen(where.Value), Style = (Style)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap });
                DockPanel.SetDock(place, Dock.Right);
                line.Children.Add(place);
            }
            var kind = new StackPanel { Orientation = Orientation.Horizontal };
            kind.Children.Add(new TextBlock { Text = KindIcon(k), Style = (Style)FindResource("Icon"), FontSize = 14, Foreground = (Brush)FindResource("Accent"), Margin = new Thickness(0, 0, 8, 0) });
            kind.Children.Add(new TextBlock { Text = KindLabel(k), FontWeight = FontWeights.SemiBold, FontSize = 13 });
            line.Children.Add(kind);
            return line;
        }

        private TextBlock HostLine(string label, string host)
        {
            var t = new TextBlock { Margin = new Thickness(0, 0, 0, 6), FontSize = 13.5 };
            t.Inlines.Add(new Run(label + ": ") { Foreground = (Brush)FindResource("FgMuted") });
            t.Inlines.Add(new Run(host) { FontWeight = FontWeights.Bold });
            return t;
        }

        private TextBox RawText(string text, bool mono)
        {
            var raw = new TextBox
            {
                Text = text,
                Style = (Style)FindResource("SelectableText"),
                MaxHeight = 150,
                FontSize = 13.5,
                Margin = new Thickness(0, 0, 0, 4),
            };
            if (mono) raw.FontFamily = (FontFamily)FindResource("MonoFont");
            return raw;
        }

        private static Grid Grid2()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            return grid;
        }

        private TextBox AddRow(Grid g, string label, string value, bool mono = false, bool bold = false, bool muted = false)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Style = (Style)FindResource("Muted"), FontSize = 13, Margin = new Thickness(0, 2, 14, 2), VerticalAlignment = VerticalAlignment.Center };
            var v = new TextBox { Text = value, Style = (Style)FindResource("SelectableText"), FontSize = 13.5, Margin = new Thickness(0, 2, 0, 2) };
            if (mono) v.FontFamily = (FontFamily)FindResource("MonoFont");
            if (bold) v.FontWeight = FontWeights.SemiBold;
            if (muted) { v.Foreground = (Brush)FindResource("FgMuted"); v.FontSize = 12.5; }
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            g.Children.Add(l);
            g.Children.Add(v);
            return v;
        }

        private Button WifiConnectButton(WifiInfo wifi)
        {
            Button b = ActionButton("", S.Connect, primary: true);
            b.Click += async (s, e) =>
            {
                b.IsEnabled = false;
                SetButtonText(b, S.Connecting);
                WifiResult result = await WifiConnector.ConnectAsync(wifi);
                bool ok = result == WifiResult.Connected;
                SetButtonText(b, ok ? S.Connected : S.Connect);
                b.IsEnabled = !ok;
                if (!ok)
                {
                    string msg =
                        result == WifiResult.NoAdapter ? S.NoWifiAdapter :
                        result == WifiResult.Unverified ? S.WifiUnverified :
                        result == WifiResult.LocationDenied ? S.WifiLocationDenied :
                        S.ConnectFailed;
                    MessageBox.Show(this, msg, S.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            return b;
        }

        private UIElement Warning(string text)
        {
            var row = new DockPanel();
            var icon = new TextBlock { Text = "", Style = (Style)FindResource("Icon"), FontSize = 13, Foreground = (Brush)FindResource("Warn"), Margin = new Thickness(0, 1, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            row.Children.Add(new TextBlock { Text = text, FontSize = 12.5, Foreground = (Brush)FindResource("Warn") });
            return new Border
            {
                Background = (Brush)FindResource("WarnBg"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 6, 0, 0),
                Child = row,
            };
        }

        private Button ActionButton(string glyph, string text, bool primary)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = glyph, Style = (Style)FindResource("Icon"), FontSize = 13, Margin = new Thickness(0, 0, 7, 0) });
            content.Children.Add(new TextBlock { Text = text });
            var b = new Button { Content = content, Margin = new Thickness(0, 0, 8, 8) };
            if (primary) b.Style = (Style)FindResource("Primary");
            return b;
        }

        private static void SetButtonText(Button b, string text)
        {
            if (b.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock tb) tb.Text = text;
        }

        private static void CopyWithFeedback(Button b, string text, bool sensitive)
        {
            if (!SafeClipboard.SetText(text, sensitive)) return;
            string original = (b.Content as StackPanel)?.Children.Count > 1 ? ((TextBlock)((StackPanel)b.Content).Children[1]).Text : null;
            SetButtonText(b, S.Copied);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (original != null) SetButtonText(b, original);
            };
            timer.Start();
        }

        private static void OpenTarget(string target)
        {
            // Only http(s), mailto, tel, sms or our own OpenStreetMap link get here (see QrContent).
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Storage.LogError(ex);
            }
        }

        private void SaveVcf(QrContent c)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "vCard (*.vcf)|*.vcf",
                FileName = SafeFileName(string.IsNullOrWhiteSpace(c.Summary) ? "contact" : c.Summary) + ".vcf",
            };
            if (dlg.ShowDialog(this) == true)
            {
                File.WriteAllText(dlg.FileName, c.Raw, new UTF8Encoding(false));
            }
        }

        private static string SafeFileName(string s)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Trim();
        }

        internal static string KindIcon(ContentKind k)
        {
            switch (k)
            {
                case ContentKind.Url: return "";
                case ContentKind.Wifi: return "";
                case ContentKind.Email: return "";
                case ContentKind.Phone: return "";
                case ContentKind.Sms: return "";
                case ContentKind.Contact: return "";
                case ContentKind.Location: return "";
                case ContentKind.Otp: return "";
                case ContentKind.Invoice: return "";
                default: return "";
            }
        }

        private static string KindLabel(ContentKind k)
        {
            switch (k)
            {
                case ContentKind.Url: return S.KindUrl;
                case ContentKind.Wifi: return S.KindWifi;
                case ContentKind.Email: return S.KindEmail;
                case ContentKind.Phone: return S.KindPhone;
                case ContentKind.Sms: return S.KindSms;
                case ContentKind.Contact: return S.KindContact;
                case ContentKind.Location: return S.KindLocation;
                case ContentKind.Otp: return S.KindOtp;
                case ContentKind.Invoice: return S.KindInvoice;
                default: return S.KindText;
            }
        }

        private void OnScanAgain(object sender, RoutedEventArgs e) => App.Instance.ScanScreen(fromWindow: false);

        private void OnSelectRegion(object sender, RoutedEventArgs e) => App.Instance.SelectRegion(fromWindow: false);

        private void OnClose(object sender, RoutedEventArgs e) => Hide();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Reused between scans: hide instead of destroying.
            e.Cancel = true;
            Hide();
        }
    }
}
