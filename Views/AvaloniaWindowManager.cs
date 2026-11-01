using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PinNote.Services;
using PinNote.ViewModels;

namespace PinNote.Views
{
    public class AvaloniaWindowManager : IWindowManager
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IClassicDesktopStyleApplicationLifetime _desktop;
        private readonly Dictionary<Guid, AvaloniaNoteWindow> _openWindows = new();
        private AvaloniaDashboardWindow? _dashboardWindow;
        private AvaloniaInstructionsWindow? _instructionsWindow;

        public AvaloniaWindowManager(MainViewModel mainViewModel, IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainViewModel = mainViewModel;
            _desktop = desktop;
        }

        public void OpenNote(NoteViewModel viewModel)
        {
            if (_openWindows.TryGetValue(viewModel.Id, out var existingWindow))
            {
                if (!existingWindow.IsVisible)
                {
                    existingWindow.Show();
                }

                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.Activate();
                existingWindow.Topmost = viewModel.IsPinned;
                return;
            }

            var window = new AvaloniaNoteWindow(viewModel);
            window.Closed += (_, _) => _openWindows.Remove(viewModel.Id);
            _openWindows[viewModel.Id] = window;
            window.Show();
            window.Activate();
        }

        public void CloseNote(Guid noteId)
        {
            if (_openWindows.TryGetValue(noteId, out var window))
            {
                window.Close();
                _openWindows.Remove(noteId);
            }
        }

        public void BringNotesOnTop()
        {
            foreach (var window in _openWindows.Values)
            {
                window.Show();
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
                window.Activate();
            }
        }

        public void HideAllNotes()
        {
            foreach (var window in _openWindows.Values)
            {
                window.Hide();
            }
        }

        public void ShowAllNotes(bool minimizeDashboard = false)
        {
            if (minimizeDashboard)
            {
                _dashboardWindow?.Hide();
            }

            foreach (var note in _mainViewModel.Notes)
            {
                OpenNote(note);
            }
        }

        public void ShowDashboard()
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new AvaloniaDashboardWindow(_mainViewModel);
                _dashboardWindow.Closed += (_, _) => _dashboardWindow = null;
            }

            _desktop.MainWindow = _dashboardWindow;
            _dashboardWindow.Show();
            if (_dashboardWindow.WindowState == WindowState.Minimized)
            {
                _dashboardWindow.WindowState = WindowState.Normal;
            }
            _dashboardWindow.Activate();
        }

        public void HideDashboard()
        {
            _dashboardWindow?.Hide();
        }

        public void ShowInstructions()
        {
            if (_instructionsWindow != null && _instructionsWindow.IsVisible)
            {
                _instructionsWindow.Activate();
                return;
            }

            _instructionsWindow = new AvaloniaInstructionsWindow();
            _instructionsWindow.Closed += (_, _) => _instructionsWindow = null;
            _instructionsWindow.Show();
        }

        public bool ConfirmDelete(string noteTitle)
        {
            return ConfirmDeleteAsync(noteTitle).GetAwaiter().GetResult();
        }

        public async Task<bool> ConfirmDeleteAsync(string noteTitle)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                return await ShowDeleteDialogInternalAsync(noteTitle);
            }

            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowDeleteDialogInternalAsync(noteTitle));
        }

        private async Task<bool> ShowDeleteDialogInternalAsync(string noteTitle)
        {
            var dialog = new ConfirmDeleteDialog(noteTitle);
            Window? owner = _desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible)
                         ?? (_dashboardWindow != null && _dashboardWindow.IsVisible ? (Window)_dashboardWindow : null)
                         ?? (_openWindows.Values.Count > 0 ? (Window?)_openWindows.Values.FirstOrDefault(w => w.IsVisible) : null);

            if (owner != null)
            {
                return await dialog.ShowDialog<bool>(owner);
            }

            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => tcs.TrySetResult(dialog.Result);
            dialog.Show();
            return await tcs.Task;
        }

        public void RunOnUiThread(Action action)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Invoke(action);
            }
        }

        public void Shutdown()
        {
            FlushPendingEdits();

            foreach (var window in new List<AvaloniaNoteWindow>(_openWindows.Values))
            {
                window.Close();
            }
            _openWindows.Clear();

            _instructionsWindow?.Close();
            _dashboardWindow?.ForceClose();

            _desktop.Shutdown();
        }

        public void ApplyTheme(string themeMode)
        {
            if (Application.Current is AvaloniaApp app)
            {
                app.SetTheme(themeMode);
            }
        }

        public void SetShowInTaskbar(bool show)
        {
            if (_dashboardWindow != null)
            {
                _dashboardWindow.ShowInTaskbar = show;
            }
        }

        public void ShowErrorMessage(string title, string message)
        {
            NotificationService.ShowNotification(title, message);
        }

        public void ShowMessage(string title, string message)
        {
            NotificationService.ShowNotification(title, message);
        }

        public void FlushPendingEdits()
        {
            foreach (var window in _openWindows.Values)
            {
                window.FlushPendingEdits();
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title, string filter)
        {
            Window? owner = _desktop.Windows.FirstOrDefault(w => w.IsVisible)
                         ?? (_dashboardWindow != null && _dashboardWindow.IsVisible ? (Window)_dashboardWindow : null)
                         ?? _openWindows.Values.FirstOrDefault();

            if (owner == null) return null;

            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var files = await owner.StorageProvider.OpenFilePickerAsync(options);
            if (files != null && files.Count > 0)
            {
                return files[0].Path.LocalPath;
            }

            return null;
        }

        public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filter)
        {
            Window? owner = _desktop.Windows.FirstOrDefault(w => w.IsVisible)
                         ?? (_dashboardWindow != null && _dashboardWindow.IsVisible ? (Window)_dashboardWindow : null)
                         ?? _openWindows.Values.FirstOrDefault();

            if (owner == null) return null;

            var options = new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                DefaultExtension = "json"
            };

            var file = await owner.StorageProvider.SaveFilePickerAsync(options);
            return file?.Path.LocalPath;
        }
    }
}
