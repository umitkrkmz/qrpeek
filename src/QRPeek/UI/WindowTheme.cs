// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace QRPeek.UI
{
    /// <summary>Follows the Windows light/dark setting, including the title bar.</summary>
    internal static class WindowTheme
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static bool IsDark { get; private set; }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
                }
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        /// <summary>Swaps the color dictionary (index 0 in App.xaml) and refreshes open windows.</summary>
        public static void Apply(Application app, bool dark)
        {
            IsDark = dark;
            var dict = new ResourceDictionary
            {
                Source = new Uri(dark ? "UI/Themes/Dark.xaml" : "UI/Themes/Light.xaml", UriKind.Relative),
            };
            app.Resources.MergedDictionaries[0] = dict;
            foreach (Window w in app.Windows) ApplyTitleBar(w);
        }

        /// <summary>Call from a window's constructor.</summary>
        public static void Attach(Window w)
        {
            w.SourceInitialized += (s, e) => ApplyTitleBar(w);
        }

        private static void ApplyTitleBar(Window w)
        {
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            int on = IsDark ? 1 : 0;
            try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)); }
            catch (DllNotFoundException) { }
        }
    }
}
