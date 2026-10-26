using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PinNote.ViewModels;

namespace PinNote.Views
{
    public partial class AvaloniaDashboardWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isShuttingDown;

        public AvaloniaDashboardWindow()
        {
            InitializeComponent();
            _viewModel = null!;
        }

        public AvaloniaDashboardWindow(MainViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            Closing += AvaloniaDashboardWindow_Closing;
        }

        private void NoteCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                sender is Border border &&
                border.DataContext is NoteViewModel noteVm)
            {
                // Prevent opening note when clicking duplicate or delete buttons
                if (e.Source is Visual sourceVisual)
                {
                    var visual = sourceVisual;
                    while (visual != null && visual != border)
                    {
                        if (visual is Button)
                        {
                            return;
                        }
                        visual = visual.GetVisualParent();
                    }
                }

                _viewModel.OpenNote(noteVm);
            }
        }

        public void ForceClose()
        {
            _isShuttingDown = true;
            Close();
        }

        private void AvaloniaDashboardWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!_isShuttingDown)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
