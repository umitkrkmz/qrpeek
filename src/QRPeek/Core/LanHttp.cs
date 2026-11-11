// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace QRPeek.Core
{
    /// <summary>One parsed HTTP request line plus headers. The body stays on the stream.</summary>
    internal sealed class LanRequest
    {
        public string Method { get; private set; }
        public string Path { get; private set; }
        public Dictionary<string, string> Query { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Header(string name) => Headers.TryGetValue(name, out string v) ? v : null;

        /// <summary>Content-Length, or -1 when it's missing or not a plain number.</summary>
        public long ContentLength =>
            long.TryParse(Header("Content-Length"), NumberStyles.None, CultureInfo.InvariantCulture, out long n) ? n : -1;

        /// <summary>Null when the header block isn't a usable HTTP/1.x request.</summary>
        public static LanRequest Parse(string head)
        {
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            if (first.Length < 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal)) return null;

            var r = new LanRequest { Method = first[0] };
            string target = first[1];
            int q = target.IndexOf('?');
            r.Path = q < 0 ? target : target.Substring(0, q);
            if (q >= 0)
            {
                foreach (string pair in target.Substring(q + 1).Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) continue;
                    r.Query[pair.Substring(0, eq)] = Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
            }
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                r.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            return r;
        }
    }

    /// <summary>
    /// The small pieces both local-network servers share: finding the PC's
    /// LAN address, the secret in the URL, reading request headers and
    /// writing simple responses. Deliberately minimal: no keep-alive, no
    /// chunked encoding, one request per connection.
    /// </summary>
    internal static class LanHttp
    {
        public const int MaxHeaderBytes = 8192;

        /// <summary>
        /// The IPv4 address a phone on the same Wi-Fi can reach: an active
        /// adapter with a default gateway, preferring real Wi-Fi/Ethernet over
        /// virtual ones (Hyper-V, WSL, VMware, VPN). Null when offline.
        /// </summary>
        public static IPAddress FindLanAddress()
        {
            IPAddress best = null;
            int bestScore = int.MinValue;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                IPInterfaceProperties props = ni.GetIPProperties();
                UnicastIPAddressInformation v4 = props.UnicastAddresses.FirstOrDefault(a =>
                    a.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(a.Address)
                    && !a.Address.ToString().StartsWith("169.254."));
                if (v4 == null) continue;

                int score = 0;
                if (props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) score += 4;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 2;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 1;
                string id = (ni.Name + " " + ni.Description).ToLowerInvariant();
                if (new[] { "virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "wsl", "docker", "vpn", "tap-", "wireguard", "tailscale", "zerotier" }.Any(id.Contains)) score -= 6;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = v4.Address;
                }
            }
            return best;
        }

        /// <summary>128 random bits, URL-safe (22 characters).</summary>
        public static string NewToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Uniform random integer in [0, max).</summary>
        public static int RandomInt(int max)
        {
            var b = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                uint limit = uint.MaxValue - uint.MaxValue % (uint)max;
                while (true)
                {
                    rng.GetBytes(b);
                    uint v = BitConverter.ToUInt32(b, 0);
                    if (v < limit) return (int)(v % (uint)max);
                }
            }
        }

        /// <summary>Reads up to the blank line; null if the client sends more than <see cref="MaxHeaderBytes"/> or hangs up.</summary>
        public static string ReadHeaders(Stream stream)
        {
            var buf = new List<byte>(1024);
            var one = new byte[1];
            while (buf.Count < MaxHeaderBytes)
            {
                int n = stream.Read(one, 0, 1);
                if (n <= 0) return null;
                buf.Add(one[0]);
                int c = buf.Count;
                if (c >= 4 && buf[c - 4] == '\r' && buf[c - 3] == '\n' && buf[c - 2] == '\r' && buf[c - 1] == '\n')
                {
                    return Encoding.ASCII.GetString(buf.ToArray());
                }
            }
            return null;
        }

        /// <summary>Reads exactly <paramref name="length"/> bytes of a small body (caller caps the length).</summary>
        public static byte[] ReadBody(Stream stream, int length)
        {
            var body = new byte[length];
            int got = 0;
            while (got < length)
            {
                int n = stream.Read(body, got, length - got);
                if (n <= 0) return null;
                got += n;
            }
            return body;
        }

        public static void Send(Stream s, int code, string type, byte[] body, bool head = false, string extraHeaders = null)
        {
            string headers = "HTTP/1.1 " + code + " " + Reason(code) + "\r\n"
                             + "Content-Type: " + type + "\r\n"
                             + "Content-Length: " + body.Length + "\r\n"
                             + "Cache-Control: no-store\r\n"
                             + "X-Content-Type-Options: nosniff\r\n"
                             + "Referrer-Policy: no-referrer\r\n"
                             + (extraHeaders ?? "")
                             + "Connection: close\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(headers);
            s.Write(h, 0, h.Length);
            if (!head) s.Write(body, 0, body.Length);
        }

        public static void SendText(Stream s, int code, string text) =>
            Send(s, code, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

        public static void SendJson(Stream s, int code, string json) =>
            Send(s, code, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));

        private static string Reason(int code)
        {
            switch (code)
            {
                case 200: return "OK";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 409: return "Conflict";
                case 410: return "Gone";
                case 411: return "Length Required";
                case 413: return "Payload Too Large";
                case 507: return "Insufficient Storage";
                default: return "Error";
            }
        }

        /// <summary>A JSON string literal (quotes included).</summary>
        public static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '<': sb.Append("\\u003c"); break; // never closes a <script> it's embedded in
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string FormatSize(long bytes)
        {
            CultureInfo ci = S.IsTurkish ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#", ci) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1048576.0).ToString("0.#", ci) + " MB";
            return (bytes / 1073741824.0).ToString("0.##", ci) + " GB";
        }
    }
}
