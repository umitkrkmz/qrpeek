// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QRPeek.Core
{
    internal sealed class SharedFile
    {
        public SharedFile(string path, string name = null, bool deleteAfter = false)
        {
            Path = path;
            Name = name ?? System.IO.Path.GetFileName(path);
            Length = new FileInfo(path).Length;
            DeleteAfter = deleteAfter;
        }

        public string Path { get; }
        public string Name { get; }
        public long Length { get; }
        /// <summary>Temp file QRPeek made (clipboard image or long text); deleted when sharing stops.</summary>
        public bool DeleteAfter { get; }
    }

    /// <summary>
    /// A tiny one-off web server on the local network: the phone scans the QR,
    /// opens http://PC-IP:port/secret/ and downloads. No cloud, no account.
    ///
    /// Safety: the URL holds a 128-bit random secret (anything else gets 404),
    /// only the listed files are served, read-only, and it stops by itself
    /// after <see cref="Lifetime"/> or when the window closes.
    /// </summary>
    internal sealed class LanShare : IDisposable
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private const int MaxHeaderBytes = 8192;

        private readonly IReadOnlyList<SharedFile> _files;
        private readonly string _token;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _disposed;

        /// <summary>Raised on a worker thread: bytes sent so far for one download, and its total.</summary>
        public event Action<long, long> Progress;

        /// <summary>Raised on a worker thread when one file finished downloading.</summary>
        public event Action<SharedFile> Completed;

        public LanShare(IReadOnlyList<SharedFile> files, IPAddress address)
        {
            _files = files;
            _token = NewToken();
            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = "http://" + address + ":" + port + "/" + _token + "/";
            Expires = DateTime.UtcNow + Lifetime;
            Task.Run(AcceptLoop);
        }

        public string Url { get; }

        public DateTime Expires { get; }

        public IReadOnlyList<SharedFile> Files => _files;

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

        private static string NewToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private async Task AcceptLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }
                catch (InvalidOperationException) { return; }

                _ = Task.Run(() => Handle(client));
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 15000;
                    client.SendTimeout = 30000;
                    NetworkStream stream = client.GetStream();
                    string request = ReadHeaders(stream);
                    if (request == null) return;

                    string[] first = request.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
                    if (first.Length < 2) return;
                    string method = first[0];
                    string path = first[1].Split('?')[0];
                    bool head = method == "HEAD";
                    if (method != "GET" && !head)
                    {
                        Send(stream, 405, "text/plain", Encoding.ASCII.GetBytes("Method not allowed"), head);
                        return;
                    }

                    string prefix = "/" + _token;
                    if (path == prefix || path == prefix + "/")
                    {
                        Send(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(LandingPage()), head);
                        return;
                    }
                    if (path.StartsWith(prefix + "/") && int.TryParse(path.Substring(prefix.Length + 1), out int index)
                        && index >= 0 && index < _files.Count)
                    {
                        SendFile(stream, _files[index], head);
                        return;
                    }
                    Send(stream, 404, "text/plain", Encoding.ASCII.GetBytes("Not found"), head);
                }
                catch (IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private static string ReadHeaders(NetworkStream stream)
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

        private static void Send(Stream s, int code, string type, byte[] body, bool head)
        {
            string status = code == 200 ? "OK" : code == 404 ? "Not Found" : code == 405 ? "Method Not Allowed" : "Error";
            string headers = "HTTP/1.1 " + code + " " + status + "\r\n"
                             + "Content-Type: " + type + "\r\n"
                             + "Content-Length: " + body.Length + "\r\n"
                             + "Cache-Control: no-store\r\n"
                             + "X-Content-Type-Options: nosniff\r\n"
                             + "Referrer-Policy: no-referrer\r\n"
                             + "Connection: close\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(headers);
            s.Write(h, 0, h.Length);
            if (!head) s.Write(body, 0, body.Length);
        }

        private void SendFile(Stream s, SharedFile f, bool head)
        {
            string ascii = new string(f.Name.Select(ch => ch < 128 && ch != '"' && ch != '\\' && !char.IsControl(ch) ? ch : '_').ToArray());
            string headers = "HTTP/1.1 200 OK\r\n"
                             + "Content-Type: " + MimeType(f.Name) + "\r\n"
                             + "Content-Length: " + f.Length + "\r\n"
                             + "Content-Disposition: attachment; filename=\"" + ascii + "\"; filename*=UTF-8''" + Uri.EscapeDataString(f.Name) + "\r\n"
                             + "Cache-Control: no-store\r\n"
                             + "X-Content-Type-Options: nosniff\r\n"
                             + "Connection: close\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(headers);
            s.Write(h, 0, h.Length);
            if (head) return;

            using (FileStream fs = File.OpenRead(f.Path))
            {
                var buffer = new byte[81920];
                long sent = 0;
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (_cts.IsCancellationRequested) return;
                    s.Write(buffer, 0, read);
                    sent += read;
                    Progress?.Invoke(sent, f.Length);
                }
            }
            Completed?.Invoke(f);
        }

        private string LandingPage()
        {
            string Esc(string t) => WebUtility.HtmlEncode(t);
            var rows = new StringBuilder();
            for (int i = 0; i < _files.Count; i++)
            {
                SharedFile f = _files[i];
                rows.Append("<a class=\"f\" href=\"").Append(i).Append("\" download=\"").Append(Esc(f.Name)).Append("\">")
                    .Append("<span class=\"n\">").Append(Esc(f.Name)).Append("</span>")
                    .Append("<span class=\"s\">").Append(Esc(FormatSize(f.Length))).Append("</span>")
                    .Append("<span class=\"b\">").Append(Esc(S.LanDownload)).Append("</span></a>");
            }
            return "<!doctype html><html lang=\"" + (S.IsTurkish ? "tr" : "en") + "\"><head><meta charset=\"utf-8\">"
                   + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>QRPeek</title>"
                   + "<style>"
                   + ":root{color-scheme:light dark}body{margin:0;font:16px/1.4 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f6f7f9;color:#1b1f24}"
                   + "@media(prefers-color-scheme:dark){body{background:#1b1d21;color:#eceef1}.f{background:#25282d!important;border-color:#3a3f46!important}}"
                   + "main{max-width:520px;margin:0 auto;padding:28px 16px}h1{font-size:20px;margin:0 0 4px}p{margin:0 0 20px;opacity:.7;font-size:14px}"
                   + ".f{display:flex;flex-wrap:wrap;align-items:center;gap:4px 12px;text-decoration:none;color:inherit;background:#fff;border:1px solid #dce0e6;border-radius:12px;padding:14px 16px;margin-bottom:10px}"
                   + ".n{flex:1 1 100%;font-weight:600;word-break:break-all}.s{flex:1;opacity:.65;font-size:14px}"
                   + ".b{background:#2563eb;color:#fff;border-radius:8px;padding:8px 16px;font-weight:600;font-size:15px}"
                   + "</style></head><body><main><h1>QRPeek</h1><p>" + Esc(S.LanPageHint) + "</p>" + rows + "</main></body></html>";
        }

        public static string FormatSize(long bytes)
        {
            System.Globalization.CultureInfo ci = S.IsTurkish ? new System.Globalization.CultureInfo("tr-TR") : System.Globalization.CultureInfo.InvariantCulture;
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#", ci) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1048576.0).ToString("0.#", ci) + " MB";
            return (bytes / 1073741824.0).ToString("0.##", ci) + " GB";
        }

        private static string MimeType(string name)
        {
            switch (System.IO.Path.GetExtension(name).ToLowerInvariant())
            {
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".heic": return "image/heic";
                case ".pdf": return "application/pdf";
                case ".txt": return "text/plain; charset=utf-8";
                case ".mp4": return "video/mp4";
                case ".mov": return "video/quicktime";
                case ".mp3": return "audio/mpeg";
                case ".zip": return "application/zip";
                case ".apk": return "application/vnd.android.package-archive";
                default: return "application/octet-stream";
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _cts.Cancel();
            try { _listener.Stop(); } catch (SocketException) { }
            foreach (SharedFile f in _files.Where(x => x.DeleteAfter))
            {
                try { File.Delete(f.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            _cts.Dispose();
        }
    }
}
