using System.Windows;
using System.Windows.Input;
using PinNote.ViewModels;
using PinNote.Services;

namespace PinNote.UI
{
    public partial class MainDashboardWindow : Window
    {
        private bool _isMinimizingToTray;
        private readonly AppStateService _appStateService = new();

        public MainDashboardWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.StateChanged += MainDashboardWindow_StateChanged;
            this.LocationChanged += MainDashboardWindow_LocationChanged;
            this.SizeChanged += MainDashboardWindow_SizeChanged;

            LoadWindowState();
        }

        private void LoadWindowState()
        {
            var state = _appStateService.GetDashboardState();
            
            this.Width = state.Width;
            this.Height = state.Height;

            if (state.X.HasValue && state.Y.HasValue)
            {
                this.Left = state.X.Value;
                this.Top = state.Y.Value;
                this.WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }

        private void SaveWindowState()
        {
            if (this.WindowState == WindowState.Normal)
            {
                _appStateService.SetDashboardState(this.Left, this.Top, this.Width, this.Height);
            }
        }

        private void MainDashboardWindow_LocationChanged(object? sender, System.EventArgs e)
        {
            SaveWindowState();
        }

        private void MainDashboardWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            SaveWindowState();
        }

        private void MainDashboardWindow_StateChanged(object? sender, System.EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
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
            var result = MessageBox.Show(
                "Do you really want to close PinNote? All open notes will be hidden.\n\nTip: Minimize the window if you want to keep PinNote running in the system tray.",
                "Exit PinNote",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
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
