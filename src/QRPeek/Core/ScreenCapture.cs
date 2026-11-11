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
    internal enum ScreenPlace { Main, Left, Right, Above, Below }

    /// <summary>One monitor's pixels, captured at full physical resolution.</summary>
    internal sealed class MonitorShot : IDisposable
    {
        public MonitorShot(Rectangle bounds, Bitmap image, bool isPrimary = false)
        {
            Bounds = bounds;
            Image = image;
            IsPrimary = isPrimary;
        }

        /// <summary>Monitor rectangle in physical pixels, virtual-screen coordinates.</summary>
        public Rectangle Bounds { get; }

        public bool IsPrimary { get; }

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
                    shots.Add(new MonitorShot(b, bmp, screen.Primary));
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Secure desktop (UAC prompt, lock screen): nothing to read.
                    bmp.Dispose();
                }
            }
            return shots;
        }

        /// <summary>
        /// Where <paramref name="monitor"/> sits relative to the main screen, for
        /// "found on the left screen". Windows' own 1/2/3 numbers aren't used:
        /// the order apps see doesn't always match Settings, but left/right
        /// is never wrong. Two screens on the same side of the main one get
        /// the same word; that's rare and still points the right way.
        /// </summary>
        public static ScreenPlace PlaceOf(Rectangle monitor, Rectangle primary)
        {
            if (monitor == primary) return ScreenPlace.Main;
            int dx = (monitor.Left + monitor.Width / 2) - (primary.Left + primary.Width / 2);
            int dy = (monitor.Top + monitor.Height / 2) - (primary.Top + primary.Height / 2);
            if (Math.Abs(dx) >= Math.Abs(dy)) return dx < 0 ? ScreenPlace.Left : ScreenPlace.Right;
            return dy < 0 ? ScreenPlace.Above : ScreenPlace.Below;
        }

        /// <summary>
        /// Which screen each decoded text was on, given the texts found on
        /// each shot (same order as <paramref name="shots"/>). A code visible
        /// on several screens keeps the first. The caller only shows this
        /// when there is more than one screen.
        /// </summary>
        public static Dictionary<string, ScreenPlace> PlacesOf(IReadOnlyList<MonitorShot> shots, IReadOnlyList<IReadOnlyList<string>> textsPerShot)
        {
            var places = new Dictionary<string, ScreenPlace>(StringComparer.Ordinal);
            MonitorShot primary = null;
            foreach (MonitorShot s in shots) if (s.IsPrimary) primary = s;
            for (int i = 0; i < shots.Count; i++)
            {
                ScreenPlace place = primary == null ? ScreenPlace.Main : PlaceOf(shots[i].Bounds, primary.Bounds);
                foreach (string t in textsPerShot[i])
                {
                    if (!places.ContainsKey(t)) places[t] = place;
                }
            }
            return places;
        }

        public static void DisposeAll(IEnumerable<MonitorShot> shots)
        {
            if (shots == null) return;
            foreach (MonitorShot s in shots) s.Dispose();
        }
    }
}
