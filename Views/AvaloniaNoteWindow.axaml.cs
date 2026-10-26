using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PinNote.ViewModels;

namespace PinNote.Views
{
    public partial class AvaloniaNoteWindow : Window
    {
        private readonly NoteViewModel _viewModel;
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _geometryTimer;
        private bool _isLoadingContent;
        private string? _originalRawContent;
        private string _loadedPlainText = string.Empty;

        public AvaloniaNoteWindow()
        {
            InitializeComponent();
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _saveTimer.Tick += SaveTimer_Tick;

            _geometryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _geometryTimer.Tick += GeometryTimer_Tick;

            _viewModel = null!;
        }

        public AvaloniaNoteWindow(NoteViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            LoadContent();

            ContentEditor.TextChanged += ContentEditor_TextChanged;
            Closing += AvaloniaNoteWindow_Closing;

            PositionChanged += (_, _) => ScheduleGeometryCommit();

            PropertyChanged += (_, e) =>
            {
                if ((e.Property == WidthProperty && Width > 50) ||
                    (e.Property == HeightProperty && Height > 50))
                {
                    ScheduleGeometryCommit();
                }
            };

            Opened += (_, _) =>
            {
                Console.WriteLine($"[TRANSPARENCY DEBUG] ActualTransparencyLevel: {ActualTransparencyLevel}, TransparencyLevelHint: {TransparencyLevelHint}");
                if (_viewModel.X > 0 && _viewModel.Y > 0)
                {
                    Position = new PixelPoint((int)_viewModel.X, (int)_viewModel.Y);
                }
            };
        }

        private void LoadContent()
        {
            _isLoadingContent = true;
            try
            {
                _originalRawContent = _viewModel.Content;
                if (string.IsNullOrWhiteSpace(_originalRawContent))
                {
                    _loadedPlainText = string.Empty;
                    ContentEditor.Text = string.Empty;
                    return;
                }

                // If content was saved by WPF as FlowDocument XAML, parse line breaks and paragraphs cleanly
                if (_originalRawContent.Contains("<FlowDocument") || _originalRawContent.Contains("<Paragraph"))
                {
                    var text = _originalRawContent;
                    text = Regex.Replace(text, @"<LineBreak\s*/?>", "\n", RegexOptions.IgnoreCase);
                    text = Regex.Replace(text, @"</Paragraph\s*>", "\n", RegexOptions.IgnoreCase);
                    text = Regex.Replace(text, @"</ListItem\s*>", "\n", RegexOptions.IgnoreCase);
                    text = Regex.Replace(text, @"<.*?>", "");
                    text = System.Net.WebUtility.HtmlDecode(text);
                    text = text.Replace("\r\n", "\n").TrimEnd('\r', '\n');
                    _loadedPlainText = text;
                    ContentEditor.Text = text;
                }
                else
                {
                    _loadedPlainText = _originalRawContent;
                    ContentEditor.Text = _originalRawContent;
                }
            }
            finally
            {
                _isLoadingContent = false;
            }
        }

        private void ContentEditor_TextChanged(object? sender, EventArgs e)
        {
            if (_isLoadingContent) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveContent();
        }

        private void SaveContent()
        {
            if (_isLoadingContent || _viewModel == null) return;
            var currentText = ContentEditor.Text ?? string.Empty;

            // If the content has not changed since loading, do not overwrite original rich text!
            if (string.Equals(currentText, _loadedPlainText, StringComparison.Ordinal))
            {
                return;
            }

            _loadedPlainText = currentText;

            // If the original content was a FlowDocument, re-wrap into clean FlowDocument XAML for WPF compatibility
            if (!string.IsNullOrEmpty(_originalRawContent) &&
                (_originalRawContent.Contains("<FlowDocument") || _originalRawContent.Contains("<Paragraph")))
            {
                _viewModel.Content = ConvertPlainTextToFlowDocumentXaml(currentText);
            }
            else
            {
                _viewModel.Content = currentText;
            }
        }

        private static string ConvertPlainTextToFlowDocumentXaml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph></Paragraph></FlowDocument>";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">");
            var lines = text.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                var escaped = System.Security.SecurityElement.Escape(line);
                sb.Append($"<Paragraph>{escaped}</Paragraph>");
            }
            sb.Append("</FlowDocument>");
            return sb.ToString();
        }

        private void ScheduleGeometryCommit()
        {
            if (_isLoadingContent || _viewModel == null) return;
            _geometryTimer.Stop();
            _geometryTimer.Start();
        }

        private void GeometryTimer_Tick(object? sender, EventArgs e)
        {
            _geometryTimer.Stop();
            CommitGeometry();
        }

        private void CommitGeometry()
        {
            if (_viewModel == null) return;
            _viewModel.UpdateBounds(Position.X, Position.Y, Width, Height);
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            _geometryTimer.Stop();
            CommitGeometry();
            SaveContent();
            Close();
        }

        private void AvaloniaNoteWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            _geometryTimer.Stop();
            CommitGeometry();
            SaveContent();
        }
    }
}
