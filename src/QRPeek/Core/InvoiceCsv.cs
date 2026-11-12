// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QRPeek.Core
{
    /// <summary>
    /// Writes e-Fatura rows as CSV that Excel opens directly: UTF-8 with BOM
    /// (Turkish letters), the system list separator (";" in Turkey) and numbers
    /// in the system's number format (120,50).
    /// </summary>
    internal static class InvoiceCsv
    {
        public static void Write(string path, IEnumerable<InvoiceInfo> invoices, CultureInfo culture = null)
        {
            culture = culture ?? CultureInfo.CurrentCulture;
            File.WriteAllText(path, Build(invoices, culture), new UTF8Encoding(true));
        }

        internal static string Build(IEnumerable<InvoiceInfo> invoices, CultureInfo culture)
        {
            string sep = culture.TextInfo.ListSeparator;
            if (string.IsNullOrEmpty(sep) || sep == culture.NumberFormat.NumberDecimalSeparator) sep = ";";

            string Cell(string s) => CsvCell(s, sep);

            string Num(decimal? d) => d.HasValue ? d.Value.ToString("0.00", culture) : "";

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(sep, new[]
            {
                S.InvNumber, S.InvDate, S.InvSeller, S.InvBuyer, S.InvScenario, S.InvType, S.InvCurrency,
                S.InvSubtotal, S.InvVat, S.InvTaxIncluded, S.InvPayable, "ETTN",
            }));
            foreach (InvoiceInfo i in invoices)
            {
                sb.AppendLine(string.Join(sep, new[]
                {
                    Cell(i.Number), Cell(i.Date), Cell(i.SellerId), Cell(i.BuyerId), Cell(i.Scenario), Cell(i.Type), Cell(i.Currency),
                    Num(i.Subtotal), Num(i.Vat), Num(i.TaxIncluded), Num(i.Payable), Cell(i.Ettn),
                }));
            }
            return sb.ToString();
        }

        /// <summary>The list separator Excel expects for <paramref name="culture"/> (";" in Turkey).</summary>
        internal static string Separator(CultureInfo culture)
        {
            string sep = culture.TextInfo.ListSeparator;
            return string.IsNullOrEmpty(sep) || sep == culture.NumberFormat.NumberDecimalSeparator ? ";" : sep;
        }

        /// <summary>One CSV cell, quoted when needed and never starting a formula.</summary>
        internal static string CsvCell(string s, string sep)
        {
            s = s ?? "";
            bool quote = s.Contains(sep) || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
            // Leading = + - @ would make Excel run a formula from QR or OCR content.
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            return quote ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
