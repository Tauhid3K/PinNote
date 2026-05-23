using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PinNote.UI
{
    public partial class TrayFallbackWindow : Window
    {
        public TrayFallbackWindow()
            : this("PinNote minimized to tray")
        {
        }

        public TrayFallbackWindow(string message)
        {
            InitializeComponent();
            Loaded += TrayFallbackWindow_Loaded;
            MessageText.Text = message;
        }

        private void TrayFallbackWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Position at bottom-right above the taskbar
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 12;
            Top = workArea.Bottom - Height - 12;
        }

        // No button handlers: fallback window shows informational message only.
    }
}
