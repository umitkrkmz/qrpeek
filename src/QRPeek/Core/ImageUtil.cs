// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QRPeek.Core
{
    /// <summary>Conversions between GDI+ bitmaps (capture, decoding) and WPF images (UI).</summary>
    internal static class ImageUtil
    {
        /// <summary>
        /// Loads any image Windows can decode (WIC: PNG, JPEG, BMP, GIF, TIFF, ICO,
        /// and WebP/HEIC when the system codecs exist), flattened onto white so
        /// a transparent background doesn't read as black.
        /// </summary>
        public static Bitmap LoadFile(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                return FromBitmapSource(decoder.Frames[0]);
            }
        }

        public static Bitmap FromBitmapSource(BitmapSource source)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
            var pixels = new byte[stride * h];
            bgra.CopyPixels(pixels, stride, 0);

            // Composite over white.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                int a = pixels[i + 3];
                if (a == 255) continue;
                pixels[i] = (byte)((pixels[i] * a + 255 * (255 - a)) / 255);
                pixels[i + 1] = (byte)((pixels[i + 1] * a + 255 * (255 - a)) / 255);
                pixels[i + 2] = (byte)((pixels[i + 2] * a + 255 * (255 - a)) / 255);
                pixels[i + 3] = 255;
            }

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
            try
            {
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), stride);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        public static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                BitmapSource src = BitmapSource.Create(bmp.Width, bmp.Height, 96, 96, PixelFormats.Bgra32, null,
                    data.Scan0, data.Stride * bmp.Height, data.Stride);
                src.Freeze();
                return src;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        public static BitmapImage FromPngBytes(byte[] png)
        {
            var img = new BitmapImage();
            using (var ms = new MemoryStream(png))
            {
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
            }
            img.Freeze();
            return img;
        }

        /// <summary>Clipboard image or the first image file copied in Explorer; null if neither.</summary>
        public static Bitmap FromClipboard()
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsImage())
                {
                    using (Image img = System.Windows.Forms.Clipboard.GetImage())
                    {
                        if (img != null) return Flatten(img);
                    }
                }
                if (System.Windows.Forms.Clipboard.ContainsFileDropList())
                {
                    foreach (string path in System.Windows.Forms.Clipboard.GetFileDropList())
                    {
                        try { return LoadFile(path); }
                        catch (Exception ex) when (IsImageLoadError(ex)) { }
                    }
                }
            }
            catch (ExternalException)
            {
                // Clipboard is locked by another app for a moment.
            }
            return null;
        }

        public static bool IsImageLoadError(Exception ex) =>
            ex is IOException || ex is NotSupportedException || ex is UnauthorizedAccessException
            || ex is FileFormatException || ex is ArgumentException || ex is COMException;

        private static Bitmap Flatten(Image img)
        {
            var bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                g.DrawImage(img, 0, 0, img.Width, img.Height);
            }
            return bmp;
        }
    }
}
