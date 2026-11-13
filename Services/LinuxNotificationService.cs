using System;
using System.Diagnostics;

namespace PinNote.Services
{
    public class LinuxNotificationService : INotificationService
    {
        public bool ShowNotification(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"{Escape(title)}\" \"{Escape(message)}\" --app-name=PinNote",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return false;
                }

                // Wait briefly for notify-send to submit to notification daemon
                bool finished = proc.WaitForExit(3000);
                return finished && proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxNotificationService error: {ex}");
                return false;
            }
        }

        private static string Escape(string s) => s?.Replace("\"", "\\\"") ?? string.Empty;
    }
}
