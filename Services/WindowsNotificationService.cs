using System;
using System.Diagnostics;

namespace PinNote.Services
{
    public class WindowsNotificationService : INotificationService
    {
        public bool ShowNotification(string title, string message)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                string safeTitle = EscapeForXml(title);
                string safeMessage = EscapeForXml(message);

                string psCommand =
                    "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null; " +
                    "$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); " +
                    "$textNodes = $xml.GetElementsByTagName('text'); " +
                    $"$textNodes.Item(0).AppendChild($xml.CreateTextNode('{EscapeForSingleQuotes(safeTitle)}')) > $null; " +
                    $"$textNodes.Item(1).AppendChild($xml.CreateTextNode('{EscapeForSingleQuotes(safeMessage)}')) > $null; " +
                    "$toast = [Windows.UI.Notifications.ToastNotification]::new($xml); " +
                    "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('PinNote').Show($toast);";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                bool exited = proc.WaitForExit(4000);
                return exited && proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowsNotificationService error: {ex}");
                return false;
            }
        }

        private static string EscapeForXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }

        private static string EscapeForSingleQuotes(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("'", "''");
        }
    }
}
