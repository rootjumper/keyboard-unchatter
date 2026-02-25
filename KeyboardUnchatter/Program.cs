using System;
using System.Windows.Forms;
using System.Windows;
using System.Configuration;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Win32;
using System.Security.Principal;

namespace KeyboardUnchatter
{
    static class Program
    {
        private static InputHook _inputHook;
        private static KeyboardMonitor _keyboardMonitor;
        private static MainWindow _mainWindow;

        #region Get/Set
        public static InputHook InputHook
        {
            get => _inputHook;
        }

        public static KeyboardMonitor KeyboardMonitor
        {
            get => _keyboardMonitor;
        }

        #endregion

        [STAThread]
        static void Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (_inputHook = new InputHook())
            {
                // PowerModeChanged abonnieren
                SystemEvents.PowerModeChanged += (s, e) =>
                {
                    if (e.Mode == PowerModes.Resume)
                    {
                        _inputHook.Rehook();
                    }
                };

                _keyboardMonitor = new KeyboardMonitor();

                _mainWindow = new MainWindow();
                _mainWindow.FormClosed += Close;

                if(!Properties.Settings.Default.openMinimized)
                {
                    _mainWindow.Show();
                }

                Application.Run();
            }
        }

        private static void Close(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private static readonly string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(bool enable)
        {
            string appName = Application.ProductName;
            string exePath = Application.ExecutablePath;

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
            {
                if (enable)
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                    // Remove admin task if registry startup is being enabled
                    SetStartupAsAdmin(false);
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
        }

        public static bool GetStartup()
        {
            // Registry key for user-specific startup programs
            string appName = Application.ProductName;
            string exePath = Application.ExecutablePath;

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, false))
            {
                if (key == null)
                    return false;

                var value = key.GetValue(appName) as string;
                if (string.IsNullOrEmpty(value))
                    return false;

                // Compare the stored path (trim quotes) with the current executable path
                return string.Equals(
                    value.Trim('"'),
                    exePath,
                    StringComparison.InvariantCultureIgnoreCase
                );
            }
        }

        public static bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static void RelaunchAsAdmin()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Verb = "runas",
                UseShellExecute = true
            };
            try
            {
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC prompt; do nothing
            }
        }

        private static readonly string _taskName = Application.ProductName;

        public static void SetStartupAsAdmin(bool enable)
        {
            if (enable)
            {
                string exePath = Application.ExecutablePath;
                string args = $"/create /tn \"{_taskName}\" /tr \"\\\"{exePath}\\\"\" /sc ONLOGON /rl HIGHEST /f";
                ProcessStartInfo psi;
                if (IsRunningAsAdmin())
                {
                    psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }
                else
                {
                    psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                }
                try
                {
                    var p = Process.Start(psi);
                    p?.WaitForExit();
                    // Remove regular registry startup entry (mutually exclusive)
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                    {
                        key?.DeleteValue(Application.ProductName, false);
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User cancelled UAC prompt; do nothing
                }
            }
            else
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/delete /tn \"{_taskName}\" /f")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                try { Process.Start(psi)?.WaitForExit(); } catch { }
            }
        }

        public static bool GetStartupAsAdmin()
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/query /tn \"{_taskName}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                var p = Process.Start(psi);
                p?.WaitForExit();
                return p?.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
