using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace PinNote.Services
{
    public class StartupService
    {
        private const string AppName = "PinNote";
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void SetStartup(bool enable)
        {
            if (OperatingSystem.IsWindows())
            {
                SetStartupWindows(enable);
            }
            else if (OperatingSystem.IsLinux())
            {
                SetStartupLinux(enable);
            }
        }

        public static bool IsStartupEnabled()
        {
            if (OperatingSystem.IsWindows())
            {
                return IsStartupEnabledWindows();
            }
            else if (OperatingSystem.IsLinux())
            {
                return IsStartupEnabledLinux();
            }

            return false;
        }

        [SupportedOSPlatform("windows")]
        private static void SetStartupWindows(bool enable)
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
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartupService.SetStartupWindows error: {ex}");
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool IsStartupEnabledWindows()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartupService.IsStartupEnabledWindows error: {ex}");
                return false;
            }
        }

        private static void SetStartupLinux(bool enable)
        {
            try
            {
                string desktopFilePath = GetLinuxAutostartFilePath();
                string directory = Path.GetDirectoryName(desktopFilePath)!;

                if (enable)
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    if (string.IsNullOrEmpty(exePath)) return;

                    string desktopFileContent =
$@"[Desktop Entry]
Type=Application
Version=1.0
Name=PinNote
Comment=PinNote Sticky Notes
Exec=""{exePath}"" --autostart
Terminal=false
Categories=Utility;
X-GNOME-Autostart-enabled=true
StartupNotify=false
";
                    File.WriteAllText(desktopFilePath, desktopFileContent);
                }
                else
                {
                    if (File.Exists(desktopFilePath))
                    {
                        File.Delete(desktopFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartupService.SetStartupLinux error: {ex}");
            }
        }

        private static bool IsStartupEnabledLinux()
        {
            try
            {
                string desktopFilePath = GetLinuxAutostartFilePath();
                return File.Exists(desktopFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartupService.IsStartupEnabledLinux error: {ex}");
                return false;
            }
        }

        private static string GetLinuxAutostartFilePath()
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(configHome))
            {
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(configHome, "autostart", "pinnote.desktop");
        }
    }
}
