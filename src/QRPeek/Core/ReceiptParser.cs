// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QRPeek.Core
{
    /// <summary>One piece of a receipt the user can pick for the CSV / summary QR.</summary>
    internal sealed class ReceiptField
    {
        public ReceiptField(string field, string value)
        {
            Field = field;
            Value = value;
        }

        /// <summary>The ReceiptTerms field: seller, number, date, total…</summary>
        public string Field { get; }
        public string Value { get; set; }
        public bool Selected { get; set; }
        /// <summary>About the buyer (name, e-mail, card): shown, but not picked unless the user ticks it.</summary>
        public bool Personal { get; set; }
        /// <summary>A product key or card number: shown masked, can never be picked.</summary>
        public bool Secret { get; set; }

        /// <summary>What the card shows: a secret only by its last 4 characters.</summary>
        public string Display => Secret ? Mask(Value) : Value;

        internal static string Mask(string v)
        {
            string plain = new string((v ?? "").Where(char.IsLetterOrDigit).ToArray());
            return plain.Length <= 4 ? "••••" : "•••• " + plain.Substring(plain.Length - 4);
        }
    }

    internal sealed class Receipt
    {
        public List<ReceiptField> Fields { get; } = new List<ReceiptField>();
        /// <summary>ISO code guessed from the amounts ($ → USD, ₺ → TRY…), or null.</summary>
        public string Currency { get; set; }

        public ReceiptField this[string field] => Fields.FirstOrDefault(f => f.Field == field);
    }

    /// <summary>
    /// Turns OCR text (rows, tab-separated columns) into receipt fields using
    /// the receipt terms: a cell that starts with a known label takes its value
    /// from the rest of the cell or the next one; money fields take the row's
    /// last amount; a table header ("Description | Qty | Amount", "Payment
    /// method | Date") maps the rows under it column by column. Guesses are
    /// conservative: an empty field is better than a wrong one, and every value
    /// stays editable on the card.
    /// </summary>
    internal static class ReceiptParser
    {
        private static readonly HashSet<string> MoneyFields = new HashSet<string> { "subtotal", "tax", "total", "amount", "unitprice" };
        private static readonly HashSet<string> TableFields = new HashSet<string> { "item", "qty", "unitprice", "amount", "tax", "date", "payment", "total", "number" };

        /// <summary>The order fields appear on the card.</summary>
        public static readonly string[] Order =
        {
            "seller", "document", "number", "date", "period", "item", "subtotal", "tax", "total", "buyer", "payment", "taxid", "secret",
        };

        internal static readonly Regex Money = new Regex(
            @"(?<![\p{L}\d])(?:[₺$€£]\s?\d{1,3}(?:[.,]\d{3})*[.,]\d{2}|\d{1,3}(?:[.,]\d{3})*[.,]\d{2}\s?(?:USD|EUR|GBP|TRY|TL|₺|€|£|\$)|(?:USD|EUR|GBP|TRY)\s?\d{1,3}(?:[.,]\d{3})*[.,]\d{2})(?:/\p{L}+)?",
            RegexOptions.CultureInvariant);

        private static readonly Regex Email = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+");
        private static readonly Regex CardNumber = new Regex(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)");
        private static readonly Regex KeyLike = new Regex(@"\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){3,}\b");

        /// <summary>Null when the text doesn't look like a receipt (no total, nor a number and a date).</summary>
        public static Receipt Parse(string text, ReceiptTerms terms)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            List<string[]> rows = text.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Split('\t').Select(c => c.Trim()).ToArray())
                .Where(r => r.Any(c => c.Length > 0))
                .ToList();

            var found = new Dictionary<string, string>();
            var items = new List<string>();
            string seller = null;
            bool seenLabel = false;
            string[] header = null;

            for (int r = 0; r < rows.Count; r++)
            {
                string[] cells = rows[r];

                // A table header: several cells that are nothing but labels, no amounts.
                string[] fieldsHere = cells.Select(c => terms.StartsWithTerm(c, out string f, out int len) && len >= c.TrimEnd(':').Length ? f : null).ToArray();
                if (fieldsHere.Count(f => f != null && TableFields.Contains(f)) >= 2 && !cells.Any(c => Money.IsMatch(c)))
                {
                    header = fieldsHere;
                    seenLabel = true;
                    continue;
                }

                // Rows under an item header: one item per row ("Claude Pro | 1 | $20.00"), or an
                // item spread over lines with its price on a line of its own (Google Play).
                // A line after an item that already has its price ends the table.
                if (header != null && header.Contains("item") && !terms.StartsWithTerm(cells[0], out _, out _))
                {
                    bool lastPriced = items.Count > 0 && Money.IsMatch(items[items.Count - 1]);
                    if (cells.Length > 1)
                    {
                        items.Add(string.Join(" · ", cells.Where(c => c.Length > 0)));
                        continue;
                    }
                    if (items.Count > 0 && !lastPriced)
                    {
                        items[items.Count - 1] += (Money.IsMatch(cells[0]) ? " · " : " ") + cells[0];
                        continue;
                    }
                    if (items.Count == 0)
                    {
                        items.Add(cells[0]);
                        continue;
                    }
                    header = null;
                }

                // Rows under another header ("Payment method | Date | Amount paid"): column by column.
                if (header != null && !terms.StartsWithTerm(cells[0], out _, out _) && cells.Length > 1)
                {
                    for (int c = 0; c < cells.Length && c < header.Length; c++)
                    {
                        string f = header[c];
                        if (f == null || cells[c].Length == 0 || found.ContainsKey(f)) continue;
                        if (MoneyFields.Contains(f) && !Money.IsMatch(cells[c])) continue;
                        found[f] = cells[c];
                    }
                    continue;
                }
                header = null;

                for (int c = 0; c < cells.Length; c++)
                {
                    if (!terms.StartsWithTerm(cells[c], out string field, out int len))
                    {
                        // The seller: an unlabeled first line before any label, or the
                        // cell next to "Bill to" ("Anthropic, PBC | Bill to").
                        if (seller == null && c == 0 && !seenLabel && LooksLikeName(cells[c])) seller = cells[c];
                        continue;
                    }
                    seenLabel = true;
                    if (field == "buyer" && c > 0 && seller == null && LooksLikeName(cells[0])) seller = cells[0];
                    if (field == "document" || found.ContainsKey(field)) continue;

                    string rest = cells[c].Substring(Math.Min(len, cells[c].Length)).Trim().TrimStart(':', '-', '–').Trim();
                    string value;
                    if (MoneyFields.Contains(field))
                    {
                        // "VAT - Turkey (20% on $20.00) | $4.00": the row's last amount.
                        Match last = Money.Matches(string.Join(" ", cells.Skip(c).Select((x, i) => i == 0 ? rest : x))).Cast<Match>().LastOrDefault();
                        if (last == null) continue;
                        value = last.Value;
                    }
                    else
                    {
                        value = rest.Length > 0 ? rest
                            : c + 1 < cells.Length && !terms.StartsWithTerm(cells[c + 1], out _, out _) ? cells[c + 1]
                            // "Bill to" with its value under it, in the same column.
                            : r + 1 < rows.Count && rows[r + 1].Length > c && !terms.StartsWithTerm(rows[r + 1][c], out _, out _) ? rows[r + 1][c]
                            : null;
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        if ((field == "number" || field == "date" || field == "taxid") && !value.Any(char.IsDigit)) continue;
                    }
                    found[field] = value;
                    if (MoneyFields.Contains(field)) break; // the rest of the row was this value
                }
            }

            // "(₺30,00 tutarında KDV dahil)": tax mentioned inside a sentence.
            if (!found.ContainsKey("tax"))
            {
                foreach (string[] cells in rows)
                {
                    string row = string.Join(" ", cells);
                    Match m = Money.Match(row);
                    if (!m.Success || !terms.Mentions(row, "tax")) continue;
                    // "Total (incl. VAT) $24.00" is the total, not the tax.
                    if (terms.StartsWithTerm(cells[0], out string f0, out _) && (f0 == "total" || f0 == "subtotal")) continue;
                    found["tax"] = m.Value;
                    break;
                }
            }

            var receipt = new Receipt();
            if (seller != null) found["seller"] = seller;
            foreach (string field in Order)
            {
                if (field == "item")
                {
                    foreach (string it in items) receipt.Fields.Add(new ReceiptField("item", it));
                    continue;
                }
                if (found.TryGetValue(field, out string v)) receipt.Fields.Add(new ReceiptField(field, v));
            }
            foreach (ReceiptField f in receipt.Fields)
            {
                f.Secret = f.Field == "secret" || KeyLike.IsMatch(f.Value) || IsCardNumber(f.Value);
                f.Personal = !f.Secret && (f.Field == "buyer" || f.Field == "payment" || Email.IsMatch(f.Value));
                f.Selected = !f.Secret && !f.Personal;
            }

            bool hasTotal = receipt["total"] != null;
            bool hasNumberAndDate = receipt["number"] != null && receipt["date"] != null;
            if (!hasTotal && !hasNumberAndDate) return null;
            receipt.Currency = GuessCurrency(receipt["total"]?.Value ?? receipt["subtotal"]?.Value ?? text);
            return receipt;
        }

        private static bool LooksLikeName(string s) =>
            s.Length >= 2 && s.Any(char.IsLetter) && !Money.IsMatch(s) && !Email.IsMatch(s) && s.Count(char.IsDigit) <= 2;

        private static bool IsCardNumber(string v)
        {
            Match m = CardNumber.Match(v ?? "");
            if (!m.Success) return false;
            string digits = new string(m.Value.Where(char.IsDigit).ToArray());
            int sum = 0;
            for (int i = 0; i < digits.Length; i++) // Luhn: real card numbers only, not order numbers
            {
                int d = digits[digits.Length - 1 - i] - '0';
                if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
            }
            return sum % 10 == 0;
        }

        internal static string GuessCurrency(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (s.Contains("₺") || Regex.IsMatch(s, @"\b(TRY|TL)\b")) return "TRY";
            if (s.Contains("€") || s.Contains("EUR")) return "EUR";
            if (s.Contains("£") || s.Contains("GBP")) return "GBP";
            if (s.Contains("$") || s.Contains("USD")) return "USD";
            return null;
        }
    }
}
