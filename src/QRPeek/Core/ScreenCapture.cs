// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QRPeek.Core
{
    /// <summary>One monitor's pixels, captured at full physical resolution.</summary>
    internal sealed class MonitorShot : IDisposable
    {
        public MonitorShot(Rectangle bounds, Bitmap image)
        {
            Bounds = bounds;
            Image = image;
        }

        /// <summary>Monitor rectangle in physical pixels, virtual-screen coordinates.</summary>
        public Rectangle Bounds { get; }

        public Bitmap Image { get; }

        public void Dispose() => Image.Dispose();
    }

    internal static class ScreenCapture
    {
        /// <summary>
        /// Captures every monitor separately. The app is per-monitor DPI aware
        /// (app.manifest), so Screen.Bounds are physical pixels and nothing is
        /// blurred by scaling.
        /// </summary>
        public static List<MonitorShot> CaptureAll()
        {
            var shots = new List<MonitorShot>();
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle b = screen.Bounds;
                if (b.Width <= 0 || b.Height <= 0) continue;

                var bmp = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size, CopyPixelOperation.SourceCopy);
                    }
                    shots.Add(new MonitorShot(b, bmp));
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Secure desktop (UAC prompt, lock screen): nothing to read.
                    bmp.Dispose();
                }
            }
            return shots;
        }

        public static void DisposeAll(IEnumerable<MonitorShot> shots)
        {
            if (shots == null) return;
            foreach (MonitorShot s in shots) s.Dispose();
        }
    }
}
