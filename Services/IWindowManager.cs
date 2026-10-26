using System;
using System.Threading.Tasks;
using PinNote.ViewModels;

namespace PinNote.Services
{
    public interface IWindowManager
    {
        void OpenNote(NoteViewModel viewModel);
        void CloseNote(Guid noteId);
        void BringNotesOnTop();
        void HideAllNotes();
        void ShowAllNotes(bool minimizeDashboard = false);
        void ShowDashboard();
        void HideDashboard();
        void ShowInstructions();
        bool ConfirmDelete(string noteTitle);
        Task<bool> ConfirmDeleteAsync(string noteTitle);
        void RunOnUiThread(Action action);
        void Shutdown();
        void ApplyTheme(string themeMode);
        void SetShowInTaskbar(bool show);
        void ShowErrorMessage(string title, string message);
    }
}
