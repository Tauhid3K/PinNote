using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PinNote.Services;
using PinNote.ViewModels;

namespace PinNote.Views
{
    public partial class AvaloniaDashboardWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly AppStateService _appStateService;
        private bool _isShuttingDown;
        private bool _boundsRestored;

        public AvaloniaDashboardWindow()
        {
            InitializeComponent();
            _viewModel = null!;
            _appStateService = new AppStateService();
        }

        public AvaloniaDashboardWindow(MainViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            Closing += AvaloniaDashboardWindow_Closing;
            Opened += AvaloniaDashboardWindow_Opened;
            PropertyChanged += AvaloniaDashboardWindow_PropertyChanged;
        }

        private void AvaloniaDashboardWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized)
            {
                SaveDashboardBounds();
                Hide();
                WindowState = WindowState.Normal;
            }
        }

        private void AvaloniaDashboardWindow_Opened(object? sender, EventArgs e)
        {
            if (_boundsRestored) return;
            _boundsRestored = true;

            try
            {
                var (savedX, savedY, savedWidth, savedHeight) = _appStateService.GetDashboardState();

                // Collect available screens for bounds-sanitization
                var screenList = new List<ScreenBounds>();
                if (Screens?.All != null)
                {
                    foreach (var s in Screens.All)
                    {
                        var b = s.Bounds;
                        screenList.Add(new ScreenBounds(b.X, b.Y, b.Width, b.Height));
                    }
                }

                var bounds = WindowRestorationHelper.SanitizeBounds(
                    savedX ?? double.NaN,
                    savedY ?? double.NaN,
                    savedWidth,
                    savedHeight,
                    screenList);

                Width = bounds.Width;
                Height = bounds.Height;

                // Only reposition if we had a saved position (not first-run)
                if (savedX.HasValue && savedY.HasValue)
                {
                    try
                    {
                        Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
                    }
                    catch { /* ignore invalid positions */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AvaloniaDashboardWindow: Failed to restore position: {ex.Message}");
            }
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
            // Save dashboard position and size whenever it closes or hides
            SaveDashboardBounds();

            if (!_isShuttingDown)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void SaveDashboardBounds()
        {
            try
            {
                var pos = Position;
                _appStateService.SetDashboardState(pos.X, pos.Y, Width, Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AvaloniaDashboardWindow: Failed to save position: {ex.Message}");
            }
        }
    }
}
