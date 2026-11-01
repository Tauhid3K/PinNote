using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PinNote.Services;
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
            ContentEditor.KeyDown += ContentEditor_KeyDown;
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
                RestoreWindowBounds();
            };
        }

        private void RestoreWindowBounds()
        {
            if (_viewModel == null) return;

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
                _viewModel.X,
                _viewModel.Y,
                _viewModel.Width,
                _viewModel.Height,
                screenList);

            Width = bounds.Width;
            Height = bounds.Height;

            try
            {
                Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
            }
            catch { }
        }

        public void FlushPendingEdits()
        {
            _saveTimer.Stop();
            _geometryTimer.Stop();
            CommitGeometry();
            SaveContent();
        }

        private void LoadContent()
        {
            if (_viewModel.IsChecklist) return;

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
            if (_isLoadingContent || _viewModel.IsChecklist) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveContent();
        }

        public void SaveContent()
        {
            if (_isLoadingContent || _viewModel == null || _viewModel.IsChecklist) return;
            var currentText = ContentEditor.Text ?? string.Empty;

            if (string.Equals(currentText, _loadedPlainText, StringComparison.Ordinal))
            {
                return;
            }

            _loadedPlainText = currentText;

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
            FlushPendingEdits();
            Close();
        }

        private void AvaloniaNoteWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            FlushPendingEdits();
        }

        private void ContentEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.B)
                {
                    ApplyInlineFormatting("**", "**");
                    e.Handled = true;
                }
                else if (e.Key == Key.I)
                {
                    ApplyInlineFormatting("*", "*");
                    e.Handled = true;
                }
                else if (e.Key == Key.U)
                {
                    ApplyInlineFormatting("<u>", "</u>");
                    e.Handled = true;
                }
            }
        }

        private void BoldButton_Click(object? sender, RoutedEventArgs e)
        {
            ApplyInlineFormatting("**", "**");
        }

        private void ItalicButton_Click(object? sender, RoutedEventArgs e)
        {
            ApplyInlineFormatting("*", "*");
        }

        private void UnderlineButton_Click(object? sender, RoutedEventArgs e)
        {
            ApplyInlineFormatting("<u>", "</u>");
        }

        private void ApplyInlineFormatting(string prefix, string suffix)
        {
            var text = ContentEditor.Text ?? string.Empty;
            int start = Math.Min(ContentEditor.SelectionStart, ContentEditor.SelectionEnd);
            int end = Math.Max(ContentEditor.SelectionStart, ContentEditor.SelectionEnd);
            int len = end - start;

            if (len > 0)
            {
                string selected = text.Substring(start, len);
                if (selected.StartsWith(prefix) && selected.EndsWith(suffix) && selected.Length >= prefix.Length + suffix.Length)
                {
                    string unwrapped = selected.Substring(prefix.Length, selected.Length - prefix.Length - suffix.Length);
                    ContentEditor.Text = text.Substring(0, start) + unwrapped + text.Substring(end);
                    ContentEditor.SelectionStart = start;
                    ContentEditor.SelectionEnd = start + unwrapped.Length;
                }
                else
                {
                    string wrapped = prefix + selected + suffix;
                    ContentEditor.Text = text.Substring(0, start) + wrapped + text.Substring(end);
                    ContentEditor.SelectionStart = start;
                    ContentEditor.SelectionEnd = start + wrapped.Length;
                }
            }
            else
            {
                int caret = ContentEditor.CaretIndex;
                if (caret < 0 || caret > text.Length) caret = text.Length;
                string insertion = prefix + suffix;
                ContentEditor.Text = text.Insert(caret, insertion);
                ContentEditor.CaretIndex = caret + prefix.Length;
            }
            ContentEditor.Focus();
        }

        private void BulletButton_Click(object? sender, RoutedEventArgs e)
        {
            var text = ContentEditor.Text ?? string.Empty;
            int start = Math.Min(ContentEditor.SelectionStart, ContentEditor.SelectionEnd);
            int end = Math.Max(ContentEditor.SelectionStart, ContentEditor.SelectionEnd);

            int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            int lineEnd = text.IndexOf('\n', end);
            if (lineEnd == -1) lineEnd = text.Length;

            string targetBlock = text.Substring(lineStart, lineEnd - lineStart);
            var lines = targetBlock.Split('\n');

            bool allBulleted = true;
            int nonEmptyCount = 0;
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    nonEmptyCount++;
                    if (!line.TrimStart().StartsWith("• ") && !line.TrimStart().StartsWith("- "))
                    {
                        allBulleted = false;
                        break;
                    }
                }
            }

            if (nonEmptyCount == 0) allBulleted = false;

            var newLines = new List<string>();
            foreach (var line in lines)
            {
                if (allBulleted)
                {
                    if (line.TrimStart().StartsWith("• "))
                    {
                        int idx = line.IndexOf("• ");
                        newLines.Add(line.Remove(idx, 2));
                    }
                    else if (line.TrimStart().StartsWith("- "))
                    {
                        int idx = line.IndexOf("- ");
                        newLines.Add(line.Remove(idx, 2));
                    }
                    else
                    {
                        newLines.Add(line);
                    }
                }
                else
                {
                    newLines.Add("• " + line);
                }
            }

            string replacement = string.Join("\n", newLines);
            ContentEditor.Text = text.Substring(0, lineStart) + replacement + text.Substring(lineEnd);
            ContentEditor.SelectionStart = lineStart;
            ContentEditor.SelectionEnd = lineStart + replacement.Length;
            ContentEditor.Focus();
        }

        private async void ImageButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Insert Image",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Images")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                        }
                    }
                };

                var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (result != null && result.Count > 0)
                {
                    var file = result[0];
                    string filePath = file.Path.LocalPath;
                    string altText = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    string markdownImage = $"\n![{altText}]({filePath})\n";

                    var text = ContentEditor.Text ?? string.Empty;
                    int caret = ContentEditor.CaretIndex;
                    if (caret < 0 || caret > text.Length) caret = text.Length;

                    ContentEditor.Text = text.Insert(caret, markdownImage);
                    ContentEditor.CaretIndex = caret + markdownImage.Length;
                    ContentEditor.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image insert error: {ex.Message}");
            }
        }
    }
}
