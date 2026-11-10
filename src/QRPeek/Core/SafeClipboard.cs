// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace QRPeek.Core
{
    internal static class SafeClipboard
    {
        /// <summary>
        /// Puts text on the clipboard. With <paramref name="sensitive"/> (Wi-Fi
        /// passwords, 2FA keys) it is marked so Windows keeps it out of the
        /// clipboard history (Win+V) and cloud clipboard sync, and clipboard
        /// managers that honor the convention skip it.
        /// </summary>
        public static bool SetText(string text, bool sensitive = false)
        {
            var data = new DataObject();
            data.SetText(text);
            if (sensitive)
            {
                data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 0 }));
                data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
                data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
            }

            // The clipboard can be locked by another app for a moment (clipboard
            // managers, RDP). A few quick retries cover nearly all of it.
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetDataObject(data, true);
                    return true;
                }
                catch (COMException)
                {
                    Thread.Sleep(40);
                }
            }
            return false;
        }
    }
}
