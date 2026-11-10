// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace QRPeek.Core
{
    /// <summary>
    /// Watch mode: looks at the screen every ~1.5 s and reports QR codes that
    /// newly appear (a slide in a meeting, a video, a web page). Cheap by
    /// design: unchanged screens are skipped by a pixel fingerprint, decoding
    /// is a single quick pass, and a code is reported once per 10 minutes.
    /// Pixels never leave memory; nothing is stored.
    /// </summary>
    internal sealed class ScreenWatcher : IDisposable
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1500);
        private static readonly TimeSpan Quiet = TimeSpan.FromMinutes(10);

        private readonly Timer _timer;
        private readonly Func<bool> _paused;
        private readonly Dictionary<string, DateTime> _seen = new Dictionary<string, DateTime>();
        private readonly Dictionary<Rectangle, ulong> _fingerprints = new Dictionary<Rectangle, ulong>();
        private int _running;

        /// <summary>Raised on a worker thread with codes not reported in the last 10 minutes.</summary>
        public event Action<List<string>> Found;

        /// <param name="paused">Checked before every look; true skips it (e.g. QRPeek is showing a QR itself).</param>
        public ScreenWatcher(Func<bool> paused)
        {
            _paused = paused;
            _timer = new Timer(_ => Tick(), null, Interval, Interval);
        }

        /// <summary>Tells the watcher a code was already shown to the user (manual scan), so it doesn't pop up again.</summary>
        public void MarkSeen(IEnumerable<string> texts)
        {
            lock (_seen)
            {
                foreach (string t in texts) _seen[t] = DateTime.UtcNow;
            }
        }

        private void Tick()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;
            List<MonitorShot> shots = null;
            try
            {
                if (_paused()) return;
                shots = ScreenCapture.CaptureAll();
                var fresh = new List<string>();
                foreach (MonitorShot shot in shots)
                {
                    ulong fp = Fingerprint(shot.Image);
                    if (_fingerprints.TryGetValue(shot.Bounds, out ulong last) && last == fp) continue;
                    _fingerprints[shot.Bounds] = fp;

                    foreach (string t in QrScanner.Decode(shot.Image, thorough: false))
                    {
                        if (IsNew(t) && !fresh.Contains(t)) fresh.Add(t);
                    }
                }
                if (fresh.Count > 0) Found?.Invoke(fresh);
            }
            catch (Exception ex)
            {
                Storage.LogError(ex); // never let the timer thread die
            }
            finally
            {
                ScreenCapture.DisposeAll(shots);
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private bool IsNew(string text)
        {
            lock (_seen)
            {
                DateTime now = DateTime.UtcNow;
                foreach (string old in _seen.Where(kv => now - kv.Value > Quiet).Select(kv => kv.Key).ToList()) _seen.Remove(old);
                if (_seen.ContainsKey(text)) return false;
                _seen[text] = now;
                return true;
            }
        }

        /// <summary>FNV-1a over a sparse grid of pixels: changes when the screen content changes.</summary>
        private static ulong Fingerprint(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                ulong h = 14695981039346656037UL;
                const int Cols = 96, Rows = 54;
                for (int r = 0; r < Rows; r++)
                {
                    int y = (int)((r + 0.5) * bmp.Height / Rows);
                    for (int c = 0; c < Cols; c++)
                    {
                        int x = (int)((c + 0.5) * bmp.Width / Cols);
                        int px = Marshal.ReadInt32(data.Scan0, y * data.Stride + x * 4);
                        h = (h ^ (uint)px) * 1099511628211UL;
                    }
                }
                return h;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
