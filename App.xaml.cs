using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using PinNote.UI;
using PinNote.Services;
using Forms = System.Windows.Forms;

namespace PinNote
{
    public partial class App : System.Windows.Application
    {
        private PinNote.ViewModels.MainViewModel? _mainViewModel;
        private Forms.NotifyIcon? _notifyIcon;
        private Bitmap? _trayIconBitmap;
        private readonly AppStateService _appStateService = new();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _mainViewModel = (PinNote.ViewModels.MainViewModel)FindResource("MainVM");

                InitializeTrayIcon();
                Debug.WriteLine("PinNote tray app is ready.");

                // If the process is running elevated, the tray icon may not appear in the normal user's notification area.
                try
                {
                    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    bool isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                    if (isElevated)
                    {
                        // Restart unelevated so the tray icon appears in the normal user's notification area.
                        RestartUnelevated();
                        return; // stop startup of elevated instance
                    }
                }
                catch { }

                // Show dashboard on startup according to settings.
                try
                {
                    bool showUi = _appStateService.GetShowUiOnStartup();
                    bool startMin = _appStateService.GetStartMinimized();
                    if (showUi && !startMin)
                    {
                        _mainViewModel?.ShowAllNotes();
                    }
                    else
                    {
                        // Ensure tray icon is present so user can access the app.
                        EnsureTrayVisible();
                    }
                }
                catch
                {
                    _mainViewModel?.ShowAllNotes();
                }

                // If this is the first run, also create a new note.
                if (_appStateService.ShouldShowFirstRunUi())
                {
                    _mainViewModel?.NewNoteCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"PinNote failed to start.\n\n{ex.Message}",
                    "PinNote Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "PinNote - Sticky Notes",
                Visible = true,
                ContextMenuStrip = CreateTrayMenu()
            };

            _notifyIcon.MouseUp += (_, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left)
                {
                    ShowDashboard();
                }
            };
        }

        private Forms.ContextMenuStrip CreateTrayMenu()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("New note", null, (_, _) => RunOnUiThread(() => _mainViewModel?.NewNoteCommand.Execute(null)));
            menu.Items.Add("Show all notes", null, (_, _) => RunOnUiThread(ShowDashboard));
            menu.Items.Add("Hide all notes", null, (_, _) => RunOnUiThread(() => _mainViewModel?.HideAllCommand.Execute(null)));
            menu.Items.Add("Bring notes on top", null, (_, _) => RunOnUiThread(() => _mainViewModel?.BringNotesOnTopCommand.Execute(null)));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => RunOnUiThread(Shutdown));
            return menu;
        }

        private void RunOnUiThread(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.Invoke(action);
        }

        private Icon CreateTrayIcon()
        {
            try
            {
                _trayIconBitmap = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(_trayIconBitmap))
                {
                    g.Clear(Color.Transparent);

                    using var shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
                    using var paperBrush = new SolidBrush(Color.FromArgb(255, 247, 213));
                    using var edgePen = new Pen(Color.FromArgb(180, 156, 96), 1);
                    using var lineBrush = new SolidBrush(Color.FromArgb(70, 70, 70));
                    using var pinBrush = new SolidBrush(Color.FromArgb(60, 120, 220));
                    using var pinDarkBrush = new SolidBrush(Color.FromArgb(35, 80, 160));

                    g.FillRectangle(shadowBrush, 3, 4, 10, 11);
                    g.FillRectangle(paperBrush, 2, 3, 11, 11);
                    g.DrawRectangle(edgePen, 2, 3, 10, 10);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(220, 244, 190, 62)), 2, 3, 10, 3);
                    g.FillEllipse(pinBrush, 10, 1, 4, 4);
                    g.FillRectangle(pinDarkBrush, 11, 3, 1, 6);
                    g.FillRectangle(lineBrush, 4, 7, 7, 1);
                    g.FillRectangle(lineBrush, 4, 9, 6, 1);
                    g.FillRectangle(lineBrush, 4, 11, 4, 1);
                }

                IntPtr hIcon = _trayIconBitmap.GetHicon();
                using var temporaryIcon = Icon.FromHandle(hIcon);
                var icon = (Icon)temporaryIcon.Clone();
                DestroyIcon(hIcon);
                return icon;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private TrayFallbackWindow? _trayFallback;

        public void ShowFallbackWindow(string? message = null)
        {
            try
            {
                if (_trayFallback == null || !_trayFallback.IsVisible)
                {
                    _trayFallback = message == null ? new TrayFallbackWindow() : new TrayFallbackWindow(message);
                    _trayFallback.Show();
                }
            }
            catch { }
        }

        public void HideFallbackWindow()
        {
            try
            {
                if (_trayFallback != null && _trayFallback.IsVisible)
                {
                    _trayFallback.Close();
                    _trayFallback = null;
                }
            }
            catch { }
        }

        public void ShowDashboard()
        {
            _mainViewModel?.ShowAllNotes();
            HideFallbackWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _trayIconBitmap?.Dispose();
            base.OnExit(e);
        }

        // Ensure the tray icon is visible (useful after windows hide to tray)
        public bool EnsureTrayVisible()
        {
            try
            {
                if (_notifyIcon == null)
                {
                    InitializeTrayIcon();
                    return _notifyIcon is { Visible: true };
                }

                _notifyIcon.Visible = true;
                _notifyIcon.Icon ??= CreateTrayIcon();
                return _notifyIcon.Visible && _notifyIcon.Icon != null;
            }
            catch
            {
                return false;
            }
        }

        // Show a brief notification balloon
        public void ShowTrayBalloon(string title, string message)
        {
            try
            {
                if (!EnsureTrayVisible())
                {
                    return;
                }

                _notifyIcon?.ShowBalloonTip(2500, title, message, Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        private void RestartUnelevated()
        {
            try
            {
                // Determine executable path
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (!string.IsNullOrEmpty(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = '"' + exePath + '"',
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
            }
            catch { }

            // Exit the elevated instance.
            try { Current?.Shutdown(); } catch { Environment.Exit(0); }
        }
    }
}

