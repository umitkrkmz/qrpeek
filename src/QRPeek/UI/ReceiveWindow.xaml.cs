// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using QRCoder;
using QRPeek.Core;
using Forms = System.Windows.Forms;

namespace QRPeek.UI
{
    /// <summary>
    /// Phone → PC. Shows a QR with a one-off link; the first device that opens
    /// it holds the session; every list of files it wants to send waits here
    /// for Accept / Reject. See <see cref="LanReceive"/> for the rules.
    /// </summary>
    public partial class ReceiveWindow : Window
    {
        private const int MaxListedNames = 8;

        private readonly DispatcherTimer _countdown;
        private LanReceive _recv;
        private ReceiveOffer _offer;
        private string _lastSaved;
        private int _lastPct = -1;

        public ReceiveWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);
            Topmost = true; // stays up while the user reaches for the phone

            _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdown.Tick += (s, e) => UpdateCountdown();

            Loaded += (s, e) => Placement.OnCursorMonitor(this, 0.12);
            Closed += (s, e) => Stop();
            Start();
        }

        private void Start()
        {
            Stop();
            string folder = App.Instance.ReceiveFolder;
            FolderText.Text = folder;
            FolderText.ToolTip = folder;
            if (!ReceivedFiles.CanWrite(folder))
            {
                ShowError(S.RecvSaveFailed);
                return;
            }

            IPAddress ip = LanHttp.FindLanAddress();
            if (ip == null)
            {
                ShowError(S.RecvOffline);
                return;
            }
            try
            {
                _recv = new LanReceive(folder, ip);
            }
            catch (SocketException ex)
            {
                Storage.LogError(ex);
                ShowError(S.RecvOffline);
                return;
            }

            _recv.DeviceConnected += d => Dispatcher.BeginInvoke(new Action(() => OnDevice(d)));
            _recv.OfferReceived += o => Dispatcher.BeginInvoke(new Action(() => OnOffer(o)));
            _recv.Progress += o =>
            {
                int pct = o.TotalBytes == 0 ? 100 : (int)(o.ReceivedBytes * 100 / o.TotalBytes);
                if (pct == _lastPct) return;
                _lastPct = pct;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Progress.Visibility = Visibility.Visible;
                    Progress.Value = pct;
                    SetStatus("", "Accent", S.RecvReceiving(pct));
                }));
            };
            _recv.OfferFinished += o => Dispatcher.BeginInvoke(new Action(() => OnFinished(o)));

            using (Bitmap bmp = QrGenerator.Render(_recv.Url, QRCodeGenerator.ECCLevel.M, new QrStyle(), 600))
            {
                QrImage.Source = ImageUtil.ToBitmapSource(bmp);
            }
            Heading.Text = S.RecvScanHint;
            QrCard.Visibility = Visibility.Visible;
            DeviceCard.Visibility = Visibility.Collapsed;
            OfferCard.Visibility = Visibility.Collapsed;
            NoteText.Text = S.RecvHint;
            NoteText.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Visible;
            Progress.Visibility = Visibility.Collapsed;
            SetStatus("", "FgMuted", S.RecvWaiting);
            UpdateCountdown();
            _countdown.Start();
        }

        private void OnDevice(ReceiveDevice d)
        {
            if (_recv == null) return;
            Heading.Text = S.ReceiveFromPhone;
            QrCard.Visibility = Visibility.Collapsed;
            DeviceCard.Visibility = Visibility.Visible;
            DeviceText.Text = S.RecvConnected(d.Label + "  ·  " + d.Address);
            CodeText.Text = d.Code;
            NoteText.Visibility = Visibility.Collapsed;
            SetStatus("", "FgMuted", S.RecvPickOnPhone);
        }

        private void OnOffer(ReceiveOffer o)
        {
            if (_recv == null) return;
            _offer = o;
            OfferTitle.Text = S.RecvOffer(o.Files.Count, LanHttp.FormatSize(o.TotalBytes));
            var list = new StringBuilder();
            foreach (OfferedFile f in o.Files.Take(MaxListedNames))
            {
                list.Append(f.Risky ? "⚠ " : "•  ").Append(f.Name).Append("   ").Append(LanHttp.FormatSize(f.Size)).Append('\n');
            }
            if (o.Files.Count > MaxListedNames) list.Append(S.RecvMore(o.Files.Count - MaxListedNames));
            OfferList.Text = list.ToString().TrimEnd('\n');
            RiskyBox.Visibility = o.HasRiskyFiles ? Visibility.Visible : Visibility.Collapsed;
            SpaceText.Visibility = o.EnoughSpace ? Visibility.Collapsed : Visibility.Visible;
            AcceptButton.IsEnabled = o.EnoughSpace;
            OfferCard.Visibility = Visibility.Visible;
            Progress.Visibility = Visibility.Collapsed;
            SetStatus("", "FgMuted", "");

            // Bring the question to the user; nothing happens until they answer.
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            if (o.EnoughSpace) AcceptButton.Focus();
        }

        private void OnAccept(object sender, RoutedEventArgs e)
        {
            if (_recv == null || _offer == null) return;
            if (!_recv.Accept(_offer))
            {
                // No longer pending, or the disk filled up meanwhile.
                SpaceText.Visibility = Visibility.Visible;
                AcceptButton.IsEnabled = false;
                return;
            }
            OfferCard.Visibility = Visibility.Collapsed;
            ChangeFolderButton.IsEnabled = false;
            _lastPct = -1;
            Progress.Value = 0;
            Progress.Visibility = Visibility.Visible;
            SetStatus("", "Accent", S.RecvReceiving(0));
        }

        private void OnReject(object sender, RoutedEventArgs e)
        {
            _recv?.Reject(_offer);
            _offer = null;
            OfferCard.Visibility = Visibility.Collapsed;
            SetStatus("", "FgMuted", S.RecvRejected);
        }

        private void OnFinished(ReceiveOffer o)
        {
            ChangeFolderButton.IsEnabled = true;
            IReadOnlyList<string> saved = o.SavedPaths;
            if (saved.Count > 0)
            {
                _lastSaved = saved[saved.Count - 1];
                ShowFolderButton.Visibility = Visibility.Visible;
            }
            if (o.State == OfferState.Done)
            {
                Progress.Value = 100;
                SetStatus("", "Ok", S.RecvDone(saved.Count));
            }
            else
            {
                Progress.Visibility = Visibility.Collapsed;
                SetStatus("", "Bad", S.RecvFailed);
            }
        }

        private void ShowError(string message)
        {
            Stop();
            Heading.Text = S.ReceiveFromPhone;
            QrCard.Visibility = Visibility.Collapsed;
            DeviceCard.Visibility = Visibility.Collapsed;
            OfferCard.Visibility = Visibility.Collapsed;
            Progress.Visibility = Visibility.Collapsed;
            NoteText.Text = message;
            NoteText.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Collapsed;
        }

        private void SetStatus(string glyph, string brushKey, string text)
        {
            StatusIcon.Text = glyph;
            StatusIcon.Foreground = (System.Windows.Media.Brush)FindResource(brushKey);
            StatusText.Text = text;
        }

        private void UpdateCountdown()
        {
            if (_recv == null) return;
            if (_recv.Transferring)
            {
                CountdownText.Text = "";
                return;
            }
            TimeSpan left = _recv.Expires - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                ShowError(S.RecvExpired);
                return;
            }
            CountdownText.Text = S.LanExpires(((int)left.TotalMinutes) + ":" + left.Seconds.ToString("00"));
        }

        private void Stop()
        {
            _countdown?.Stop();
            _recv?.Dispose();
            _recv = null;
            _offer = null;
        }

        private void OnChangeFolder(object sender, RoutedEventArgs e)
        {
            using (var dlg = new Forms.FolderBrowserDialog { SelectedPath = App.Instance.ReceiveFolder, ShowNewFolderButton = true })
            {
                if (dlg.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrEmpty(dlg.SelectedPath)) return;
                if (!ReceivedFiles.CanWrite(dlg.SelectedPath))
                {
                    NoteText.Text = S.RecvSaveFailed;
                    NoteText.Visibility = Visibility.Visible;
                    return;
                }
                App app = App.Instance;
                app.Settings.ReceiveFolder = dlg.SelectedPath;
                app.SaveSettings();
                FolderText.Text = dlg.SelectedPath;
                FolderText.ToolTip = dlg.SelectedPath;
                if (_recv != null) _recv.Folder = dlg.SelectedPath;
                else Start(); // e.g. the old folder wasn't writable
            }
        }

        private void OnShowInFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastSaved != null && File.Exists(_lastSaved))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _lastSaved + "\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + App.Instance.ReceiveFolder + "\"");
            }
            catch (System.ComponentModel.Win32Exception ex) { Storage.LogError(ex); }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
