// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using QRPeek.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QRPeek.UI
{
    /// <summary>
    /// Shows the frozen screenshot on every monitor (one window per monitor, so
    /// mixed-DPI setups line up exactly) and lets the user drag a rectangle.
    /// </summary>
    internal static class RegionSelector
    {
        /// <param name="shots">Ownership passes to the selector; they're disposed when it closes.</param>
        /// <param name="onDone">Receives the cropped area (caller disposes it) or null if cancelled.</param>
        public static void Show(List<MonitorShot> shots, string hint, Action<Drawing.Bitmap> onDone)
        {
            if (shots.Count == 0)
            {
                onDone(null);
                return;
            }

            var windows = new List<RegionOverlayWindow>();
            bool finished = false;

            void Finish(MonitorShot shot, Drawing.Rectangle? area)
            {
                if (finished) return;
                finished = true;

                Drawing.Bitmap crop = null;
                if (shot != null && area.HasValue)
                {
                    Drawing.Rectangle r = Drawing.Rectangle.Intersect(area.Value, new Drawing.Rectangle(0, 0, shot.Image.Width, shot.Image.Height));
                    if (r.Width > 0 && r.Height > 0)
                    {
                        crop = shot.Image.Clone(r, Drawing.Imaging.PixelFormat.Format32bppArgb);
                    }
                }

                foreach (RegionOverlayWindow w in windows) w.Close();
                ScreenCapture.DisposeAll(shots);
                onDone(crop);
            }

            foreach (MonitorShot shot in shots)
            {
                var w = new RegionOverlayWindow(shot, hint, Finish);
                windows.Add(w);
                w.Show();
            }

            // Keyboard focus goes to the monitor under the mouse so Esc works right away.
            Drawing.Point cursor = Forms.Cursor.Position;
            RegionOverlayWindow active = windows.FirstOrDefault(w => w.Shot.Bounds.Contains(cursor)) ?? windows[0];
            active.Activate();
            active.Focus();
        }
    }

    internal sealed class RegionOverlayWindow : Window
    {
        private const int MinSize = 6; // physical px; smaller drags count as a stray click

        private readonly Action<MonitorShot, Drawing.Rectangle?> _done;
        private readonly RectangleGeometry _hole = new RectangleGeometry();
        private readonly Rectangle _frame;
        private readonly Border _hint;
        private Point? _start;

        public RegionOverlayWindow(MonitorShot shot, string hint, Action<MonitorShot, Drawing.Rectangle?> done)
        {
            Shot = shot;
            _done = done;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.Cross;
            Background = Brushes.Black;
            UseLayoutRounding = true;
            // Placeholder size; the real physical bounds are applied once the HWND exists.
            Left = 0; Top = 0; Width = 200; Height = 200;

            var image = new Image { Source = ImageUtil.ToBitmapSource(shot.Image), Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            var dim = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0)),
                IsHitTestVisible = false,
            };
            var full = new RectangleGeometry();
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(full);
            group.Children.Add(_hole);
            dim.Data = group;

            _frame = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)),
                StrokeThickness = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            _hint = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1B, 0x1D, 0x21)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 28, 0, 0),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = hint,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                    FontSize = 15,
                },
            };

            var root = new Grid();
            root.Children.Add(image);
            root.Children.Add(dim);
            root.Children.Add(_frame);
            root.Children.Add(_hint);
            Content = root;

            root.SizeChanged += (s, e) => full.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);

            SourceInitialized += (s, e) => Place();
            Loaded += (s, e) => Place();
            DpiChanged += (s, e) => Dispatcher.BeginInvoke(new Action(Place));

            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnUp;
            MouseRightButtonUp += (s, e) => _done(null, null);
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) _done(null, null);
            };
        }

        public MonitorShot Shot { get; }

        private void Place()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            Drawing.Rectangle b = Shot.Bounds;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, Native.SWP_SHOWWINDOW);
        }

        private void OnDown(object sender, MouseButtonEventArgs e)
        {
            _start = e.GetPosition(this);
            CaptureMouse();
            _hint.Visibility = Visibility.Collapsed;
            Update(_start.Value);
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            if (_start.HasValue) Update(e.GetPosition(this));
        }

        private void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (!_start.HasValue) return;
            ReleaseMouseCapture();
            Rect sel = new Rect(_start.Value, e.GetPosition(this));
            _start = null;

            // DIPs -> physical pixels of this monitor's capture.
            double sx = Shot.Bounds.Width / Math.Max(1, ActualWidth);
            double sy = Shot.Bounds.Height / Math.Max(1, ActualHeight);
            var area = new Drawing.Rectangle(
                (int)Math.Floor(sel.X * sx), (int)Math.Floor(sel.Y * sy),
                (int)Math.Ceiling(sel.Width * sx), (int)Math.Ceiling(sel.Height * sy));

            if (area.Width < MinSize || area.Height < MinSize)
            {
                _hole.Rect = Rect.Empty;
                _frame.Visibility = Visibility.Collapsed;
                _hint.Visibility = Visibility.Visible;
                return;
            }
            _done(Shot, area);
        }

        private void Update(Point current)
        {
            Rect r = new Rect(_start.Value, current);
            _hole.Rect = r;
            _frame.Margin = new Thickness(r.X - 1, r.Y - 1, 0, 0);
            _frame.Width = r.Width + 2;
            _frame.Height = r.Height + 2;
            _frame.Visibility = Visibility.Visible;
        }
    }
}
