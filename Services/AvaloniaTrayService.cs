using Avalonia;
using Avalonia.Controls;
using System;
using System.IO;
using System.Windows.Input;

namespace PinNote.Services
{
    public class AvaloniaTrayService : ITrayService
    {
        private TrayIcon? _trayIcon;

        public void Initialize(
            ICommand newNoteCommand,
            ICommand showAllNotesCommand,
            ICommand hideAllNotesCommand,
            ICommand bringNotesOnTopCommand,
            Action<string> setThemeAction,
            Func<string> getThemeAction,
            ICommand exitCommand,
            Action onLeftClick)
        {
            var menu = new NativeMenu();

            var newNoteItem = new NativeMenuItem("New note") { Command = newNoteCommand };
            var showAllItem = new NativeMenuItem("Show all notes") { Command = showAllNotesCommand };
            var hideAllItem = new NativeMenuItem("Hide all notes") { Command = hideAllNotesCommand };
            var onTopItem = new NativeMenuItem("Bring notes on top") { Command = bringNotesOnTopCommand };

            // Theme submenu
            var themeMenu = new NativeMenu();
            var currentTheme = getThemeAction();
            var systemTheme = new NativeMenuItem("System") { IsChecked = currentTheme == "System" };
            var lightTheme = new NativeMenuItem("Light") { IsChecked = currentTheme == "Light" };
            var darkTheme = new NativeMenuItem("Dark") { IsChecked = currentTheme == "Dark" };

            systemTheme.Click += (_, _) => setThemeAction("System");
            lightTheme.Click += (_, _) => setThemeAction("Light");
            darkTheme.Click += (_, _) => setThemeAction("Dark");

            themeMenu.Items.Add(systemTheme);
            themeMenu.Items.Add(lightTheme);
            themeMenu.Items.Add(darkTheme);

            var themeItem = new NativeMenuItem("Theme") { Menu = themeMenu };
            var exitItem = new NativeMenuItem("Exit") { Command = exitCommand };

            menu.Items.Add(newNoteItem);
            menu.Items.Add(showAllItem);
            menu.Items.Add(hideAllItem);
            menu.Items.Add(onTopItem);
            menu.Items.Add(themeItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exitItem);

            _trayIcon = new TrayIcon
            {
                ToolTipText = "PinNote - Sticky Notes",
                Menu = menu,
                IsVisible = true
            };

            // Load icon
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new WindowIcon(iconPath);
                }
                else if (Avalonia.Platform.AssetLoader.Exists(new Uri("avares://PinNote/Assets/icon.png")))
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://PinNote/Assets/icon.png"));
                    _trayIcon.Icon = new WindowIcon(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex}");
            }

            _trayIcon.Clicked += (_, _) => onLeftClick();

            if (Application.Current != null)
            {
                var icons = TrayIcon.GetIcons(Application.Current);
                if (icons == null)
                {
                    icons = new TrayIcons();
                    TrayIcon.SetIcons(Application.Current, icons);
                }
                icons.Add(_trayIcon);
            }
        }

        public void SetVisible(bool isVisible)
        {
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = isVisible;
                if (Application.Current != null)
                {
                    var icons = TrayIcon.GetIcons(Application.Current);
                    if (isVisible)
                    {
                        if (icons == null)
                        {
                            icons = new TrayIcons();
                            TrayIcon.SetIcons(Application.Current, icons);
                        }
                        if (!icons.Contains(_trayIcon))
                        {
                            icons.Add(_trayIcon);
                        }
                    }
                    else
                    {
                        icons?.Remove(_trayIcon);
                    }
                }
            }
        }

        public bool EnsureVisible()
        {
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = true;
                return true;
            }
            return false;
        }

        public void ShowNotification(string title, string message)
        {
            NotificationService.ShowNotification(title, message);
        }

        public void SetToolTip(string toolTip)
        {
            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = toolTip;
            }
        }

        public void Dispose()
        {
            if (_trayIcon != null)
            {
                if (Application.Current != null)
                {
                    var icons = TrayIcon.GetIcons(Application.Current);
                    icons?.Remove(_trayIcon);
                }
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }
}
