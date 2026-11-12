// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using QRPeek.Core;
using Xunit;

namespace QRPeek.Tests
{
    public class ReceiptTermsTests
    {
        private static string P(string s) => ReceiptTerms.Default.Polish(s).Replace("\r\n", "\n");

        [Fact]
        public void Reads_the_format_and_merges_added_terms_in_any_language()
        {
            var t = new ReceiptTerms(ReceiptTerms.BuiltIn, "# German\n[total] Gesamtbetrag, Summe\n[tax] MwSt\nnot a line\n[total] total\n");
            Assert.Contains("Gesamtbetrag", t.TermsOf("total"));
            Assert.Contains("Toplam", t.TermsOf("total"));
            Assert.Contains("MwSt", t.TermsOf("tax"));
            Assert.Single(t.TermsOf("total"), x => x.ToLowerInvariant() == "total"); // no duplicates
            Assert.Contains("number", t.Fields);
        }

        [Fact]
        public void Scores_the_reading_that_knows_more_labels_higher()
        {
            // The same receipt read by the English and by the Turkish OCR engine.
            string english = "Invoice number PWZ-0006\nDate paid October 26, 2026\nVAT Registration Turkey VAT: 7012\nSubtotal $20.00\nTotal $24.00\nAmount paid $24.00";
            string turkish = "Invoİce n um ber PWZ-0006\nDate paİd October 26, 2026\nVAT Regiştra Turkey VAT: 7012\nSubtotal $20.00\nTotal $24.00\nAmount paİd $24.00";
            Assert.True(ReceiptTerms.Default.Score(english) > ReceiptTerms.Default.Score(turkish));
            Assert.Equal(0, ReceiptTerms.Default.Score("Merhaba dünya, bugün hava güzel."));
        }

        [Theory]
        [InlineData("Invoice n um ber PWZ1PZQ-0006", "Invoice number PWZ1PZQ-0006")]
        [InlineData("Date Of issue\tOctober 26, 2026", "Date of issue\tOctober 26, 2026")]
        [InlineData("Total excludng tax\t$20.00", "Total excluding tax\t$20.00")]
        [InlineData("Ammount paid\t$24.00", "Amount paid\t$24.00")]
        [InlineData("Sipariş Numaras: 1679897483", "Sipariş Numarası: 1679897483")]
        [InlineData("Ara toplam\t20,00 USD", "Ara toplam\t20,00 USD")]
        [InlineData("TOTAL\t$24.00", "TOTAL\t$24.00")]
        // Letter pairs OCR confuses (rn/m, cl/d, vv/w) count as one mistake.
        [InlineData("Description\tQty\tArnount", "Description\tQty\tAmount")]
        [InlineData("Arnount paid\t$24.00", "Amount paid\t$24.00")]
        [InlineData("Paynnent method", "Payment method")]
        // Values and anything that isn't a near-miss of a label stay exactly as read.
        [InlineData("Anthropic, PBC\tBill to", "Anthropic, PBC\tBill to")]
        [InlineData("548 Market Street\tümit", "548 Market Street\tümit")]
        [InlineData("support@anthropic.com", "support@anthropic.com")]
        [InlineData("Tex\t$4.00", "Tex\t$4.00")] // too short to guess
        [InlineData("Mustafa Kemal Mah. 192. Cd.", "Mustafa Kemal Mah. 192. Cd.")]
        [InlineData("Otomatik yenilenen abonelik", "Otomatik yenilenen abonelik")]
        public void Polishes_labels_and_leaves_values_alone(string ocr, string expected)
        {
            Assert.Equal(expected, P(ocr));
        }

        [Fact]
        public void Keeps_rows_and_columns()
        {
            Assert.Equal("Öğe açıklaması\tMiktar\tFiyat\nQRPeek\t1\tUSD 1.99", P("Öğe açıklaması\tMiktar\tFiyat\r\nQRPeek\t1\tUSD 1.99"));
        }

        [Fact]
        public void Takes_an_AIs_answer_as_pasted()
        {
            // AIs wrap the list in a code block and put dashes or numbers in front,
            // even when told not to. The list itself is still good.
            string text = "```text\n- [total] Gesamtbetrag, Summe\n* [tax] MwSt.\n• [qty] Menge\n1. [date] Datum\n2) [number] Rechnungsnummer\n```";
            Assert.Empty(ReceiptTerms.Check(text, out int count));
            Assert.Equal(6, count);
            var t = new ReceiptTerms("");
            t.Add(text.Replace("\n", "\r\n"));
            Assert.Equal(new[] { "Gesamtbetrag", "Summe" }, t.TermsOf("total"));
            Assert.Equal(new[] { "MwSt." }, t.TermsOf("tax"));
            Assert.Equal(new[] { "Datum" }, t.TermsOf("date"));
            Assert.Equal(new[] { "Rechnungsnummer" }, t.TermsOf("number"));
        }

        [Fact]
        public void The_AI_prompt_explains_secret_and_asks_for_plain_text()
        {
            foreach (string lang in new[] { "tr", "en" })
            {
                S.Init(lang);
                string p = S.TermsAiPromptText(string.Join(", ", ReceiptTerms.KnownFields));
                Assert.Contains("[secret] ", p);              // an example, so "secret" isn't read as "confidential"
                foreach (string f in ReceiptTerms.KnownFields) Assert.Contains(f, p);
            }
            S.Init("en");
        }

        [Fact]
        public void Checks_a_users_list_line_by_line()
        {
            S.Init("en");
            string text = "# notes are fine\n[total] Gesamtbetrag, Summe\nGesamtbetrag\n[toplm] Toplam\n[tax]\n\n[tax] MwSt";
            var problems = ReceiptTerms.Check(text, out int terms);
            Assert.Equal(4, terms); // Gesamtbetrag, Summe, Toplam, MwSt
            Assert.Equal(new[] { 3, 4, 5 }, problems.Select(p => p.Line));
            Assert.Contains("format", problems[0].Message);
            Assert.Contains("toplm", problems[1].Message);
            Assert.Empty(ReceiptTerms.Check(ReceiptTerms.BuiltIn, out _)); // the built-in list passes its own check
        }

        [Fact]
        public void The_users_terms_are_what_OCR_uses()
        {
            try
            {
                ReceiptTerms.UseUserTerms("[total] Gesamtbetrag\n[tax] MwSt");
                Assert.Equal("Gesamtbetrag\t12,00 €", ReceiptTerms.Current.Polish("Gesamtbetrog\t12,00 €").Replace("\r\n", "\n"));
                Assert.True(ReceiptTerms.Current.Score("MwSt 19%\nGesamtbetrag 12,00 €") > 0);
                Assert.Contains("Toplam", ReceiptTerms.Current.TermsOf("total")); // built-in terms stay

                ReceiptTerms.UseUserTerms("   ");
                Assert.Same(ReceiptTerms.Default, ReceiptTerms.Current);
            }
            finally
            {
                ReceiptTerms.UseUserTerms("");
            }
        }

        [Theory]
        [InlineData("total", "totla", 1)]
        [InlineData("registration", "regiştra", 5)]
        [InlineData("abc", "abc", 0)]
        public void Measures_edit_distance_with_transpositions(string a, string b, int expected)
        {
            Assert.Equal(expected, ReceiptTerms.Distance(a, b));
        }
    }
}
