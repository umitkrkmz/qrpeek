// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QRPeek.Core
{
    [DataContract]
    internal sealed class AppSettings
    {
        public const char ScanKey = 'Q';
        public const char SendKey = 'W';
        public const char ReceiveKey = 'R';

        /// <summary>Scan screen shortcut (letter <see cref="ScanKey"/>).</summary>
        [DataMember] public string Hotkey { get; set; }
        /// <summary>"Send clipboard to phone" shortcut (letter <see cref="SendKey"/>).</summary>
        [DataMember] public string SendHotkey { get; set; }
        /// <summary>"Receive from phone" shortcut (letter <see cref="ReceiveKey"/>); off by default.</summary>
        [DataMember] public string ReceiveHotkey { get; set; }
        /// <summary>Where files from the phone are saved; empty = Downloads\QRPeek.</summary>
        [DataMember] public string ReceiveFolder { get; set; }
        /// <summary>The Settings section open last time (shortcuts, general, language, ocr, about).</summary>
        [DataMember] public string SettingsPage { get; set; }
        /// <summary>Receipt card fields the user ticked / unticked against the default (remembered for the next receipt).</summary>
        [DataMember] public List<string> ReceiptOn { get; set; }
        [DataMember] public List<string> ReceiptOff { get; set; }
        [DataMember] public bool CopyAutomatically { get; set; }
        [DataMember] public bool KeepHistory { get; set; }
        [DataMember] public bool WatchScreen { get; set; }
        [DataMember] public string Language { get; set; }
        [DataMember] public bool TrayHintShown { get; set; }
        [DataMember] public bool WelcomeShown { get; set; }
        /// <summary>GitHub build: ask GitHub once a day whether a newer release exists.</summary>
        [DataMember] public bool CheckUpdates { get; set; }
        [DataMember] public DateTime LastUpdateCheck { get; set; }
        /// <summary>The newest version the user was already told about (tell once per version).</summary>
        [DataMember] public string NotifiedVersion { get; set; }

        public AppSettings() => SetDefaults();

        // DataContractJsonSerializer skips constructors; this keeps defaults for
        // fields that a settings.json from an older version doesn't have.
        [OnDeserializing]
        private void OnDeserializing(StreamingContext _) => SetDefaults();

        private void SetDefaults()
        {
            Hotkey = HotkeyPreset.For(ScanKey)[0].Id;
            SendHotkey = HotkeyPreset.For(SendKey)[0].Id;
            ReceiveHotkey = "none";
            ReceiveFolder = "";
            SettingsPage = "shortcuts";
            ReceiptOn = new List<string>();
            ReceiptOff = new List<string>();
            CopyAutomatically = true;
            KeepHistory = true;
            WatchScreen = false;
            Language = "auto";
            TrayHintShown = false;
            WelcomeShown = false;
            CheckUpdates = true;
            // Not DateTime.MinValue: DataContractJsonSerializer fails on it east of UTC.
            LastUpdateCheck = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            NotifiedVersion = "";
        }
    }

    [DataContract]
    internal sealed class HistoryEntry
    {
        [DataMember] public string Text { get; set; }
        [DataMember] public DateTime Time { get; set; }
    }

    /// <summary>
    /// Plain JSON files in %LOCALAPPDATA%\QRPeek. No database, nothing leaves the PC.
    /// Writes go to a temp file first and replace the old one, so a crash never
    /// leaves a half-written file behind.
    /// </summary>
    internal static class Storage
    {
        public const int MaxHistory = 100;

        public static string Folder { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QRPeek");

        private static string SettingsPath => Path.Combine(Folder, "settings.json");
        private static string HistoryPath => Path.Combine(Folder, "history.json");

        public static AppSettings LoadSettings() => Read<AppSettings>(SettingsPath) ?? new AppSettings();

        public static void SaveSettings(AppSettings s) => Write(SettingsPath, s);

        public static List<HistoryEntry> LoadHistory() => Read<List<HistoryEntry>>(HistoryPath) ?? new List<HistoryEntry>();

        public static void SaveHistory(List<HistoryEntry> items) => Write(HistoryPath, items);

        /// <summary>The user's own receipt terms (plain text, same format as ReceiptTerms.BuiltIn); empty if none.</summary>
        private static string TermsPath => Path.Combine(Folder, "terms.txt");

        public static string LoadUserTerms()
        {
            try { return File.Exists(TermsPath) ? File.ReadAllText(TermsPath, System.Text.Encoding.UTF8) : ""; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ""; }
        }

        public static void SaveUserTerms(string text)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = TermsPath + ".tmp";
                File.WriteAllText(tmp, text ?? "", new System.Text.UTF8Encoding(false));
                if (File.Exists(TermsPath)) File.Replace(tmp, TermsPath, null);
                else File.Move(tmp, TermsPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LogError(ex);
            }
        }

        public static void DeleteHistory()
        {
            try { File.Delete(HistoryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "error.log"),
                    DateTime.Now.ToString("u") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static T Read<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (FileStream fs = File.OpenRead(path))
                {
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(fs);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SerializationException || ex is UnauthorizedAccessException)
            {
                return null; // corrupt or unreadable: fall back to defaults
            }
        }

        private static void Write<T>(string path, T value)
        {
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Folder);
                using (FileStream fs = File.Create(tmp))
                {
                    new DataContractJsonSerializer(typeof(T)).WriteObject(fs, value);
                }
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // Saving settings/history must never break a scan.
                LogError(ex);
                try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
