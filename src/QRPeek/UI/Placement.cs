// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using QRPeek.Core;

namespace QRPeek.UI
{
    internal static class Placement
    {
        /// <summary>
        /// Moves an already shown window onto the monitor under the mouse (where
        /// the user is looking), horizontally centered, in physical pixels so
        /// mixed-DPI setups work too.
        /// </summary>
        public static void OnCursorMonitor(Window w, double topFraction = 0.15)
        {
            System.Drawing.Rectangle wa = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            DpiScale dpi = VisualTreeHelper.GetDpi(w);
            int widthPx = (int)(w.ActualWidth * dpi.DpiScaleX);
            int heightPx = (int)(w.ActualHeight * dpi.DpiScaleY);
            int x = wa.Left + Math.Max(0, (wa.Width - widthPx) / 2);
            int y = wa.Top + Math.Max(0, Math.Min((int)(wa.Height * topFraction), wa.Height - heightPx));
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }
    }
}
