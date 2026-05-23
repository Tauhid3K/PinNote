using System.Windows;

namespace PinNote.UI
{
    public partial class ColorChoiceWindow : Window
    {
        public enum Choice
        {
            None,
            Apply,
            ApplyAndAdd,
            Cancel
        }

        public Choice SelectedChoice { get; private set; } = Choice.None;

        public ColorChoiceWindow()
        {
            InitializeComponent();

            ApplyButton.Click += (_, _) => { SelectedChoice = Choice.Apply; DialogResult = true; Close(); };
            ApplyAddButton.Click += (_, _) => { SelectedChoice = Choice.ApplyAndAdd; DialogResult = true; Close(); };
            CancelButton.Click += (_, _) => { SelectedChoice = Choice.Cancel; DialogResult = false; Close(); };
        }
    }
}
