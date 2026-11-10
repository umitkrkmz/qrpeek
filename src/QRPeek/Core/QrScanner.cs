// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace QRPeek.Core
{
    internal static class QrScanner
    {
        /// <summary>
        /// Finds every QR code in the image. Returns distinct texts in the order
        /// found (top-to-bottom is not guaranteed). Never throws for "nothing found".
        /// </summary>
        public static List<string> Decode(Bitmap image) => Decode(image, thorough: true);

        /// <param name="thorough">
        /// False for the background screen watcher: one quick pass, no rescaled
        /// retries, so watching costs little CPU. A manual scan is always thorough.
        /// </param>
        public static List<string> Decode(Bitmap image, bool thorough)
        {
            var found = new List<string>();
            if (image == null || image.Width < 8 || image.Height < 8) return found;

            AddAll(found, DecodeOnce(image, thorough));
            if (!thorough) return found;

            // A small crop (region select, a thumbnail from the clipboard) often
            // has QR modules only 1-2 px wide. Enlarging with hard edges gives
            // the binarizer enough pixels to work with.
            if (found.Count == 0 && Math.Max(image.Width, image.Height) < 900)
            {
                int factor = Math.Max(image.Width, image.Height) < 300 ? 4 : 2;
                using (Bitmap big = Scale(image, factor))
                {
                    AddAll(found, DecodeOnce(big));
                }
            }

            // A very large on-screen QR (presentation, full-screen image) can
            // be harder at full resolution than at half.
            if (found.Count == 0 && Math.Min(image.Width, image.Height) >= 1200)
            {
                using (Bitmap small = ScaleTo(image, image.Width / 2, image.Height / 2, InterpolationMode.HighQualityBilinear))
                {
                    AddAll(found, DecodeOnce(small));
                }
            }

            // Last resort for dense codes drawn small (e-invoices, PDFs at page
            // zoom): see DecodeByGrid.
            if (found.Count == 0)
            {
                string text = DecodeByGrid(image);
                if (text != null) found.Add(text);
            }

            return found;
        }

        /// <summary>
        /// Screen-specific fallback. A dense QR (version 15+) that a browser
        /// shrank to ~2 px per module is smeared: about 40% of its pixels are
        /// grey, and module edges fall between pixels. ZXing samples each module
        /// at a rounded pixel, which then often hits the neighbour. Here the
        /// three finder squares still give the grid, and every module is
        /// sampled at its exact (fractional) center with bilinear
        /// interpolation, then thresholded halfway between the code's own dark
        /// and light levels. Screen codes are flat and unrotated, so the affine
        /// grid from the finders is exact. Neighbouring versions are tried
        /// because the size estimate from blurred finders can be off by one.
        /// </summary>
        internal static string DecodeByGrid(Bitmap image)
        {
            // The finder squares of a code at ~2.3 px per module binarize
            // unreliably: whether they're found depended on where the selection
            // edge fell. A white margin and a smooth 2x enlargement (rings
            // become ~4.7 px) make finding them stable. Enlarging is skipped
            // for big captures (full screen) to keep memory and time in check.
            using (Bitmap padded = Pad(image, 16, 1))
            {
                string text = GridCore(padded);
                if (text != null) return text;
            }
            if (Math.Max(image.Width, image.Height) <= 2000) // up to a full-HD screen
            {
                using (Bitmap big = Pad(image, 32, 2))
                {
                    return GridCore(big);
                }
            }
            return null;
        }

        /// <summary>A copy on a white margin, optionally enlarged with bicubic smoothing.</summary>
        private static Bitmap Pad(Bitmap src, int margin, int scale)
        {
            var dst = new Bitmap(src.Width * scale + 2 * margin, src.Height * scale + 2 * margin, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.Clear(Color.White);
                g.InterpolationMode = scale == 1 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(margin, margin, src.Width * scale, src.Height * scale));
            }
            return dst;
        }

        private static string GridCore(Bitmap image)
        {
            byte[] bgra = ToBgra(image, out int w, out int h);
            var lum = new float[w * h];
            for (int i = 0; i < lum.Length; i++)
            {
                lum[i] = bgra[i * 4] * 0.114f + bgra[i * 4 + 1] * 0.587f + bgra[i * 4 + 2] * 0.299f;
            }

            var hints = new Dictionary<DecodeHintType, object>
            {
                { DecodeHintType.TRY_HARDER, true },
                { DecodeHintType.CHARACTER_SET, "UTF-8" },
            };
            var source = new RGBLuminanceSource(bgra, w, h, RGBLuminanceSource.BitmapFormat.BGRA32);

            var tried = new HashSet<string>();
            foreach (Binarizer binarizer in new Binarizer[] { new HybridBinarizer(source), new GlobalHistogramBinarizer(source) })
            {
                BitMatrix matrix;
                try { matrix = binarizer.BlackMatrix; }
                catch (ReaderException) { continue; }

                // Every plausible finder triple, not only ZXing's single "best"
                // one: logos with rings (the GİB seal), bold letters and table
                // borders next to the code produce look-alike finders, and which
                // triple wins could change with a few pixels of selection.
                foreach (Triple t in Triples(matrix, hints))
                {
                    string key = (int)t.TL.X + "," + (int)t.TL.Y + "," + (int)t.TR.X + "," + (int)t.BL.Y;
                    if (!tried.Add(key)) continue;

                    float mid = Threshold(lum, w, h, t.TL, t.TR, t.BL, t.Guess);
                    foreach (int delta in new[] { 0, 4, -4, 8, -8, 12, -12 })
                    {
                        int n = t.Guess + delta;
                        if (n < 21 || n > 177 || (n - 17) % 4 != 0) continue;
                        string text = SampleAndDecode(lum, w, h, t.TL, t.TR, t.BL, n, mid, hints);
                        if (text != null) return text;
                    }
                }
            }
            return null;
        }

        private sealed class Triple
        {
            public ResultPoint TL, TR, BL;
            /// <summary>Estimated modules per side (17 + 4 * version).</summary>
            public int Guess;
        }

        private const int MaxTriples = 24;

        private static IEnumerable<Triple> Triples(BitMatrix matrix, IDictionary<DecodeHintType, object> hints)
        {
            // ZXing's own pick first: right in the common case, and it estimates the size well.
            DetectorResult det = null;
            try { det = new ZXing.QrCode.Internal.Detector(matrix).detect(hints); }
            catch (ReaderException) { }
            if (det?.Points != null && det.Points.Length >= 3 && det.Bits != null)
            {
                // ZXing's order: bottom-left, top-left, top-right finder centers.
                yield return new Triple { BL = det.Points[0], TL = det.Points[1], TR = det.Points[2], Guess = det.Bits.Width };
            }

            ZXing.QrCode.Internal.FinderPatternInfo[] infos = null;
            try { infos = new ZXing.Multi.QrCode.Internal.MultiFinderPatternFinder(matrix, null).findMulti(hints); }
            catch (ReaderException) { }
            if (infos == null) yield break;

            int count = 0;
            foreach (ZXing.QrCode.Internal.FinderPatternInfo info in infos)
            {
                if (info == null || ++count > MaxTriples) yield break;
                ZXing.QrCode.Internal.FinderPattern tl = info.TopLeft, tr = info.TopRight, bl = info.BottomLeft;
                float module = (tl.EstimatedModuleSize + tr.EstimatedModuleSize + bl.EstimatedModuleSize) / 3f;
                if (module <= 0) continue;
                float across = (Dist(tl, tr) + Dist(tl, bl)) / 2f;
                int raw = (int)Math.Round(across / module) + 7;
                int guess = 17 + 4 * (int)Math.Round((raw - 17) / 4.0); // nearest real QR size
                if (guess < 21 || guess > 177) continue;
                yield return new Triple { TL = tl, TR = tr, BL = bl, Guess = guess };
            }
        }

        private static float Dist(ResultPoint a, ResultPoint b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static string SampleAndDecode(float[] lum, int w, int h, ResultPoint tl, ResultPoint tr, ResultPoint bl,
            int n, float threshold, IDictionary<DecodeHintType, object> hints)
        {
            // Finder centers sit 3.5 modules in from the edges, (n - 7) modules apart.
            float ux = (tr.X - tl.X) / (n - 7f), uy = (tr.Y - tl.Y) / (n - 7f);
            float vx = (bl.X - tl.X) / (n - 7f), vy = (bl.Y - tl.Y) / (n - 7f);
            var bits = new BitMatrix(n);
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    float x = tl.X + (col - 3) * ux + (row - 3) * vx;
                    float y = tl.Y + (col - 3) * uy + (row - 3) * vy;
                    if (Bilinear(lum, w, h, x, y) < threshold) bits[col, row] = true;
                }
            }
            try
            {
                DecoderResult r = new ZXing.QrCode.Internal.Decoder().decode(bits, hints);
                return string.IsNullOrEmpty(r?.Text) ? null : r.Text;
            }
            catch (ReaderException)
            {
                return null;
            }
        }

        /// <summary>Halfway between the mean dark and mean light level inside the code (iterated).</summary>
        private static float Threshold(float[] lum, int w, int h, ResultPoint tl, ResultPoint tr, ResultPoint bl, int n)
        {
            float module = (float)Math.Sqrt((tr.X - tl.X) * (tr.X - tl.X) + (tr.Y - tl.Y) * (tr.Y - tl.Y)) / Math.Max(1, n - 7);
            int x0 = Clamp((int)(Math.Min(tl.X, bl.X) - 3.5f * module), 0, w - 1);
            int x1 = Clamp((int)(tr.X + 3.5f * module), 0, w - 1);
            int y0 = Clamp((int)(Math.Min(tl.Y, tr.Y) - 3.5f * module), 0, h - 1);
            int y1 = Clamp((int)(bl.Y + 3.5f * module), 0, h - 1);

            float mid = 128;
            for (int iter = 0; iter < 6; iter++)
            {
                double dark = 0, light = 0;
                int nd = 0, nl = 0;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float v = lum[y * w + x];
                        if (v < mid) { dark += v; nd++; } else { light += v; nl++; }
                    }
                }
                if (nd == 0 || nl == 0) break;
                mid = (float)((dark / nd + light / nl) / 2);
            }
            return mid;
        }

        private static float Bilinear(float[] lum, int w, int h, float x, float y)
        {
            x -= 0.5f; // pixel centers are at +0.5
            y -= 0.5f;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float P(int xx, int yy) => lum[Clamp(yy, 0, h - 1) * w + Clamp(xx, 0, w - 1)];
            return P(x0, y0) * (1 - fx) * (1 - fy) + P(x0 + 1, y0) * fx * (1 - fy)
                 + P(x0, y0 + 1) * (1 - fx) * fy + P(x0 + 1, y0 + 1) * fx * fy;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static IEnumerable<string> DecodeOnce(Bitmap image, bool thorough = true)
        {
            byte[] pixels = ToBgra(image, out int width, out int height);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = thorough,
                Options = new DecodingOptions
                {
                    TryHarder = thorough,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    CharacterSet = "UTF-8",
                },
            };

            LuminanceSource source = new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
            Result[] results = reader.DecodeMultiple(source);

            // Light-on-dark codes (dark mode pages). ZXing's TryInverted option
            // only applies to single decoding, so invert by hand for the
            // multi-code pass.
            if (results == null || results.Length == 0) results = reader.DecodeMultiple(source.invert());

            if (results == null) yield break;
            foreach (Result r in results)
            {
                if (r != null && !string.IsNullOrEmpty(r.Text)) yield return r.Text;
            }
        }

        private static void AddAll(List<string> into, IEnumerable<string> items)
        {
            foreach (string s in items)
            {
                if (!into.Contains(s)) into.Add(s);
            }
        }

        private static byte[] ToBgra(Bitmap image, out int width, out int height)
        {
            width = image.Width;
            height = image.Height;
            var rect = new Rectangle(0, 0, width, height);
            BitmapData data = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowBytes = width * 4;
                var buffer = new byte[rowBytes * height];
                for (int y = 0; y < height; y++)
                {
                    IntPtr row = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(row, buffer, y * rowBytes, rowBytes);
                }
                return buffer;
            }
            finally
            {
                image.UnlockBits(data);
            }
        }

        private static Bitmap Scale(Bitmap src, int factor) =>
            ScaleTo(src, src.Width * factor, src.Height * factor, InterpolationMode.NearestNeighbor);

        private static Bitmap ScaleTo(Bitmap src, int w, int h, InterpolationMode mode)
        {
            var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = mode;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            return dst;
        }
    }
}
