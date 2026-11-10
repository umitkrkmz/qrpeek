// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QRPeek.Core
{
    /// <summary>
    /// Reads a flat JSON object ({"key": "text" | 12.5 | true | null, ...}) into
    /// strings. Enough for e-Fatura QR codes; anything nested or malformed
    /// returns null instead of throwing. (System.Web.Extensions'
    /// JavaScriptSerializer would pull System.Web into a WPF build.)
    /// </summary>
    internal static class FlatJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            try
            {
                Skip(json, ref i);
                if (json[i++] != '{') return null;
                Skip(json, ref i);
                if (json[i] == '}') return result;
                while (true)
                {
                    Skip(json, ref i);
                    string key = ReadString(json, ref i);
                    Skip(json, ref i);
                    if (json[i++] != ':') return null;
                    Skip(json, ref i);
                    string value = ReadValue(json, ref i); // null only for a literal null
                    result[key] = value ?? "";
                    Skip(json, ref i);
                    char c = json[i++];
                    if (c == '}') break;
                    if (c != ',') return null;
                }
                Skip(json, ref i);
                return i == json.Length ? result : null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadValue(string s, ref int i)
        {
            char c = s[i];
            if (c == '"') return ReadString(s, ref i);
            if (c == '{' || c == '[') throw new FormatException("nested");
            int start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i])) i++;
            string token = s.Substring(start, i - start);
            if (token == "null") return null;
            if (token == "true" || token == "false") return token;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return token;
            throw new FormatException("bad token");
        }

        private static string ReadString(string s, ref int i)
        {
            if (s[i++] != '"') throw new FormatException("string expected");
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
        }
    }
}
