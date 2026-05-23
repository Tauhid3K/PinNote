using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace PinNote.Services
{
    public class StartupService
    {
        private const string AppName = "PinNote";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;

                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, exePath);
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting startup: {ex.Message}");
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
