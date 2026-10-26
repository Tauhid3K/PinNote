using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PinNote.ViewModels;
using PinNote.Services;

namespace PinNote.UI
{
    public partial class MainDashboardWindow : Window
    {
        private bool _isMinimizingToTray;
        private readonly AppStateService _appStateService = new();
        private readonly DispatcherTimer _windowStateDebounceTimer;

        public MainDashboardWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            _windowStateDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _windowStateDebounceTimer.Tick += (s, e) =>
            {
                _windowStateDebounceTimer.Stop();
                SaveWindowState();
            };

            this.StateChanged += MainDashboardWindow_StateChanged;
            this.LocationChanged += MainDashboardWindow_LocationChanged;
            this.SizeChanged += MainDashboardWindow_SizeChanged;
            this.Closing += MainDashboardWindow_Closing;

            LoadWindowState();
        }

        private void MainDashboardWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (Application.Current is App { IsShuttingDown: true } or not App)
            {
                return;
            }

            // Keep the app running in the system tray when dashboard is closed
            e.Cancel = true;
            _windowStateDebounceTimer.Stop();
            SaveWindowState();
            MinimizeToTray();
        }

        private void LoadWindowState()
        {
            var state = _appStateService.GetDashboardState();

            this.Width = Math.Max(MinWidth, Math.Min(state.Width, SystemParameters.VirtualScreenWidth));
            this.Height = Math.Max(MinHeight, Math.Min(state.Height, SystemParameters.VirtualScreenHeight));

            if (state.X.HasValue && state.Y.HasValue)
            {
                double x = state.X.Value;
                double y = state.Y.Value;

                double minX = SystemParameters.VirtualScreenLeft;
                double maxX = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100;
                double minY = SystemParameters.VirtualScreenTop;
                double maxY = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;

                if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                {
                    this.Left = x;
                    this.Top = y;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                }
                else
                {
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
        }

        private void SaveWindowState()
        {
            if (this.WindowState == WindowState.Normal)
            {
                _appStateService.SetDashboardState(this.Left, this.Top, this.Width, this.Height);
            }
        }

        private void MainDashboardWindow_LocationChanged(object? sender, EventArgs e)
        {
            _windowStateDebounceTimer.Stop();
            _windowStateDebounceTimer.Start();
        }

        private void MainDashboardWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            _windowStateDebounceTimer.Stop();
            _windowStateDebounceTimer.Start();
        }

        private void MainDashboardWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                _windowStateDebounceTimer.Stop();
                SaveWindowState();
                MinimizeToTray();
            }
            else
            {
                this.Show();
                this.Activate();
            }

            if (MaximizeButton != null)
            {
                MaximizeButton.Content = this.WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                this.DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _windowStateDebounceTimer.Stop();
            SaveWindowState();
            MinimizeToTray();
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            if (_isMinimizingToTray)
            {
                return;
            }

            try
            {
                _isMinimizingToTray = true;

                if (Application.Current is App app)
                {
                    if (app.EnsureTrayVisible())
                    {
                        ShowInTaskbar = false;
                        WindowState = WindowState.Normal;
                        Hide();
                        app.ShowTrayBalloon("PinNote", "Running in system tray");
                        return;
                    }
                }

                ShowInTaskbar = true;
                WindowState = WindowState.Minimized;
            }
            finally
            {
                _isMinimizingToTray = false;
            }
        }

        private void ToggleMaximize()
        {
            this.WindowState = this.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void NoteCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is NoteViewModel note && DataContext is MainViewModel viewModel)
            {
                viewModel.OpenNote(note);
                e.Handled = true;
            }
        }
    }
}
