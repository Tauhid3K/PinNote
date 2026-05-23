using System;
using System.Windows.Input;
using System.Windows.Media;
using PinNote.Models;

namespace PinNote.ViewModels
{
    public class NoteViewModel : ViewModelBase
    {
        private readonly NoteModel _model;
        private readonly MainViewModel _mainViewModel;

        public NoteViewModel(NoteModel model, MainViewModel mainViewModel)
        {
            _model = model;
            _mainViewModel = mainViewModel;
            DeleteCommand = new RelayCommand(_ => OnDeleteRequested());
            TogglePinCommand = new RelayCommand(_ => IsPinned = !IsPinned);
            NewNoteCommand = _mainViewModel.NewNoteCommand;
            ChangeColorCommand = new RelayCommand(colorParam => 
            {
                if (colorParam is string colors)
                {
                    var parts = colors.Split('|');
                    if (parts.Length == 2)
                    {
                        BodyColor = parts[1];
                        TitleBarColor = CreateTitleBarColor(parts[0], parts[1]);
                    }
                }
            });
            SetOpacityCommand = new RelayCommand(opacityParam =>
            {
                if (opacityParam is string value && double.TryParse(value, out double opacity))
                {
                    // Allow more transparency down to 0.2 (20%)
                    Opacity = Math.Clamp(opacity, 0.2, 1.0);
                }
            });

            SetFontSizeCommand = new RelayCommand(param =>
            {
                if (param is string s)
                {
                    var parts = s.Split('|');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out double titleFs) &&
                        double.TryParse(parts[1], out double bodyFs))
                    {
                        TitleFontSize = titleFs;
                        BodyFontSize = bodyFs;
                    }
                }
            });
        }

        public Guid Id => _model.Id;
        public ICommand NewNoteCommand { get; }

        public string Title
        {
            get => _model.Title;
            set
            {
                if (_model.Title != value)
                {
                    _model.Title = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string Content
        {
            get => _model.Content;
            set
            {
                if (_model.Content != value)
                {
                    _model.Content = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double X
        {
            get => _model.X;
            set
            {
                if (_model.X != value)
                {
                    _model.X = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double Y
        {
            get => _model.Y;
            set
            {
                if (_model.Y != value)
                {
                    _model.Y = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double Width
        {
            get => _model.Width;
            set
            {
                if (_model.Width != value)
                {
                    _model.Width = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double Height
        {
            get => _model.Height;
            set
            {
                if (_model.Height != value)
                {
                    _model.Height = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double Opacity
        {
            get => _model.Opacity;
            set
            {
                if (_model.Opacity != value)
                {
                    _model.Opacity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TextBrush));
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double TitleFontSize
        {
            get => _model.TitleFontSize;
            set
            {
                if (_model.TitleFontSize != value)
                {
                    _model.TitleFontSize = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double BodyFontSize
        {
            get => _model.BodyFontSize;
            set
            {
                if (_model.BodyFontSize != value)
                {
                    _model.BodyFontSize = value;
                    OnPropertyChanged();

                    // Keep title font size exactly the same as body font size.
                    if (!_updatingTitleFromBody)
                    {
                        try
                        {
                            _updatingTitleFromBody = true;
                            TitleFontSize = value;
                        }
                        finally
                        {
                            _updatingTitleFromBody = false;
                        }
                    }

                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _updatingTitleFromBody = false;


        public string TitleBarColor
        {
            get => _model.TitleBarColor;
            set
            {
                if (_model.TitleBarColor != value)
                {
                    _model.TitleBarColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TextBrush));
                    OnPropertyChanged(nameof(TextColor));
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(ActiveTitleBarColor));
                }
            }
        }

        public string BodyColor
        {
            get => _model.BodyColor;
            set
            {
                if (_model.BodyColor != value)
                {
                    _model.BodyColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TextBrush));
                    OnPropertyChanged(nameof(TextColor));
                    OnPropertyChanged(nameof(ActiveTitleBarColor));
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string TextColor
        {
            get
            {
                try
                {
                    if (TryParseColor(BodyColor, out var c))
                    {
                        double brightness = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                        return brightness > 0.5 ? "Black" : "White";
                    }

                    return "Black";
                }
                catch
                {
                    return "Black";
                }
            }
        }

        public System.Windows.Media.Brush TextBrush
        {
            get
            {
                var alpha = (byte)(Math.Clamp(Opacity - 0.05, 0.35, 1.0) * 255);
                var brushColor = TextColor == "White"
                    ? System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255)
                    : System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0);
                var brush = new SolidColorBrush(brushColor);
                if (brush.CanFreeze) brush.Freeze();
                return brush;
            }
        }

        /// <summary>
        /// Returns a darker version of BodyColor for the active title bar
        /// </summary>
        public string ActiveTitleBarColor
        {
            get
            {
                return CreateTitleBarColor(TitleBarColor, BodyColor);
            }
        }

        public bool IsPinned
        {
            get => _model.IsPinned;
            set
            {
                if (_model.IsPinned != value)
                {
                    _model.IsPinned = value;
                    OnPropertyChanged();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ICommand DeleteCommand { get; }
        public ICommand TogglePinCommand { get; }
        public ICommand ChangeColorCommand { get; }
        public ICommand SetOpacityCommand { get; }
        public ICommand SetFontSizeCommand { get; }

        public event EventHandler? DeleteRequested;
        public event EventHandler? SaveRequested;

        private static string CreateTitleBarColor(string titleBarColor, string bodyColor)
        {
            var baseColor = !string.IsNullOrWhiteSpace(titleBarColor) && !string.Equals(titleBarColor, "#FFFFFFFF", StringComparison.OrdinalIgnoreCase)
                ? titleBarColor
                : bodyColor;

            return AdjustColor(baseColor, 0.82);
        }

        private static string AdjustColor(string color, double factor)
        {
            try
            {
                if (!TryParseColor(color, out var c)) return color;

                int r = Math.Clamp((int)(c.R * factor), 0, 255);
                int g = Math.Clamp((int)(c.G * factor), 0, 255);
                int b = Math.Clamp((int)(c.B * factor), 0, 255);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return color;
            }
        }

        private static bool TryParseColor(string? input, out System.Windows.Media.Color color)
        {
            color = System.Windows.Media.Colors.Transparent;
            if (string.IsNullOrWhiteSpace(input)) return false;

            try
            {
                var conv = System.Windows.Media.ColorConverter.ConvertFromString(input!);
                if (conv is System.Windows.Media.Color c)
                {
                    color = c;
                    return true;
                }

                if (conv is System.Windows.Media.SolidColorBrush b)
                {
                    color = b.Color;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void OnDeleteRequested()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        public NoteModel GetModel() => _model;
    }
}
