using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PinNote.Views
{
    public partial class ConfirmDeleteDialog : Window
    {
        public bool Result { get; private set; }

        public ConfirmDeleteDialog()
        {
            InitializeComponent();
        }

        public ConfirmDeleteDialog(string noteTitle) : this()
        {
            var displayTitle = string.IsNullOrWhiteSpace(noteTitle) ? "this note" : $"\"{noteTitle}\"";
            PromptText.Text = $"Are you sure you want to delete {displayTitle}? This action cannot be undone.";
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close(false);
        }

        private void Delete_Click(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close(true);
        }
    }
}
