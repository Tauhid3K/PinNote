using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
#if WINDOWS
using System.Windows.Media;
#endif
using PinNote.Models;
using PinNote.Services;

namespace PinNote.ViewModels
{
    public class NoteViewModel : ViewModelBase
    {
        private readonly NoteModel _model;
        private readonly MainViewModel _mainViewModel;
        private readonly ObservableCollection<ChecklistItemViewModel> _checklistItems = new();
        private string? _searchableTextCache;
        private bool _updatingTitleFromBody;

        public NoteViewModel(NoteModel model, MainViewModel mainViewModel)
        {
            _model = model;
            _mainViewModel = mainViewModel;

            // Initialize checklist items from model
            if (_model.ChecklistItems != null)
            {
                foreach (var item in _model.ChecklistItems.OrderBy(i => i.Order))
                {
                    _checklistItems.Add(CreateChecklistItemViewModel(item));
                }
            }

            DeleteCommand = new RelayCommand(_ => OnDeleteRequested());
            DuplicateCommand = new RelayCommand(_ => _mainViewModel.DuplicateNote(this));
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
                        _updatingTitleFromBody = true;
                        try
                        {
                            TitleFontSize = titleFs;
                            BodyFontSize = bodyFs;
                        }
                        finally
                        {
                            _updatingTitleFromBody = false;
                        }
                    }
                }
            });

            ToggleCrystalClearCommand = new RelayCommand(_ => IsCrystalClear = !IsCrystalClear);

            ChangeTextColorCommand = new RelayCommand(colorParam =>
            {
                if (colorParam is string color)
                {
                    if (color == "Auto")
                    {
                        CustomTextColor = null;
                    }
                    else
                    {
                        CustomTextColor = color;
                    }
                }
            });

            SetTextOpacityCommand = new RelayCommand(p =>
            {
                if (p is string str && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                {
                    TextOpacity = val;
                }
                else if (p is double d)
                {
                    TextOpacity = d;
                }
            });

            AddChecklistItemCommand = new RelayCommand(param => AddChecklistItem(param as string));
            ToggleNoteTypeCommand = new RelayCommand(_ => ToggleNoteType());
        }

        public Guid Id => _model.Id;
        public ICommand NewNoteCommand { get; }
        public ICommand ToggleCrystalClearCommand { get; }
        public ICommand ChangeTextColorCommand { get; }
        public ICommand SetTextOpacityCommand { get; }
        public ICommand AddChecklistItemCommand { get; }
        public ICommand ToggleNoteTypeCommand { get; }

        public NoteType NoteType
        {
            get => _model.NoteType;
            set
            {
                if (_model.NoteType != value)
                {
                    _model.NoteType = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsChecklist));
                    InvalidateSearchableText();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsChecklist
        {
            get => NoteType == NoteType.Checklist;
            set
            {
                if (value != IsChecklist)
                {
                    NoteType = value ? NoteType.Checklist : NoteType.Text;
                }
            }
        }

        public ObservableCollection<ChecklistItemViewModel> ChecklistItems => _checklistItems;

        public void AddChecklistItem(string? text = null)
        {
            var itemModel = new ChecklistItemModel
            {
                Id = Guid.NewGuid(),
                Text = text ?? string.Empty,
                IsChecked = false,
                Order = _checklistItems.Count
            };

            var itemVm = CreateChecklistItemViewModel(itemModel);
            _checklistItems.Add(itemVm);
            SyncModelChecklist();
            InvalidateSearchableText();
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveChecklistItem(ChecklistItemViewModel item)
        {
            if (_checklistItems.Remove(item))
            {
                ReorderChecklistItems();
                SyncModelChecklist();
                InvalidateSearchableText();
                SaveRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void MoveChecklistItemUp(ChecklistItemViewModel item)
        {
            int index = _checklistItems.IndexOf(item);
            if (index > 0)
            {
                _checklistItems.Move(index, index - 1);
                ReorderChecklistItems();
                SyncModelChecklist();
                InvalidateSearchableText();
                SaveRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void MoveChecklistItemDown(ChecklistItemViewModel item)
        {
            int index = _checklistItems.IndexOf(item);
            if (index >= 0 && index < _checklistItems.Count - 1)
            {
                _checklistItems.Move(index, index + 1);
                ReorderChecklistItems();
                SyncModelChecklist();
                InvalidateSearchableText();
                SaveRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ReorderChecklistItems()
        {
            for (int i = 0; i < _checklistItems.Count; i++)
            {
                _checklistItems[i].Order = i;
            }
        }

        private void SyncModelChecklist()
        {
            _model.ChecklistItems = _checklistItems.Select(vm => vm.GetModel()).ToList();
        }

        private ChecklistItemViewModel CreateChecklistItemViewModel(ChecklistItemModel model)
        {
            return new ChecklistItemViewModel(
                model,
                deleteAction: RemoveChecklistItem,
                moveUpAction: MoveChecklistItemUp,
                moveDownAction: MoveChecklistItemDown,
                onChangedAction: () =>
                {
                    SyncModelChecklist();
                    InvalidateSearchableText();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                });
        }

        public void ToggleNoteType()
        {
            if (NoteType == NoteType.Text)
            {
                // Convert text to checklist
                string plainText = GetPlainText();
                _checklistItems.Clear();

                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    var lines = plainText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        bool isChecked = false;
                        if (trimmed.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
                        {
                            isChecked = true;
                            trimmed = trimmed.Substring(trimmed.IndexOf(']') + 1).Trim();
                        }
                        else if (trimmed.StartsWith("[ ] ", StringComparison.OrdinalIgnoreCase) ||
                                 trimmed.StartsWith("- [ ] ", StringComparison.OrdinalIgnoreCase))
                        {
                            isChecked = false;
                            trimmed = trimmed.Substring(trimmed.IndexOf(']') + 1).Trim();
                        }
                        else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                        {
                            trimmed = trimmed.Substring(2).Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            var item = new ChecklistItemModel
                            {
                                Text = trimmed,
                                IsChecked = isChecked,
                                Order = _checklistItems.Count
                            };
                            _checklistItems.Add(CreateChecklistItemViewModel(item));
                        }
                    }
                }

                if (_checklistItems.Count == 0)
                {
                    AddChecklistItem(string.Empty);
                }

                NoteType = NoteType.Checklist;
                SyncModelChecklist();
            }
            else
            {
                // Convert checklist to text
                var sb = new System.Text.StringBuilder();
                foreach (var item in _checklistItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Text)) continue;
                    sb.AppendLine(item.IsChecked ? $"[x] {item.Text}" : $"[ ] {item.Text}");
                }

                Content = sb.ToString().TrimEnd();
                NoteType = NoteType.Text;
            }

            InvalidateSearchableText();
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        public string SearchableText
        {
            get
            {
                if (_searchableTextCache == null)
                {
                    _searchableTextCache = SearchService.ExtractSearchableText(_model.Content, _model.NoteType, _model.ChecklistItems);
                }
                return _searchableTextCache;
            }
        }

        public void InvalidateSearchableText()
        {
            _searchableTextCache = null;
            OnPropertyChanged(nameof(SearchableText));
            OnPropertyChanged(nameof(PlainTextPreview));
        }

        public string Title
        {
            get => _model.Title;
            set
            {
                if (_model.Title != value)
                {
                    _model.Title = value;
                    _model.ModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ModifiedAt));
                    InvalidateSearchableText();
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public DateTime CreatedAt => _model.CreatedAt;
        public DateTime ModifiedAt => _model.ModifiedAt;

#if WINDOWS
        public System.Windows.Media.Brush PreviewTextBrush
        {
            get
            {
                var resultColor = TextColor == "White"
                    ? System.Windows.Media.Colors.White
                    : System.Windows.Media.Colors.Black;

                var resultBrush = new SolidColorBrush(resultColor);
                if (resultBrush.CanFreeze) resultBrush.Freeze();
                return resultBrush;
            }
        }
#endif

        public string Content
        {
            get => _model.Content;
            set
            {
                if (_model.Content != value)
                {
                    _model.Content = value;
                    _model.ModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ModifiedAt));
                    InvalidateSearchableText();
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
            get => _model.Opacity <= 0 ? 1.0 : _model.Opacity;
            set
            {
                if (Math.Abs(_model.Opacity - value) > 0.001)
                {
                    _model.Opacity = Math.Clamp(value, 0.1, 1.0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveBodyColor));
                    OnPropertyChanged(nameof(EffectiveTitleBarColor));
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double TextOpacity
        {
            get => _model.TextOpacity <= 0 ? 1.0 : _model.TextOpacity;
            set
            {
                if (Math.Abs(_model.TextOpacity - value) > 0.001)
                {
                    _model.TextOpacity = Math.Clamp(value, 0.1, 1.0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveTextColor));
                    OnPropertyChanged(nameof(HeaderIconColor));
                    OnPropertyChanged("TextBrush");
                    OnPropertyChanged("PreviewTextBrush");
#if !WINDOWS
                    OnPropertyChanged(nameof(TextColorObj));
#endif
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsCrystalClear
        {
            get => _model.IsCrystalClear;
            set
            {
                if (_model.IsCrystalClear != value)
                {
                    _model.IsCrystalClear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveBodyColor));
                    OnPropertyChanged(nameof(EffectiveTitleBarColor));
                    OnPropertyChanged(nameof(EffectiveBorderBrush));
                    OnPropertyChanged(nameof(EffectiveTextColor));
                    OnPropertyChanged(nameof(HeaderIconColor));
                    OnPropertyChanged("TextBrush");
                    OnPropertyChanged("PreviewTextBrush");
#if !WINDOWS
                    OnPropertyChanged(nameof(TextColorObj));
#endif
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

        public string TitleBarColor
        {
            get => _model.TitleBarColor;
            set
            {
                if (_model.TitleBarColor != value)
                {
                    _model.TitleBarColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveTitleBarColor));
                    OnPropertyChanged("TextBrush");
                    OnPropertyChanged("PreviewTextBrush");
                    OnPropertyChanged(nameof(TextColor));
                    OnPropertyChanged(nameof(CardTextColor));
                    OnPropertyChanged(nameof(CardHeaderTextColor));
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
                    OnPropertyChanged(nameof(EffectiveBodyColor));
                    OnPropertyChanged(nameof(EffectiveTitleBarColor));
                    OnPropertyChanged(nameof(EffectiveTextColor));
                    OnPropertyChanged(nameof(HeaderIconColor));
                    OnPropertyChanged("TextBrush");
                    OnPropertyChanged("PreviewTextBrush");
                    OnPropertyChanged(nameof(TextColor));
                    OnPropertyChanged(nameof(CardTextColor));
                    OnPropertyChanged(nameof(CardBodyTextColor));
                    OnPropertyChanged(nameof(CardHeaderTextColor));
                    OnPropertyChanged(nameof(ActiveTitleBarColor));
#if !WINDOWS
                    OnPropertyChanged(nameof(NoteColorObj));
                    OnPropertyChanged(nameof(TextColorObj));
#endif
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string? CustomTextColor
        {
            get => _model.CustomTextColor;
            set
            {
                if (_model.CustomTextColor != value)
                {
                    _model.CustomTextColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveTextColor));
                    OnPropertyChanged(nameof(HeaderIconColor));
                    OnPropertyChanged("TextBrush");
                    OnPropertyChanged("PreviewTextBrush");
#if !WINDOWS
                    OnPropertyChanged(nameof(TextColorObj));
#endif
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string EffectiveBodyColor
        {
            get
            {
                if (IsCrystalClear)
                {
                    byte alpha = (byte)(Math.Clamp(Opacity, 0.0, 1.0) * 45);
                    if (TryParseHexColor(BodyColor, out var c))
                    {
                        return $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                    }
                    return $"#{alpha:X2}FFFFFF";
                }

                if (TryParseHexColor(BodyColor, out var col))
                {
                    byte a = (byte)(Math.Clamp(Opacity, 0.1, 1.0) * 255);
                    return $"#{a:X2}{col.R:X2}{col.G:X2}{col.B:X2}";
                }
                return BodyColor;
            }
        }

        public string EffectiveTitleBarColor
        {
            get
            {
                if (IsCrystalClear)
                {
                    byte alpha = (byte)(Math.Clamp(Opacity, 0.0, 1.0) * 60);
                    var baseCol = !string.IsNullOrWhiteSpace(TitleBarColor) ? TitleBarColor : BodyColor;
                    if (TryParseHexColor(baseCol, out var c))
                    {
                        return $"#{alpha:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                    }
                    return $"#{alpha:X2}FFFFFF";
                }

                var tColor = !string.IsNullOrWhiteSpace(TitleBarColor) ? TitleBarColor : BodyColor;
                if (TryParseHexColor(tColor, out var col))
                {
                    byte a = (byte)(Math.Clamp(Opacity, 0.15, 1.0) * 255);
                    return $"#{a:X2}{col.R:X2}{col.G:X2}{col.B:X2}";
                }
                return TitleBarColor;
            }
        }

        public string EffectiveBorderBrush => IsCrystalClear ? "#44FFFFFF" : "#44000000";

        public string EffectiveTextColor
        {
            get
            {
                var alpha = (byte)(Math.Clamp(TextOpacity, 0.1, 1.0) * 255);
                if (!string.IsNullOrEmpty(CustomTextColor) && TryParseHexColor(CustomTextColor, out var customP))
                {
                    return $"#{alpha:X2}{customP.R:X2}{customP.G:X2}{customP.B:X2}";
                }

                if (IsCrystalClear)
                {
                    if (TryParseHexColor(BodyColor, out var parsed))
                    {
                        double brightness = (0.299 * parsed.R + 0.587 * parsed.G + 0.114 * parsed.B) / 255.0;
                        if (brightness < 0.25)
                        {
                            return $"#{alpha:X2}FFFFFF";
                        }
                        return $"#{alpha:X2}{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}";
                    }
                    return $"#{alpha:X2}FFFFFF";
                }

                if (TextColor == "White")
                {
                    return $"#{alpha:X2}FFFFFF";
                }
                return $"#{alpha:X2}000000";
            }
        }

#if !WINDOWS
        public Avalonia.Media.Color NoteColorObj
        {
            get
            {
                if (Avalonia.Media.Color.TryParse(BodyColor, out var c))
                    return c;
                return Avalonia.Media.Colors.White;
            }
            set
            {
                var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
                BodyColor = hex;
                TitleBarColor = CreateTitleBarColor(hex, hex);
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoteColorObj));
            }
        }

        public Avalonia.Media.Color TextColorObj
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomTextColor) && Avalonia.Media.Color.TryParse(CustomTextColor, out var c))
                    return c;
                if (Avalonia.Media.Color.TryParse(EffectiveTextColor, out var eff))
                    return eff;
                return Avalonia.Media.Colors.Black;
            }
            set
            {
                var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
                CustomTextColor = hex;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextColorObj));
            }
        }
#endif

        public string TextColor
        {
            get
            {
                try
                {
                    if (TryParseHexColor(BodyColor, out var c))
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

#if WINDOWS
        public System.Windows.Media.Brush TextBrush
        {
            get
            {
                var alpha = (byte)(Math.Clamp(TextOpacity, 0.2, 1.0) * 255);

                System.Windows.Media.Color resultColor;
                if (!string.IsNullOrEmpty(CustomTextColor) && TryParseHexColor(CustomTextColor, out var customP))
                {
                    resultColor = System.Windows.Media.Color.FromArgb(alpha, customP.R, customP.G, customP.B);
                }
                else if (IsCrystalClear)
                {
                    if (TryParseHexColor(BodyColor, out var parsed))
                    {
                        resultColor = System.Windows.Media.Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B);
                    }
                    else
                    {
                        resultColor = System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255);
                    }
                }
                else
                {
                    resultColor = TextColor == "White"
                        ? System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255)
                        : System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0);
                }

                var resultBrush = new SolidColorBrush(resultColor);
                if (resultBrush.CanFreeze) resultBrush.Freeze();
                return resultBrush;
            }
        }
#endif

        public string ActiveTitleBarColor => CreateTitleBarColor(TitleBarColor, BodyColor);

        public bool IsPinned
        {
            get => _model.IsPinned;
            set
            {
                if (_model.IsPinned != value)
                {
                    _model.IsPinned = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PinOpacity));
                    SaveRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double PinOpacity => IsPinned ? 1.0 : 0.65;

        public string HeaderIconColor
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomTextColor)) return CustomTextColor;
                if (IsCrystalClear)
                {
                    if (TryParseHexColor(BodyColor, out var parsed))
                    {
                        double brightness = (0.299 * parsed.R + 0.587 * parsed.G + 0.114 * parsed.B) / 255.0;
                        if (brightness < 0.25) return "#FFFFFF";
                        return BodyColor;
                    }
                    return "#FFFFFF";
                }

                return TextColor == "White" ? "#FFFFFF" : "#1A1A1A";
            }
        }

        public string CardHeaderTextColor
        {
            get
            {
                if (TryParseHexColor(TitleBarColor, out var c))
                {
                    double brightness = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                    return brightness > 0.55 ? "#1A1A1A" : "#FFFFFF";
                }
                return "#1A1A1A";
            }
        }

        public string CardBodyTextColor
        {
            get
            {
                if (TryParseHexColor(BodyColor, out var c))
                {
                    double brightness = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                    return brightness > 0.55 ? "#1A1A1A" : "#FFFFFF";
                }
                return "#1A1A1A";
            }
        }

        public string CardTextColor => CardBodyTextColor;

        public string PlainTextPreview => GetPlainText();

        public void UpdateBounds(double x, double y, double width, double height)
        {
            bool changed = false;
            if (Math.Abs(_model.X - x) > 0.5) { _model.X = x; OnPropertyChanged(nameof(X)); changed = true; }
            if (Math.Abs(_model.Y - y) > 0.5) { _model.Y = y; OnPropertyChanged(nameof(Y)); changed = true; }
            if (Math.Abs(_model.Width - width) > 0.5) { _model.Width = width; OnPropertyChanged(nameof(Width)); changed = true; }
            if (Math.Abs(_model.Height - height) > 0.5) { _model.Height = height; OnPropertyChanged(nameof(Height)); changed = true; }

            if (changed)
            {
                SaveRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public ICommand DeleteCommand { get; }
        public ICommand DuplicateCommand { get; }
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
                if (!TryParseHexColor(color, out var c)) return color;

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

        private static bool TryParseHexColor(string? input, out (byte R, byte G, byte B) color)
        {
            color = (0, 0, 0);
            if (string.IsNullOrWhiteSpace(input)) return false;

            var hex = input.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                    byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                {
                    color = (r, g, b);
                    return true;
                }
            }
            else if (hex.Length == 8)
            {
                if (byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                    byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                {
                    color = (r, g, b);
                    return true;
                }
            }

            return false;
        }

        private void OnDeleteRequested()
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        public string GetPlainText()
        {
            return SearchService.ExtractSearchableText(_model.Content, _model.NoteType, _model.ChecklistItems);
        }

        public NoteModel GetModel() => _model;
    }
}
