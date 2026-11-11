// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QRPeek.Core
{
    /// <summary>
    /// How a file sent from a phone lands on disk. A name from the network is
    /// never used as a path: only a cleaned-up file name is kept, it always
    /// goes into the receive folder, nothing is ever overwritten, and the file
    /// is marked as coming from the internet so Windows warns before running it.
    /// </summary>
    internal static class ReceivedFiles
    {
        public const int MaxNameLength = 120;
        private const string PartExtension = ".qrpeek-part";

        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
        };

        /// <summary>Files that run code when opened. The approval dialog warns about these.</summary>
        private static readonly HashSet<string> RiskyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".com", ".scr", ".pif", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".appinstaller",
            ".application", ".appref-ms", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse",
            ".wsf", ".wsh", ".hta", ".lnk", ".url", ".scf", ".reg", ".cpl", ".msc", ".inf", ".dll", ".sys",
            ".jar", ".iso", ".img", ".vhd", ".vhdx", ".chm", ".settingcontent-ms", ".library-ms", ".search-ms",
        };

        /// <summary>
        /// A safe file name for <paramref name="raw"/> as the phone sent it:
        /// no folders, no characters Windows forbids or treats specially
        /// (':' would write a hidden stream), no direction-override tricks
        /// ("gpj.exe" shown as "exe.jpg"), no device names like CON, and not
        /// too long. Never empty.
        /// </summary>
        public static string SafeName(string raw)
        {
            string name = raw ?? "";
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (slash >= 0) name = name.Substring(slash + 1);

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (IsBidiControl(c)) continue;
                sb.Append(c < 0x20 || c == 0x7F || invalid.Contains(c) ? '_' : c);
            }
            name = sb.ToString().Trim().TrimEnd('.', ' ');

            if (name.Length == 0) name = "file";

            string stem = name.Split('.')[0].TrimEnd(' ');
            if (Reserved.Contains(stem)) name = "_" + name;

            if (name.Length > MaxNameLength)
            {
                string ext = Path.GetExtension(name);
                if (ext.Length > 20) ext = "";
                name = name.Substring(0, MaxNameLength - ext.Length).TrimEnd('.', ' ') + ext;
            }
            return name;
        }

        public static bool IsRisky(string safeName) => RiskyExtensions.Contains(Path.GetExtension(safeName) ?? "");

        private static bool IsBidiControl(char c) =>
            c == '‎' || c == '‏' || c == '؜' || (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩');

        /// <summary>A new, empty temp file in <paramref name="folder"/> to receive into; it never collides with a real file.</summary>
        public static string NewPartPath(string folder)
        {
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "." + Guid.NewGuid().ToString("N").Substring(0, 12) + PartExtension);
        }

        /// <summary>
        /// Moves a finished part file to its final name: <paramref name="safeName"/>,
        /// or "name (1).ext", "name (2).ext"… if that exists. Never overwrites.
        /// </summary>
        public static string Finish(string partPath, string folder, string safeName)
        {
            string stem = Path.GetFileNameWithoutExtension(safeName);
            string ext = Path.GetExtension(safeName);
            for (int i = 0; i < 10000; i++)
            {
                string target = Path.Combine(folder, i == 0 ? safeName : stem + " (" + i + ")" + ext);
                if (File.Exists(target) || Directory.Exists(target)) continue;
                try
                {
                    File.Move(partPath, target); // fails instead of overwriting if it appeared meanwhile
                    return target;
                }
                catch (IOException) when (File.Exists(target)) { }
            }
            throw new IOException("No free file name for " + safeName);
        }

        /// <summary>
        /// Adds the "downloaded from the internet" mark (Zone.Identifier,
        /// ZoneId=3), as browsers do, so SmartScreen and Office warn before a
        /// received program or macro document runs. Silently skipped on drives
        /// without NTFS streams (FAT32/exFAT USB sticks).
        /// </summary>
        public static void MarkFromInternet(string path)
        {
            byte[] mark = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
            using (SafeFileHandle h = Native.CreateFile(path + ":Zone.Identifier", Native.GENERIC_WRITE, 0, IntPtr.Zero, Native.CREATE_ALWAYS, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return;
                using (var fs = new FileStream(h, FileAccess.Write)) fs.Write(mark, 0, mark.Length);
            }
        }

        /// <summary>The Zone.Identifier contents, or null if the file has none. For tests.</summary>
        internal static string ReadZoneMark(string path)
        {
            using (SafeFileHandle h = Native.CreateFile(path + ":Zone.Identifier", Native.GENERIC_READ, 1, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) return null;
                using (var fs = new FileStream(h, FileAccess.Read))
                using (var r = new StreamReader(fs, Encoding.ASCII)) return r.ReadToEnd();
            }
        }

        /// <summary>True if the folder exists (or can be created) and a file can be written in it.</summary>
        public static bool CanWrite(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                using (new FileStream(NewPartPath(folder), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// How to show a folder: "Downloads\QRPeek" instead of
        /// "C:\Users\name\Downloads\QRPeek" (shorter, and no user name on screen).
        /// </summary>
        public static string Display(string folder, string downloads, string downloadsName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(downloads)) return folder;
            string d = downloads.TrimEnd('\\');
            if (string.Equals(folder.TrimEnd('\\'), d, StringComparison.OrdinalIgnoreCase)) return downloadsName;
            if (folder.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase)) return downloadsName + folder.Substring(d.Length);
            return folder;
        }

        /// <summary>The default receive folder: Downloads\QRPeek.</summary>
        public static string DefaultFolder() => Path.Combine(Native.DownloadsFolder(), "QRPeek");

        public static void TryDelete(string path)
        {
            if (path == null) return;
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>Free bytes on the drive that holds <paramref name="folder"/>; long.MaxValue if unknown.</summary>
        public static long FreeSpace(string folder)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(folder));
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException || ex is Win32Exception)
            {
                return long.MaxValue;
            }
        }
    }
}
