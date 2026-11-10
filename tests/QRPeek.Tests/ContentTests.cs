// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class ContentTests
    {
        public ContentTests() => S.Init("en");

        [Theory]
        [InlineData("https://example.com", "Url")]
        [InlineData("www.example.com/a", "Url")]
        [InlineData("mailto:a@b.com?subject=hi", "Email")]
        [InlineData("MATMSG:TO:a@b.com;SUB:x;BODY:y;;", "Email")]
        [InlineData("tel:+905551112233", "Phone")]
        [InlineData("SMSTO:+905551112233:hello", "Sms")]
        [InlineData("BEGIN:VCARD\nVERSION:3.0\nFN:Ada Lovelace\nEND:VCARD", "Contact")]
        [InlineData("MECARD:N:Lovelace,Ada;TEL:123;;", "Contact")]
        [InlineData("geo:41.0082,28.9784", "Location")]
        [InlineData("WIFI:T:WPA;S:Home;P:secret;;", "Wifi")]
        [InlineData("just some words", "Text")]
        [InlineData("javascript:alert(1)", "Text")]
        [InlineData("file:///C:/Windows/System32/calc.exe", "Text")]
        public void Detects_the_kind(string raw, string expected)
        {
            Assert.Equal(expected, QrContent.Parse(raw).Kind.ToString());
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("ms-settings:privacy")]
        [InlineData("just text")]
        public void Never_offers_to_open_anything_but_safe_schemes(string raw)
        {
            Assert.Null(QrContent.Parse(raw).OpenTarget);
        }

        [Fact]
        public void A_normal_https_link_has_no_warnings()
        {
            QrContent c = QrContent.Parse("https://www.example.com/path?q=1");
            Assert.Empty(c.Warnings);
            Assert.Equal("www.example.com", c.Host);
        }

        [Fact]
        public void Warns_about_http_and_bare_ip()
        {
            QrContent c = QrContent.Parse("http://192.168.1.1/login");
            Assert.Contains(S.WarnHttp, c.Warnings);
            Assert.Contains(S.WarnIpHost, c.Warnings);
        }

        [Fact]
        public void Warns_about_lookalike_domains()
        {
            Assert.Contains(S.WarnIdn, QrContent.Parse("https://xn--pple-43d.com").Warnings);
            Assert.Contains(S.WarnIdn, QrContent.Parse("https://аpple.com").Warnings); // Cyrillic а
        }

        [Fact]
        public void Warns_about_the_at_sign_trick()
        {
            QrContent c = QrContent.Parse("https://bank.com@evil.example/login");
            Assert.Contains(S.WarnUserInfo, c.Warnings);
            Assert.Equal("evil.example", c.Host);
        }

        [Fact]
        public void Warns_about_shorteners()
        {
            Assert.Contains(S.WarnShortener, QrContent.Parse("https://bit.ly/3abc").Warnings);
        }

        [Fact]
        public void Parses_wifi_fields_in_any_order_with_escapes()
        {
            WifiInfo w = QrContent.Parse(@"WIFI:P:a\;b\:c\\d;S:My\,Net;T:SAE;H:true;;").Wifi;
            Assert.Equal("My,Net", w.Ssid);
            Assert.Equal(@"a;b:c\d", w.Password);
            Assert.Equal("SAE", w.Security);
            Assert.True(w.Hidden);
        }

        [Fact]
        public void Open_wifi_has_no_password()
        {
            WifiInfo w = QrContent.Parse("WIFI:T:nopass;S:Cafe;;").Wifi;
            Assert.True(w.IsOpen);
        }

        [Fact]
        public void Wlan_profile_is_valid_xml_even_with_special_characters()
        {
            var wifi = new WifiInfo { Ssid = "Tom & Jerry <5G>", Password = "a<b>&\"c'", Security = "WPA" };
            var doc = new XmlDocument();
            doc.LoadXml(WifiConnector.BuildProfile(wifi)); // throws if escaping is wrong
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("w", "http://www.microsoft.com/networking/WLAN/profile/v1");
            Assert.Equal("Tom & Jerry <5G>", doc.SelectSingleNode("//w:SSID/w:name", ns).InnerText);
            Assert.Equal("a<b>&\"c'", doc.SelectSingleNode("//w:keyMaterial", ns).InnerText);
            Assert.Equal("WPA2PSK", doc.SelectSingleNode("//w:authentication", ns).InnerText);
        }

        [Fact]
        public void Open_network_profile_has_no_key()
        {
            var doc = new XmlDocument();
            doc.LoadXml(WifiConnector.BuildProfile(new WifiInfo { Ssid = "Cafe", Security = "nopass" }));
            Assert.DoesNotContain("sharedKey", doc.OuterXml);
            Assert.Contains("<authentication>open</authentication>", doc.OuterXml);
        }
    }
}
