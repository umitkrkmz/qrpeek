// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QRPeek.Core
{
    internal enum WifiResult
    {
        Connected,
        NoAdapter,
        /// <summary>Profile saved, but no connection within the timeout: out of range or wrong password.</summary>
        NotConnected,
        /// <summary>
        /// Connect was requested, but Windows won't tell us the current network
        /// (location permission denied: Windows 11 24H2+ treats Wi-Fi details as
        /// location data). The connection may well be up; we just can't confirm it.
        /// </summary>
        Unverified,
        /// <summary>Windows refused the connect request itself (location permission denied).</summary>
        LocationDenied,
    }

    /// <summary>
    /// Joins the Wi-Fi network from a WIFI: QR code through the Windows Native
    /// Wifi API (wlanapi.dll): save the profile, ask to connect, then wait until
    /// Windows really reports the connection.
    ///
    /// Why not netsh: "netsh wlan add profile user=current" fails when the same
    /// network is already saved for all users (the usual case for a network
    /// joined before), and "netsh wlan connect" only means "request sent".
    /// </summary>
    internal static class WifiConnector
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        public static Task<WifiResult> ConnectAsync(WifiInfo wifi) => Task.Run(() => Connect(wifi));

        private static WifiResult Connect(WifiInfo wifi)
        {
            if (wifi == null || string.IsNullOrEmpty(wifi.Ssid)) return WifiResult.NotConnected;

            IntPtr handle;
            try
            {
                if (WlanOpenHandle(2, IntPtr.Zero, out _, out handle) != 0) return WifiResult.NoAdapter;
            }
            catch (DllNotFoundException)
            {
                return WifiResult.NoAdapter; // WLAN service not installed (some Server/VM images)
            }

            try
            {
                List<Guid> interfaces = Interfaces(handle);
                if (interfaces.Count == 0) return WifiResult.NoAdapter;

                // Adapters like Wi-Fi 7 "HBS" show up as two interfaces; the
                // first one is the one Windows connects with.
                Guid iface = interfaces[0];
                string xml = BuildProfile(wifi);

                // All-user profile, overwriting a saved one: this is what the
                // Windows Wi-Fi flyout does when you join with a new password.
                // If policy forbids that, try a per-user profile; if that fails
                // too, a saved profile may still work, so connect anyway.
                if (WlanSetProfile(handle, ref iface, 0, xml, null, true, IntPtr.Zero, out _) != 0)
                {
                    WlanSetProfile(handle, ref iface, WLAN_PROFILE_USER, xml, null, true, IntPtr.Zero, out _);
                }

                var p = new WLAN_CONNECTION_PARAMETERS
                {
                    wlanConnectionMode = 0, // wlan_connection_mode_profile
                    strProfile = wifi.Ssid,
                    dot11BssType = 1,       // infrastructure
                };
                uint rc = WlanConnect(handle, ref iface, ref p, IntPtr.Zero);
                if (rc == ERROR_ACCESS_DENIED) return WifiResult.LocationDenied;
                if (rc != 0) return WifiResult.NotConnected;

                DateTime until = DateTime.UtcNow + Timeout;
                while (DateTime.UtcNow < until)
                {
                    Thread.Sleep(500);
                    bool? connected = IsConnectedTo(handle, iface, wifi.Ssid);
                    if (connected == null) return WifiResult.Unverified;
                    if (connected == true) return WifiResult.Connected;
                }
                return WifiResult.NotConnected;
            }
            finally
            {
                WlanCloseHandle(handle, IntPtr.Zero);
            }
        }

        private static List<Guid> Interfaces(IntPtr handle)
        {
            var list = new List<Guid>();
            if (WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr ptr) != 0) return list;
            try
            {
                // WLAN_INTERFACE_INFO_LIST: DWORD count, DWORD index, then
                // WLAN_INTERFACE_INFO[] (GUID 16 + WCHAR[256] 512 + state 4 = 532 bytes).
                int count = Marshal.ReadInt32(ptr, 0);
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = IntPtr.Add(ptr, 8 + i * 532);
                    list.Add((Guid)Marshal.PtrToStructure(item, typeof(Guid)));
                }
            }
            finally
            {
                WlanFreeMemory(ptr);
            }
            return list;
        }

        /// <summary>True/false, or null when Windows won't say (location permission denied).</summary>
        private static bool? IsConnectedTo(IntPtr handle, Guid iface, string profile)
        {
            uint rc = WlanQueryInterface(handle, ref iface, wlan_intf_opcode_current_connection, IntPtr.Zero,
                out _, out IntPtr data, IntPtr.Zero);
            if (rc == ERROR_ACCESS_DENIED) return null;
            if (rc != 0) return false; // not connected yet (ERROR_INVALID_STATE)
            try
            {
                // WLAN_CONNECTION_ATTRIBUTES: isState (DWORD), wlanConnectionMode (DWORD), WCHAR strProfileName[256]
                int state = Marshal.ReadInt32(data, 0);
                string name = Marshal.PtrToStringUni(IntPtr.Add(data, 8));
                return state == wlan_interface_state_connected && name == profile;
            }
            finally
            {
                WlanFreeMemory(data);
            }
        }

        internal static string BuildProfile(WifiInfo wifi)
        {
            string auth, enc, keyType = "passPhrase";
            string sec = (wifi.Security ?? "").ToUpperInvariant();
            if (wifi.IsOpen) { auth = "open"; enc = "none"; }
            else if (sec == "WEP") { auth = "open"; enc = "WEP"; keyType = "networkKey"; }
            else if (sec == "SAE" || sec == "WPA3") { auth = "WPA3SAE"; enc = "AES"; }
            else { auth = "WPA2PSK"; enc = "AES"; } // "WPA" in QR codes means WPA/WPA2 personal

            string name = SecurityElement.Escape(wifi.Ssid);
            string hex = BitConverter.ToString(Encoding.UTF8.GetBytes(wifi.Ssid)).Replace("-", "");

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?>\n");
            sb.Append("<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\n");
            sb.Append("  <name>").Append(name).Append("</name>\n");
            sb.Append("  <SSIDConfig>\n    <SSID>\n");
            sb.Append("      <hex>").Append(hex).Append("</hex>\n");
            sb.Append("      <name>").Append(name).Append("</name>\n");
            sb.Append("    </SSID>\n");
            sb.Append("    <nonBroadcast>").Append(wifi.Hidden ? "true" : "false").Append("</nonBroadcast>\n");
            sb.Append("  </SSIDConfig>\n");
            sb.Append("  <connectionType>ESS</connectionType>\n");
            sb.Append("  <connectionMode>auto</connectionMode>\n");
            sb.Append("  <MSM>\n    <security>\n      <authEncryption>\n");
            sb.Append("        <authentication>").Append(auth).Append("</authentication>\n");
            sb.Append("        <encryption>").Append(enc).Append("</encryption>\n");
            sb.Append("        <useOneX>false</useOneX>\n");
            sb.Append("      </authEncryption>\n");
            if (!wifi.IsOpen)
            {
                sb.Append("      <sharedKey>\n");
                sb.Append("        <keyType>").Append(keyType).Append("</keyType>\n");
                sb.Append("        <protected>false</protected>\n");
                sb.Append("        <keyMaterial>").Append(SecurityElement.Escape(wifi.Password)).Append("</keyMaterial>\n");
                sb.Append("      </sharedKey>\n");
            }
            sb.Append("    </security>\n  </MSM>\n</WLANProfile>\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------ wlanapi.dll

        private const uint WLAN_PROFILE_USER = 0x00000002;
        private const uint ERROR_ACCESS_DENIED = 5;
        private const int wlan_intf_opcode_current_connection = 7;
        private const int wlan_interface_state_connected = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_PARAMETERS
        {
            public int wlanConnectionMode;
            [MarshalAs(UnmanagedType.LPWStr)] public string strProfile;
            public IntPtr pDot11Ssid;
            public IntPtr pDesiredBssidList;
            public int dot11BssType;
            public uint dwFlags;
        }

        [DllImport("wlanapi.dll")]
        private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr clientHandle);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr memory);

        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
        private static extern uint WlanSetProfile(IntPtr clientHandle, ref Guid interfaceGuid, uint flags, string profileXml,
            string allUserProfileSecurity, bool overwrite, IntPtr reserved, out uint reasonCode);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanConnect(IntPtr clientHandle, ref Guid interfaceGuid, ref WLAN_CONNECTION_PARAMETERS parameters, IntPtr reserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanQueryInterface(IntPtr clientHandle, ref Guid interfaceGuid, int opCode, IntPtr reserved,
            out uint dataSize, out IntPtr data, IntPtr opcodeValueType);
    }
}
