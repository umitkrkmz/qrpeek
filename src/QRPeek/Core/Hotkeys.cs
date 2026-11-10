// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace QRPeek.Core
{
    /// <summary>One of the fixed shortcut choices: four modifier combinations on a letter, or off.</summary>
    internal sealed class HotkeyPreset
    {
        private HotkeyPreset(string id, string display, uint modifiers, uint key)
        {
            Id = id;
            DisplayRaw = display;
            Modifiers = modifiers;
            Key = key;
        }

        public string Id { get; }
        private string DisplayRaw { get; }
        public string Display => DisplayRaw ?? S.HotkeyNone;
        public uint Modifiers { get; }
        public uint Key { get; }
        public bool IsNone => Key == 0;

        /// <summary>The choices for a letter key; the first is the default.</summary>
        public static IReadOnlyList<HotkeyPreset> For(char letter)
        {
            char k = char.ToUpperInvariant(letter);
            string l = char.ToLowerInvariant(letter).ToString();
            uint vk = k; // VK_A..VK_Z equal the ASCII capitals
            return new[]
            {
                new HotkeyPreset("ctrl+alt+" + l, "Ctrl + Alt + " + k, Native.MOD_CONTROL | Native.MOD_ALT, vk),
                new HotkeyPreset("ctrl+shift+" + l, "Ctrl + Shift + " + k, Native.MOD_CONTROL | Native.MOD_SHIFT, vk),
                new HotkeyPreset("alt+shift+" + l, "Alt + Shift + " + k, Native.MOD_ALT | Native.MOD_SHIFT, vk),
                new HotkeyPreset("win+alt+" + l, "Win + Alt + " + k, Native.MOD_WIN | Native.MOD_ALT, vk),
                new HotkeyPreset("none", null, 0, 0),
            };
        }

        public static HotkeyPreset Find(string id, char letter)
        {
            IReadOnlyList<HotkeyPreset> all = For(letter);
            return all.FirstOrDefault(p => p.Id == id) ?? all[0];
        }

        public override string ToString() => Display;
    }

    /// <summary>System-wide hotkeys through RegisterHotKey on a message-only window.</summary>
    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HwndSource _source;
        private readonly HashSet<int> _registered = new HashSet<int>();

        /// <summary>Raised with the id passed to <see cref="Register"/>.</summary>
        public event Action<int> Pressed;

        public HotkeyManager()
        {
            var p = new HwndSourceParameters("QRPeekHotkey")
            {
                ParentWindow = Native.HWND_MESSAGE,
                WindowStyle = 0,
            };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        /// <summary>False when another app already owns the combination.</summary>
        public bool Register(int id, HotkeyPreset preset)
        {
            Unregister(id);
            if (preset == null || preset.IsNone) return true;
            bool ok = Native.RegisterHotKey(_source.Handle, id, preset.Modifiers | Native.MOD_NOREPEAT, preset.Key);
            if (ok) _registered.Add(id);
            return ok;
        }

        public void Unregister(int id)
        {
            if (_registered.Remove(id)) Native.UnregisterHotKey(_source.Handle, id);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY && _registered.Contains(wParam.ToInt32()))
            {
                handled = true;
                Pressed?.Invoke(wParam.ToInt32());
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            foreach (int id in _registered.ToList()) Unregister(id);
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }
}
