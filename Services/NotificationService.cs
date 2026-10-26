using System;
using System.Diagnostics;

namespace PinNote.Services
{
    public static class NotificationService
    {
        public static void ShowNotification(string title, string message)
        {
            if (OperatingSystem.IsWindows())
            {
                // On Windows, tray balloon or notification is managed via the app window/tray
            }
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    // Use standard Linux desktop notification daemon via notify-send if present
                    var psi = new ProcessStartInfo
                    {
                        FileName = "notify-send",
                        Arguments = $"\"{Escape(title)}\" \"{Escape(message)}\" --app-name=PinNote",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NotificationService Linux error: {ex}");
                }
            }
        }

        private static string Escape(string s) => s?.Replace("\"", "\\\"") ?? string.Empty;
    }
}
