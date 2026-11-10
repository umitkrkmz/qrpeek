// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using QRCoder;
using QRPeek.Core;

namespace QRPeek.UI
{
    public partial class GeneratorWindow : Window
    {
        private const int PreviewPx = 640;
        private readonly DispatcherTimer _debounce;
        private readonly bool _ready;
        private Bitmap _customLogo;
        private Bitmap _appLogo;
        private System.Windows.Controls.RadioButton _lastLogoMode;
        private System.Windows.Controls.RadioButton _eccBeforeLogo;
        private int _renderId;

        public GeneratorWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                Render();
            };
            _ready = true;
            Render();
            Loaded += (s, e) => TextField.Focus();
            Closed += (s, e) =>
            {
                _customLogo?.Dispose();
                _appLogo?.Dispose();
            };
        }

        internal void SetText(string text)
        {
            ModeText.IsChecked = true;
            TextField.Text = text;
        }

        private void OnInputChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            TextPanel.Visibility = ModeText.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            WifiPanel.Visibility = ModeWifi.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PasswordInput.IsEnabled = SecNone.IsChecked != true;
            _debounce.Stop();
            _debounce.Start();
        }

        /// <summary>The text that goes into the QR, or null when there's nothing to encode yet.</summary>
        private string Payload()
        {
            if (ModeText.IsChecked == true)
            {
                return string.IsNullOrEmpty(TextField.Text) ? null : TextField.Text;
            }
            if (string.IsNullOrEmpty(SsidInput.Text)) return null;
            string sec = SecWpa3.IsChecked == true ? "SAE" : SecWep.IsChecked == true ? "WEP" : SecNone.IsChecked == true ? "nopass" : "WPA";
            return QrGenerator.WifiPayload(SsidInput.Text, PasswordInput.Text, sec, HiddenInput.IsChecked == true);
        }

        private QRCodeGenerator.ECCLevel Ecc =>
            EccL.IsChecked == true ? QRCodeGenerator.ECCLevel.L :
            EccQ.IsChecked == true ? QRCodeGenerator.ECCLevel.Q :
            EccH.IsChecked == true ? QRCodeGenerator.ECCLevel.H :
            QRCodeGenerator.ECCLevel.M;

        private QrStyle CurrentStyle() => new QrStyle
        {
            DarkHex = ColorChoices.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? "#000000",
            Modules = PatRounded.IsChecked == true ? ModuleStyle.Rounded : PatDots.IsChecked == true ? ModuleStyle.Dots : ModuleStyle.Square,
            Eyes = EyeRounded.IsChecked == true ? EyeStyle.Rounded : EyeCircle.IsChecked == true ? EyeStyle.Circle : EyeStyle.Square,
            Logo = CurrentLogo,
            LogoScale = LogoSmall.IsChecked == true ? 0.18 : LogoLarge.IsChecked == true ? 0.28 : 0.22,
        };

        private int ExportPx => Size512.IsChecked == true ? 512 : Size2048.IsChecked == true ? 2048 : 1024;

        private void Render()
        {
            bool hasLogo = CurrentLogo != null;
            LogoOptions.Visibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;
            EccChoices.IsEnabled = !hasLogo;
            EccNote.Visibility = hasLogo ? Visibility.Visible : Visibility.Collapsed;

            int id = ++_renderId;
            string payload = Payload();
            Bitmap bmp = null;
            if (payload != null)
            {
                try
                {
                    bmp = QrGenerator.Render(payload, Ecc, CurrentStyle(), PreviewPx);
                }
                catch (QRCoder.Exceptions.DataTooLongException)
                {
                    PreviewMessage.Text = S.GenTooLong;
                }
            }
            else
            {
                PreviewMessage.Text = S.GenEmpty;
            }

            bool ok = bmp != null;
            Preview.Source = ok ? ImageUtil.ToBitmapSource(bmp) : null;
            PreviewMessage.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
            CopyButton.IsEnabled = PngButton.IsEnabled = SvgButton.IsEnabled = ok;

            if (ok) CheckReadable(bmp, payload, id);
            else CheckRow.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Reads the freshly drawn code back with QRPeek's own scanner, so a
        /// style or logo that breaks the code is caught before it's printed.
        /// </summary>
        private async void CheckReadable(Bitmap bmp, string payload, int id)
        {
            ShowCheck(null);
            bool readable;
            try
            {
                List<string> found = await Task.Run(() => QrScanner.Decode(bmp));
                readable = found.Contains(payload);
            }
            finally
            {
                bmp.Dispose();
            }
            if (id == _renderId) ShowCheck(readable);
        }

        private void ShowCheck(bool? readable)
        {
            CheckRow.Visibility = Visibility.Visible;
            if (readable == null)
            {
                CheckIcon.Text = ""; // sync
                CheckIcon.Foreground = CheckText.Foreground = (System.Windows.Media.Brush)FindResource("FgMuted");
                CheckText.Text = S.Checking;
                return;
            }
            CheckIcon.Text = readable.Value ? "" : "";
            CheckIcon.Foreground = CheckText.Foreground = (System.Windows.Media.Brush)FindResource(readable.Value ? "Ok" : "Warn");
            CheckText.Text = readable.Value ? S.CheckOk : S.CheckFail;
        }

        /// <summary>The logo the current choice stands for: none, QRPeek's own, or the user's file.</summary>
        private Bitmap CurrentLogo =>
            LogoApp.IsChecked == true ? AppLogo() :
            LogoCustom.IsChecked == true ? _customLogo :
            null;

        private Bitmap AppLogo()
        {
            if (_appLogo == null)
            {
                var uri = new Uri("pack://application:,,,/Assets/QRPeek.png");
                using (Stream s = Application.GetResourceStream(uri).Stream)
                using (var tmp = new Bitmap(s))
                {
                    _appLogo = new Bitmap(tmp); // detached from the stream
                }
            }
            return _appLogo;
        }

        private void OnLogoMode(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            // "My own logo" without a file yet: ask for one right away; cancelling goes back.
            if (LogoCustom.IsChecked == true && _customLogo == null && !PickLogo())
            {
                (_lastLogoMode ?? LogoNone).IsChecked = true;
                return;
            }
            _lastLogoMode = (System.Windows.Controls.RadioButton)sender;

            // A logo always uses H (see QrGenerator.EffectiveEcc): show that, and
            // give the user's own choice back when the logo is turned off.
            bool logo = LogoNone.IsChecked != true;
            if (logo && _eccBeforeLogo == null)
            {
                _eccBeforeLogo = new[] { EccL, EccM, EccQ, EccH }.First(r => r.IsChecked == true);
                EccH.IsChecked = true;
            }
            else if (!logo && _eccBeforeLogo != null)
            {
                _eccBeforeLogo.IsChecked = true;
                _eccBeforeLogo = null;
            }

            CustomLogoRow.Visibility = LogoCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            Render();
        }

        private void OnPickLogo(object sender, RoutedEventArgs e)
        {
            if (PickLogo()) Render();
        }

        private bool PickLogo()
        {
            var dlg = new OpenFileDialog { Filter = S.ImageFilter };
            if (dlg.ShowDialog(this) != true) return false;
            try
            {
                Bitmap logo = ImageUtil.LoadFile(dlg.FileName);
                _customLogo?.Dispose();
                _customLogo = logo;
                LogoName.Text = Path.GetFileName(dlg.FileName);
                return true;
            }
            catch (Exception ex) when (ImageUtil.IsImageLoadError(ex))
            {
                MessageBox.Show(this, S.ImageOpenFailed, S.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            string payload = Payload();
            if (payload == null) return;
            try
            {
                using (Bitmap bmp = QrGenerator.Render(payload, Ecc, CurrentStyle(), ExportPx))
                {
                    Clipboard.SetImage(ImageUtil.ToBitmapSource(bmp));
                }
                CopyLabel.Text = S.Copied;
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (s, a) => { t.Stop(); CopyLabel.Text = S.CopyImage; };
                t.Start();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                Storage.LogError(ex);
            }
        }

        private void OnSavePng(object sender, RoutedEventArgs e)
        {
            string payload = Payload();
            if (payload == null) return;
            string path = AskPath("PNG (*.png)|*.png", ".png");
            if (path != null) File.WriteAllBytes(path, QrGenerator.Png(payload, Ecc, CurrentStyle(), ExportPx));
        }

        private void OnSaveSvg(object sender, RoutedEventArgs e)
        {
            string payload = Payload();
            if (payload == null) return;
            string path = AskPath("SVG (*.svg)|*.svg", ".svg");
            if (path != null) File.WriteAllText(path, QrGenerator.Svg(payload, Ecc, CurrentStyle()), new UTF8Encoding(false));
        }

        private string AskPath(string filter, string ext)
        {
            string name = ModeWifi.IsChecked == true && !string.IsNullOrWhiteSpace(SsidInput.Text) ? "wifi-" + SsidInput.Text : "qrcode";
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            var dlg = new SaveFileDialog { Filter = filter, FileName = name + ext };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        }
    }
}
