// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QRPeek.Core
{
    internal sealed class ReceiveLimits
    {
        public int MaxFiles { get; set; } = 100;
        public long MaxTotalBytes { get; set; } = 4L * 1024 * 1024 * 1024;
        /// <summary>The server closes after this long without any activity.</summary>
        public TimeSpan IdleLifetime { get; set; } = TimeSpan.FromMinutes(10);
        /// <summary>Kept free on the drive on top of the offered files.</summary>
        public long FreeSpaceMargin { get; set; } = 64L * 1024 * 1024;
    }

    /// <summary>The one device that holds the session: the first to connect.</summary>
    internal sealed class ReceiveDevice
    {
        public ReceiveDevice(string label, string address, string code, string key)
        {
            Label = label;
            Address = address;
            Code = code;
            Key = key;
        }

        /// <summary>From the browser's User-Agent, e.g. "iPhone · Safari". Not proof of anything.</summary>
        public string Label { get; }
        public string Address { get; }
        /// <summary>Four digits shown on both screens, so the user can tell it's their phone.</summary>
        public string Code { get; }
        internal string Key { get; }
    }

    internal enum OfferState { Pending, Accepted, Rejected, Done, Failed }

    internal sealed class OfferedFile
    {
        public OfferedFile(string name, long size)
        {
            Name = name;
            Size = size;
            Risky = ReceivedFiles.IsRisky(name);
        }

        /// <summary>Already cleaned up with <see cref="ReceivedFiles.SafeName"/>.</summary>
        public string Name { get; }
        public long Size { get; }
        public bool Risky { get; }
        public string SavedPath { get; internal set; }
        internal bool Busy { get; set; }
    }

    /// <summary>A list of files the phone wants to send. Nothing is written until the user accepts it.</summary>
    internal sealed class ReceiveOffer
    {
        internal long ReceivedBytesField;

        public ReceiveOffer(string id, IReadOnlyList<OfferedFile> files, bool enoughSpace)
        {
            Id = id;
            Files = files;
            TotalBytes = files.Sum(f => f.Size);
            EnoughSpace = enoughSpace;
        }

        public string Id { get; }
        public IReadOnlyList<OfferedFile> Files { get; }
        public long TotalBytes { get; }
        public bool HasRiskyFiles => Files.Any(f => f.Risky);
        public bool EnoughSpace { get; }
        public OfferState State { get; internal set; }
        public long ReceivedBytes => Interlocked.Read(ref ReceivedBytesField);
        public IReadOnlyList<string> SavedPaths => Files.Where(f => f.SavedPath != null).Select(f => f.SavedPath).ToList();
    }

    /// <summary>
    /// Phone → PC. A one-off web server on the local network that accepts
    /// files, but only on these terms:
    /// <list type="bullet">
    /// <item>It exists only while the "Receive from phone" window is open, behind
    /// a 128-bit secret in the URL, and closes after <see cref="ReceiveLimits.IdleLifetime"/> idle.</item>
    /// <item>The first device to connect holds the session (a second key-less
    /// device gets 409); every later request must carry that device's key.</item>
    /// <item>The phone first sends only a list (names and sizes). Not a byte is
    /// written to disk until the user accepts that exact list on the PC.</item>
    /// <item>Each upload must be exactly the size it announced; files go into
    /// <see cref="Folder"/> only, under a cleaned-up name, never overwriting,
    /// and marked as downloaded from the internet. Nothing is opened.</item>
    /// <item>No directory listing, no reading files back, no paths from the client.</item>
    /// </list>
    /// Plain HTTP on the LAN, like <see cref="LanShare"/>: meant for a trusted home or office network.
    /// </summary>
    internal sealed class LanReceive : IDisposable
    {
        private const int MaxOfferBytes = 64 * 1024;
        public const string KeyHeader = "X-QRPeek-Key";

        private readonly object _gate = new object();
        private readonly string _token;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly HashSet<string> _parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ReceiveDevice _device;
        private ReceiveOffer _offer;
        private long _expiresTicks;
        private int _disposed;

        /// <summary>Raised on a worker thread when the first device connects.</summary>
        public event Action<ReceiveDevice> DeviceConnected;

        /// <summary>Raised on a worker thread: the phone wants to send these files. Call <see cref="Accept"/> or <see cref="Reject"/>.</summary>
        public event Action<ReceiveOffer> OfferReceived;

        /// <summary>Raised on a worker thread while files arrive (read <see cref="ReceiveOffer.ReceivedBytes"/>).</summary>
        public event Action<ReceiveOffer> Progress;

        /// <summary>Raised on a worker thread when an accepted offer is <see cref="OfferState.Done"/> or <see cref="OfferState.Failed"/>.</summary>
        public event Action<ReceiveOffer> OfferFinished;

        public LanReceive(string folder, IPAddress address, ReceiveLimits limits = null)
        {
            Folder = folder;
            Limits = limits ?? new ReceiveLimits();
            _token = LanHttp.NewToken();
            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = "http://" + address + ":" + port + "/" + _token + "/";
            Touch();
            Task.Run(AcceptLoop);
        }

        public string Url { get; }

        /// <summary>Where files are saved. Can change between lists; a running upload keeps the folder it started in.</summary>
        public string Folder { get; set; }

        public ReceiveLimits Limits { get; }

        /// <summary>When the session closes unless something happens; moves forward with every action.</summary>
        public DateTime Expires => new DateTime(Interlocked.Read(ref _expiresTicks), DateTimeKind.Utc);

        /// <summary>Files are arriving right now (the session doesn't expire meanwhile).</summary>
        public bool Transferring
        {
            get { lock (_gate) return _offer != null && _offer.State == OfferState.Accepted; }
        }

        public ReceiveDevice Device
        {
            get { lock (_gate) return _device; }
        }

        /// <summary>
        /// The user said yes to this list. False if it's no longer the current
        /// pending offer or the drive doesn't have room for it.
        /// </summary>
        public bool Accept(ReceiveOffer offer)
        {
            lock (_gate)
            {
                if (offer == null || offer != _offer || offer.State != OfferState.Pending) return false;
                if (!HasSpace(offer.TotalBytes)) return false;
                offer.State = OfferState.Accepted;
            }
            Touch();
            return true;
        }

        public void Reject(ReceiveOffer offer)
        {
            lock (_gate)
            {
                if (offer != null && offer == _offer && offer.State == OfferState.Pending) offer.State = OfferState.Rejected;
            }
        }

        private void Touch() => Interlocked.Exchange(ref _expiresTicks, (DateTime.UtcNow + Limits.IdleLifetime).Ticks);

        private bool HasSpace(long bytes) => ReceivedFiles.FreeSpace(Folder) - Limits.FreeSpaceMargin >= bytes;

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
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    NetworkStream stream = client.GetStream();
                    string head = LanHttp.ReadHeaders(stream);
                    if (head == null) return;
                    LanRequest req = LanRequest.Parse(head);
                    if (req == null) return;

                    string prefix = "/" + _token;
                    if (req.Path != prefix && !req.Path.StartsWith(prefix + "/", StringComparison.Ordinal))
                    {
                        LanHttp.SendText(stream, 404, "Not found");
                        return;
                    }
                    if (req.Method != "GET" && req.Method != "POST" && req.Method != "PUT")
                    {
                        LanHttp.SendText(stream, 405, "Method not allowed");
                        return;
                    }
                    if (DateTime.UtcNow > Expires && !Transferring)
                    {
                        LanHttp.SendJson(stream, 410, "{\"error\":\"closed\"}");
                        return;
                    }
                    if (req.Header("Transfer-Encoding") != null)
                    {
                        LanHttp.SendJson(stream, 411, "{\"error\":\"length\"}");
                        return;
                    }
                    if (string.Equals(req.Header("Expect"), "100-continue", StringComparison.OrdinalIgnoreCase))
                    {
                        // Some clients (curl, .NET) wait for this before sending a body. Browsers don't ask.
                        byte[] cont = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                        stream.Write(cont, 0, cont.Length);
                    }

                    string route = req.Path.Substring(prefix.Length).Trim('/');
                    string remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
                    switch (route)
                    {
                        case "" when req.Method == "GET":
                            LanHttp.Send(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(ReceivePage.Build()),
                                extraHeaders: "Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'none'\r\n");
                            return;
                        case "hello" when req.Method == "POST":
                            Hello(req, stream, remote);
                            return;
                        case "offer" when req.Method == "POST":
                            Offer(req, stream);
                            return;
                        case "status" when req.Method == "GET":
                            Status(req, stream);
                            return;
                        case "file" when req.Method == "PUT":
                            Upload(req, stream);
                            return;
                        case "":
                        case "hello":
                        case "offer":
                        case "status":
                        case "file":
                            LanHttp.SendText(stream, 405, "Method not allowed");
                            return;
                        default:
                            LanHttp.SendText(stream, 404, "Not found");
                            return;
                    }
                }
                catch (IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                finally
                {
                    CloseGracefully(client);
                }
            }
        }

        /// <summary>
        /// After an early answer (403, 409…) the request body may still be
        /// unread. Closing then makes Windows reset the connection, and the
        /// phone sees "connection reset" instead of the answer. So: say we're
        /// done sending, then read and discard what's left (up to 1 MB, 0.5 s).
        /// </summary>
        private static void CloseGracefully(TcpClient client)
        {
            try
            {
                client.Client.Shutdown(SocketShutdown.Send);
                client.Client.ReceiveTimeout = 500;
                var buf = new byte[16384];
                long total = 0;
                int n;
                while (total < 1024 * 1024 && (n = client.Client.Receive(buf)) > 0) total += n;
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        private bool Authorized(LanRequest req)
        {
            string key = req.Header(KeyHeader);
            lock (_gate) return _device != null && key != null && string.Equals(key, _device.Key, StringComparison.Ordinal);
        }

        private void Hello(LanRequest req, Stream stream, string remote)
        {
            string key = req.Header(KeyHeader);
            ReceiveDevice device;
            bool isNew = false;
            lock (_gate)
            {
                if (_device == null)
                {
                    _device = new ReceiveDevice(DeviceLabel(req.Header("User-Agent")), remote,
                        LanHttp.RandomInt(10000).ToString("D4", CultureInfo.InvariantCulture), LanHttp.NewToken());
                    isNew = true;
                }
                else if (key == null || !string.Equals(key, _device.Key, StringComparison.Ordinal))
                {
                    LanHttp.SendJson(stream, 409, "{\"error\":\"busy\"}");
                    return;
                }
                device = _device;
            }
            Touch();
            if (isNew) DeviceConnected?.Invoke(device);
            LanHttp.SendJson(stream, 200, "{\"key\":" + LanHttp.JsonString(device.Key) + ",\"code\":" + LanHttp.JsonString(device.Code) + "}");
        }

        private void Offer(LanRequest req, Stream stream)
        {
            if (!Authorized(req))
            {
                LanHttp.SendJson(stream, 403, "{\"error\":\"key\"}");
                return;
            }
            long len = req.ContentLength;
            if (len < 0)
            {
                LanHttp.SendJson(stream, 411, "{\"error\":\"length\"}");
                return;
            }
            if (len > MaxOfferBytes)
            {
                LanHttp.SendJson(stream, 413, "{\"error\":\"limit\"}");
                return;
            }
            byte[] body = LanHttp.ReadBody(stream, (int)len);
            if (body == null) return;

            // One file per line: "<size> <URI-encoded name>".
            var files = new List<OfferedFile>();
            long total = 0;
            foreach (string raw in Encoding.UTF8.GetString(body).Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                int sp = line.IndexOf(' ');
                if (sp <= 0 || !long.TryParse(line.Substring(0, sp), NumberStyles.None, CultureInfo.InvariantCulture, out long size))
                {
                    LanHttp.SendJson(stream, 400, "{\"error\":\"format\"}");
                    return;
                }
                string name;
                try { name = Uri.UnescapeDataString(line.Substring(sp + 1)); }
                catch (UriFormatException)
                {
                    LanHttp.SendJson(stream, 400, "{\"error\":\"format\"}");
                    return;
                }
                if (files.Count >= Limits.MaxFiles || size > Limits.MaxTotalBytes || (total += size) > Limits.MaxTotalBytes)
                {
                    LanHttp.SendJson(stream, 413, "{\"error\":\"limit\"}");
                    return;
                }
                files.Add(new OfferedFile(ReceivedFiles.SafeName(name), size));
            }
            if (files.Count == 0)
            {
                LanHttp.SendJson(stream, 400, "{\"error\":\"empty\"}");
                return;
            }

            ReceiveOffer offer;
            lock (_gate)
            {
                if (_offer != null && (_offer.State == OfferState.Pending || _offer.State == OfferState.Accepted))
                {
                    LanHttp.SendJson(stream, 409, "{\"error\":\"pending\"}");
                    return;
                }
                offer = new ReceiveOffer(LanHttp.NewToken(), files, HasSpace(total));
                _offer = offer;
            }
            Touch();
            OfferReceived?.Invoke(offer);
            LanHttp.SendJson(stream, 200, "{\"offer\":" + LanHttp.JsonString(offer.Id) + "}");
        }

        private void Status(LanRequest req, Stream stream)
        {
            if (!Authorized(req))
            {
                LanHttp.SendJson(stream, 403, "{\"error\":\"key\"}");
                return;
            }
            req.Query.TryGetValue("offer", out string id);
            ReceiveOffer offer;
            lock (_gate) offer = _offer;
            if (offer == null || offer.Id != id)
            {
                LanHttp.SendJson(stream, 404, "{\"error\":\"offer\"}");
                return;
            }
            LanHttp.SendJson(stream, 200, "{\"state\":\"" + offer.State.ToString().ToLowerInvariant() + "\"}");
        }

        private void Upload(LanRequest req, Stream stream)
        {
            if (!Authorized(req))
            {
                LanHttp.SendJson(stream, 403, "{\"error\":\"key\"}");
                return;
            }
            req.Query.TryGetValue("offer", out string id);
            req.Query.TryGetValue("i", out string idx);

            ReceiveOffer offer;
            OfferedFile file;
            lock (_gate)
            {
                offer = _offer;
                if (offer == null || offer.Id != id || offer.State != OfferState.Accepted)
                {
                    LanHttp.SendJson(stream, 403, "{\"error\":\"not-accepted\"}");
                    return;
                }
                if (!int.TryParse(idx, NumberStyles.None, CultureInfo.InvariantCulture, out int i) || i >= offer.Files.Count)
                {
                    LanHttp.SendJson(stream, 400, "{\"error\":\"index\"}");
                    return;
                }
                file = offer.Files[i];
                if (file.SavedPath != null || file.Busy)
                {
                    LanHttp.SendJson(stream, 409, "{\"error\":\"again\"}");
                    return;
                }
                if (req.ContentLength != file.Size)
                {
                    // Bigger (or smaller) than announced: refuse before writing anything.
                    LanHttp.SendJson(stream, 400, "{\"error\":\"size\"}");
                    return;
                }
                file.Busy = true;
            }

            string part = null;
            try
            {
                string folder = Folder;
                part = ReceivedFiles.NewPartPath(folder);
                lock (_gate) _parts.Add(part);
                using (var fs = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920))
                {
                    var buf = new byte[81920];
                    long left = file.Size;
                    while (left > 0)
                    {
                        if (_cts.IsCancellationRequested) throw new OperationCanceledException();
                        int n = stream.Read(buf, 0, (int)Math.Min(buf.Length, left));
                        if (n <= 0) throw new IOException("The phone stopped sending.");
                        fs.Write(buf, 0, n);
                        left -= n;
                        Interlocked.Add(ref offer.ReceivedBytesField, n);
                        Touch();
                        Progress?.Invoke(offer);
                    }
                }

                string saved = ReceivedFiles.Finish(part, folder, file.Name);
                lock (_gate) _parts.Remove(part);
                part = null;
                try { ReceivedFiles.MarkFromInternet(saved); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                bool done;
                lock (_gate)
                {
                    file.SavedPath = saved;
                    file.Busy = false;
                    done = offer.Files.All(f => f.SavedPath != null);
                    if (done) offer.State = OfferState.Done;
                }
                LanHttp.SendJson(stream, 200, "{\"ok\":true}");
                if (done) OfferFinished?.Invoke(offer);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is OperationCanceledException
                                       || ex is SocketException || ex is ObjectDisposedException)
            {
                if (ex is UnauthorizedAccessException) Storage.LogError(ex);
                bool failed;
                lock (_gate)
                {
                    file.Busy = false;
                    if (part != null) _parts.Remove(part);
                    failed = offer.State == OfferState.Accepted;
                    if (failed) offer.State = OfferState.Failed;
                }
                ReceivedFiles.TryDelete(part);
                if (failed) OfferFinished?.Invoke(offer);
                try { LanHttp.SendJson(stream, 400, "{\"error\":\"failed\"}"); }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>"iPhone · Safari", "Android · Chrome"… from a User-Agent. Display only; anyone can send any User-Agent.</summary>
        internal static string DeviceLabel(string ua)
        {
            ua = ua ?? "";
            string os =
                ua.Contains("iPhone") ? "iPhone" :
                ua.Contains("iPad") ? "iPad" :
                ua.Contains("Android") ? "Android" :
                ua.Contains("Macintosh") ? "iPad / Mac" : // iPadOS Safari reports itself as a Mac
                ua.Contains("Windows") ? "Windows" :
                ua.Contains("CrOS") ? "ChromeOS" :
                ua.Contains("Linux") ? "Linux" : S.UnknownDevice;
            string browser =
                ua.Contains("SamsungBrowser") ? "Samsung Internet" :
                ua.Contains("EdgA/") || ua.Contains("EdgiOS/") || ua.Contains("Edg/") ? "Edge" :
                ua.Contains("OPR/") || ua.Contains("OPT/") ? "Opera" :
                ua.Contains("FxiOS/") || ua.Contains("Firefox/") ? "Firefox" :
                ua.Contains("CriOS/") || ua.Contains("Chrome/") ? "Chrome" :
                ua.Contains("Safari/") ? "Safari" : null;
            return browser == null ? os : os + " · " + browser;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _cts.Cancel();
            try { _listener.Stop(); } catch (SocketException) { }
            string[] leftovers;
            lock (_gate) leftovers = _parts.ToArray();
            foreach (string p in leftovers) ReceivedFiles.TryDelete(p); // an upload still running deletes its own
        }
    }
}
