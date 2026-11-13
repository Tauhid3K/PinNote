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

        public async Task<bool> ConfirmActionAsync(string title, string message)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                return await ShowConfirmActionInternalAsync(title, message);
            }

            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowConfirmActionInternalAsync(title, message));
        }

        private async Task<bool> ShowConfirmActionInternalAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 380,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };

            var sp = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 14 };
            var lbl = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            var btnPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            bool result = false;
            var yesBtn = new Button { Content = "Yes", Width = 75, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var noBtn = new Button { Content = "No", Width = 75, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
            noBtn.Click += (_, _) => { result = false; dialog.Close(); };

            btnPanel.Children.Add(yesBtn);
            btnPanel.Children.Add(noBtn);
            sp.Children.Add(lbl);
            sp.Children.Add(btnPanel);
            dialog.Content = sp;

            Window? owner = _desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible)
                         ?? (_dashboardWindow != null && _dashboardWindow.IsVisible ? (Window)_dashboardWindow : null);

            if (owner != null)
            {
                await dialog.ShowDialog(owner);
                return result;
            }

            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => tcs.TrySetResult(result);
            dialog.Show();
            return await tcs.Task;
        }

        public async Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultText = "")
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                return await ShowInputDialogInternalAsync(title, prompt, defaultText);
            }

            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ShowInputDialogInternalAsync(title, prompt, defaultText));
        }

        private async Task<string?> ShowInputDialogInternalAsync(string title, string prompt, string defaultText)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 380,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };

            var sp = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
            var lbl = new TextBlock { Text = prompt, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            var txt = new TextBox { Text = defaultText, Height = 28 };
            var btnPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            string? result = null;
            var okBtn = new Button { Content = "OK", Width = 75, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var cancelBtn = new Button { Content = "Cancel", Width = 75, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center };

            okBtn.Click += (_, _) => { result = txt.Text; dialog.Close(); };
            cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
            txt.KeyDown += (_, ke) =>
            {
                if (ke.Key == Avalonia.Input.Key.Enter)
                {
                    result = txt.Text;
                    dialog.Close();
                }
                else if (ke.Key == Avalonia.Input.Key.Escape)
                {
                    result = null;
                    dialog.Close();
                }
            };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnPanel);
            dialog.Content = sp;

            Window? owner = _desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible)
                         ?? (_dashboardWindow != null && _dashboardWindow.IsVisible ? (Window)_dashboardWindow : null);

            if (owner != null)
            {
                await dialog.ShowDialog(owner);
                return result;
            }

            var tcs = new TaskCompletionSource<string?>();
            dialog.Closed += (_, _) => tcs.TrySetResult(result);
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
            AvaloniaApp.IsShuttingDown = true;
            _mainViewModel.IsShuttingDown = true;
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
