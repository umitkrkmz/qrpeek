// QRPeek - read QR codes on your screen, create QR codes
// Copyright (C) 2026 QRPeek
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace QRPeek.Core
{
    internal enum AutostartState
    {
        Off,
        On,
        /// <summary>Store build: the user turned it off in Task Manager / Settings, so only they can turn it back on.</summary>
        BlockedByUser,
        /// <summary>Store build: turned off by group policy.</summary>
        BlockedByPolicy,
    }

    /// <summary>
    /// "Start with Windows".
    /// GitHub build: the HKCU Run key, started with --tray.
    /// Store (MSIX) build: the Run key doesn't apply to packaged apps (writes go
    /// to the package's private registry), so it's the StartupTask declared in
    /// the package manifest (TaskId below). Windows then asks the user once.
    /// </summary>
    internal static class Autostart
    {
        /// <summary>Must match &lt;uap5:StartupTask TaskId&gt; in packaging/msix/AppxManifest.xml.</summary>
        public const string StartupTaskId = "QRPeekStartup";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "QRPeek";

        public static async Task<AutostartState> GetAsync()
        {
            if (!Native.IsPackaged()) return RunKeyEnabled() ? AutostartState.On : AutostartState.Off;
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId).AsTask();
            return Map(task.State);
        }

        /// <summary>Returns the state after the change; it can differ from the request (user or policy blocked it).</summary>
        public static async Task<AutostartState> SetAsync(bool enabled)
        {
            if (!Native.IsPackaged())
            {
                SetRunKey(enabled);
                return RunKeyEnabled() ? AutostartState.On : AutostartState.Off;
            }

            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId).AsTask();
            if (enabled)
            {
                return Map(await task.RequestEnableAsync().AsTask());
            }
            task.Disable();
            return Map(task.State);
        }

        /// <summary>True when Windows started the Store build at sign-in (start quietly in the tray).</summary>
        public static bool LaunchedAtStartup()
        {
            if (!Native.IsPackaged()) return false;
            try
            {
                var args = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                return args != null && args.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.Runtime.InteropServices.COMException)
            {
                return false;
            }
        }

        private static AutostartState Map(Windows.ApplicationModel.StartupTaskState s)
        {
            switch (s)
            {
                case Windows.ApplicationModel.StartupTaskState.Enabled:
                case Windows.ApplicationModel.StartupTaskState.EnabledByPolicy:
                    return AutostartState.On;
                case Windows.ApplicationModel.StartupTaskState.DisabledByUser:
                    return AutostartState.BlockedByUser;
                case Windows.ApplicationModel.StartupTaskState.DisabledByPolicy:
                    return AutostartState.BlockedByPolicy;
                default:
                    return AutostartState.Off;
            }
        }

        private static bool RunKeyEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                return k?.GetValue(ValueName) is string;
            }
        }

        private static void SetRunKey(bool enabled)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (k == null) return;
                if (enabled)
                {
                    string exe = Process.GetCurrentProcess().MainModule.FileName;
                    k.SetValue(ValueName, "\"" + exe + "\" --tray");
                }
                else if (k.GetValue(ValueName) != null)
                {
                    k.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
