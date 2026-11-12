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
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class HistoryPrivacyTests
    {
        public HistoryPrivacyTests() => S.Init("en");

        [Fact]
        public void Two_factor_codes_are_never_stored()
        {
            Assert.Null(QrContent.ForHistory("otpauth://totp/GitHub:ada?secret=JBSWY3DPEHPK3PXP&issuer=GitHub"));
        }

        [Fact]
        public void Wifi_codes_are_stored_without_the_password()
        {
            string stored = QrContent.ForHistory("WIFI:T:WPA;S:Ev Ağı;P:gizli-sifre;H:true;;");
            Assert.DoesNotContain("gizli-sifre", stored);
            WifiInfo w = QrContent.Parse(stored).Wifi;
            Assert.Equal("Ev Ağı", w.Ssid);
            Assert.Equal("WPA", w.Security);
            Assert.True(w.Hidden);
            Assert.True(w.PasswordMissing);
            Assert.False(w.IsOpen);
        }

        [Fact]
        public void Open_wifi_and_plain_text_are_stored_as_is()
        {
            Assert.Equal("WIFI:T:nopass;S:Cafe;;", QrContent.ForHistory("WIFI:T:nopass;S:Cafe;;"));
            Assert.Equal("hello", QrContent.ForHistory("hello"));
        }

        [Theory]
        [InlineData("WIFI:S:Cafe;;", true)]
        [InlineData("WIFI:S:Home;P:secret;;", false)]
        public void Missing_type_is_inferred_from_the_password(string raw, bool open)
        {
            Assert.Equal(open, QrContent.Parse(raw).Wifi.IsOpen);
        }

        [Fact]
        public void Secrets_are_marked_sensitive()
        {
            Assert.True(QrContent.Parse("otpauth://totp/x?secret=ABC").IsSensitive);
            Assert.True(QrContent.Parse("WIFI:T:WPA;S:a;P:b;;").IsSensitive);
            Assert.False(QrContent.Parse("WIFI:T:nopass;S:a;;").IsSensitive);
            Assert.False(QrContent.Parse("https://example.com").IsSensitive);
        }
    }

    public class OtpTests
    {
        [Fact]
        public void Parses_an_authenticator_setup_code()
        {
            QrContent c = QrContent.Parse("otpauth://totp/ACME%20Co:ada%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=ACME%20Co&digits=6&period=30");
            Assert.Equal(ContentKind.Otp.ToString(), c.Kind.ToString());
            Assert.Equal("ACME Co", c.Otp.Issuer);
            Assert.Equal("ada@example.com", c.Otp.Account);
            Assert.Equal("JBSWY3DPEHPK3PXP", c.Otp.Secret);
            Assert.Null(c.OpenTarget);
        }

        [Fact]
        public void Without_a_secret_it_is_just_text()
        {
            Assert.Equal("Text", QrContent.Parse("otpauth://totp/x?issuer=y").Kind.ToString());
        }
    }

    public class InvoiceTests
    {
        private const string Sample =
            "{\"vkntckn\":\"1234567890\",\"avkntckn\":\"9876543210\",\"senaryo\":\"TEMELFATURA\",\"tip\":\"SATIS\"," +
            "\"tarih\":\"2026-11-10\",\"no\":\"ABC2026000000123\",\"ettn\":\"3f2a9c1e-0d4b-4c7a-9e21-6b8f0c2d1a55\"," +
            "\"parabirimi\":\"TRY\",\"malhizmettoplam\":\"1000.00\",\"kdvmatrah(20)\":\"800.00\",\"hesaplanankdv(20)\":\"160.00\"," +
            "\"kdvmatrah(10)\":\"200.00\",\"hesaplanankdv(10)\":20,\"vergidahil\":\"1180.00\",\"odenecek\":\"1180.00\"}";

        public InvoiceTests() => S.Init("tr");

        [Fact]
        public void Reads_a_gib_e_invoice_qr()
        {
            QrContent c = QrContent.Parse(Sample);
            Assert.Equal("Invoice", c.Kind.ToString());
            InvoiceInfo i = c.Invoice;
            Assert.Equal("ABC2026000000123", i.Number);
            Assert.Equal("2026-11-10", i.Date);
            Assert.Equal("1234567890", i.SellerId);
            Assert.Equal("9876543210", i.BuyerId);
            Assert.Equal(1000.00m, i.Subtotal);
            Assert.Equal(180.00m, i.Vat); // 160 + 20, the second one given as a number
            Assert.Equal(1180.00m, i.Payable);
        }

        [Theory]
        [InlineData("11373.33", "11373.33")]
        [InlineData("13.648,00", "13648.00")]
        [InlineData("2.274,67", "2274.67")]
        [InlineData("120,5", "120.5")]
        [InlineData("1.234.567,89", "1234567.89")]
        [InlineData("1,234,567.89", "1234567.89")]
        [InlineData("1.234.567", "1234567")]
        [InlineData("1234", "1234")]
        [InlineData("", null)]
        [InlineData("abc", null)]
        public void Reads_amounts_in_both_number_styles(string raw, string expected)
        {
            decimal? v = QrContent.ParseAmount(raw);
            Assert.Equal(expected, v?.ToString(CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Mixed_number_styles_in_one_code_like_real_gib_invoices()
        {
            // Real e-Arşiv codes mix "11373.33" and "13.648,00" in the same JSON.
            InvoiceInfo i = QrContent.Parse(
                "{\"no\":\"X2025000000001\",\"malhizmettoplam\":\"11373.33\",\"hesaplanankdv(20)\":\"2.274,67\"," +
                "\"vergidahil\":\"13.648,00\",\"odenecek\":\"13.648,00\",\"ettn\":\"a\"}").Invoice;
            Assert.Equal(11373.33m, i.Subtotal);
            Assert.Equal(2274.67m, i.Vat);
            Assert.Equal(13648.00m, i.TaxIncluded);
            Assert.Equal(13648.00m, i.Payable);
        }

        [Fact]
        public void Csv_uses_the_turkish_excel_format()
        {
            string csv = InvoiceCsv.Build(new[] { QrContent.Parse(Sample).Invoice }, new CultureInfo("tr-TR"));
            string[] lines = csv.TrimEnd().Split('\n');
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("Fatura no;Tarih;", lines[0]);
            Assert.Contains(";1000,00;180,00;1180,00;1180,00;", lines[1]);
        }

        [Fact]
        public void Csv_does_not_let_qr_content_become_an_excel_formula()
        {
            var inv = new InvoiceInfo { Number = "=HYPERLINK(\"http://evil\")" };
            string csv = InvoiceCsv.Build(new[] { inv }, new CultureInfo("tr-TR"));
            Assert.Contains("'=HYPERLINK", csv);
        }

        [Theory]
        [InlineData("{\"no\":{\"x\":1}}")]
        [InlineData("{\"no\":\"A\"")]
        [InlineData("{\"no\":\"A\"} trailing")]
        [InlineData("not json")]
        public void Malformed_json_is_not_an_invoice(string raw)
        {
            Assert.Null(FlatJson.Parse(raw));
        }

        [Fact]
        public void Json_escapes_are_decoded()
        {
            Dictionary<string, string> d = FlatJson.Parse("{\"a\":\"x\\\"y\\u00e7\",\"b\":null,\"c\":true}");
            Assert.Equal("x\"yç", d["a"]);
            Assert.Equal("", d["b"]);
            Assert.Equal("true", d["c"]);
        }
    }

    public class LanShareTests
    {
        public LanShareTests() => S.Init("en");

        private static string Get(string url, out int status, out byte[] body)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 5000;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var ms = new MemoryStream())
                {
                    resp.GetResponseStream().CopyTo(ms);
                    status = (int)resp.StatusCode;
                    body = ms.ToArray();
                    return resp.Headers["Content-Disposition"];
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r)
            {
                status = (int)r.StatusCode;
                body = new byte[0];
                return null;
            }
        }

        [Fact]
        public void Serves_the_file_only_behind_the_secret_link()
        {
            string dir = Path.Combine(Path.GetTempPath(), "qrpeek-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "Rapor ç.pdf");
            byte[] content = new byte[300_000];
            new Random(1).NextBytes(content);
            File.WriteAllBytes(path, content);

            SharedFile completed = null;
            using (var share = new LanShare(new[] { new SharedFile(path, deleteAfter: true) }, IPAddress.Loopback))
            {
                share.Completed += f => completed = f;
                Assert.Matches(@"^http://127\.0\.0\.1:\d+/[A-Za-z0-9_-]{22}/$", share.Url);

                Get(share.Url, out int s1, out byte[] page);
                Assert.Equal(200, s1);
                Assert.Contains("Rapor ç.pdf", WebUtility.HtmlDecode(Encoding.UTF8.GetString(page)));

                string disposition = Get(share.Url + "0", out int s2, out byte[] file);
                Assert.Equal(200, s2);
                Assert.Equal(content, file);
                Assert.Contains("filename*=UTF-8''Rapor%20%C3%A7.pdf", disposition);

                Get(share.Url + "1", out int s3, out _);
                Assert.Equal(404, s3);

                string wrong = share.Url.Substring(0, share.Url.LastIndexOf('/', share.Url.Length - 2) + 1) + "guess/0";
                Get(wrong, out int s4, out _);
                Assert.Equal(404, s4);

                for (int i = 0; i < 50 && completed == null; i++) Thread.Sleep(20);
                Assert.NotNull(completed);
            }

            Assert.False(File.Exists(path)); // temp files are removed when sharing stops
            Directory.Delete(dir, true);
        }
    }

    public class UpdateCheckerTests
    {
        // Shape of GitHub's /releases/latest answer (trimmed): nested objects come before tag_name.
        private const string Release =
            "{\"url\":\"https://api.github.com/repos/umitkrkmz/qrpeek/releases/1\"," +
            "\"html_url\":\"https://github.com/umitkrkmz/qrpeek/releases/tag/v0.2.0\"," +
            "\"author\":{\"login\":\"umitkrkmz\",\"html_url\":\"https://github.com/umitkrkmz\"}," +
            "\"tag_name\":\"v0.2.0\",\"name\":\"QRPeek 0.2.0\",\"assets\":[]}";

        [Fact]
        public void Reads_the_version_and_release_page()
        {
            UpdateInfo u = UpdateChecker.Parse(Release);
            Assert.Equal(new Version(0, 2, 0), u.Latest);
            Assert.Equal("https://github.com/umitkrkmz/qrpeek/releases/tag/v0.2.0", u.Url);
        }

        [Theory]
        [InlineData("v1.2", "1.2.0")]
        [InlineData("1.10.3", "1.10.3")]
        [InlineData("v2.0.1-beta", "2.0.1")]
        public void Understands_common_tag_styles(string tag, string expected)
        {
            Assert.Equal(Version.Parse(expected), UpdateChecker.Parse("{\"tag_name\":\"" + tag + "\"}").Latest);
        }

        [Fact]
        public void Never_trusts_a_download_link_outside_the_repository()
        {
            UpdateInfo u = UpdateChecker.Parse("{\"html_url\":\"https://evil.example/qrpeek.exe\",\"tag_name\":\"v9.9.9\"}");
            Assert.Equal("https://github.com/umitkrkmz/qrpeek/releases", u.Url);
        }

        [Fact]
        public void Garbage_is_no_update()
        {
            Assert.Null(UpdateChecker.Parse("{\"message\":\"Not Found\"}"));
            Assert.Null(UpdateChecker.Parse(""));
        }

        [Fact]
        public void Only_a_higher_version_counts_as_newer()
        {
            Version cur = UpdateChecker.Current;
            Assert.True(new UpdateInfo { Latest = new Version(cur.Major, cur.Minor + 1, 0) }.IsNewer);
            Assert.False(new UpdateInfo { Latest = cur }.IsNewer);
        }
    }

    public class LinkResolverTests
    {
        public LinkResolverTests() => S.Init("en");

        /// <summary>A tiny local server: /a -> 301 /b -> 302 /c -> 200; HEAD on /c is refused with 405.</summary>
        private static int StartRedirectServer(CancellationToken stop)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            stop.Register(() => listener.Stop());
            Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    TcpClient c;
                    try { c = listener.AcceptTcpClient(); } catch (SocketException) { return; } catch (ObjectDisposedException) { return; }
                    using (c)
                    {
                        var reader = new StreamReader(c.GetStream(), Encoding.ASCII);
                        string first = reader.ReadLine() ?? "";
                        while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                        string[] parts = first.Split(' ');
                        string method = parts[0], path = parts.Length > 1 ? parts[1] : "/";
                        string resp =
                            // A shortener behind a bot check (seen with shorturl.at on Cloudflare): 403 to everything.
                            path == "/short" ? "HTTP/1.1 403 Forbidden\r\ncf-mitigated: challenge\r\nContent-Length: 0\r\n\r\n" :
                            path == "/hop" ? "HTTP/1.1 301 Moved\r\nLocation: /short\r\nContent-Length: 0\r\n\r\n" :
                            path == "/gone" ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n" :
                            path == "/a" ? "HTTP/1.1 301 Moved\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n" :
                            path == "/b" ? "HTTP/1.1 302 Found\r\nLocation: /c\r\nContent-Length: 0\r\n\r\n" :
                            path == "/c" && method == "HEAD" ? "HTTP/1.1 405 No\r\nContent-Length: 0\r\n\r\n" :
                            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
                        byte[] b = Encoding.ASCII.GetBytes(resp);
                        c.GetStream().Write(b, 0, b.Length);
                    }
                }
            });
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        [Fact]
        public async Task Follows_the_redirect_chain_to_the_real_destination()
        {
            using (var cts = new CancellationTokenSource())
            {
                int port = StartRedirectServer(cts.Token);
                ResolveResult r = await LinkResolver.ResolveAsync(new Uri("http://127.0.0.1:" + port + "/a"));
                Assert.Null(r.Error);
                Assert.Equal(3, r.Chain.Count);
                Assert.Equal("/c", r.Final.AbsolutePath);
                cts.Cancel();
            }
        }

        [Fact]
        public async Task A_refused_check_is_never_reported_as_no_redirect()
        {
            using (var cts = new CancellationTokenSource())
            {
                int port = StartRedirectServer(cts.Token);
                string root = "http://127.0.0.1:" + port;

                ResolveResult r = await LinkResolver.ResolveAsync(new Uri(root + "/short"));
                Assert.Equal(403, r.RefusedWith);
                Assert.Single(r.Chain);

                // Refused after a redirect: the chain so far is kept, the end stays unknown.
                r = await LinkResolver.ResolveAsync(new Uri(root + "/hop"));
                Assert.Equal(403, r.RefusedWith);
                Assert.Equal(new[] { "/hop", "/short" }, r.Chain.Select(u => u.AbsolutePath));

                // A plain redirect chain or a missing page is not a refusal.
                r = await LinkResolver.ResolveAsync(new Uri(root + "/a"));
                Assert.Null(r.RefusedWith);
                r = await LinkResolver.ResolveAsync(new Uri(root + "/gone"));
                Assert.Null(r.RefusedWith);
                cts.Cancel();
            }
        }

        [Fact]
        public async Task Reports_an_unreachable_host_instead_of_throwing()
        {
            ResolveResult r = await LinkResolver.ResolveAsync(new Uri("http://127.0.0.1:1/x"));
            Assert.NotNull(r.Error);
            Assert.Single(r.Chain);
        }
    }
}
