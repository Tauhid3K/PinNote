using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PinNote.Views
{
    public partial class AvaloniaInstructionsWindow : Window
    {
        public AvaloniaInstructionsWindow()
        {
            InitializeComponent();
        }

        private void GotIt_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
