// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace QRPeek.Core
{
    internal readonly struct OcrWord
    {
        public OcrWord(string text, RectangleF box)
        {
            Text = text;
            Box = box;
        }

        public string Text { get; }
        public RectangleF Box { get; }
    }

    /// <summary>
    /// Puts OCR words back into the rows a person sees. Windows OCR splits a
    /// row into separate "lines" wherever there is a wide gap, so a receipt's
    /// "Order total ........ USD 24.00" came out as two lines far apart, and a
    /// table's columns as one column after another. Here words on the same
    /// baseline form one row, left to right, and a wide gap becomes a tab
    /// (it pastes into Excel as separate cells).
    /// </summary>
    internal static class OcrLayout
    {
        /// <summary>Two words share a row when they overlap vertically by at least this much of the shorter one.</summary>
        private const float RowOverlap = 0.5f;

        /// <summary>A horizontal gap wider than this many text heights separates columns, not words.</summary>
        private const float ColumnGap = 1.6f;

        public static string Rows(IEnumerable<OcrWord> words)
        {
            var rows = new List<List<OcrWord>>();
            foreach (OcrWord w in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.Box.Top))
            {
                List<OcrWord> row = rows.FirstOrDefault(r => SameRow(r, w));
                if (row == null) rows.Add(row = new List<OcrWord>());
                row.Add(w);
            }

            var sb = new StringBuilder();
            foreach (List<OcrWord> row in rows.OrderBy(r => r.Average(w => w.Box.Top + w.Box.Height / 2)))
            {
                List<OcrWord> ordered = row.OrderBy(w => w.Box.Left).ToList();
                float height = Median(ordered.Select(w => w.Box.Height));
                if (sb.Length > 0) sb.Append(Environment.NewLine);
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (i > 0)
                    {
                        float gap = ordered[i].Box.Left - ordered[i - 1].Box.Right;
                        sb.Append(gap > height * ColumnGap ? '\t' : ' ');
                    }
                    sb.Append(FixDigits(ordered[i].Text, i + 1 < ordered.Count ? ordered[i + 1].Text : null));
                }
            }
            return sb.ToString();
        }

        // Letters OCR puts where a digit was: 1 -> I l | (and Turkish İ ı), 0 -> O o.
        private static readonly Dictionary<char, char> DigitLookalikes = new Dictionary<char, char>
        {
            ['I'] = '1', ['l'] = '1', ['|'] = '1', ['İ'] = '1', ['ı'] = '1', ['O'] = '0', ['o'] = '0',
        };

        // English "May" is left out on purpose: "I may" is a sentence, not a date.
        private static readonly HashSet<string> Months = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ocak", "şubat", "mart", "nisan", "mayıs", "haziran", "temmuz", "ağustos", "eylül", "ekim", "kasım", "aralık",
            "january", "february", "march", "april", "june", "july", "august", "september", "october", "november", "december",
        };

        /// <summary>
        /// Turns look-alike letters back into digits, only where the context
        /// says it's a number: inside a token that already has a digit and no
        /// other letters ("2O26", "I2,", "II.11.2026"), or a lone "I"/"II"
        /// right before a month ("II Kasım"). "World War II", "USD", "A1B2"
        /// and "I love" stay as they are.
        /// </summary>
        internal static string FixDigits(string token, string next)
        {
            if (token.IndexOf('\'') >= 0 || token.IndexOf('’') >= 0) return token; // Turkish suffix: "5'ı", "1'i"
            bool hasDigit = token.Any(char.IsDigit);
            bool onlyLookalikeLetters = token.All(c => !char.IsLetter(c) || DigitLookalikes.ContainsKey(c)) && token.Any(DigitLookalikes.ContainsKey);
            if (hasDigit && onlyLookalikeLetters) return Map(token);

            bool loneOne = token.Length <= 2 && token.All(c => c == 'I' || c == 'l' || c == '|');
            if (loneOne && next != null && Months.Contains(next.Trim(',', '.', ':'))) return Map(token);
            return token;
        }

        private static string Map(string token) =>
            new string(token.Select(c => DigitLookalikes.TryGetValue(c, out char d) ? d : c).ToArray());

        private static bool SameRow(List<OcrWord> row, OcrWord w)
        {
            float top = row.Min(x => x.Box.Top), bottom = row.Max(x => x.Box.Bottom);
            float overlap = Math.Min(bottom, w.Box.Bottom) - Math.Max(top, w.Box.Top);
            float shorter = Math.Min(Median(row.Select(x => x.Box.Height)), w.Box.Height);
            return overlap >= shorter * RowOverlap;
        }

        private static float Median(IEnumerable<float> values)
        {
            List<float> v = values.OrderBy(x => x).ToList();
            return v.Count == 0 ? 0 : v[v.Count / 2];
        }
    }
}
