// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using QRCoder;

namespace QRPeek.Core
{
    internal static class QrGenerator
    {
        public static string WifiPayload(string ssid, string password, string security, bool hidden)
        {
            string t = security == "nopass" || string.IsNullOrEmpty(password) ? "nopass" : security;
            string s = "WIFI:T:" + t + ";S:" + QrContent.EscapeWifi(ssid) + ";";
            if (t != "nopass") s += "P:" + QrContent.EscapeWifi(password) + ";";
            if (hidden) s += "H:true;";
            return s + ";";
        }

        /// <summary>
        /// A logo hides part of the code, so it always gets the highest error
        /// correction (H, ~30% of the code can be missing) whatever was chosen.
        /// </summary>
        public static QRCodeGenerator.ECCLevel EffectiveEcc(QRCodeGenerator.ECCLevel chosen, QrStyle style) =>
            style?.Logo != null ? QRCodeGenerator.ECCLevel.H : chosen;

        public static Bitmap Render(string payload, QRCodeGenerator.ECCLevel ecc, QrStyle style, int targetPx)
        {
            using (QRCodeData data = Create(payload, EffectiveEcc(ecc, style)))
            {
                return QrRenderer.Render(data, style, targetPx);
            }
        }

        public static byte[] Png(string payload, QRCodeGenerator.ECCLevel ecc, QrStyle style, int targetPx)
        {
            using (QRCodeData data = Create(payload, EffectiveEcc(ecc, style)))
            {
                return QrRenderer.Png(data, style, targetPx);
            }
        }

        public static string Svg(string payload, QRCodeGenerator.ECCLevel ecc, QrStyle style)
        {
            using (QRCodeData data = Create(payload, EffectiveEcc(ecc, style)))
            {
                return QrRenderer.Svg(data, style);
            }
        }

        private static QRCodeData Create(string payload, QRCodeGenerator.ECCLevel ecc)
        {
            using (var gen = new QRCodeGenerator())
            {
                return gen.CreateQrCode(payload, ecc, forceUtf8: NeedsUtf8(payload));
            }
        }

        // Plain ASCII stays in the most compact mode every scanner reads;
        // anything else (ç, ğ, ı, ş, emoji...) is encoded as UTF-8.
        private static bool NeedsUtf8(string s)
        {
            foreach (char c in s)
            {
                if (c > 127) return true;
            }
            return false;
        }
    }
}
