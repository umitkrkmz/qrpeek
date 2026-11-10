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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using QRCoder;
using QRPeek.Core;

namespace QRPeek.UI
{
    /// <summary>
    /// PC → phone. Short text and links become a QR directly (no network at
    /// all). Files, clipboard images and long text are shared over the local
    /// network: the QR holds a one-off link to this PC.
    /// </summary>
    public partial class SendWindow : Window
    {
        /// <summary>Above this, a QR gets too dense for a phone to read off a screen reliably.</summary>
        private const int MaxQrTextBytes = 1200;

        private readonly DispatcherTimer _countdown;
        private LanShare _share;
        private string _tempDir;
        private int _lastPct = -1;

        public SendWindow()
        {
            InitializeComponent();
            WindowTheme.Attach(this);
            Topmost = true; // stays up while the user reaches for the phone

            // Leftovers from a crash or a share that was replaced; only one send window exists at a time.
            try { Directory.Delete(Path.Combine(Path.GetTempPath(), "QRPeek"), true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdown.Tick += (s, e) => UpdateCountdown();

            Loaded += (s, e) => Placement.OnCursorMonitor(this, 0.12);
            Closed += (s, e) => StopShare();
            DragEnter += (s, e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            Drop += (s, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) LoadFiles(paths);
            };
        }

        internal void LoadClipboard()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    LoadFiles(Clipboard.GetFileDropList().Cast<string>().ToList());
                    return;
                }
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        LoadText(text);
                        return;
                    }
                }
                if (Clipboard.ContainsImage())
                {
                    using (Bitmap bmp = ImageUtil.FromClipboard())
                    {
                        if (bmp != null)
                        {
                            string path = TempPath(S.ClipboardFileName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            LoadFiles(new[] { path }, temp: true);
                            return;
                        }
                    }
                }
            }
            catch (COMException)
            {
                // Clipboard locked by another app: show the empty state.
            }
            ShowEmpty();
        }

        internal void LoadText(string text)
        {
            StopShare();
            if (Encoding.UTF8.GetByteCount(text) > MaxQrTextBytes)
            {
                string path = TempPath(S.ClipboardFileName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(path, text, new UTF8Encoding(false));
                LoadFiles(new[] { path }, temp: true, note: S.TextTooLongForQr);
                return;
            }

            ShowQr(text);
            string oneLine = text.Trim().Replace("\r", " ").Replace("\n", " ");
            WhatText.Text = oneLine.Length > 140 ? oneLine.Substring(0, 140) + "…" : oneLine;
            NoteText.Text = "";
            NoteText.Visibility = Visibility.Collapsed;
            StatusRow.Visibility = Visibility.Collapsed;
            Progress.Visibility = Visibility.Collapsed;
        }

        internal void LoadFiles(IEnumerable<string> paths, bool temp = false, string note = null)
        {
            StopShare(keepTemp: temp);
            List<SharedFile> files = paths.Where(File.Exists).Select(p => new SharedFile(p, deleteAfter: temp)).ToList();
            if (files.Count == 0)
            {
                ShowEmpty();
                return;
            }

            IPAddress ip = LanShare.FindLanAddress();
            if (ip == null)
            {
                ShowError(S.LanOffline);
                return;
            }
            try
            {
                _share = new LanShare(files, ip);
            }
            catch (SocketException ex)
            {
                Storage.LogError(ex);
                ShowError(S.LanOffline);
                return;
            }

            _share.Progress += (sent, total) =>
            {
                int pct = total == 0 ? 100 : (int)(sent * 100 / total);
                if (pct == _lastPct) return;
                _lastPct = pct;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Progress.Visibility = Visibility.Visible;
                    Progress.Value = pct;
                    SetStatus("", "Accent", S.LanSending(pct));
                }));
            };
            _share.Completed += f => Dispatcher.BeginInvoke(new Action(() =>
            {
                Progress.Value = 100;
                SetStatus("", "Ok", S.LanDone(f.Name));
            }));

            ShowQr(_share.Url);
            long total = files.Sum(f => f.Length);
            WhatText.Text = files.Count == 1 ? files[0].Name + "  ·  " + LanShare.FormatSize(total) : S.LanFiles(files.Count, LanShare.FormatSize(total));
            NoteText.Text = (note != null ? note + " " : "") + S.LanHint;
            NoteText.Visibility = Visibility.Visible;
            StatusRow.Visibility = Visibility.Visible;
            Progress.Visibility = Visibility.Collapsed;
            _lastPct = -1;
            SetStatus("", "FgMuted", S.LanWaiting);
            UpdateCountdown();
            _countdown.Start();
        }

        private void ShowQr(string payload)
        {
            using (Bitmap bmp = QrGenerator.Render(payload, QRCodeGenerator.ECCLevel.M, new QrStyle(), 600))
            {
                QrImage.Source = ImageUtil.ToBitmapSource(bmp);
            }
            Heading.Text = S.SendScanHint;
            QrCard.Visibility = Visibility.Visible;
            EmptyCard.Visibility = Visibility.Collapsed;
        }

        private void ShowEmpty()
        {
            StopShare();
            Heading.Text = S.SendToPhone;
            QrCard.Visibility = Visibility.Collapsed;
            EmptyCard.Visibility = Visibility.Visible;
            WhatText.Text = "";
            NoteText.Visibility = Visibility.Collapsed;
            StatusRow.Visibility = Visibility.Collapsed;
            Progress.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            ShowEmpty();
            NoteText.Text = message;
            NoteText.Visibility = Visibility.Visible;
        }

        private void SetStatus(string glyph, string brushKey, string text)
        {
            StatusIcon.Text = glyph;
            StatusIcon.Foreground = (System.Windows.Media.Brush)FindResource(brushKey);
            StatusText.Text = text;
        }

        private void UpdateCountdown()
        {
            if (_share == null) return;
            TimeSpan left = _share.Expires - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                ShowError(S.LanExpired);
                return;
            }
            CountdownText.Text = S.LanExpires(((int)left.TotalMinutes) + ":" + left.Seconds.ToString("00"));
        }

        private string TempPath(string fileName)
        {
            // A folder per share keeps the friendly file name the phone will see.
            _tempDir = Path.Combine(Path.GetTempPath(), "QRPeek", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
            return Path.Combine(_tempDir, fileName);
        }

        private void StopShare(bool keepTemp = false)
        {
            _countdown.Stop();
            _share?.Dispose(); // also deletes temp files it was given
            _share = null;
            if (!keepTemp && _tempDir != null)
            {
                try { Directory.Delete(_tempDir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                _tempDir = null;
            }
        }

        private void OnPickFiles(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Multiselect = true };
            if (dlg.ShowDialog(this) == true) LoadFiles(dlg.FileNames);
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
