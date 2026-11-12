// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    /// <summary>
    /// Shaped like OCR output of three real receipts (an AI subscription, a
    /// Google Play order, a Microsoft Store order), with every personal detail
    /// replaced by made-up data.
    /// </summary>
    public class ReceiptParserTests
    {
        private const string Anthropic =
            "Receipt\n" +
            "Invoice number PWZPIPZQ-0006\n" +
            "Receipt number 2843-7979-5973\n" +
            "Date paid\tOctober 26, 2026\n" +
            "VAT Registration Turkey VAT: 701236788\n" +
            "Anthropic, PBC\tBill to\n" +
            "548 Market Street\tJane\n" +
            "PMB 90375\tSample Sk. No:1\n" +
            "San Francisco, California 94104\t06000 ANKARA/Ankara\n" +
            "United States\tTurkey\n" +
            "support@anthropic.com\tjane.doe@example.com\n" +
            "$24.00 paid on October 26, 2026\n" +
            "Description\tQty\tunit price\tTax\tAmount\n" +
            "Claude Pro\t1\t$20.00\t20%\t$20.00\n" +
            "Oct 26-Nov 26, 2026\n" +
            "Subtotal\t$20.00\n" +
            "Total excluding tax\t$20.00\n" +
            "VAT - Turkey (20% on $20.00)\t$4.00\n" +
            "Total\t$24.00\n" +
            "Amount paid\t$24.00\n" +
            "Payment history\n" +
            "Payment method\tDate\tAmount paid\tReceipt number\n" +
            "Mastercard - 5328\tOctober 26, 2026\t$24.00\t2843-7979-\n" +
            "5973";

        private const string GooglePlay =
            "Google Play\n" +
            "Teşekkür ederiz\n" +
            "Google Play'deki Google Commerce Limited aboneliğiniz devam\n" +
            "ediyor ve bunun için sizden ödeme alındı. Aboneliklerinizi yönetin\n" +
            "Sipariş numarası: SOP.3361-3923-9728-12645..0\n" +
            "Sipariş tarihi: 5 Kas 2026 19:50:03 GMT+3\n" +
            "Hesabınız: jane.doe@gmail.com\n" +
            "Ürün\tFiyat\n" +
            "Google AI Pro (5 TB) (Google One) (Geliştirici: Google\n" +
            "₺179,99/ay\n" +
            "LLC)\n" +
            "Otomatik yenilenen abonelik\n" +
            "Toplam: ₺179,99/ay\n" +
            "(₺30,00 tutarında KDV dahil)\n" +
            "Ödeme şekli:\tWORLDCARD PLATİNUM\n" +
            "Kazanılan Play Puanları\t+198";

        private const string MicrosoftStore =
            "Sipariş Numarası: 1679897483\n" +
            "Satın alma tarihi: 11 Kasım 2026 Çarşamba\n" +
            "Sipariş ayrıntıları\n" +
            "Öğe açıklaması\tMiktar\tFiyat\n" +
            "QRPeek\t1\tUSD 0.00\n" +
            "someone tarafından\n" +
            "Ürün Anahtarı: ABCDE-FGHIJ-KLMNO-PQRST-UVWXY\n" +
            "Sipariş Alt Toplamı\tUSD 0.00\n" +
            "Sipariş Toplamı\tUSD 0.00";

        private static string V(Receipt r, string field) => r[field]?.Value;

        [Fact]
        public void Reads_an_english_invoice_with_tables()
        {
            Receipt r = ReceiptParser.Parse(Anthropic, ReceiptTerms.Default);
            Assert.NotNull(r);
            Assert.Equal("Anthropic, PBC", V(r, "seller"));
            Assert.Equal("PWZPIPZQ-0006", V(r, "number"));
            Assert.Equal("October 26, 2026", V(r, "date"));
            Assert.Equal("Turkey VAT: 701236788", V(r, "taxid"));
            Assert.Equal("$20.00", V(r, "subtotal")); // "Subtotal" came first; "Total excluding tax" is the same field
            Assert.Equal("$4.00", V(r, "tax"));        // the row's last amount, not "$20.00" inside the label
            Assert.Equal("$24.00", V(r, "total"));
            Assert.Equal("Claude Pro · 1 · $20.00 · 20% · $20.00", V(r, "item"));
            Assert.Equal("Jane", V(r, "buyer"));
            Assert.Equal("Mastercard - 5328", V(r, "payment")); // from the "Payment method | Date …" table
            Assert.Equal("USD", r.Currency);
        }

        [Fact]
        public void Reads_a_turkish_receipt_with_the_price_on_its_own_line()
        {
            Receipt r = ReceiptParser.Parse(GooglePlay, ReceiptTerms.Default);
            Assert.NotNull(r);
            Assert.Equal("Google Play", V(r, "seller"));
            Assert.Equal("SOP.3361-3923-9728-12645..0", V(r, "number"));
            Assert.Equal("5 Kas 2026 19:50:03 GMT+3", V(r, "date"));
            Assert.Equal("Google AI Pro (5 TB) (Google One) (Geliştirici: Google · ₺179,99/ay", V(r, "item"));
            Assert.Equal("₺179,99/ay", V(r, "total"));
            Assert.Equal("₺30,00", V(r, "tax")); // "(₺30,00 tutarında KDV dahil)"
            Assert.Equal("WORLDCARD PLATİNUM", V(r, "payment"));
            Assert.Equal("TRY", r.Currency);
        }

        [Fact]
        public void Never_offers_a_product_key()
        {
            Receipt r = ReceiptParser.Parse(MicrosoftStore, ReceiptTerms.Default);
            Assert.NotNull(r);
            Assert.Equal("1679897483", V(r, "number"));
            Assert.Equal("QRPeek · 1 · USD 0.00", V(r, "item")); // the "… tarafından" line after it is not part of it
            Assert.Equal("USD 0.00", V(r, "subtotal"));
            Assert.Equal("USD 0.00", V(r, "total"));

            ReceiptField key = r["secret"];
            Assert.True(key.Secret);
            Assert.False(key.Selected);
            Assert.Equal("•••• VWXY", key.Display);
            Assert.DoesNotContain("ABCDE", key.Display);
        }

        [Fact]
        public void Picks_business_fields_and_leaves_personal_ones_for_the_user()
        {
            Receipt r = ReceiptParser.Parse(Anthropic, ReceiptTerms.Default);
            Assert.True(r["total"].Selected);
            Assert.True(r["seller"].Selected);
            Assert.False(r["buyer"].Selected);
            Assert.True(r["buyer"].Personal);
            Assert.False(r["payment"].Selected);

            // Card numbers anywhere are secrets (Luhn-valid only: order numbers are not cards).
            Receipt c = ReceiptParser.Parse("Total\t$5.00\nPayment method\t4111 1111 1111 1111", ReceiptTerms.Default);
            Assert.True(c["payment"].Secret);
            Assert.Equal("•••• 1111", c["payment"].Display);
            Receipt o = ReceiptParser.Parse("Order number 1234567890123\nDate 1 Jan 2026", ReceiptTerms.Default);
            Assert.False(o["number"].Secret);
        }

        [Fact]
        public void Is_not_a_receipt_without_a_total_or_a_number_and_date()
        {
            Assert.Null(ReceiptParser.Parse("Merhaba dünya\nBugün hava güzel", ReceiptTerms.Default));
            Assert.Null(ReceiptParser.Parse("Invoice number 123", ReceiptTerms.Default));
            Assert.Null(ReceiptParser.Parse("", ReceiptTerms.Default));
            Assert.NotNull(ReceiptParser.Parse("Toplam: 12,50 TL", ReceiptTerms.Default));
        }

        [Fact]
        public void The_summary_has_only_ticked_fields_and_never_a_secret()
        {
            S.Init("tr");
            try
            {
                Receipt r = ReceiptParser.Parse(MicrosoftStore, ReceiptTerms.Default);
                r["secret"].Selected = true; // even if something ticks it, it must never leave the card
                r["subtotal"].Selected = false;
                string s = ReceiptExport.Summary(r).Replace("\r\n", "\n");
                Assert.StartsWith("QRPeek makbuz özeti\n", s);
                Assert.Contains("Belge no: 1679897483", s);
                Assert.Contains("Toplam: USD 0.00", s);
                Assert.Contains("Para birimi: USD", s);
                Assert.DoesNotContain("Ara toplam", s);
                Assert.DoesNotContain("ABCDE", s);
                Assert.DoesNotContain("VWXY", s);
                Assert.EndsWith("(Resmî belge değildir.)", s);

                string csv = ReceiptExport.Csv(r, new System.Globalization.CultureInfo("tr-TR"));
                Assert.DoesNotContain("ABCDE", csv);
                Assert.DoesNotContain("VWXY", csv);
                Assert.DoesNotContain("Ürün anahtarı", csv);
            }
            finally { S.Init("en"); }
        }

        [Fact]
        public void The_csv_has_fixed_columns_and_no_formulas()
        {
            S.Init("en");
            Receipt r = ReceiptParser.Parse(Anthropic, ReceiptTerms.Default);
            r["seller"].Value = "=HYPERLINK(\"http://evil\")"; // OCR'd (or edited) text must not run in Excel
            string[] lines = ReceiptExport.Csv(r, new System.Globalization.CultureInfo("tr-TR")).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("Seller;Number;Date;", lines[0]);
            Assert.StartsWith("\"'=HYPERLINK(", lines[1]);
            Assert.Contains(";$24.00;USD;", lines[1]);
            Assert.DoesNotContain("Jane", lines[1]); // personal: not ticked by default

            string[] tab = ReceiptExport.Tabbed(r).Replace("\r\n", "\n").Split('\n');
            Assert.Equal(ReceiptExport.Columns.Length, tab[0].Split('\t').Length);
            Assert.Equal(ReceiptExport.Columns.Length, tab[1].Split('\t').Length);
        }

        [Theory]
        [InlineData("$24.00", "USD")]
        [InlineData("₺179,99/ay", "TRY")]
        [InlineData("12,50 TL", "TRY")]
        [InlineData("€9.99", "EUR")]
        [InlineData("USD 0.00", "USD")]
        [InlineData("42", null)]
        public void Guesses_the_currency(string amount, string expected)
        {
            Assert.Equal(expected, ReceiptParser.GuessCurrency(amount));
        }

        [Fact]
        public void The_user_terms_work_for_other_languages()
        {
            var terms = new ReceiptTerms(ReceiptTerms.BuiltIn, "[total] Gesamtbetrag\n[tax] MwSt\n[number] Rechnungsnummer");
            Receipt r = ReceiptParser.Parse("Rechnungsnummer: 2026-118\nMwSt 19%\t1,90 €\nGesamtbetrag\t11,90 €", terms);
            Assert.Equal("2026-118", r["number"].Value);
            Assert.Equal("1,90 €", r["tax"].Value);
            Assert.Equal("11,90 €", r["total"].Value);
            Assert.Equal("EUR", r.Currency);
        }
    }
}
