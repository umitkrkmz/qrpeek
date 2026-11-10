// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Xml;
using QRCoder;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    /// <summary>Every style the generator offers must still be readable.</summary>
    public class StyleTests
    {
        private const string Url = "https://github.com/umitkrkmz/qrpeek";

        private static Bitmap Logo()
        {
            // A busy, dark logo is the worst case for a scanner.
            var bmp = new Bitmap(120, 120);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(29, 63, 184));
                g.FillEllipse(Brushes.White, 20, 20, 80, 80);
                g.FillRectangle(Brushes.Black, 50, 10, 20, 100);
            }
            return bmp;
        }

        [Theory]
        [InlineData("Square", "Square")]
        [InlineData("Square", "Rounded")]
        [InlineData("Square", "Circle")]
        [InlineData("Rounded", "Square")]
        [InlineData("Rounded", "Rounded")]
        [InlineData("Rounded", "Circle")]
        [InlineData("Dots", "Square")]
        [InlineData("Dots", "Rounded")]
        [InlineData("Dots", "Circle")]
        public void Every_shape_combination_is_readable(string modules, string eyes)
        {
            var style = new QrStyle
            {
                Modules = (ModuleStyle)System.Enum.Parse(typeof(ModuleStyle), modules),
                Eyes = (EyeStyle)System.Enum.Parse(typeof(EyeStyle), eyes),
                DarkHex = "#1E3A8A",
            };
            using (Bitmap qr = QrGenerator.Render(Url, QRCodeGenerator.ECCLevel.M, style, 400))
            {
                Assert.Equal(new[] { Url }, QrScanner.Decode(qr));
            }
        }

        [Theory]
        [InlineData(0.18)]
        [InlineData(0.22)]
        [InlineData(0.28)]
        public void Codes_with_a_center_logo_are_readable(double scale)
        {
            using (Bitmap logo = Logo())
            {
                var style = new QrStyle { Logo = logo, LogoScale = scale, Modules = ModuleStyle.Rounded, Eyes = EyeStyle.Rounded };
                using (Bitmap qr = QrGenerator.Render(Url, QRCodeGenerator.ECCLevel.L, style, 500))
                {
                    Assert.Equal(new[] { Url }, QrScanner.Decode(qr));
                }
            }
        }

        [Fact]
        public void A_logo_forces_the_highest_error_correction()
        {
            using (Bitmap logo = Logo())
            {
                Assert.Equal(QRCodeGenerator.ECCLevel.H, QrGenerator.EffectiveEcc(QRCodeGenerator.ECCLevel.L, new QrStyle { Logo = logo }));
            }
            Assert.Equal(QRCodeGenerator.ECCLevel.Q, QrGenerator.EffectiveEcc(QRCodeGenerator.ECCLevel.Q, new QrStyle()));
        }

        [Fact]
        public void Svg_is_well_formed_and_embeds_the_logo()
        {
            using (Bitmap logo = Logo())
            {
                var style = new QrStyle { Logo = logo, Modules = ModuleStyle.Dots, Eyes = EyeStyle.Circle };
                string svg = QrGenerator.Svg(Url, QRCodeGenerator.ECCLevel.M, style);
                var doc = new XmlDocument();
                doc.LoadXml(svg);
                Assert.Equal("svg", doc.DocumentElement.LocalName);
                Assert.Contains("data:image/png;base64,", svg);
            }
        }
    }
}
