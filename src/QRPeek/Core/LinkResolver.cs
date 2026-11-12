// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace QRPeek.Core
{
    internal sealed class ResolveResult
    {
        public List<Uri> Chain { get; } = new List<Uri>();
        public Uri Final => Chain[Chain.Count - 1];
        /// <summary>Set when a hop failed; Chain still holds what was reached.</summary>
        public string Error { get; set; }

        /// <summary>
        /// The HTTP status (401, 403, 429…) when the last address refused the
        /// check instead of answering it. Then we don't know where a browser
        /// would end up: <see cref="Final"/> is only how far we got.
        /// </summary>
        public int? RefusedWith { get; set; }
    }

    /// <summary>
    /// Follows HTTP redirects (bit.ly -> ... -> real site) without opening a
    /// browser: no cookies, no JavaScript, HEAD requests where the server
    /// allows them. This is the only place QRPeek talks to the network, and
    /// only when the user presses "show real destination".
    /// </summary>
    internal static class LinkResolver
    {
        private const int MaxHops = 10;
        private const int TimeoutMs = 8000;

        public static Task<ResolveResult> ResolveAsync(Uri start) => Task.Run(() => Resolve(start));

        private static ResolveResult Resolve(Uri start)
        {
            var result = new ResolveResult();
            result.Chain.Add(start);
            Uri current = start;

            for (int hop = 0; hop < MaxHops; hop++)
            {
                Uri next;
                int refused;
                try
                {
                    next = NextHop(current, "HEAD", out refused);
                    // Some sites refuse HEAD but answer GET normally: ask once more before giving up.
                    if (refused != 0) next = NextHop(current, "GET", out refused);
                }
                catch (WebException ex) when (IsMethodRejected(ex))
                {
                    try { next = NextHop(current, "GET", out refused); }
                    catch (WebException ex2) { result.Error = Describe(ex2); return result; }
                }
                catch (WebException ex)
                {
                    result.Error = Describe(ex);
                    return result;
                }

                if (refused != 0)
                {
                    // e.g. a bot check (403): where a browser ends up is unknown, never "no redirect".
                    result.RefusedWith = refused;
                    return result;
                }
                if (next == null) return result; // not a redirect: this is the destination
                if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps) return result;
                if (result.Chain.Contains(next)) return result; // loop
                result.Chain.Add(next);
                current = next;
            }
            return result;
        }

        /// <summary>
        /// The Location of a 3xx answer, or null for any other answer.
        /// <paramref name="refused"/> is the status when the site refused to
        /// answer (see <see cref="IsRefusal"/>), else 0.
        /// </summary>
        private static Uri NextHop(Uri url, string method, out int refused)
        {
            refused = 0;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.AllowAutoRedirect = false;
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) QRPeek";
            req.Accept = "*/*";
            req.CookieContainer = null;

            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r && (int)r.StatusCode < 500 && (int)r.StatusCode != 405 && (int)r.StatusCode != 501)
            {
                // 4xx. A missing page (404, 410) is still the destination. But a
                // refusal (403 bot check, 429, login) says nothing about where a
                // browser would be sent, so it must not read as "no redirect".
                if (IsRefusal(r)) refused = (int)r.StatusCode;
                r.Dispose();
                return null;
            }

            using (resp)
            {
                int code = (int)resp.StatusCode;
                string location = resp.Headers[HttpResponseHeader.Location];
                if (code >= 300 && code < 400 && !string.IsNullOrEmpty(location)
                    && Uri.TryCreate(url, location, out Uri next))
                {
                    return next;
                }
                return null;
            }
        }

        /// <summary>Answers that mean "not telling you" rather than "this is the page".</summary>
        private static bool IsRefusal(HttpWebResponse r)
        {
            int code = (int)r.StatusCode;
            return code == 401 || code == 403 || code == 407 || code == 429 || code == 451
                   || !string.IsNullOrEmpty(r.Headers["cf-mitigated"]); // Cloudflare challenge, whatever the status
        }

        private static bool IsMethodRejected(WebException ex) =>
            ex.Response is HttpWebResponse r && ((int)r.StatusCode == 405 || (int)r.StatusCode == 501 || (int)r.StatusCode >= 500);

        private static string Describe(WebException ex)
        {
            if (ex.Response is HttpWebResponse r) return "HTTP " + (int)r.StatusCode;
            switch (ex.Status)
            {
                case WebExceptionStatus.Timeout: return S.ResolveTimeout;
                case WebExceptionStatus.NameResolutionFailure: return S.ResolveNoDns;
                case WebExceptionStatus.ConnectFailure: return S.ResolveNoConnect;
                case WebExceptionStatus.TrustFailure:
                case WebExceptionStatus.SecureChannelFailure: return S.ResolveTls;
                default: return ex.Status.ToString();
            }
        }
    }
}
