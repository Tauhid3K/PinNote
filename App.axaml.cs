using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using PinNote.Services;
using PinNote.ViewModels;
using PinNote.Views;

namespace PinNote
{
    public partial class AvaloniaApp : Application
    {
        private MainViewModel? _mainViewModel;
        private AvaloniaTrayService? _trayService;
        private readonly AppStateService _appStateService = new();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _mainViewModel = new MainViewModel();
                var windowManager = new AvaloniaWindowManager(_mainViewModel, desktop);
                _mainViewModel.WindowManager = windowManager;

                try
                {
                    var themeMode = _appStateService.GetThemeMode();
                    SetTheme(themeMode);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"App.SetTheme startup error: {ex}");
                }

                try
                {
                    _trayService = new AvaloniaTrayService();
                    _trayService.Initialize(
                        newNoteCommand: _mainViewModel.NewNoteCommand,
                        showAllNotesCommand: _mainViewModel.ShowAllCommand,
                        hideAllNotesCommand: _mainViewModel.HideAllCommand,
                        bringNotesOnTopCommand: _mainViewModel.BringNotesOnTopCommand,
                        setThemeAction: SetTheme,
                        getThemeAction: () => _appStateService.GetThemeMode(),
                        exitCommand: _mainViewModel.ExitCommand,
                        onLeftClick: () => _mainViewModel.ShowDashboard()
                    );
                    _mainViewModel.TrayService = _trayService;
                    _trayService.SetVisible(_mainViewModel.ShowTrayIcon);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AvaloniaTrayService initialization error: {ex}");
                }

                bool isAutostart = Array.Exists(desktop.Args ?? Array.Empty<string>(),
                    arg => string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));

                bool showPostInstallInstructions = Array.Exists(desktop.Args ?? Array.Empty<string>(),
                    arg => string.Equals(arg, "--show-instructions", StringComparison.OrdinalIgnoreCase));

                bool showUi = _appStateService.GetShowUiOnStartup();
                bool startMinimized = _appStateService.GetStartMinimized();

                if (showPostInstallInstructions)
                {
                    _mainViewModel.ShowDashboard();
                    if (_mainViewModel.Notes.Count > 0)
                    {
                        _mainViewModel.ShowAllNotes(minimizeDashboard: false);
                    }
                    _mainViewModel.ShowInstructions();
                }
                else if (isAutostart || startMinimized)
                {
                    if (showUi && _mainViewModel.Notes.Count > 0)
                    {
                        _mainViewModel.ShowAllNotes(minimizeDashboard: true);
                    }
                }
                else
                {
                    _mainViewModel.ShowDashboard();
                    if (showUi && _mainViewModel.Notes.Count > 0)
                    {
                        _mainViewModel.ShowAllNotes(minimizeDashboard: false);
                    }
                    if (_appStateService.ShouldShowFirstRunUi())
                    {
                        _mainViewModel.ShowInstructions();
                    }
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void SetTheme(string themeMode)
        {
            _appStateService.SetThemeMode(themeMode);
            if (string.Equals(themeMode, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                RequestedThemeVariant = ThemeVariant.Dark;
            }
            else if (string.Equals(themeMode, "Light", StringComparison.OrdinalIgnoreCase))
            {
                RequestedThemeVariant = ThemeVariant.Light;
            }
            else
            {
                RequestedThemeVariant = ThemeVariant.Default;
            }
        }
    }
}
