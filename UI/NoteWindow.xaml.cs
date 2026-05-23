using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PinNote.ViewModels;

namespace PinNote.UI
{
    // Color palette item for the color picker
    public class ColorItem
    {
        public string Name { get; set; } = "";
        public System.Windows.Media.Brush Color { get; set; } = System.Windows.Media.Brushes.Transparent;
        public string CommandParam { get; set; } = "";
    }

    public partial class NoteWindow : Window
    {
        private bool _isLoadingEditorContent;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _chromeIdleTimer;

        public NoteWindow(NoteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _saveTimer.Tick += SaveTimer_Tick;
            _chromeIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _chromeIdleTimer.Tick += ChromeIdleTimer_Tick;

            if (viewModel is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += ViewModel_PropertyChanged;
            }

            LoadColorPalette();
            LoadEditorContent();
            SyncEditorStyles();
            EditorBox.TextChanged += EditorBox_TextChanged;
            Closing += NoteWindow_Closing;
            ShowChromeTemporarily();
        }

        private void LoadColorPalette()
        {
            var colors = new List<ColorItem>
            {
                new() { Name = "Sky Header / White Note", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD1EAF7")), CommandParam = "#FFD1EAF7|#FFFFFFFF" },
                new() { Name = "White", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFFFF")), CommandParam = "#FFFFFFFF|#FFFFFFFF" },

                // Row 1: Warm pastels
                new() { Name = "Classic Yellow", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFDFD86")), CommandParam = "#FFFDFD86|#FFFDFD86" },
                new() { Name = "Light Yellow", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF99")), CommandParam = "#FFFFFF99|#FFFFFF99" },
                new() { Name = "Soft Orange", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFE4CC")), CommandParam = "#FFFFE4CC|#FFFFE4CC" },
                new() { Name = "Peachy", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFCDB2")), CommandParam = "#FFFFCDB2|#FFFFCDB2" },
                new() { Name = "Light Pink", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9D9E6")), CommandParam = "#FFF9D9E6|#FFF9D9E6" },
                new() { Name = "Rose", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF99CC")), CommandParam = "#FFFF99CC|#FFFF99CC" },

                // Row 2: Cool pastels
                new() { Name = "Lavender", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE2D9F3")), CommandParam = "#FFE2D9F3|#FFE2D9F3" },
                new() { Name = "Light Purple", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE6D5FF")), CommandParam = "#FFE6D5FF|#FFE6D5FF" },
                new() { Name = "Mint", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE2F7D1")), CommandParam = "#FFE2F7D1|#FFE2F7D1" },
                new() { Name = "Light Green", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCCF0DD")), CommandParam = "#FFCCF0DD|#FFCCF0DD" },
                // note: Sky Header color above already provides this shade; removed duplicate entry
                new() { Name = "Light Blue", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB3E5FF")), CommandParam = "#FFB3E5FF|#FFB3E5FF" },

                // Row 3: Vibrant colors
                new() { Name = "Bright Yellow", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF00")), CommandParam = "#FFFFFF00|#FFFFFF00" },
                new() { Name = "Bright Orange", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF9900")), CommandParam = "#FFFF9900|#FFFF9900" },
                new() { Name = "Bright Red", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF3333")), CommandParam = "#FFFF3333|#FFFF3333" },
                new() { Name = "Bright Pink", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF1493")), CommandParam = "#FFFF1493|#FFFF1493" },
                new() { Name = "Bright Green", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00CC00")), CommandParam = "#FF00CC00|#FF00CC00" },
                new() { Name = "Bright Blue", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0066FF")), CommandParam = "#FF0066FF|#FF0066FF" },

                // Row 4: Neutral & Dark (adjusted to improve distinction)
                new() { Name = "Light Gray", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7F7F7")), CommandParam = "#FFF7F7F7|#FFF7F7F7" },
                new() { Name = "Ash", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE6E6E6")), CommandParam = "#FFE6E6E6|#FFE6E6E6" },
                new() { Name = "Dark Gray", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF666666")), CommandParam = "#FF666666|#FF666666" },
                new() { Name = "Charcoal", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF444444")), CommandParam = "#FF444444|#FF444444" },
                new() { Name = "Midnight", Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF333333")), CommandParam = "#FF333333|#FF333333" }
            };

            ColorPalette.ItemsSource = colors;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject source)
            {
                // Ignore button clicks and textbox editing clicks
                if (HasAncestor<Button>(source) || HasAncestor<TextBox>(source))
                {
                    return;
                }

                this.DragMove();
                UpdatePosition();
                e.Handled = true;
            }
        }

        private static bool HasAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            var current = source;
            while (current != null)
            {
                if (current is T)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Focus window when clicking anywhere
            this.Activate();
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb || thumb.Tag is not string direction)
            {
                return;
            }

            ResizeWindow(direction, e.HorizontalChange, e.VerticalChange);
        }

        private void ResizeWindow(string direction, double horizontalChange, double verticalChange)
        {
            double newLeft = Left;
            double newTop = Top;
            double newWidth = Width;
            double newHeight = Height;

            if (direction.Contains("Left", StringComparison.Ordinal))
            {
                double targetWidth = Width - horizontalChange;
                if (targetWidth >= MinWidth)
                {
                    newWidth = targetWidth;
                    newLeft = Left + horizontalChange;
                }
            }

            if (direction.Contains("Right", StringComparison.Ordinal))
            {
                double targetWidth = Width + horizontalChange;
                if (targetWidth >= MinWidth)
                {
                    newWidth = targetWidth;
                }
            }

            if (direction.Contains("Top", StringComparison.Ordinal))
            {
                double targetHeight = Height - verticalChange;
                if (targetHeight >= MinHeight)
                {
                    newHeight = targetHeight;
                    newTop = Top + verticalChange;
                }
            }

            if (direction.Contains("Bottom", StringComparison.Ordinal))
            {
                double targetHeight = Height + verticalChange;
                if (targetHeight >= MinHeight)
                {
                    newHeight = targetHeight;
                }
            }

            Left = newLeft;
            Top = newTop;
            Width = newWidth;
            Height = newHeight;

            if (DataContext is NoteViewModel vm)
            {
                vm.X = Left;
                vm.Y = Top;
                vm.Width = Width;
                vm.Height = Height;
            }
        }

        private void UpdatePosition()
        {
            if (DataContext is NoteViewModel vm)
            {
                vm.X = this.Left;
                vm.Y = this.Top;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NoteViewModel.BodyColor) or nameof(NoteViewModel.TitleBarColor) or nameof(NoteViewModel.Opacity) or nameof(NoteViewModel.TextBrush) or nameof(NoteViewModel.TextColor))
            {
                SyncEditorStyles();
            }
            else if (e.PropertyName == nameof(NoteViewModel.BodyFontSize))
            {
                SyncEditorFontSize();
            }
        }

        private void SyncEditorStyles()
        {
            if (DataContext is not NoteViewModel vm)
            {
                return;
            }

            if (EditorBox?.Document != null)
            {
                var brush = vm.TextBrush;
                EditorBox.Document.Foreground = brush;

                // Ensure the entire document range uses this brush to override any local formatting
                var range = new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd);
                if (!range.IsEmpty)
                {
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                }
            }
        }

        private void SyncEditorFontSize()
        {
            if (DataContext is not NoteViewModel vm || EditorBox?.Document == null)
            {
                return;
            }

            // Apply font size to the entire document range to override any local formatting
            var range = new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd);
            if (!range.IsEmpty)
            {
                range.ApplyPropertyValue(TextElement.FontSizeProperty, vm.BodyFontSize);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ShowChrome();
            if (DataContext is not NoteViewModel)
            {
                return;
            }

            if (FindName("NoteMenu") is ContextMenu menu)
            {
                menu.PlacementTarget = sender as UIElement;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            ShowChromeTemporarily();
        }

        private void Window_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ShowChromeTemporarily();
        }

        private void ShowChromeTemporarily()
        {
            ShowChrome();
            _chromeIdleTimer.Stop();
            _chromeIdleTimer.Start();
        }

        private void ShowChrome()
        {
            OptionsBar.Visibility = Visibility.Visible;
            FormattingBar.Visibility = Visibility.Visible;
            OptionsRow.Height = new GridLength(28);
            FormattingRow.Height = new GridLength(34);
        }

        private void HideChrome()
        {
            if (NoteMenu?.IsOpen == true)
            {
                ShowChromeTemporarily();
                return;
            }

            OptionsBar.Visibility = Visibility.Collapsed;
            FormattingBar.Visibility = Visibility.Collapsed;
            OptionsRow.Height = new GridLength(0);
            FormattingRow.Height = new GridLength(0);
        }

        private void ChromeIdleTimer_Tick(object? sender, EventArgs e)
        {
            _chromeIdleTimer.Stop();
            HideChrome();
        }

        private void CloseNote_Click(object sender, RoutedEventArgs e)
        {
            SaveEditorContent();
            Close();
        }

        private void NoteWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveEditorContent();
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleBold.Execute(null, EditorBox);
            EditorBox.Focus();
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleItalic.Execute(null, EditorBox);
            EditorBox.Focus();
        }

        private void UnderlineButton_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleUnderline.Execute(null, EditorBox);
            EditorBox.Focus();
        }

        private void BulletsButton_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleBullets.Execute(null, EditorBox);
            EditorBox.Focus();
        }

        private void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Insert image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(dialog.FileName);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            var image = new Image
            {
                Source = bitmap,
                MaxWidth = 240,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(0, 6, 0, 6)
            };

            var insertionPoint = EditorBox.CaretPosition;
            _ = new InlineUIContainer(image, insertionPoint);

            EditorBox.Focus();
            SaveEditorContent();
        }

        private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            QueueSaveEditorContent();
        }

        private void QueueSaveEditorContent()
        {
            if (_isLoadingEditorContent)
            {
                return;
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveEditorContent();
        }

        private void LoadEditorContent()
        {
            if (DataContext is not NoteViewModel vm)
            {
                return;
            }

            _isLoadingEditorContent = true;
            try
            {
                EditorBox.Document = new FlowDocument();
                SyncEditorStyles();

                if (string.IsNullOrWhiteSpace(vm.Content))
                {
                    EditorBox.Document.Blocks.Clear();
                    EditorBox.Document.Blocks.Add(new Paragraph());
                    return;
                }

                var range = new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(vm.Content));

                try
                {
                    range.Load(stream, DataFormats.Xaml);
                }
                catch
                {
                    EditorBox.Document.Blocks.Clear();
                    EditorBox.Document.Blocks.Add(new Paragraph(new Run(vm.Content)));
                }
            }
            finally
            {
                _isLoadingEditorContent = false;
            }
        }

        private void SaveEditorContent()
        {
            if (_isLoadingEditorContent || DataContext is not NoteViewModel vm)
            {
                return;
            }

            _saveTimer.Stop();
            var range = new TextRange(EditorBox.Document.ContentStart, EditorBox.Document.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, DataFormats.Xaml);
            vm.Content = Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
