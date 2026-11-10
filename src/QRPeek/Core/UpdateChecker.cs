// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QRPeek.Core
{
    internal sealed class UpdateInfo
    {
        public Version Latest { get; set; }
        public string Url { get; set; }
        public bool IsNewer => Latest > UpdateChecker.Current;
    }

    /// <summary>
    /// GitHub build only (the Store updates its build by itself): asks GitHub's
    /// public API for the latest release. One small anonymous request, sends
    /// nothing about the user, at most once a day, and only while the setting
    /// is on. Network errors just mean "no news".
    /// </summary>
    internal static class UpdateChecker
    {
        public const string Repo = "umitkrkmz/qrpeek";
        public static readonly TimeSpan Interval = TimeSpan.FromDays(1);
        private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        private const string ReleasesPage = "https://github.com/" + Repo + "/releases";

        public static bool Supported => !Native.IsPackaged();

        public static Version Current
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        /// <summary>The latest release (compare with <see cref="Current"/>), or null if GitHub couldn't be reached.</summary>
        public static Task<UpdateInfo> CheckAsync() => Task.Run(() =>
        {
            try
            {
                return Parse(Fetch());
            }
            catch (Exception ex) when (ex is WebException || ex is IOException || ex is NotSupportedException)
            {
                return null;
            }
        });

        private static string Fetch()
        {
            var req = (HttpWebRequest)WebRequest.Create(Api);
            req.UserAgent = "QRPeek/" + Current;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 10000;
            req.ReadWriteTimeout = 10000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads tag_name ("v0.2.0") and the release page from GitHub's answer.
        /// The page link is only trusted if it points at this repository's
        /// releases; anything else falls back to the releases list.
        /// </summary>
        internal static UpdateInfo Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?(\\d+(?:\\.\\d+){1,3})[^\"]*\"");
            if (!tag.Success || !Version.TryParse(tag.Groups[1].Value, out Version v)) return null;
            if (v.Build < 0) v = new Version(v.Major, v.Minor, 0);

            Match page = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/" + Regex.Escape(Repo) + "/releases/[^\"]+)\"");
            return new UpdateInfo { Latest = v, Url = page.Success ? page.Groups[1].Value : ReleasesPage };
        }
    }
}
