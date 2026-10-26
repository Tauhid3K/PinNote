using System;

namespace PinNote.Services
{
    public interface ITrayService : IDisposable
    {
        bool EnsureVisible();
        void SetVisible(bool isVisible);
        void ShowNotification(string title, string message);
        void SetToolTip(string toolTip);
    }
}
