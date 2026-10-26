#if WINDOWS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PinNote.Services;
using PinNote.ViewModels;

namespace PinNote.UI
{
    public class WpfWindowManager : IWindowManager
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Dictionary<Guid, NoteWindow> _openWindows = new();
        private MainDashboardWindow? _dashboardWindow;
        private InstructionsWindow? _instructionsWindow;

        public WpfWindowManager(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public void OpenNote(NoteViewModel viewModel)
        {
            if (_openWindows.TryGetValue(viewModel.Id, out var existingWindow))
            {
                if (!existingWindow.IsLoaded)
                {
                    _openWindows.Remove(viewModel.Id);
                }
                else
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
                    existingWindow.Topmost = true;
                    existingWindow.Topmost = viewModel.IsPinned;
                    return;
                }
            }

            var window = new NoteWindow(viewModel);
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

                window.Topmost = true;
                window.Topmost = window.DataContext is NoteViewModel vm && vm.IsPinned;
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
            if (_dashboardWindow != null)
            {
                _dashboardWindow.Hide();
            }

            foreach (var note in _mainViewModel.Notes)
            {
                OpenNote(note);
            }

            try
            {
                if (minimizeDashboard && _dashboardWindow != null)
                {
                    _dashboardWindow.WindowState = WindowState.Minimized;
                }
            }
            catch { }
        }

        public void ShowDashboard()
        {
            if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
            {
                _dashboardWindow = new MainDashboardWindow(_mainViewModel);
                _dashboardWindow.Closed += (_, _) => _dashboardWindow = null;
            }

            _dashboardWindow.ShowInTaskbar = true;
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
            if (_instructionsWindow != null && _instructionsWindow.IsLoaded)
            {
                _instructionsWindow.Activate();
                return;
            }

            _instructionsWindow = new InstructionsWindow();
            _instructionsWindow.Show();
        }

        public bool ConfirmDelete(string noteTitle)
        {
            var title = string.IsNullOrWhiteSpace(noteTitle) ? "this note" : noteTitle;
            var res = MessageBox.Show($"Are you sure you want to delete '{title}'?", "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return res == MessageBoxResult.Yes;
        }

        public Task<bool> ConfirmDeleteAsync(string noteTitle)
        {
            return Task.FromResult(ConfirmDelete(noteTitle));
        }

        public void RunOnUiThread(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }

        public void Shutdown()
        {
            Application.Current.Shutdown();
        }

        public void ApplyTheme(string themeMode)
        {
            if (Application.Current is App app)
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
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
#endif
