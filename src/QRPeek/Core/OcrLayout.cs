// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// When one OCR language read the image best overall (e.g. English, for
        /// an English invoice), it still mangles words in another language
        /// ("ümit" → "omit", "SİNCAN" → "SiNCAN"). For each word of
        /// <paramref name="best"/>, take the other reading of the same spot if
        /// that one has letters the best reading can't produce (ç ğ ö ş ü, or
        /// İ in a capitalized word), looks like the same word without them,
        /// and the best word isn't a known receipt label.
        /// </summary>
        public static List<OcrWord> KeepNativeLetters(IReadOnlyList<OcrWord> best, IReadOnlyList<OcrWord> other, Func<string, bool> isKnownLabelWord)
        {
            var result = new List<OcrWord>(best.Count);
            foreach (OcrWord w in best)
            {
                OcrWord? match = null;
                foreach (OcrWord o in other)
                {
                    if (Overlap(w.Box, o.Box) >= 0.5f) { match = o; break; }
                }
                if (match.HasValue && HasNativeLetters(match.Value.Text) && !HasNativeLetters(w.Text)
                    && !isKnownLabelWord(w.Text.Trim(',', '.', ':'))
                    && ReceiptTerms.Distance(Plain(match.Value.Text), Plain(w.Text)) <= 2)
                {
                    result.Add(new OcrWord(match.Value.Text, w.Box));
                }
                else
                {
                    result.Add(w);
                }
            }
            return result;
        }

        private static bool HasNativeLetters(string s) =>
            s.IndexOfAny("çğöşüÇĞÖŞÜ".ToCharArray()) >= 0
            || (s.IndexOf('İ') >= 0 && s.Where(char.IsLetter).All(char.IsUpper)); // "SİNCAN", but not "Anthropİc"

        /// <summary>Lower case without Turkish diacritics, to compare two readings of one word.</summary>
        private static string Plain(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'ç': case 'Ç': sb.Append('c'); break;
                    case 'ğ': case 'Ğ': sb.Append('g'); break;
                    case 'ö': case 'Ö': sb.Append('o'); break;
                    case 'ş': case 'Ş': sb.Append('s'); break;
                    case 'ü': case 'Ü': sb.Append('u'); break;
                    case 'ı': case 'İ': case 'I': sb.Append('i'); break;
                    default: sb.Append(char.ToLowerInvariant(c)); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Intersection over the smaller box: 1 = one box inside the other.</summary>
        private static float Overlap(RectangleF a, RectangleF b)
        {
            RectangleF i = RectangleF.Intersect(a, b);
            if (i.Width <= 0 || i.Height <= 0) return 0;
            float smaller = Math.Min(a.Width * a.Height, b.Width * b.Height);
            return smaller <= 0 ? 0 : i.Width * i.Height / smaller;
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

            // "S4.oo" → "$4.00": an S in front of something shaped exactly like a price with cents.
            if (token.Length >= 5 && token[0] == 'S')
            {
                string rest = token.Substring(1);
                if (rest.Any(char.IsDigit) && rest.All(c => !char.IsLetter(c) || DigitLookalikes.ContainsKey(c))
                    && Regex.IsMatch(Map(rest), @"^\d{1,3}([.,]\d{3})*[.,]\d{2}$"))
                {
                    return "$" + Map(rest);
                }
            }

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
