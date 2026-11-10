// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QRCoder;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    /// <summary>Generate a QR with QRPeek's own generator, draw it somewhere on a fake "screen", read it back.</summary>
    public class ScannerTests
    {
        private static readonly QrStyle Plain = new QrStyle();

        private static Bitmap Qr(string text, int targetPx) =>
            (Bitmap)Image.FromStream(new MemoryStream(QrGenerator.Png(text, QRCodeGenerator.ECCLevel.M, Plain, targetPx)));

        private static Bitmap Screen(int w, int h, Color background)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) g.Clear(background);
            return bmp;
        }

        private static void Paste(Bitmap onto, Bitmap qr, int x, int y)
        {
            using (Graphics g = Graphics.FromImage(onto))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(qr, new Rectangle(x, y, qr.Width, qr.Height));
            }
        }

        [Fact]
        public void Finds_a_url_on_a_full_hd_screen()
        {
            using (Bitmap screen = Screen(1920, 1080, Color.FromArgb(240, 240, 240)))
            using (Bitmap qr = Qr("https://example.com/menu?table=12", 220))
            {
                Paste(screen, qr, 1300, 600);
                Assert.Equal(new[] { "https://example.com/menu?table=12" }, QrScanner.Decode(screen));
            }
        }

        [Fact]
        public void Keeps_turkish_characters()
        {
            const string text = "Merhaba dünya: çğıöşü ÇĞİÖŞÜ";
            using (Bitmap screen = Screen(1280, 720, Color.White))
            using (Bitmap qr = Qr(text, 300))
            {
                Paste(screen, qr, 100, 100);
                Assert.Equal(new[] { text }, QrScanner.Decode(screen));
            }
        }

        [Fact]
        public void Finds_several_codes_at_once()
        {
            using (Bitmap screen = Screen(1920, 1080, Color.White))
            using (Bitmap a = Qr("first code", 200))
            using (Bitmap b = Qr("second code", 200))
            using (Bitmap c = Qr("third code", 200))
            {
                Paste(screen, a, 80, 80);
                Paste(screen, b, 900, 400);
                Paste(screen, c, 1600, 800);
                var found = QrScanner.Decode(screen);
                Assert.Equal(3, found.Count);
                Assert.Contains("first code", found);
                Assert.Contains("second code", found);
                Assert.Contains("third code", found);
            }
        }

        [Fact]
        public void Reads_light_on_dark_codes()
        {
            using (Bitmap png = Qr("dark mode", 240))
            using (Bitmap qr = new Bitmap(png.Width, png.Height, PixelFormat.Format32bppArgb))
            using (Bitmap screen = Screen(800, 600, Color.FromArgb(30, 30, 30)))
            {
                // Invert the QR: white modules on a dark page. (The PNG is
                // palette-based, so read from it and write into a 32-bit copy.)
                for (int y = 0; y < png.Height; y++)
                    for (int x = 0; x < png.Width; x++)
                    {
                        Color p = png.GetPixel(x, y);
                        qr.SetPixel(x, y, Color.FromArgb(255 - p.R, 255 - p.G, 255 - p.B));
                    }
                Paste(screen, qr, 200, 150);
                Assert.Equal(new[] { "dark mode" }, QrScanner.Decode(screen));
            }
        }

        [Fact]
        public void Reads_a_tiny_code_from_a_selected_area()
        {
            // 2 px per module: about what a small QR in a PDF looks like at 100% zoom.
            using (Bitmap qr = Qr("tiny", 1))
            using (Bitmap enlarged = (Bitmap)Image.FromStream(new MemoryStream(
                       QrGenerator.Png("tiny", QRCodeGenerator.ECCLevel.M, Plain, qr.Width * 2))))
            using (Bitmap area = Screen(enlarged.Width + 20, enlarged.Height + 20, Color.White))
            {
                Paste(area, enlarged, 10, 10);
                Assert.Equal(new[] { "tiny" }, QrScanner.Decode(area));
            }
        }

        /// <summary>
        /// The real-world case that failed: a dense e-Arşiv invoice QR (~330
        /// bytes of JSON, ~85 modules) that the browser shrank to ~2.36 px per
        /// module with smoothing, so most module edges are grey.
        /// </summary>
        [Theory]
        [InlineData(2, 2.36)]
        [InlineData(2, 2.06)]
        [InlineData(10, 2.36)]
        [InlineData(4, 2.36)]
        [InlineData(3, 2.36)]
        [InlineData(4, 2.1)]
        [InlineData(5, 2.6)]
        public void Reads_a_dense_code_the_browser_shrank_with_smoothing(int sourcePxPerModule, double screenPxPerModule)
        {
            // Like the server-made PNG (sourcePxPerModule) that the browser then draws smaller with smoothing.
            string json = "{\"vkntckn\":\"1234567890\",\"avkntckn\":\"11111111111\",\"senaryo\":\"EARSIVFATURA\",\"tip\":\"SATIS\"," +
                          "\"tarih\":\"2025-09-14\",\"no\":\"AGO2025000016612\",\"ettn\":\"485CC3EA-C749-4B53-A6BE-E8140C8969C0\"," +
                          "\"parabirimi\":\"TRY\",\"malhizmettoplam\":\"11373.33\",\"kdvmatrah(20)\":\"11.373,33\"," +
                          "\"hesaplanankdv(20)\":\"2.274,67\",\"vergidahil\":\"13.648,00\",\"odenecek\":\"13.648,00\"}";
            using (Bitmap big = QrGenerator.Render(json, QRCodeGenerator.ECCLevel.M, Plain, 2000))
            {
                int n = CountModules(big);
                using (Bitmap source = new Bitmap(n * sourcePxPerModule, n * sourcePxPerModule))
                {
                    using (Graphics g = Graphics.FromImage(source))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        g.DrawImage(big, new Rectangle(0, 0, source.Width, source.Height));
                    }
                    int size = (int)Math.Round(n * screenPxPerModule);
                    using (var page = Screen(size + 200, size + 200, Color.White))
                    {
                        using (Graphics g = Graphics.FromImage(page))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.DrawImage(source, new RectangleF(100.4f, 100.3f, size, size)); // off the pixel grid, like real layouts
                        }
                        Assert.Equal(new[] { json }, QrScanner.Decode(page));
                    }
                }
            }
        }

        /// <summary>Module count of a QRCoder image: width / width of the top-left finder's dark run * 7.</summary>
        private static int CountModules(Bitmap qr)
        {
            int y = -1, x0 = -1;
            for (int yy = 0; yy < qr.Height && y < 0; yy++)
                for (int xx = 0; xx < qr.Width; xx++)
                    if (qr.GetPixel(xx, yy).R < 128) { y = yy; x0 = xx; break; }
            int x1 = x0;
            while (x1 < qr.Width && qr.GetPixel(x1, y).R < 128) x1++;
            double module = (x1 - x0) / 7.0;
            return (int)Math.Round(qr.Width / module);
        }

        [Fact]
        public void Returns_empty_when_there_is_no_code()
        {
            using (Bitmap screen = Screen(1024, 768, Color.SteelBlue))
            {
                Assert.Empty(QrScanner.Decode(screen));
            }
        }

        [Fact]
        public void Wifi_code_round_trips_through_generator_and_scanner()
        {
            string payload = QrGenerator.WifiPayload("Ev;Ağı:5G", "p@ss;w\\rd\"", "WPA", hidden: true);
            using (Bitmap screen = Screen(1000, 800, Color.White))
            using (Bitmap qr = Qr(payload, 320))
            {
                Paste(screen, qr, 300, 200);
                var found = QrScanner.Decode(screen);
                Assert.Single(found);
                WifiInfo w = QrContent.Parse(found[0]).Wifi;
                Assert.Equal("Ev;Ağı:5G", w.Ssid);
                Assert.Equal("p@ss;w\\rd\"", w.Password);
                Assert.Equal("WPA", w.Security);
                Assert.True(w.Hidden);
            }
        }
    }
}
