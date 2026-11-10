// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using QRCoder;

namespace QRPeek.Core
{
    internal enum ModuleStyle { Square, Rounded, Dots }

    internal enum EyeStyle { Square, Rounded, Circle }

    internal sealed class QrStyle
    {
        public string DarkHex { get; set; } = "#000000";
        public ModuleStyle Modules { get; set; } = ModuleStyle.Square;
        public EyeStyle Eyes { get; set; } = EyeStyle.Square;

        /// <summary>Optional center image. Owned by the caller.</summary>
        public Bitmap Logo { get; set; }

        /// <summary>Logo box width as a fraction of the code width (without quiet zone).</summary>
        public double LogoScale { get; set; } = 0.22;
    }

    /// <summary>
    /// Draws a QR matrix with module/eye shapes and an optional center logo.
    /// The same geometry feeds both PNG (GDI+) and SVG output, so the two
    /// always look the same.
    /// </summary>
    internal static class QrRenderer
    {
        private const int Quiet = 4; // QRCoder's ModuleMatrix includes a 4-module quiet zone

        public static Bitmap Render(QRCodeData data, QrStyle style, int targetPx)
        {
            int n = data.ModuleMatrix.Count;
            int ppm = Math.Max(1, targetPx / n);
            int size = n * ppm;

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var path = new GraphicsPath(FillMode.Alternate))
            using (var dark = new SolidBrush(ColorTranslator.FromHtml(style.DarkHex)))
            {
                g.Clear(Color.White);
                bool smooth = style.Modules != ModuleStyle.Square || style.Eyes != EyeStyle.Square;
                g.SmoothingMode = smooth ? SmoothingMode.AntiAlias : SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                var sink = new PathSink(path, null, ppm);
                Build(data, style, sink, out RectangleF logoBox);
                g.FillPath(dark, path);

                if (style.Logo != null && !logoBox.IsEmpty)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    RectangleF box = Scale(logoBox, ppm);
                    using (var white = new SolidBrush(Color.White))
                    using (GraphicsPath bg = RoundRect(box, box.Width * 0.18f))
                    {
                        g.FillPath(white, bg);
                    }
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(style.Logo, Fit(style.Logo.Size, Inset(box, box.Width * 0.08f)));
                }
            }
            return bmp;
        }

        public static byte[] Png(QRCodeData data, QrStyle style, int targetPx)
        {
            using (Bitmap bmp = Render(data, style, targetPx))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        public static string Svg(QRCodeData data, QrStyle style)
        {
            int n = data.ModuleMatrix.Count;
            var d = new StringBuilder();
            Build(data, style, new PathSink(null, d, 1), out RectangleF logoBox);

            var svg = new StringBuilder();
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(n).Append(' ').Append(n)
               .Append("\" width=\"").Append(n * 10).Append("\" height=\"").Append(n * 10).Append("\" shape-rendering=\"")
               .Append(style.Modules == ModuleStyle.Square && style.Eyes == EyeStyle.Square ? "crispEdges" : "geometricPrecision").Append("\">\n");
            svg.Append("  <rect width=\"").Append(n).Append("\" height=\"").Append(n).Append("\" fill=\"#FFFFFF\"/>\n");
            svg.Append("  <path fill=\"").Append(style.DarkHex).Append("\" fill-rule=\"evenodd\" d=\"").Append(d).Append("\"/>\n");

            if (style.Logo != null && !logoBox.IsEmpty)
            {
                float r = logoBox.Width * 0.18f;
                svg.Append("  <rect x=\"").Append(F(logoBox.X)).Append("\" y=\"").Append(F(logoBox.Y))
                   .Append("\" width=\"").Append(F(logoBox.Width)).Append("\" height=\"").Append(F(logoBox.Height))
                   .Append("\" rx=\"").Append(F(r)).Append("\" fill=\"#FFFFFF\"/>\n");
                RectangleF img = Inset(logoBox, logoBox.Width * 0.08f);
                string b64;
                using (var ms = new MemoryStream())
                {
                    style.Logo.Save(ms, ImageFormat.Png);
                    b64 = Convert.ToBase64String(ms.ToArray());
                }
                svg.Append("  <image x=\"").Append(F(img.X)).Append("\" y=\"").Append(F(img.Y))
                   .Append("\" width=\"").Append(F(img.Width)).Append("\" height=\"").Append(F(img.Height))
                   .Append("\" preserveAspectRatio=\"xMidYMid meet\" href=\"data:image/png;base64,").Append(b64).Append("\"/>\n");
            }
            svg.Append("</svg>\n");
            return svg.ToString();
        }

        /// <summary>Emits every dark shape in module units. Returns the cleared logo area (empty if none).</summary>
        private static void Build(QRCodeData data, QrStyle style, PathSink sink, out RectangleF logoBox)
        {
            int n = data.ModuleMatrix.Count;
            int code = n - 2 * Quiet;

            logoBox = RectangleF.Empty;
            if (style.Logo != null)
            {
                float side = (float)Math.Round(code * style.LogoScale);
                if (side >= 3)
                {
                    float o = (n - side) / 2f;
                    logoBox = new RectangleF(o, o, side, side);
                }
            }

            bool Dark(int x, int y) =>
                x >= 0 && y >= 0 && x < n && y < n && data.ModuleMatrix[y][x] && !InEye(x, y, n);

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (!Dark(x, y)) continue;
                    if (!logoBox.IsEmpty && logoBox.IntersectsWith(new RectangleF(x, y, 1, 1))) continue;

                    switch (style.Modules)
                    {
                        case ModuleStyle.Dots:
                            sink.Ellipse(x + 0.08f, y + 0.08f, 0.84f, 0.84f);
                            break;
                        case ModuleStyle.Rounded:
                            // Round only the outside corners, so neighbours melt together.
                            const float r = 0.5f;
                            bool l = Dark(x - 1, y), t = Dark(x, y - 1), rr = Dark(x + 1, y), b = Dark(x, y + 1);
                            sink.Rect(x, y, 1, 1, !l && !t ? r : 0, !t && !rr ? r : 0, !rr && !b ? r : 0, !b && !l ? r : 0);
                            break;
                        default:
                            sink.Rect(x, y, 1, 1, 0, 0, 0, 0);
                            break;
                    }
                }
            }

            foreach (Point e in new[] { new Point(Quiet, Quiet), new Point(n - Quiet - 7, Quiet), new Point(Quiet, n - Quiet - 7) })
            {
                Eye(sink, e.X, e.Y, style.Eyes);
            }
        }

        private static bool InEye(int x, int y, int n)
        {
            bool InBox(int ex, int ey) => x >= ex && x < ex + 7 && y >= ey && y < ey + 7;
            return InBox(Quiet, Quiet) || InBox(n - Quiet - 7, Quiet) || InBox(Quiet, n - Quiet - 7);
        }

        // Outer 7x7 ring (5x5 hole) + 3x3 center. Even-odd fill makes the hole.
        private static void Eye(PathSink s, float x, float y, EyeStyle style)
        {
            switch (style)
            {
                case EyeStyle.Circle:
                    s.Ellipse(x, y, 7, 7);
                    s.Ellipse(x + 1, y + 1, 5, 5);
                    s.Ellipse(x + 2, y + 2, 3, 3);
                    break;
                case EyeStyle.Rounded:
                    s.Rect(x, y, 7, 7, 2.2f, 2.2f, 2.2f, 2.2f);
                    s.Rect(x + 1, y + 1, 5, 5, 1.4f, 1.4f, 1.4f, 1.4f);
                    s.Rect(x + 2, y + 2, 3, 3, 0.9f, 0.9f, 0.9f, 0.9f);
                    break;
                default:
                    s.Rect(x, y, 7, 7, 0, 0, 0, 0);
                    s.Rect(x + 1, y + 1, 5, 5, 0, 0, 0, 0);
                    s.Rect(x + 2, y + 2, 3, 3, 0, 0, 0, 0);
                    break;
            }
        }

        private static RectangleF Scale(RectangleF r, float k) => new RectangleF(r.X * k, r.Y * k, r.Width * k, r.Height * k);

        private static RectangleF Inset(RectangleF r, float by) => new RectangleF(r.X + by, r.Y + by, r.Width - 2 * by, r.Height - 2 * by);

        private static RectangleF Fit(Size img, RectangleF box)
        {
            float k = Math.Min(box.Width / img.Width, box.Height / img.Height);
            float w = img.Width * k, h = img.Height * k;
            return new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            new PathSink(p, null, 1).Rect(r.X, r.Y, r.Width, r.Height, radius, radius, radius, radius);
            return p;
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Writes shapes to a GDI+ path, an SVG path string, or both.</summary>
        private sealed class PathSink
        {
            private readonly GraphicsPath _gp;
            private readonly StringBuilder _svg;
            private readonly float _k;

            public PathSink(GraphicsPath gp, StringBuilder svg, float scale)
            {
                _gp = gp;
                _svg = svg;
                _k = scale;
            }

            /// <summary>Rectangle with its own radius per corner (tl, tr, br, bl), module units.</summary>
            public void Rect(float x, float y, float w, float h, float tl, float tr, float br, float bl)
            {
                if (_gp != null)
                {
                    float X = x * _k, Y = y * _k, W = w * _k, H = h * _k;
                    _gp.StartFigure();
                    Corner(X, Y, tl * _k, 180);
                    Corner(X + W - 2 * tr * _k, Y, tr * _k, 270, X + W, Y);
                    Corner(X + W - 2 * br * _k, Y + H - 2 * br * _k, br * _k, 0, X + W, Y + H);
                    Corner(X, Y + H - 2 * bl * _k, bl * _k, 90, X, Y + H);
                    _gp.CloseFigure();
                }
                if (_svg != null)
                {
                    _svg.Append('M').Append(F(x + tl)).Append(',').Append(F(y));
                    _svg.Append('H').Append(F(x + w - tr));
                    if (tr > 0) Arc(tr, x + w, y + tr);
                    _svg.Append('V').Append(F(y + h - br));
                    if (br > 0) Arc(br, x + w - br, y + h);
                    _svg.Append('H').Append(F(x + bl));
                    if (bl > 0) Arc(bl, x, y + h - bl);
                    _svg.Append('V').Append(F(y + tl));
                    if (tl > 0) Arc(tl, x + tl, y);
                    _svg.Append('Z');
                }
            }

            // GDI+: a quarter arc, or just the corner point when the radius is 0.
            private void Corner(float ax, float ay, float r, float startAngle, float px = float.NaN, float py = float.NaN)
            {
                if (r > 0)
                {
                    _gp.AddArc(ax, ay, 2 * r, 2 * r, startAngle, 90);
                }
                else
                {
                    float cx = float.IsNaN(px) ? ax : px, cy = float.IsNaN(py) ? ay : py;
                    _gp.AddLine(cx, cy, cx, cy);
                }
            }

            public void Ellipse(float x, float y, float w, float h)
            {
                if (_gp != null)
                {
                    _gp.StartFigure();
                    _gp.AddEllipse(x * _k, y * _k, w * _k, h * _k);
                }
                if (_svg != null)
                {
                    float rx = w / 2, ry = h / 2, cy = y + ry;
                    _svg.Append('M').Append(F(x)).Append(',').Append(F(cy))
                        .Append('a').Append(F(rx)).Append(',').Append(F(ry)).Append(" 0 1,0 ").Append(F(w)).Append(",0")
                        .Append('a').Append(F(rx)).Append(',').Append(F(ry)).Append(" 0 1,0 ").Append(F(-w)).Append(",0Z");
                }
            }

            private void Arc(float r, float toX, float toY)
            {
                _svg.Append('A').Append(F(r)).Append(',').Append(F(r)).Append(" 0 0,1 ").Append(F(toX)).Append(',').Append(F(toY));
            }
        }
    }
}
