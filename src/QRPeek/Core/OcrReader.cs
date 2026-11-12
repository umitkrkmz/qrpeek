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

        /// <summary>Upper bound for the enlarged image (about 24 MP, ~100 MB as 32-bit pixels).</summary>
        private const double MaxPixels = 24_000_000;

        /// <summary>True when Windows has at least one OCR language.</summary>
        public static bool Available
        {
            get
            {
                try { return OcrEngine.AvailableRecognizerLanguages.Count > 0; }
                catch (Exception ex) when (ex is TypeLoadException || ex is COMException || ex is InvalidCastException) { return false; }
            }
        }

        /// <summary>Most OCR languages tried on one image (each pass takes a few hundred ms).</summary>
        private const int MaxLanguages = 3;

        /// <summary>
        /// The text in <paramref name="image"/>: one line per visual row,
        /// columns separated by a tab (so it pastes into Excel as cells);
        /// empty when there is none; null when Windows has no OCR language.
        ///
        /// Every installed OCR language reads the image (the user's own first)
        /// and the reading that finds the most known receipt labels wins: the
        /// Turkish engine turns an English invoice's "Registration" into
        /// "Regiştra", the English one gets it right. With no labels at all
        /// (a plain paragraph) the user's language is kept.
        /// </summary>
        /// <param name="terms">Receipt labels to judge and clean readings with; null = built-in.</param>
        /// <remarks>
        /// The image is read at two sizes too: 2x is the base, and 3x catches
        /// small faint words 2x misses (a lone "Qty" header) but loses others
        /// ("20%"). Words of the other passes only fill the gaps of the best one.
        /// All boxes are kept in the original image's coordinates, so readings
        /// of different sizes line up.
        /// </remarks>
        public static async Task<string> ReadAsync(Bitmap image, ReceiptTerms terms = null)
        {
            terms = terms ?? ReceiptTerms.Default;
            List<OcrEngine> engines = CreateEngines();
            if (engines.Count == 0) return null;

            var readings = new List<Reading>();
            var scalesDone = new List<float>();
            foreach (double minScale in new[] { 2.0, 3.0 })
            {
                using (PreparedImage prepared = Prepare(image, (int)OcrEngine.MaxImageDimension, minScale))
                {
                    if (scalesDone.Any(s => Math.Abs(s - prepared.ScaleX) < 0.05f)) continue; // capped to the same size: nothing new
                    scalesDone.Add(prepared.ScaleX);
                    using (SoftwareBitmap sb = await ToSoftwareBitmap(prepared.Bitmap).ConfigureAwait(false))
                    {
                        for (int e = 0; e < engines.Count; e++)
                        {
                            OcrResult result = await engines[e].RecognizeAsync(sb).AsTask().ConfigureAwait(false);
                            List<OcrWord> words = result.Lines.SelectMany(l => l.Words)
                                .Select(w => new OcrWord(w.Text, prepared.ToSource(w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height)))
                                .ToList();
                            readings.Add(new Reading(e, readings.Count(r => r.Engine == e) == 0, words, terms.Score(terms.Polish(OcrLayout.Rows(words)))));
                        }
                    }
                }
            }

            // The base size (first pass) of the language that knows the most labels.
            Reading best = readings.Where(r => r.BaseSize).OrderByDescending(r => r.Score).ThenBy(r => r.Engine).First();
            List<OcrWord> merged = best.Words;
            // The winner may still have mangled names in the other language ("ümit" → "omit").
            foreach (Reading other in readings.Where(r => r.BaseSize && r != best))
                merged = OcrLayout.KeepNativeLetters(merged, other.Words, terms.IsLabelWord);
            // A valid e-mail or amount from another reading beats a broken one on the same spot.
            foreach (Reading other in readings.Where(r => r != best))
                merged = OcrLayout.PreferWellFormed(merged, other.Words);
            // Words only another size or language saw, where the winner has nothing.
            foreach (Reading other in readings.Where(r => r != best).OrderBy(r => r.Engine == best.Engine ? 0 : 1))
                merged = OcrLayout.FillGaps(merged, other.Words, sameLanguage: other.Engine == best.Engine, terms.IsLabelWord);
            return terms.Polish(OcrLayout.Rows(merged));
        }

        private sealed class Reading
        {
            public Reading(int engine, bool baseSize, List<OcrWord> words, int score)
            {
                Engine = engine;
                BaseSize = baseSize;
                Words = words;
                Score = score;
            }

            public int Engine { get; }
            public bool BaseSize { get; }
            public List<OcrWord> Words { get; }
            public int Score { get; }
        }

        /// <summary>An image enlarged and padded for OCR, and how to map its boxes back to the source.</summary>
        internal sealed class PreparedImage : IDisposable
        {
            public PreparedImage(Bitmap bitmap, float scaleX, float scaleY, int pad)
            {
                Bitmap = bitmap;
                ScaleX = scaleX;
                ScaleY = scaleY;
                Pad = pad;
            }

            public Bitmap Bitmap { get; }
            public float ScaleX { get; }
            public float ScaleY { get; }
            public int Pad { get; }

            public RectangleF ToSource(double x, double y, double w, double h) =>
                new RectangleF((float)((x - Pad) / ScaleX), (float)((y - Pad) / ScaleY), (float)(w / ScaleX), (float)(h / ScaleY));

            public void Dispose() => Bitmap.Dispose();
        }

        /// <summary>The user's own OCR language first (keeps ç, ğ, ı, ş…), then the other installed ones.</summary>
        private static List<OcrEngine> CreateEngines()
        {
            var engines = new List<OcrEngine>();
            try
            {
                OcrEngine own = OcrEngine.TryCreateFromUserProfileLanguages();
                if (own != null) engines.Add(own);
                foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                {
                    if (engines.Count >= MaxLanguages) break;
                    if (engines.Any(e => string.Equals(e.RecognizerLanguage.LanguageTag, lang.LanguageTag, StringComparison.OrdinalIgnoreCase))) continue;
                    OcrEngine e2 = OcrEngine.TryCreateFromLanguage(lang);
                    if (e2 != null) engines.Add(e2);
                }
            }
            catch (Exception ex) when (ex is TypeLoadException || ex is InvalidCastException || ex is COMException)
            {
                // Keep whatever was created before the failure.
            }
            return engines;
        }

        /// <summary>
        /// A copy prepared for OCR: small crops are enlarged (screen text is
        /// often 9-12 px tall, too small for good results), big images are
        /// shrunk to the engine's limit, and a margin in the image's own
        /// background color is added. Without it, a letter touching the edge
        /// of a tight selection is dropped or misread ("Microsoft" →
        /// "icrosoft"), and a thin one-line crop may read as nothing at all.
        /// </summary>
        internal static PreparedImage Prepare(Bitmap src, int maxDimension, double minScale = 2.0)
        {
            // Screen text is small whatever the size of the selection (a whole receipt
            // still has 10 px gray table headers), so always enlarge at least 2x;
            // thin crops up to 3x. Capped so a full 4K screen stays within limits.
            double scale = minScale;
            int shortSide = Math.Min(src.Width, src.Height);
            if (shortSide < MinShortSide) scale = Math.Max(scale, Math.Min(3.0, (double)MinShortSide / Math.Max(1, shortSide)));
            double pixels = (double)src.Width * src.Height;
            if (pixels * scale * scale > MaxPixels) scale = Math.Max(1.0, Math.Sqrt(MaxPixels / pixels));

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
            return new PreparedImage(dst, (float)w / src.Width, (float)h / src.Height, pad);
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
