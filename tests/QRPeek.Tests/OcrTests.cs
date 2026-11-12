// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Threading.Tasks;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class OcrTests
    {
        /// <summary>Text drawn like on a screen: dark on white, ClearType-ish, at a given pixel size.</summary>
        private static Bitmap Render(string[] lines, float px, int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                float y = px * 0.6f;
                foreach (string line in lines)
                {
                    g.DrawString(line, font, Brushes.Black, 10, y);
                    y += px * 1.6f;
                }
            }
            return bmp;
        }

        [Fact]
        public void Enlarges_small_crops_pads_them_and_caps_big_images()
        {
            using (var small = new Bitmap(300, 100))
            using (Bitmap p = OcrReader.Prepare(small, 10000))
            {
                // short side 100 -> 3x (900x300), plus a margin of 300/4 = 75 on every side
                Assert.Equal(900 + 150, p.Width);
                Assert.Equal(300 + 150, p.Height);
            }
            using (var big = new Bitmap(4000, 2000))
            using (Bitmap p = OcrReader.Prepare(big, 2600))
            {
                Assert.True(p.Width <= 2600 && p.Height <= 2600);
            }
        }

        [Fact]
        public void The_margin_takes_the_background_color_of_dark_text()
        {
            using (var dark = new Bitmap(200, 40))
            {
                using (Graphics g = Graphics.FromImage(dark)) g.Clear(Color.FromArgb(30, 30, 34));
                using (Bitmap p = OcrReader.Prepare(dark, 10000))
                {
                    Color corner = p.GetPixel(0, 0);
                    Assert.True(corner.R < 60 && corner.G < 60 && corner.B < 60, corner.ToString()); // not a white frame
                }
            }
        }

        private static OcrWord W(string text, float x, float y, float w, float h = 14) => new OcrWord(text, new RectangleF(x, y, w, h));

        [Fact]
        public void Puts_words_back_into_rows_and_columns()
        {
            // As Windows OCR returns it: the left column's lines first, the right column's later.
            var words = new[]
            {
                W("Order", 10, 100, 40), W("total", 54, 101, 34),
                W("Item", 10, 40, 30), W("QRPeek", 10, 70, 52),
                W("Price", 400, 41, 36), W("USD", 400, 71, 30), W("1.99", 434, 70, 30),
                W("USD", 400, 99, 30), W("1.99", 434, 100, 30),
            };
            Assert.Equal("Item\tPrice\r\nQRPeek\tUSD 1.99\r\nOrder total\tUSD 1.99", OcrLayout.Rows(words));
        }

        [Theory]
        // OCR reads "1" as I or l and "0" as O; only where the context says it's a number.
        [InlineData("Satın alma tarihi: II Kasım 2026", "Satın alma tarihi: 11 Kasım 2026")]
        [InlineData("I Ocak 2026", "1 Ocak 2026")]
        [InlineData("Paid on October I2, 2O26", "Paid on October 12, 2026")]
        [InlineData("Order l679897483", "Order 1679897483")]
        [InlineData("Date: II.11.2026", "Date: 11.11.2026")]
        [InlineData("I January", "1 January")]
        // ...and nowhere else.
        [InlineData("II. Dünya Savaşı", "II. Dünya Savaşı")]
        [InlineData("World War II", "World War II")]
        [InlineData("USD 0.00", "USD 0.00")]
        [InlineData("I love Istanbul", "I love Istanbul")]
        [InlineData("Office 365 Il", "Office 365 Il")]
        [InlineData("SKU A1B2", "SKU A1B2")]
        [InlineData("toplamın 5'ı", "toplamın 5'ı")]
        [InlineData("Total S24.oo", "Total $24.00")]
        [InlineData("VAT S4.00", "VAT $4.00")]
        [InlineData("Paid S1,234.5O", "Paid $1,234.50")]
        [InlineData("Model S2000", "Model S2000")] // no cents: could be anything
        [InlineData("SKU S-100", "SKU S-100")]
        public void Fixes_letters_read_in_place_of_digits(string ocr, string expected)
        {
            var words = new List<OcrWord>();
            float x = 10;
            foreach (string t in ocr.Split(' '))
            {
                words.Add(W(t, x, 10, t.Length * 8));
                x += t.Length * 8 + 5;
            }
            Assert.Equal(expected, OcrLayout.Rows(words));
        }

        [Fact]
        public void Keeps_native_letters_from_the_other_language_but_not_in_labels()
        {
            // The English reading won (more labels); the Turkish one got the names right.
            var english = new[] { W("Bill", 400, 10, 24), W("to", 428, 10, 14), W("omit", 400, 30, 30),
                                  W("SiNCAN/ANKARA", 400, 50, 100), W("Registration", 10, 70, 80), W("Anthropic,", 10, 90, 70) };
            var turkish = new[] { W("Bill", 400, 10, 24), W("to", 428, 10, 14), W("ümit", 401, 30, 30),
                                  W("SİNCAN/ANKARA", 400, 51, 100), W("Regiştra", 10, 70, 66), W("Anthropİc,", 10, 90, 70) };
            string merged = OcrLayout.Rows(OcrLayout.KeepNativeLetters(english, turkish, ReceiptTerms.Default.IsLabelWord));
            Assert.Contains("ümit", merged);
            Assert.Contains("SİNCAN/ANKARA", merged);
            Assert.Contains("Registration", merged);   // a label: the English reading stays
            Assert.Contains("Anthropic,", merged);     // "İ" inside a lower-case word is a misread, not Turkish
            Assert.DoesNotContain("omit", merged);
        }

        [Fact]
        public void Keeps_close_lines_apart_and_paragraphs_as_they_are()
        {
            var words = new[]
            {
                W("first", 10, 10, 30), W("line", 44, 10, 26),
                W("second", 10, 27, 46), W("line", 60, 27, 26), // 17 px below: line spacing, not the same row
            };
            Assert.Equal("first line\r\nsecond line", OcrLayout.Rows(words));
            Assert.Equal("", OcrLayout.Rows(new OcrWord[0]));
        }

        [Fact]
        public async Task Reads_small_screen_text()
        {
            // Windows Server images used by CI may have no OCR language; there is nothing to test then.
            if (!OcrReader.Available) return;

            using (Bitmap bmp = Render(new[] { "Receipt 2843-7979-5973", "Total 24.00 USD" }, 13, 360, 70))
            {
                string text = await OcrReader.ReadAsync(bmp);
                Assert.NotNull(text);
                Assert.Contains("2843-7979-5973", text.Replace(" ", ""));
                Assert.Contains("24.00", text);
                Assert.Contains("\n", text); // one line per OCR line
            }
        }

        /// <summary>An order summary like a store receipt e-mail: labels on the left, amounts far right.</summary>
        private static Bitmap RenderTwoColumns()
        {
            var bmp = new Bitmap(560, 130);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var font = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                string[,] rows = { { "Name", "Count", "Amount" }, { "QRPeek", "1", "USD 1.99" }, { "Order total", "", "USD 1.99" } };
                for (int r = 0; r < 3; r++)
                {
                    int y = 12 + r * 38;
                    g.DrawString(rows[r, 0], font, Brushes.Black, 12, y);
                    g.DrawString(rows[r, 1], font, Brushes.Black, 330, y);
                    g.DrawString(rows[r, 2], font, Brushes.Black, 440, y);
                }
            }
            return bmp;
        }

        [Fact]
        public async Task Keeps_each_row_together_with_columns_as_tabs()
        {
            if (!OcrReader.Available) return;
            using (Bitmap bmp = RenderTwoColumns())
            {
                string[] lines = (await OcrReader.ReadAsync(bmp)).Replace("\r", "").Split('\n');
                Assert.Equal(3, lines.Length);
                Assert.Equal(new[] { "Name", "Count", "Amount" }, lines[0].Split('\t'));
                Assert.Equal(new[] { "QRPeek", "1", "USD 1.99" }, lines[1].Split('\t'));
                Assert.Equal(new[] { "Order total", "USD 1.99" }, lines[2].Split('\t'));
            }
        }

        /// <summary>
        /// A tight selection: it starts exactly at the first dark pixel of the
        /// "M" and is barely taller than the text. Without the margin added in
        /// Prepare, 13 px read as "Vicrosoft" and 12 px as nothing at all.
        /// </summary>
        [Theory]
        [InlineData(12f)]
        [InlineData(13f)]
        [InlineData(14f)]
        public async Task Reads_letters_that_touch_the_edge_of_the_selection(float px)
        {
            if (!OcrReader.Available) return;
            using (var full = new Bitmap(400, 80))
            {
                using (Graphics g = Graphics.FromImage(full))
                using (var font = new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.DrawString("Microsoft", font, Brushes.Black, 40, 30);
                }
                int left = 0;
                for (int x = 38; x < full.Width && left == 0; x++)
                    for (int y = 0; y < full.Height; y++)
                        if (full.GetPixel(x, y).R < 100) { left = x; break; }
                using (Bitmap crop = full.Clone(new Rectangle(left, 22, 200, (int)(px * 1.8)), full.PixelFormat))
                {
                    Assert.Contains("Microsoft", await OcrReader.ReadAsync(crop));
                }
            }
        }

        /// <summary>
        /// An English invoice on a PC whose OCR language is Turkish (the
        /// user's setup): the Turkish engine read "Regiştra", "Invoİce n um ber".
        /// Trying every installed language and keeping the reading with the
        /// most known labels fixes it. Needs both an English and another OCR language.
        /// </summary>
        [Fact]
        public async Task Picks_the_OCR_language_that_reads_the_receipt_best()
        {
            if (!OcrReader.Available) return;
            using (var bmp = new Bitmap(520, 150))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var bold = new Font("Segoe UI Semibold", 13, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var font = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    string[,] rows = { { "Invoice number", "PWZP1PZQ-0006" }, { "Date of issue", "October 26, 2026" },
                                       { "VAT Registration", "Turkey VAT: 701236788" }, { "Amount paid", "$24.00" } };
                    for (int r = 0; r < 4; r++)
                    {
                        g.DrawString(rows[r, 0], bold, Brushes.Black, 12, 12 + r * 32);
                        g.DrawString(rows[r, 1], font, Brushes.Black, 150, 12 + r * 32);
                    }
                }
                string text = await OcrReader.ReadAsync(bmp);
                Assert.Contains("Invoice number", text);
                Assert.Contains("VAT Registration", text);
                Assert.Contains("Amount paid", text);
                Assert.DoesNotContain("İ", text);
            }
        }

        [Fact]
        public async Task Returns_empty_for_an_image_without_text()
        {
            if (!OcrReader.Available) return;
            using (var blank = new Bitmap(400, 200))
            {
                using (Graphics g = Graphics.FromImage(blank)) g.Clear(Color.White);
                Assert.Equal("", await OcrReader.ReadAsync(blank));
            }
        }
    }
}
