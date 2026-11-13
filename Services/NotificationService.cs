using System;
using System.Diagnostics;

namespace PinNote.Services
{
    public static class NotificationService
    {
        private static INotificationService? _customProvider;

        /// <summary>
        /// Allows overriding the notification provider (e.g. for unit testing or platform-specific adapters).
        /// </summary>
        public static INotificationService? Provider
        {
            get => _customProvider;
            set => _customProvider = value;
        }

        /// <summary>
        /// Allows disabling system notifications (e.g. during unit tests or headless runs).
        /// Automatically defaults to true when running inside a test host process.
        /// </summary>
        public static bool IsDisabled { get; set; } =
            AppDomain.CurrentDomain.FriendlyName.Contains("testhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("PINNOTE_DISABLE_NOTIFICATIONS"), "1", StringComparison.OrdinalIgnoreCase);

        public static bool ShowNotification(string title, string message)
        {
            if (_customProvider != null)
            {
                return _customProvider.ShowNotification(title, message);
            }

            if (IsDisabled) return true;

            if (OperatingSystem.IsWindows())
            {
                var winService = new WindowsNotificationService();
                return winService.ShowNotification(title, message);
            }
            else if (OperatingSystem.IsLinux())
            {
                var linuxService = new LinuxNotificationService();
                return linuxService.ShowNotification(title, message);
            }

            return false;
        }
    }
}
