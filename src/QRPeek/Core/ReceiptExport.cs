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
    /// What leaves the receipt card, built only from the fields the user
    /// ticked. Secrets (product keys, card numbers) are never in it, ticked or
    /// not. The summary is plain text on purpose: any phone camera shows it,
    /// and its first line says it's a QRPeek summary, not an official document.
    /// </summary>
    internal static class ReceiptExport
    {
        /// <summary>Columns of the CSV and the Excel copy, always the same order (empty when not picked).</summary>
        public static readonly string[] Columns = { "seller", "number", "date", "period", "item", "subtotal", "tax", "total", "currency", "buyer", "payment", "taxid" };

        private static IEnumerable<ReceiptField> Picked(Receipt r) => r.Fields.Where(f => f.Selected && !f.Secret && !string.IsNullOrWhiteSpace(f.Value));

        public static string Summary(Receipt r)
        {
            var sb = new StringBuilder(S.ReceiptSummaryTitle);
            foreach (ReceiptField f in Picked(r)) sb.Append('\n').Append(FieldName(f, r)).Append(": ").Append(f.Value.Trim());
            if (r.Currency != null && Picked(r).Any(f => ReceiptParser.Money.IsMatch(f.Value)))
                sb.Append('\n').Append(S.ReceiptField("currency")).Append(": ").Append(r.Currency);
            sb.Append('\n').Append(S.NotOfficial);
            return sb.ToString().Replace("\n", Environment.NewLine);
        }

        /// <summary>
        /// A summary made by <see cref="Summary"/> (on this PC or another, in
        /// Turkish or English) back into a receipt, so a scanned summary QR opens
        /// as a card with the CSV and Excel buttons. Anyone can make a QR that
        /// looks like one, so it goes through the same rules as OCR: product keys
        /// are dropped, card numbers masked, the buyer's details unticked.
        /// Null when it isn't a summary or has no field we know.
        /// </summary>
        public static Receipt ReadSummary(string text)
        {
            string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            if (lines.Length < 2) return null;
            bool turkish;
            if (lines[0] == S.SummaryTitle(true)) turkish = true;
            else if (lines[0] == S.SummaryTitle(false)) turkish = false;
            else return null;

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string field in ReceiptParser.Order.Concat(Columns)) names[S.ReceiptField(field, turkish)] = field;

            var r = new Receipt();
            foreach (string line in lines.Skip(1))
            {
                if (line == S.NotOfficialIn(turkish)) continue;
                int colon = line.IndexOf(": ", StringComparison.Ordinal);
                if (colon <= 0) continue;
                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 2).Trim();
                string label = null;
                if (!names.TryGetValue(name, out string field))
                {
                    // "Number (Receipt number)": the label tells several numbers apart.
                    Match m = Regex.Match(name, @"^(.+?) \((.+)\)$");
                    if (!m.Success || !names.TryGetValue(m.Groups[1].Value, out field)) continue;
                    label = m.Groups[2].Value;
                }
                if (value.Length == 0 || field == "secret") continue;
                if (field == "currency")
                {
                    if (Regex.IsMatch(value, @"^[A-Z]{3}$")) r.Currency = value;
                    continue;
                }
                var f = new ReceiptField(field, value) { Label = label };
                ReceiptParser.Classify(f);
                r.Fields.Add(f);
            }
            return r.Fields.Count > 0 ? r : null;
        }

        /// <summary>"Number (Receipt number)" when a receipt has several numbers or dates, so they can be told apart.</summary>
        internal static string FieldName(ReceiptField f, Receipt r) =>
            S.ReceiptField(f.Field) + (Ambiguous(f, r) ? " (" + f.Label + ")" : "");

        internal static bool Ambiguous(ReceiptField f, Receipt r) =>
            f.Label != null && (f.Field == "number" || f.Field == "date") && r.Fields.Count(x => x.Field == f.Field) > 1;

        private static string Value(Receipt r, string column)
        {
            if (column == "currency") return Picked(r).Any(f => ReceiptParser.Money.IsMatch(f.Value)) ? r.Currency ?? "" : "";
            // Several items go into one cell.
            return string.Join(" | ", Picked(r).Where(f => f.Field == column).Select(f => f.Value.Trim()));
        }

        /// <summary>Header + one row, tab-separated: pastes into Excel as cells.</summary>
        public static string Tabbed(Receipt r) =>
            string.Join("\t", Columns.Select(S.ReceiptField)) + Environment.NewLine +
            string.Join("\t", Columns.Select(c => Value(r, c).Replace('\t', ' ')));

        public static string Csv(Receipt r, CultureInfo culture)
        {
            string sep = InvoiceCsv.Separator(culture);
            return string.Join(sep, Columns.Select(c => InvoiceCsv.CsvCell(S.ReceiptField(c), sep))) + Environment.NewLine +
                   string.Join(sep, Columns.Select(c => InvoiceCsv.CsvCell(Value(r, c), sep))) + Environment.NewLine;
        }
    }
}
