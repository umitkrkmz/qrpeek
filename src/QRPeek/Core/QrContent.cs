// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace QRPeek.Core
{
    internal enum ContentKind
    {
        Text,
        Url,
        Wifi,
        Email,
        Phone,
        Sms,
        Contact,
        Location,
        /// <summary>otpauth:// - a two-factor (2FA) setup code. Sensitive: never stored, never auto-copied.</summary>
        Otp,
        /// <summary>Turkish GİB e-Fatura / e-Arşiv invoice QR (JSON).</summary>
        Invoice,
        /// <summary>QRPeek's own receipt summary ("Summary QR" on the receipt card), read back into a card.</summary>
        Receipt,
    }

    internal sealed class WifiInfo
    {
        public string Ssid { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>QR value of T: - "WPA", "SAE" (WPA3), "WEP" or "nopass".</summary>
        public string Security { get; set; } = "WPA";
        public bool Hidden { get; set; }

        public bool IsOpen => string.Equals(Security, "nopass", StringComparison.OrdinalIgnoreCase);

        /// <summary>A secured network whose password isn't here (history keeps Wi-Fi codes without it).</summary>
        public bool PasswordMissing => !IsOpen && Password.Length == 0;
    }

    internal sealed class OtpInfo
    {
        public string Type { get; set; } = "totp";
        public string Issuer { get; set; } = "";
        public string Account { get; set; } = "";
        public string Secret { get; set; } = "";
        public string Digits { get; set; } = "6";
        public string Period { get; set; } = "30";
    }

    /// <summary>Fields of a GİB e-Fatura / e-Arşiv QR code (keys as in GİB's QR guide).</summary>
    internal sealed class InvoiceInfo
    {
        public string Number { get; set; } = "";
        public string Date { get; set; } = "";
        public string SellerId { get; set; } = "";
        public string BuyerId { get; set; } = "";
        public string Scenario { get; set; } = "";
        public string Type { get; set; } = "";
        public string Currency { get; set; } = "TRY";
        public decimal? Subtotal { get; set; }
        public decimal? Vat { get; set; }
        public decimal? TaxIncluded { get; set; }
        public decimal? Payable { get; set; }
        public string Ettn { get; set; } = "";
    }

    /// <summary>What a decoded QR text means, plus anything worth warning about.</summary>
    internal sealed class QrContent
    {
        private static readonly string[] Shorteners =
        {
            "bit.ly", "tinyurl.com", "t.co", "goo.gl", "ow.ly", "is.gd", "buff.ly", "cutt.ly",
            "rebrand.ly", "shorturl.at", "rb.gy", "t.ly", "s.id", "tiny.cc", "lnkd.in", "qrco.de",
            "bl.ink", "short.io", "v.gd", "shorturl.com",
        };

        private QrContent(string raw, ContentKind kind)
        {
            Raw = raw;
            Kind = kind;
        }

        public string Raw { get; }
        public ContentKind Kind { get; }

        /// <summary>For URL / mailto / tel / sms: what Open launches. Null if nothing to open.</summary>
        public string OpenTarget { get; private set; }

        /// <summary>For URLs: the host to show in bold, so the user sees where it really goes.</summary>
        public string Host { get; private set; }

        public WifiInfo Wifi { get; private set; }

        public OtpInfo Otp { get; private set; }

        public InvoiceInfo Invoice { get; private set; }

        public Receipt Receipt { get; private set; }

        /// <summary>Holds a secret (2FA key, Wi-Fi password): no auto-copy, no clipboard history.</summary>
        public bool IsSensitive => Kind == ContentKind.Otp || (Kind == ContentKind.Wifi && !Wifi.IsOpen);

        /// <summary>
        /// What may be written to history: 2FA codes never, Wi-Fi codes without
        /// the password, everything else as is. Null means "don't store".
        /// </summary>
        public static string ForHistory(string raw)
        {
            QrContent c = Parse(raw);
            if (c.Kind == ContentKind.Otp) return null;
            if (c.Kind == ContentKind.Wifi && !c.Wifi.IsOpen)
            {
                string s = "WIFI:T:" + EscapeWifi(c.Wifi.Security) + ";S:" + EscapeWifi(c.Wifi.Ssid) + ";";
                if (c.Wifi.Hidden) s += "H:true;";
                return s + ";";
            }
            return raw;
        }

        /// <summary>Short human summary shown above the raw text (e.g. the network name).</summary>
        public string Summary { get; private set; }

        public List<string> Warnings { get; } = new List<string>();

        public static QrContent Parse(string raw)
        {
            string text = (raw ?? "").Trim();
            string lower = text.ToLowerInvariant();

            if (lower.StartsWith("otpauth://"))
            {
                OtpInfo otp = ParseOtp(text);
                if (otp != null)
                {
                    string who = otp.Issuer.Length > 0 && otp.Account.Length > 0 ? otp.Issuer + " · " + otp.Account : otp.Issuer + otp.Account;
                    return new QrContent(raw, ContentKind.Otp) { Otp = otp, Summary = who };
                }
            }

            if (text.StartsWith("{") && (lower.Contains("\"ettn\"") || lower.Contains("\"vkntckn\"")))
            {
                InvoiceInfo inv = ParseInvoice(text);
                if (inv != null)
                {
                    return new QrContent(raw, ContentKind.Invoice) { Invoice = inv, Summary = inv.Number };
                }
            }

            if (lower.StartsWith("wifi:"))
            {
                WifiInfo wifi = ParseWifi(text);
                if (wifi != null)
                {
                    return new QrContent(raw, ContentKind.Wifi) { Wifi = wifi, Summary = wifi.Ssid };
                }
            }

            if (lower.StartsWith("qrpeek "))
            {
                Receipt receipt = ReceiptExport.ReadSummary(text);
                if (receipt != null)
                {
                    string who = string.Join(" · ", new[] { receipt["seller"]?.Value, receipt["total"]?.Value }.Where(s => !string.IsNullOrEmpty(s)));
                    return new QrContent(raw, ContentKind.Receipt) { Receipt = receipt, Summary = who };
                }
            }

            if (lower.StartsWith("mailto:") || lower.StartsWith("matmsg:"))
            {
                var c = new QrContent(raw, ContentKind.Email);
                if (lower.StartsWith("mailto:"))
                {
                    c.OpenTarget = text;
                    c.Summary = WebUtility.UrlDecode(text.Substring(7).Split('?')[0]);
                }
                else
                {
                    string to = MeField(text, "TO");
                    c.Summary = to;
                    if (!string.IsNullOrEmpty(to)) c.OpenTarget = "mailto:" + to;
                }
                return c;
            }

            if (lower.StartsWith("tel:"))
            {
                return new QrContent(raw, ContentKind.Phone) { OpenTarget = text, Summary = text.Substring(4) };
            }

            if (lower.StartsWith("smsto:") || lower.StartsWith("sms:"))
            {
                string rest = text.Substring(text.IndexOf(':') + 1);
                string number = rest.Split(':', '?')[0];
                return new QrContent(raw, ContentKind.Sms) { OpenTarget = "sms:" + number, Summary = number };
            }

            if (lower.StartsWith("begin:vcard") || lower.StartsWith("mecard:"))
            {
                return new QrContent(raw, ContentKind.Contact) { Summary = ContactName(text) };
            }

            if (lower.StartsWith("geo:"))
            {
                string coords = text.Substring(4).Split('?')[0];
                var c = new QrContent(raw, ContentKind.Location) { Summary = coords };
                string[] parts = coords.Split(',');
                if (parts.Length >= 2)
                {
                    c.OpenTarget = "https://www.openstreetmap.org/?mlat=" + Uri.EscapeDataString(parts[0].Trim())
                        + "&mlon=" + Uri.EscapeDataString(parts[1].Trim()) + "&zoom=16";
                }
                return c;
            }

            Uri uri = TryUrl(text);
            if (uri != null)
            {
                var c = new QrContent(raw, ContentKind.Url)
                {
                    OpenTarget = uri.AbsoluteUri,
                    Host = uri.IdnHost,
                    Summary = uri.Host,
                };
                c.AddLinkWarnings(uri);
                return c;
            }

            return new QrContent(raw, ContentKind.Text);
        }

        private static Uri TryUrl(string text)
        {
            if (text.Length == 0 || text.Any(char.IsWhiteSpace)) return null;

            string candidate = text;
            if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) candidate = "https://" + candidate;

            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrEmpty(uri.Host))
            {
                return uri;
            }
            return null;
        }

        private void AddLinkWarnings(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttp) Warnings.Add(S.WarnHttp);

            if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
            {
                Warnings.Add(S.WarnIpHost);
            }

            // Uri.Host keeps Unicode for IDN hosts; IdnHost is the punycode form.
            bool idn = uri.IdnHost.Split('.').Any(p => p.StartsWith("xn--", StringComparison.OrdinalIgnoreCase))
                       || uri.Host.Any(ch => ch > 127);
            if (idn) Warnings.Add(S.WarnIdn);

            if (!string.IsNullOrEmpty(uri.UserInfo)) Warnings.Add(S.WarnUserInfo);

            string host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            if (Shorteners.Contains(host)) Warnings.Add(S.WarnShortener);
        }

        /// <summary>
        /// Parses "WIFI:T:WPA;S:name;P:pass;H:true;;". Fields can come in any
        /// order; \ ; , : and " are backslash-escaped inside values.
        /// </summary>
        internal static WifiInfo ParseWifi(string text)
        {
            var info = new WifiInfo();
            bool hasSsid = false, hasType = false;
            foreach (string field in SplitUnescaped(text.Substring(5), ';'))
            {
                int colon = IndexOfUnescaped(field, ':');
                if (colon <= 0) continue;
                string key = field.Substring(0, colon).Trim().ToUpperInvariant();
                string value = Unescape(field.Substring(colon + 1));
                switch (key)
                {
                    case "S": info.Ssid = value; hasSsid = true; break;
                    case "P": info.Password = value; break;
                    case "T": info.Security = value.Length == 0 ? "nopass" : value; hasType = true; break;
                    case "H": info.Hidden = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                }
            }
            // Some generators leave T: out; then a password means WPA, none means open.
            if (!hasType) info.Security = info.Password.Length > 0 ? "WPA" : "nopass";
            return hasSsid ? info : null;
        }

        /// <summary>otpauth://totp/Issuer:account?secret=...&amp;issuer=...&amp;digits=6&amp;period=30</summary>
        internal static OtpInfo ParseOtp(string text)
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri)) return null;
            var info = new OtpInfo { Type = uri.Host.ToLowerInvariant() };

            string label = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            int colon = label.IndexOf(':');
            if (colon >= 0)
            {
                info.Issuer = label.Substring(0, colon).Trim();
                info.Account = label.Substring(colon + 1).Trim();
            }
            else
            {
                info.Account = label.Trim();
            }

            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq).ToLowerInvariant();
                string value = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                switch (key)
                {
                    case "secret": info.Secret = value; break;
                    case "issuer": info.Issuer = value; break;
                    case "digits": info.Digits = value; break;
                    case "period": info.Period = value; break;
                }
            }
            return info.Secret.Length > 0 ? info : null;
        }

        /// <summary>
        /// Amounts in real e-Fatura QR codes come in both styles, even within
        /// one code: "11373.33" and "13.648,00". The last separator is the
        /// decimal one; any earlier separators are thousands grouping. A lone
        /// comma is a decimal comma; a lone dot is a decimal point.
        /// </summary>
        internal static decimal? ParseAmount(string s)
        {
            s = (s ?? "").Trim().Replace(" ", "").Replace(" ", "");
            if (s.Length == 0) return null;
            int lastDot = s.LastIndexOf('.'), lastComma = s.LastIndexOf(',');
            int dec = Math.Max(lastDot, lastComma);
            if (dec >= 0)
            {
                char decChar = s[dec];
                char other = decChar == '.' ? ',' : '.';
                bool groupedOnly = s.IndexOf(decChar) != dec; // "1.234.567": several of the same, no other kind
                if (groupedOnly && s.IndexOf(other) < 0)
                {
                    s = s.Replace(decChar.ToString(), "");
                }
                else
                {
                    string whole = s.Substring(0, dec).Replace(".", "").Replace(",", "");
                    s = whole + "." + s.Substring(dec + 1);
                }
            }
            return decimal.TryParse(s, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : (decimal?)null;
        }

        internal static InvoiceInfo ParseInvoice(string json)
        {
            Dictionary<string, string> d = FlatJson.Parse(json);
            if (d == null) return null;

            string Get(string key) => d.TryGetValue(key, out string v) ? v.Trim() : "";

            decimal? Num(string s) => ParseAmount(s);

            // VAT can be split by rate: hesaplanankdv(20), hesaplanankdv(10)...
            decimal? vat = null;
            foreach (KeyValuePair<string, string> kv in d)
            {
                if (kv.Key.StartsWith("hesaplanankdv", StringComparison.OrdinalIgnoreCase))
                {
                    decimal? part = Num(kv.Value);
                    if (part.HasValue) vat = (vat ?? 0) + part.Value;
                }
            }

            var inv = new InvoiceInfo
            {
                Number = Get("no"),
                Date = Get("tarih"),
                SellerId = Get("vkntckn"),
                BuyerId = Get("avkntckn"),
                Scenario = Get("senaryo"),
                Type = Get("tip"),
                Currency = Get("parabirimi").Length > 0 ? Get("parabirimi") : "TRY",
                Subtotal = Num(Get("malhizmettoplam")),
                Vat = vat,
                TaxIncluded = Num(Get("vergidahil")),
                Payable = Num(Get("odenecek")),
                Ettn = Get("ettn"),
            };
            return inv.Number.Length > 0 || inv.Ettn.Length > 0 ? inv : null;
        }

        private static IEnumerable<string> SplitUnescaped(string s, char sep)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\' && i + 1 < s.Length)
                {
                    sb.Append(ch).Append(s[i + 1]);
                    i++;
                }
                else if (ch == sep)
                {
                    if (sb.Length > 0) yield return sb.ToString();
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static int IndexOfUnescaped(string s, char target)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; }
                if (s[i] == target) return i;
            }
            return -1;
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[i + 1]); i++; }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        internal static string EscapeWifi(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == '\\' || ch == ';' || ch == ',' || ch == ':' || ch == '"') sb.Append('\\');
                sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string MeField(string text, string key)
        {
            foreach (string part in SplitUnescaped(text.Substring(text.IndexOf(':') + 1), ';'))
            {
                if (part.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return Unescape(part.Substring(key.Length + 1));
                }
            }
            return "";
        }

        private static string ContactName(string text)
        {
            if (text.StartsWith("MECARD:", StringComparison.OrdinalIgnoreCase))
            {
                return MeField(text, "N").Replace(",", " ").Trim();
            }
            foreach (string line in text.Split('\n'))
            {
                string l = line.Trim('\r', ' ');
                if (l.StartsWith("FN", StringComparison.OrdinalIgnoreCase))
                {
                    int c = l.IndexOf(':');
                    if (c > 0) return l.Substring(c + 1).Trim();
                }
            }
            return "";
        }
    }
}
