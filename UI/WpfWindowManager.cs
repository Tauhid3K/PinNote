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
                if (window.DataContext is NoteViewModel vm)
                {
                    vm.IsVisible = false;
                }
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

        public Task<bool> ConfirmActionAsync(string title, string message)
        {
            var res = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return Task.FromResult(res == MessageBoxResult.Yes);
        }

        public Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultText = "")
        {
            string? result = null;
            var dialog = new Window
            {
                Title = title,
                Width = 360,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var sp = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            var txt = new TextBox { Text = defaultText, Margin = new Thickness(0, 0, 0, 12), Height = 26 };
            txt.SelectAll();

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 75, Height = 26, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancelBtn = new Button { Content = "Cancel", Width = 75, Height = 26, IsCancel = true };

            okBtn.Click += (_, _) => { result = txt.Text; dialog.DialogResult = true; dialog.Close(); };
            cancelBtn.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnPanel);
            dialog.Content = sp;

            bool? dialogRes = dialog.ShowDialog();
            return Task.FromResult(dialogRes == true ? result : null);
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
            _mainViewModel.IsShuttingDown = true;
            FlushPendingEdits();
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

        public void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void FlushPendingEdits()
        {
            foreach (var window in _openWindows.Values)
            {
                window.SaveEditorContent();
            }
        }

        public Task<string?> ShowOpenFileDialogAsync(string title, string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter
            };
            bool? res = dlg.ShowDialog();
            return Task.FromResult(res == true ? dlg.FileName : null);
        }

        public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filter)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                Filter = filter,
                DefaultExt = "json"
            };
            bool? res = dlg.ShowDialog();
            return Task.FromResult(res == true ? dlg.FileName : null);
        }
    }
}
#endif
