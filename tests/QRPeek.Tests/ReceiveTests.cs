// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using QRPeek;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class ReceivedFileNameTests
    {
        [Theory]
        [InlineData("foto.jpg", "foto.jpg")]
        [InlineData("Rapor ç ş.pdf", "Rapor ç ş.pdf")]
        [InlineData(@"..\..\Windows\System32\evil.exe", "evil.exe")]
        [InlineData("../../etc/passwd", "passwd")]
        [InlineData("C:\\Users\\x\\a.txt", "a.txt")]
        [InlineData("a:b.txt", "a_b.txt")] // ':' would write a hidden NTFS stream
        [InlineData("a<b>c|d?*.txt", "a_b_c_d__.txt")]
        [InlineData("CON.txt", "_CON.txt")]
        [InlineData("nul", "_nul")]
        [InlineData("com1.jpg", "_com1.jpg")]
        [InlineData("LPT9.tar.gz", "_LPT9.tar.gz")]
        [InlineData("report\u202Egpj.exe", "reportgpj.exe")] // would display as "reportexe.jpg"
        [InlineData("x\u0000y\u001F.txt", "x_y_.txt")]
        [InlineData("name. . .", "name")]
        [InlineData("..", "file")]
        [InlineData("", "file")]
        [InlineData(null, "file")]
        [InlineData("  spaced.txt  ", "spaced.txt")]
        public void Keeps_only_a_safe_file_name(string raw, string expected)
        {
            Assert.Equal(expected, ReceivedFiles.SafeName(raw));
        }

        [Fact]
        public void Shortens_long_names_but_keeps_the_extension()
        {
            string name = ReceivedFiles.SafeName(new string('a', 300) + ".pdf");
            Assert.Equal(ReceivedFiles.MaxNameLength, name.Length);
            Assert.EndsWith(".pdf", name);
        }

        [Theory]
        [InlineData("setup.exe", true)]
        [InlineData("run.PS1", true)]
        [InlineData("link.lnk", true)]
        [InlineData("disk.iso", true)]
        [InlineData("photo.jpg", false)]
        [InlineData("notes.txt", false)]
        [InlineData("noext", false)]
        public void Flags_files_that_run_code(string name, bool risky)
        {
            Assert.Equal(risky, ReceivedFiles.IsRisky(name));
        }

        [Fact]
        public void Never_overwrites_an_existing_file()
        {
            string dir = ReceiveTests.TempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "a.txt"), "old");
                File.WriteAllText(Path.Combine(dir, "a (1).txt"), "older");
                string part = ReceivedFiles.NewPartPath(dir);
                File.WriteAllText(part, "new");

                string saved = ReceivedFiles.Finish(part, dir, "a.txt");

                Assert.Equal(Path.Combine(dir, "a (2).txt"), saved);
                Assert.Equal("old", File.ReadAllText(Path.Combine(dir, "a.txt")));
                Assert.Equal("older", File.ReadAllText(Path.Combine(dir, "a (1).txt")));
                Assert.Equal("new", File.ReadAllText(saved));
                Assert.False(File.Exists(part));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Theory]
        [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "iPhone · Safari")]
        [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36", "Android · Chrome")]
        [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S921B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0.0.0 Mobile Safari/537.36", "Android · Samsung Internet")]
        [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1", "iPhone · Chrome")]
        [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "iPad / Mac · Safari")]
        [InlineData(null, "Unknown device")]
        public void Describes_the_device_from_its_browser(string ua, string expected)
        {
            S.Init("en");
            Assert.Equal(expected, LanReceive.DeviceLabel(ua));
        }
    }

    public class ReceiveTests
    {
        public ReceiveTests() => S.Init("en");

        internal static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "qrpeek-recv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static int Call(string method, string url, string key, byte[] body, out string text, out WebHeaderCollection headers)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = 5000;
            req.KeepAlive = false;
            if (key != null) req.Headers[LanReceive.KeyHeader] = key;
            if (body != null)
            {
                req.ContentLength = body.Length;
                using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            }
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    text = r.ReadToEnd();
                    headers = resp.Headers;
                    return (int)resp.StatusCode;
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse resp)
            {
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) text = r.ReadToEnd();
                headers = resp.Headers;
                return (int)resp.StatusCode;
            }
        }

        private static int Call(string method, string url, string key = null, byte[] body = null) => Call(method, url, key, body, out _, out _);

        private static int Call(string method, string url, string key, byte[] body, out string text) => Call(method, url, key, body, out text, out _);

        /// <summary>Sends hand-written request bytes (for things HttpWebRequest won't do) and returns the status, or 0 if the server just closed.</summary>
        private static int Raw(string url, string request, byte[] body = null, bool halfClose = true)
        {
            var uri = new Uri(url);
            using (var c = new TcpClient(uri.Host, uri.Port))
            {
                c.ReceiveTimeout = 5000;
                NetworkStream s = c.GetStream();
                byte[] h = Encoding.ASCII.GetBytes(request);
                s.Write(h, 0, h.Length);
                if (body != null) s.Write(body, 0, body.Length);
                if (halfClose) c.Client.Shutdown(SocketShutdown.Send);
                try
                {
                    var buf = new byte[512];
                    int n = s.Read(buf, 0, buf.Length);
                    if (n <= 0) return 0;
                    Match m = Regex.Match(Encoding.ASCII.GetString(buf, 0, n), @"^HTTP/1\.1 (\d{3})");
                    return m.Success ? int.Parse(m.Groups[1].Value) : 0;
                }
                catch (IOException) { return 0; }
            }
        }

        private static string Json(string json, string name) => Regex.Match(json, "\"" + name + "\":\"([^\"]*)\"").Groups[1].Value;

        private static byte[] OfferBody(params (string name, long size)[] files) =>
            Encoding.UTF8.GetBytes(string.Join("\n", files.Select(f => f.size + " " + Uri.EscapeDataString(f.name))));

        private static string Hello(LanReceive r)
        {
            Assert.Equal(200, Call("POST", r.Url + "hello", null, new byte[0], out string json));
            return Json(json, "key");
        }

        private static string Offer(LanReceive r, string key, params (string name, long size)[] files)
        {
            Assert.Equal(200, Call("POST", r.Url + "offer", key, OfferBody(files), out string json));
            return Json(json, "offer");
        }

        private static string[] FilesIn(string dir) => Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        [Fact]
        public void Receives_only_what_the_user_approved()
        {
            string dir = TempDir();
            try
            {
                using (var r = new LanReceive(dir, IPAddress.Loopback))
                {
                    ReceiveDevice connected = null;
                    ReceiveOffer offered = null;
                    r.DeviceConnected += d => connected = d;
                    r.OfferReceived += o => offered = o;
                    Assert.Matches(@"^http://127\.0\.0\.1:\d+/[A-Za-z0-9_-]{22}/$", r.Url);

                    // The page is self-contained and locked down.
                    Assert.Equal(200, Call("GET", r.Url, null, null, out string page, out WebHeaderCollection h));
                    Assert.Contains("QRPeek", page);
                    Assert.Contains("default-src 'none'", h["Content-Security-Policy"]);

                    // The first device gets a key and a 4-digit code; the PC is told.
                    Assert.Equal(200, Call("POST", r.Url + "hello", null, new byte[0], out string hello));
                    string key = Json(hello, "key");
                    Assert.Matches("^[A-Za-z0-9_-]{22}$", key);
                    Assert.Matches("^[0-9]{4}$", Json(hello, "code"));
                    Assert.NotNull(connected);
                    Assert.Equal(Json(hello, "code"), connected.Code);
                    Assert.Equal("127.0.0.1", connected.Address);
                    Assert.Equal(200, Call("POST", r.Url + "hello", key, new byte[0])); // same device again (page reload) is fine

                    // The phone announces a list; nothing is written yet.
                    byte[] content = new byte[200_000];
                    new Random(7).NextBytes(content);
                    string id = Offer(r, key, (@"..\..\evil\Tatil ç.jpg", content.Length));
                    Assert.NotNull(offered);
                    Assert.Equal("Tatil ç.jpg", offered.Files[0].Name);
                    Assert.Equal(OfferState.Pending, offered.State);
                    Assert.True(offered.EnoughSpace);
                    Assert.Equal(200, Call("GET", r.Url + "status?offer=" + id, key, null, out string st));
                    Assert.Equal("pending", Json(st, "state"));

                    // Uploading before the user approves is refused, and nothing lands on disk.
                    Assert.Equal(403, Call("PUT", r.Url + "file?offer=" + id + "&i=0", key, content));
                    Assert.Empty(FilesIn(dir));

                    Assert.True(r.Accept(offered));
                    Assert.False(r.Accept(offered)); // only once

                    // A body that doesn't match the announced size is refused before writing.
                    Assert.Equal(400, Call("PUT", r.Url + "file?offer=" + id + "&i=0", key, new byte[content.Length + 1]));
                    Assert.Equal(400, Call("PUT", r.Url + "file?offer=" + id + "&i=5", key, content));
                    Assert.Empty(FilesIn(dir));

                    Assert.Equal(200, Call("PUT", r.Url + "file?offer=" + id + "&i=0", key, content));
                    Assert.Equal(OfferState.Done, offered.State);
                    Assert.Equal(content.Length, offered.ReceivedBytes);
                    Assert.Equal(new[] { "Tatil ç.jpg" }, FilesIn(dir)); // no folders, no leftover part files
                    string saved = Path.Combine(dir, "Tatil ç.jpg");
                    Assert.Equal(content, File.ReadAllBytes(saved));
                    Assert.Contains("ZoneId=3", ReceivedFiles.ReadZoneMark(saved));
                    Assert.Equal(new[] { saved }, offered.SavedPaths);

                    Assert.Equal(200, Call("GET", r.Url + "status?offer=" + id, key, null, out st));
                    Assert.Equal("done", Json(st, "state"));
                    Assert.Equal(403, Call("PUT", r.Url + "file?offer=" + id + "&i=0", key, content)); // a finished list takes nothing more

                    // The same name again is kept next to the first, never over it.
                    ReceiveOffer first = offered;
                    string id2 = Offer(r, key, ("Tatil ç.jpg", 3));
                    Assert.NotSame(first, offered);
                    Assert.False(r.Accept(first)); // an old offer can't be approved again
                    Assert.True(r.Accept(offered));
                    Assert.Equal(200, Call("PUT", r.Url + "file?offer=" + id2 + "&i=0", key, new byte[] { 1, 2, 3 }));
                    Assert.Equal(new[] { "Tatil ç (1).jpg", "Tatil ç.jpg" }, FilesIn(dir));
                    Assert.Equal(content, File.ReadAllBytes(saved));
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void A_second_device_cannot_join_or_act()
        {
            string dir = TempDir();
            try
            {
                using (var r = new LanReceive(dir, IPAddress.Loopback))
                {
                    int connects = 0;
                    r.DeviceConnected += d => connects++;
                    string key = Hello(r);

                    Assert.Equal(409, Call("POST", r.Url + "hello", null, new byte[0]));
                    Assert.Equal(409, Call("POST", r.Url + "hello", "made-up-key", new byte[0]));
                    Assert.Equal(1, connects);

                    Assert.Equal(403, Call("POST", r.Url + "offer", null, OfferBody(("a.txt", 1))));
                    Assert.Equal(403, Call("POST", r.Url + "offer", "made-up-key", OfferBody(("a.txt", 1))));

                    string id = Offer(r, key, ("a.txt", 1));
                    Assert.Equal(403, Call("GET", r.Url + "status?offer=" + id, null, null));
                    Assert.Equal(403, Call("PUT", r.Url + "file?offer=" + id + "&i=0", "made-up-key", new byte[1]));
                }
                Assert.Empty(FilesIn(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void A_rejected_list_cannot_be_uploaded()
        {
            string dir = TempDir();
            try
            {
                using (var r = new LanReceive(dir, IPAddress.Loopback))
                {
                    ReceiveOffer offered = null;
                    r.OfferReceived += o => offered = o;
                    string key = Hello(r);
                    string id = Offer(r, key, ("a.txt", 2));

                    r.Reject(offered);
                    Assert.False(r.Accept(offered));
                    Assert.Equal(403, Call("PUT", r.Url + "file?offer=" + id + "&i=0", key, new byte[2]));
                    Assert.Equal(200, Call("GET", r.Url + "status?offer=" + id, key, null, out string st));
                    Assert.Equal("rejected", Json(st, "state"));

                    // While a list waits for an answer, the phone can't stack up another one.
                    Offer(r, key, ("b.txt", 1));
                    Assert.Equal(409, Call("POST", r.Url + "offer", key, OfferBody(("c.txt", 1))));
                }
                Assert.Empty(FilesIn(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Enforces_the_file_count_and_size_limits()
        {
            string dir = TempDir();
            try
            {
                var limits = new ReceiveLimits { MaxFiles = 2, MaxTotalBytes = 10 };
                using (var r = new LanReceive(dir, IPAddress.Loopback, limits))
                {
                    string key = Hello(r);
                    Assert.Equal(413, Call("POST", r.Url + "offer", key, OfferBody(("a", 1), ("b", 1), ("c", 1))));
                    Assert.Equal(413, Call("POST", r.Url + "offer", key, OfferBody(("a", 6), ("b", 5))));
                    Assert.Equal(413, Call("POST", r.Url + "offer", key, OfferBody(("a", long.MaxValue), ("b", 1))));
                    Assert.Equal(400, Call("POST", r.Url + "offer", key, Encoding.UTF8.GetBytes("-5 a.txt")));
                    Assert.Equal(400, Call("POST", r.Url + "offer", key, new byte[0]));
                    Assert.Equal(413, Call("POST", r.Url + "offer", key, new byte[70_000]));
                    Offer(r, key, ("a", 5), ("b", 5));
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Refuses_anything_off_the_beaten_path()
        {
            string dir = TempDir();
            try
            {
                using (var r = new LanReceive(dir, IPAddress.Loopback))
                {
                    string root = r.Url.Substring(0, r.Url.IndexOf('/', 7) + 1);
                    Assert.Equal(404, Call("GET", root));
                    Assert.Equal(404, Call("GET", root + "guess/"));
                    Assert.Equal(404, Call("POST", root + "guess/hello", null, new byte[0]));
                    Assert.Equal(404, Call("GET", r.Url + "../"));
                    Assert.Equal(404, Call("GET", r.Url + "files"));
                    Assert.Equal(405, Call("DELETE", r.Url + "file"));
                    Assert.Equal(405, Call("GET", r.Url + "file"));
                    Assert.Equal(405, Call("POST", r.Url, null, new byte[0]));

                    string key = Hello(r);
                    string path = new Uri(r.Url).AbsolutePath;
                    Assert.Equal(411, Raw(r.Url, "POST " + path + "offer HTTP/1.1\r\nHost: x\r\n" + LanReceive.KeyHeader + ": " + key +
                                                  "\r\nTransfer-Encoding: chunked\r\n\r\n", Encoding.ASCII.GetBytes("5\r\nhello\r\n0\r\n\r\n")));
                    Assert.Equal(411, Raw(r.Url, "POST " + path + "offer HTTP/1.1\r\nHost: x\r\n" + LanReceive.KeyHeader + ": " + key + "\r\n\r\n"));
                    Assert.Equal(0, Raw(r.Url, "POST " + path + "hello HTTP/1.1\r\nHost: x\r\nX-Pad: " + new string('a', 9000) + "\r\n\r\n"));
                }
                Assert.Empty(FilesIn(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void An_interrupted_upload_leaves_nothing_behind()
        {
            string dir = TempDir();
            try
            {
                using (var r = new LanReceive(dir, IPAddress.Loopback))
                {
                    ReceiveOffer offered = null, finished = null;
                    r.OfferReceived += o => offered = o;
                    r.OfferFinished += o => finished = o;
                    string key = Hello(r);
                    string id = Offer(r, key, ("big.bin", 100_000));
                    Assert.True(r.Accept(offered));

                    string path = new Uri(r.Url).AbsolutePath;
                    Raw(r.Url, "PUT " + path + "file?offer=" + id + "&i=0 HTTP/1.1\r\nHost: x\r\n" + LanReceive.KeyHeader + ": " + key +
                               "\r\nContent-Length: 100000\r\n\r\n", new byte[1000]);

                    for (int i = 0; i < 100 && finished == null; i++) Thread.Sleep(20);
                    Assert.Same(offered, finished);
                    Assert.Equal(OfferState.Failed, offered.State);
                    Assert.Empty(Directory.GetFiles(dir, "*", SearchOption.AllDirectories)); // the part file is gone too

                    // The phone can try again with a new list.
                    Offer(r, key, ("big.bin", 3));
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Closes_when_idle_and_when_disposed()
        {
            string dir = TempDir();
            try
            {
                var r = new LanReceive(dir, IPAddress.Loopback, new ReceiveLimits { IdleLifetime = TimeSpan.FromMilliseconds(300) });
                Assert.Equal(200, Call("GET", r.Url));
                Thread.Sleep(500);
                Assert.Equal(410, Call("GET", r.Url));
                Assert.Equal(410, Call("POST", r.Url + "hello", null, new byte[0]));

                int port = new Uri(r.Url).Port;
                r.Dispose();
                // The port is free again, i.e. nothing is listening any more.
                var probe = new TcpListener(IPAddress.Any, port);
                probe.Start();
                probe.Stop();
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Refuses_a_list_when_the_disk_is_too_full()
        {
            string dir = TempDir();
            try
            {
                var limits = new ReceiveLimits { FreeSpaceMargin = long.MaxValue / 2 };
                using (var r = new LanReceive(dir, IPAddress.Loopback, limits))
                {
                    ReceiveOffer offered = null;
                    r.OfferReceived += o => offered = o;
                    string key = Hello(r);
                    Offer(r, key, ("a.txt", 1));
                    Assert.False(offered.EnoughSpace);
                    Assert.False(r.Accept(offered));
                    Assert.Equal(OfferState.Pending, offered.State);
                }
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
