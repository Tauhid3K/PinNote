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
                    // Basic brightness check to decide text color (Black or White)
                    var hex = BodyColor.Replace("#", "");
                    if (hex.Length == 8) hex = hex.Substring(2); // Remove Alpha
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
                    return brightness > 0.5 ? "Black" : "White";
                }
                catch { return "Black"; }
            }
        }

        public System.Windows.Media.Brush TextBrush
        {
            get
            {
                var alpha = (byte)(Math.Clamp(Opacity - 0.05, 0.35, 1.0) * 255);
                return TextColor == "White"
                    ? new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
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
                var hex = color.Replace("#", "");
                if (hex.Length == 8)
                {
                    hex = hex.Substring(2);
                }

                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);

                r = Math.Clamp((int)(r * factor), 0, 255);
                g = Math.Clamp((int)(g * factor), 0, 255);
                b = Math.Clamp((int)(b * factor), 0, 255);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return color;
            }
        }

        private void OnDeleteRequested()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        public NoteModel GetModel() => _model;
    }
}
