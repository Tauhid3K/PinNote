using System.Windows;
using System.Windows.Input;
using PinNote.ViewModels;

namespace PinNote.UI
{
    public partial class MainDashboardWindow : Window
    {
        private bool _isMinimizingToTray;

        public MainDashboardWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.StateChanged += MainDashboardWindow_StateChanged;
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
                "Do you really want to close PinNote? All open notes will be hidden, but they will be restored when you next launch the app.",
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
