using System;
using System.Diagnostics;

namespace PinNote.Services
{
    public class LinuxNotificationService : INotificationService
    {
        public bool ShowNotification(string title, string message)
        {
            if (TryNotifySend(title, message)) return true;
            if (TryKDialog(title, message)) return true;
            if (TryZenity(title, message)) return true;
            return false;
        }

        private static bool TryNotifySend(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "notify-send",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("-a");
                psi.ArgumentList.Add("PinNote");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add("pinnote");
                psi.ArgumentList.Add(title ?? string.Empty);
                psi.ArgumentList.Add(message ?? string.Empty);

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                bool finished = proc.WaitForExit(3000);
                return finished && proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"notify-send error: {ex.Message}");
                return false;
            }
        }

        private static bool TryKDialog(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "kdialog",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("--passivepopup");
                psi.ArgumentList.Add(message ?? string.Empty);
                psi.ArgumentList.Add("5");
                psi.ArgumentList.Add("--title");
                psi.ArgumentList.Add(title ?? "PinNote");

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                bool finished = proc.WaitForExit(2000);
                return finished && proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryZenity(string title, string message)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "zenity",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("--notification");
                psi.ArgumentList.Add($"--text={title}: {message}");

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                bool finished = proc.WaitForExit(2000);
                return finished && proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
