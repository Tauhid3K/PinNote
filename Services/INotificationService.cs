namespace PinNote.Services
{
    /// <summary>
    /// Shared abstraction for delivering platform-specific desktop notifications.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Displays a desktop notification.
        /// </summary>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message text.</param>
        /// <returns>True if the notification was successfully dispatched or delivered; false otherwise.</returns>
        bool ShowNotification(string title, string message);
    }
}
