using System;

namespace PinNote.Services
{
    public interface IGlobalShortcutService : IDisposable
    {
        bool RegisterQuickNoteShortcut(Action onTriggered);
        void Unregister();
    }
}
