// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace QRPeek.Core
{
    /// <summary>
    /// Reads text from an image with the OCR built into Windows 10/11
    /// (Windows.Media.Ocr): nothing to download, nothing leaves the PC.
    /// Used when an area or image has no QR code, so its text can be copied.
    /// The languages are the ones installed in Windows (Settings → Time &amp;
    /// language → Language: a language with "Optical character recognition").
    /// </summary>
    internal static class OcrReader
    {
        /// <summary>Text this small reads badly; smaller crops are enlarged first.</summary>
        private const int MinShortSide = 600;

        /// <summary>True when Windows has at least one OCR language.</summary>
        public static bool Available
        {
            get
            {
                try { return OcrEngine.AvailableRecognizerLanguages.Count > 0; }
                catch (Exception ex) when (ex is TypeLoadException || ex is COMException || ex is InvalidCastException) { return false; }
            }
        }

        /// <summary>
        /// The text in <paramref name="image"/>: one line per visual row,
        /// columns separated by a tab (so it pastes into Excel as cells);
        /// empty when there is none; null when Windows has no OCR language.
        /// </summary>
        public static async Task<string> ReadAsync(Bitmap image)
        {
            OcrEngine engine = CreateEngine();
            if (engine == null) return null;

            using (Bitmap prepared = Prepare(image, (int)OcrEngine.MaxImageDimension))
            using (SoftwareBitmap sb = await ToSoftwareBitmap(prepared).ConfigureAwait(false))
            {
                OcrResult result = await engine.RecognizeAsync(sb).AsTask().ConfigureAwait(false);
                var words = result.Lines.SelectMany(l => l.Words)
                    .Select(w => new OcrWord(w.Text, new RectangleF((float)w.BoundingRect.X, (float)w.BoundingRect.Y,
                                                                    (float)w.BoundingRect.Width, (float)w.BoundingRect.Height)));
                return OcrLayout.Rows(words);
            }
        }

        private static OcrEngine CreateEngine()
        {
            try
            {
                // The user's own languages first (e.g. Turkish keeps ç, ğ, ı, ş…); else any installed one.
                OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine != null) return engine;
                var lang = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                return lang == null ? null : OcrEngine.TryCreateFromLanguage(lang);
            }
            catch (Exception ex) when (ex is TypeLoadException || ex is InvalidCastException || ex is COMException)
            {
                return null;
            }
        }

        /// <summary>
        /// A copy prepared for OCR: small crops are enlarged (screen text is
        /// often 9-12 px tall, too small for good results), big images are
        /// shrunk to the engine's limit, and a margin in the image's own
        /// background color is added. Without it, a letter touching the edge
        /// of a tight selection is dropped or misread ("Microsoft" →
        /// "icrosoft"), and a thin one-line crop may read as nothing at all.
        /// </summary>
        internal static Bitmap Prepare(Bitmap src, int maxDimension)
        {
            double scale = 1.0;
            int shortSide = Math.Min(src.Width, src.Height);
            if (shortSide < MinShortSide) scale = Math.Min(3.0, (double)MinShortSide / Math.Max(1, shortSide));

            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            int pad = Math.Min(80, Math.Max(24, Math.Min(w, h) / 4));
            int longSide = Math.Max(w, h) + 2 * pad;
            if (longSide > maxDimension)
            {
                double shrink = (double)(maxDimension - 2 * pad) / Math.Max(w, h);
                w = Math.Max(1, (int)(w * shrink));
                h = Math.Max(1, (int)(h * shrink));
            }

            var dst = new Bitmap(w + 2 * pad, h + 2 * pad, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.Clear(EdgeColor(src));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, pad, pad, w, h);
            }
            return dst;
        }

        /// <summary>The usual color along the image's border: its background, light or dark.</summary>
        private static Color EdgeColor(Bitmap src)
        {
            var counts = new Dictionary<int, int>();
            void Count(int x, int y)
            {
                Color c = src.GetPixel(x, y);
                int key = (c.R / 16 << 8) | (c.G / 16 << 4) | (c.B / 16); // coarse buckets ignore anti-aliasing noise
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            int stepX = Math.Max(1, src.Width / 64), stepY = Math.Max(1, src.Height / 64);
            for (int x = 0; x < src.Width; x += stepX) { Count(x, 0); Count(x, src.Height - 1); }
            for (int y = 0; y < src.Height; y += stepY) { Count(0, y); Count(src.Width - 1, y); }
            int best = counts.OrderByDescending(kv => kv.Value).First().Key;
            return Color.FromArgb((best >> 8 & 15) * 16 + 8, (best >> 4 & 15) * 16 + 8, (best & 15) * 16 + 8);
        }

        private static async Task<SoftwareBitmap> ToSoftwareBitmap(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream()).AsTask().ConfigureAwait(false);
                return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied).AsTask().ConfigureAwait(false);
            }
        }

    }
}
