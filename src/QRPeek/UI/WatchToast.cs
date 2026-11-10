// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using QRPeek.Core;

namespace QRPeek.UI
{
    /// <summary>
    /// Watch mode's own pop-up, bottom-right of the monitor the user is on.
    /// Windows notifications are not used for this: Do Not Disturb (which
    /// Windows turns on by itself while presenting or in full screen) would
    /// swallow them in exactly the meeting situation watch mode is for.
    /// Never takes focus; closes by itself after 10 s.
    /// </summary>
    internal sealed class WatchToast : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        private static WatchToast _current;
        private readonly DispatcherTimer _life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };

        public static void Show(string message, Action onOpen)
        {
            _current?.Close();
            _current = new WatchToast(message, onOpen);
            _current.Show();
        }

        private WatchToast(string message, Action onOpen)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = (FontFamily)FindResource("UiFont");
            Foreground = (Brush)FindResource("Fg");
            UseLayoutRounding = true;

            var icon = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/QRPeek.png")),
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            DockPanel.SetDock(icon, Dock.Left);

            var close = new Button
            {
                Style = (Style)FindResource("Ghost"),
                Content = new TextBlock { Text = "", Style = (Style)FindResource("Icon"), FontSize = 11 },
                Padding = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, -4, -6, 0),
            };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Right);

            var open = new Button
            {
                Style = (Style)FindResource("Primary"),
                Content = S.Show,
                Padding = new Thickness(14, 5, 14, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 0),
            };
            open.Click += (s, e) =>
            {
                Close();
                onOpen();
            };

            var text = new StackPanel { Width = 260 };
            text.Children.Add(new TextBlock { Text = S.AppName, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            text.Children.Add(new TextBlock { Text = message, FontSize = 13, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 40 });
            text.Children.Add(open);

            var row = new DockPanel();
            row.Children.Add(icon);
            row.Children.Add(close);
            row.Children.Add(text);

            Content = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 14),
                Margin = new Thickness(12), // room for the shadow
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.35 },
                Child = row,
            };

            SourceInitialized += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            };
            Loaded += (s, e) => PlaceBottomRight();
            MouseEnter += (s, e) => _life.Stop();  // reading it: don't vanish
            MouseLeave += (s, e) => _life.Start();
            _life.Tick += (s, e) => Close();
            _life.Start();
            Closed += (s, e) =>
            {
                _life.Stop();
                if (_current == this) _current = null;
            };
        }

        private void PlaceBottomRight()
        {
            System.Drawing.Rectangle wa = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int w = (int)(ActualWidth * dpi.DpiScaleX);
            int h = (int)(ActualHeight * dpi.DpiScaleY);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, wa.Right - w - 8, wa.Bottom - h - 8, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }
}
