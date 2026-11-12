// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QRPeek.Core
{
    /// <summary>
    /// The words invoices and receipts use for their labels ("Invoice number",
    /// "Amount paid", "KDV", "Ara toplam"…), grouped by what they mean. Not
    /// tied to a language: Turkish and English are built in, and users can
    /// add terms in any language (one text format, easy to share).
    ///
    /// Format, one field per line (a field may appear on several lines):
    /// <code>
    /// # comment
    /// [total] Total, Amount paid, Toplam, Gesamtbetrag
    /// </code>
    ///
    /// Used on OCR text to: pick the OCR language that reads the image best
    /// (<see cref="Score"/>), glue words OCR split apart ("n um ber"), and fix
    /// small typos in labels only (<see cref="Polish"/>). Values (names,
    /// numbers, addresses, e-mails) are never touched.
    /// </summary>
    internal sealed class ReceiptTerms
    {
        public const string BuiltIn = @"# QRPeek built-in receipt terms (Turkish and English).
[document]  Invoice, Receipt, Tax invoice, Fatura, Fiş, Makbuz, e-Arşiv Fatura, e-Fatura
[number]    Invoice number, Invoice no, Receipt number, Receipt no, Order number, Order no, Document number
[number]    Fatura no, Fatura numarası, Fiş no, Belge no, Sipariş numarası, Sipariş no
[date]      Date, Date paid, Date of issue, Issue date, Date due, Due date, Order date, Invoice date, Payment date
[date]      Tarih, Fatura tarihi, Sipariş tarihi, Satın alma tarihi, Ödeme tarihi, Düzenleme tarihi, Son ödeme tarihi
[seller]    Seller, Vendor, Sold by, Merchant, Satıcı
[buyer]     Bill to, Billed to, Customer, Ship to, Alıcı, Müşteri, Fatura adresi
[item]      Description, Item, Items, Product, Service, Ürün, Hizmet, Açıklama, Öğe açıklaması, Mal hizmet
[qty]       Qty, Quantity, Miktar, Adet
[unitprice] Unit price, Price, Birim fiyat, Fiyat
[amount]    Amount, Line total, Tutar, Toplam tutar
[subtotal]  Subtotal, Sub total, Total excluding tax, Net amount, Ara toplam, Sipariş alt toplamı, Mal hizmet toplamı, Matrah
[tax]       VAT, Tax, Sales tax, GST, KDV, Vergi, Hesaplanan KDV
[taxid]     VAT registration, VAT number, VAT ID, Tax ID, VKN, TCKN, Vergi no, Vergi numarası, Vergi dairesi
[total]     Total, Amount paid, Amount due, Grand total, Order total, Total paid, Toplam, Genel toplam, Sipariş toplamı, Ödenecek, Ödenecek tutar, Ödenen tutar, Vergiler dahil
[payment]   Payment method, Paid with, Payment history, Ödeme şekli, Ödeme yöntemi, Kart
[period]    Billing period, Period, Dönem, Fatura dönemi
";

        /// <summary>Labels shorter than this are only matched exactly (a typo fix on "Tax" could turn any 3-letter word into it).</summary>
        private const int MinFuzzyLength = 5;

        private readonly List<Term> _terms = new List<Term>();
        private readonly HashSet<string> _vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class Term
        {
            public string Field;
            public string Text;
            public string[] Words;
        }

        public ReceiptTerms(params string[] sources)
        {
            foreach (string src in sources) Add(src);
        }

        public static ReceiptTerms Default { get; } = new ReceiptTerms(BuiltIn);

        public IEnumerable<string> Fields => _terms.Select(t => t.Field).Distinct();

        public IEnumerable<string> TermsOf(string field) => _terms.Where(t => t.Field == field).Select(t => t.Text);

        /// <summary>True for a word that appears in a label ("Registration", "Toplam"); short words count only if they are a whole term ("VAT", "KDV").</summary>
        public bool IsLabelWord(string word) =>
            !string.IsNullOrEmpty(word)
            && (_vocabulary.Contains(word) || _terms.Any(t => t.Words.Length == 1 && string.Equals(t.Text, word, StringComparison.OrdinalIgnoreCase)));

        /// <summary>Adds the terms in <paramref name="text"/> (same format as <see cref="BuiltIn"/>); bad lines are skipped.</summary>
        public void Add(string text)
        {
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                Match m = Regex.Match(line, @"^\[([^\]]+)\]\s*(.*)$");
                if (!m.Success) continue;
                string field = m.Groups[1].Value.Trim().ToLowerInvariant();
                foreach (string part in m.Groups[2].Value.Split(','))
                {
                    string t = Regex.Replace(part.Trim(), @"\s+", " ");
                    if (t.Length == 0 || _terms.Any(x => x.Field == field && string.Equals(x.Text, t, StringComparison.OrdinalIgnoreCase))) continue;
                    string[] words = t.Split(' ');
                    _terms.Add(new Term { Field = field, Text = t, Words = words });
                    foreach (string w in words) if (w.Length >= 4) _vocabulary.Add(w);
                }
            }
        }

        /// <summary>
        /// How much <paramref name="text"/> looks like a receipt in a language
        /// these terms know: each label found counts by its number of words.
        /// Used to choose between OCR languages (the wrong one reads
        /// "Registration" as "Regiştra", "Invoice" as "Invoİce").
        /// </summary>
        public int Score(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string norm = " " + Regex.Replace(text.ToLower(CultureInfo.InvariantCulture), @"[^\p{L}\p{N}]+", " ") + " ";
            int score = 0;
            foreach (Term t in _terms)
            {
                string needle = " " + Regex.Replace(t.Text.ToLower(CultureInfo.InvariantCulture), @"[^\p{L}\p{N}]+", " ") + " ";
                int i = 0;
                while ((i = norm.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
                {
                    score += t.Words.Length;
                    i += needle.Length - 1;
                }
            }
            return score;
        }

        /// <summary>
        /// Cleans OCR text with the terms, row by row (rows are lines, cells
        /// are separated by tabs): words split apart are glued back ("n um
        /// ber" → "number") and a label with a small typo becomes the known
        /// term ("Total excludng tax" → "Total excluding tax"). Only a cell's
        /// leading label is ever changed, never values.
        /// </summary>
        public string Polish(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] cells = lines[i].Split('\t');
                for (int c = 0; c < cells.Length; c++) cells[c] = PolishCell(cells[c]);
                lines[i] = string.Join("\t", cells);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private string PolishCell(string cell)
        {
            List<string> tokens = cell.Split(' ').Where(t => t.Length > 0).ToList();
            JoinSplitWords(tokens);

            // The label: the leading run of plain words, up to a colon or the first
            // token that is a value (digits, @, currency, punctuation inside).
            int n = 0;
            bool colon = false;
            while (n < tokens.Count)
            {
                string t = tokens[n];
                string bare = t.TrimEnd(':');
                if (bare.Length == 0 || !bare.All(char.IsLetter)) break;
                n++;
                if (t.EndsWith(":")) { colon = true; break; }
            }
            if (n > 0)
            {
                string label = string.Join(" ", tokens.Take(n)).TrimEnd(':');
                string fixedLabel = FixLabel(label);
                if (fixedLabel != null && fixedLabel != label)
                {
                    tokens.RemoveRange(0, n);
                    tokens.InsertRange(0, (fixedLabel + (colon ? ":" : "")).Split(' '));
                }
            }
            return string.Join(" ", tokens);
        }

        /// <summary>"Invoice n um ber" → "Invoice number": 2-4 short letter-only pieces whose join is a known word.</summary>
        private void JoinSplitWords(List<string> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                for (int len = 4; len >= 2; len--)
                {
                    if (i + len > tokens.Count) continue;
                    List<string> parts = tokens.GetRange(i, len);
                    string last = parts[len - 1];
                    string trailing = last.Length > 1 && !char.IsLetter(last[last.Length - 1]) ? last.Substring(last.Length - 1) : "";
                    parts[len - 1] = trailing.Length > 0 ? last.Substring(0, last.Length - 1) : last;
                    if (!parts.All(p => p.Length > 0 && p.Length <= 4 && p.All(char.IsLetter))) continue;
                    if (parts.All(p => p.Length >= 3)) continue; // "Ara toplam" etc. are real word pairs, not a split word
                    string joined = string.Concat(parts);
                    if (!_vocabulary.Contains(joined)) continue;
                    tokens.RemoveRange(i, len);
                    tokens.Insert(i, joined + trailing);
                    break;
                }
            }
        }

        /// <summary>The known term this label is a near-miss of, in the label's capitalization; null if none.</summary>
        private string FixLabel(string label)
        {
            int words = label.Split(' ').Length;
            string best = null;
            int bestDistance = int.MaxValue;
            foreach (Term t in _terms)
            {
                if (t.Words.Length != words) continue;
                if (string.Equals(t.Text, label, StringComparison.OrdinalIgnoreCase)) return MatchCase(t.Text, label);
                if (label.Length < MinFuzzyLength || t.Text.Length < MinFuzzyLength) continue;
                int allowed = t.Text.Length >= 12 ? 2 : 1;
                string a = label.ToLowerInvariant(), b = t.Text.ToLowerInvariant();
                // "Arnount": OCR reads m as rn (also d as cl, w as vv); one such swap is one mistake.
                int d = Math.Min(Distance(a, b), Distance(Confusables(a), Confusables(b)));
                if (d <= allowed && d < bestDistance)
                {
                    best = t.Text;
                    bestDistance = d;
                }
            }
            return best == null ? null : MatchCase(best, label);
        }

        /// <summary>
        /// Keeps how the label was written: "TOTAL" stays upper case, and each
        /// word keeps its first letter's case ("Sipariş Numaras" → "Sipariş
        /// Numarası"). A wrongly capitalized "Of" in the middle becomes "of" only
        /// when the term has it lower case and the label's first word is the only capital.
        /// </summary>
        private static string MatchCase(string term, string label)
        {
            CultureInfo ci = CultureInfo.CurrentCulture;
            if (label.Length > 1 && label.Where(char.IsLetter).All(char.IsUpper)) return term.ToUpper(ci);
            string[] t = term.Split(' ');
            string[] l = label.Split(' ');
            bool titleCase = l.Length > 1 && l.All(w => w.Length > 0 && char.IsUpper(w[0]));
            for (int i = 0; i < t.Length && i < l.Length; i++)
            {
                if (t[i].Length == 0 || l[i].Length == 0) continue;
                bool upper = i == 0 || titleCase ? char.IsUpper(l[i][0]) : char.IsUpper(t[i][0]);
                t[i] = (upper ? char.ToUpper(t[i][0], ci) : char.ToLower(t[i][0], ci)) + t[i].Substring(1);
            }
            return string.Join(" ", t);
        }

        private static string Confusables(string s) => s.Replace("rn", "m").Replace("cl", "d").Replace("vv", "w");

        /// <summary>Edit distance with transpositions (optimal string alignment).</summary>
        internal static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
