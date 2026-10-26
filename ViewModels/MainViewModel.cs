using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;

namespace PinNote.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AppStateService _appStateService = new();
        private bool _showUiOnStartup;
        private readonly StorageService _storageService;
        private readonly ObservableCollection<NoteViewModel> _notes;
        private string _searchText = string.Empty;
        private bool _isStartupEnabled;
        private bool _startMinimized;
        private bool _showTrayIcon;
        private bool _showInTaskbar;
        private string _themeMode;
        private IWindowManager? _windowManager;
        private ITrayService? _trayService;
        private readonly System.Timers.Timer _saveDebounceTimer;

        public IWindowManager? WindowManager
        {
            get => _windowManager;
            set => _windowManager = value;
        }

        public ITrayService? TrayService
        {
            get => _trayService;
            set
            {
                _trayService = value;
                _trayService?.SetVisible(_showTrayIcon);
            }
        }

        public MainViewModel(IWindowManager? windowManager = null)
        {
            _windowManager = windowManager;
            _showUiOnStartup = _appStateService.GetShowUiOnStartup();
            _showTrayIcon = _appStateService.GetShowTrayIcon();
            _showInTaskbar = _appStateService.GetShowInTaskbar();
            _storageService = new StorageService();
            _storageService.SaveFailed += (_, msg) => _windowManager?.ShowErrorMessage("Storage Error", $"Failed to save notes: {msg}");
            _storageService.LoadFailed += (_, msg) => _windowManager?.ShowErrorMessage("Storage Error", $"Failed to load notes: {msg}");

            _saveDebounceTimer = new System.Timers.Timer(300) { AutoReset = false };
            _saveDebounceTimer.Elapsed += (_, _) => SaveNotesImmediately();

            _notes = new ObservableCollection<NoteViewModel>();
            _notes.CollectionChanged += Notes_CollectionChanged;
            _isStartupEnabled = StartupService.IsStartupEnabled();
            _startMinimized = _appStateService.GetStartMinimized();

            NewNoteCommand = new RelayCommand(_ => CreateNewNote());
            ShowAllCommand = new RelayCommand(_ => ShowAllNotes());
            HideAllCommand = new RelayCommand(_ => HideAllNotes());
            BringNotesOnTopCommand = new RelayCommand(_ => BringNotesOnTop());
            ToggleStartupCommand = new RelayCommand(_ => ToggleStartup());
            SettingsCommand = new RelayCommand(_ => ShowAllNotes());
            HelpCommand = new RelayCommand(_ => ShowInstructions());
            ExitCommand = new RelayCommand(_ =>
            {
                SaveNotesImmediately();
                _windowManager?.Shutdown();
            });

            ToggleShowUiOnStartupCommand = new RelayCommand(_ => ShowUiOnStartup = !ShowUiOnStartup);
            ToggleShowTrayIconCommand = new RelayCommand(_ => ShowTrayIcon = !ShowTrayIcon);
            ToggleShowInTaskbarCommand = new RelayCommand(_ => ShowInTaskbar = !ShowInTaskbar);
            SetThemeCommand = new RelayCommand(p => ThemeMode = (p as string) ?? "System");

            // Load theme preference
            _themeMode = _appStateService.GetThemeMode();

            LoadNotes();
        }

        public ObservableCollection<NoteViewModel> Notes => _notes;

        public IEnumerable<NoteViewModel> FilteredNotes
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return _notes;
                }

                var filter = SearchText.Trim();
                return _notes.Where(note =>
                    (note.Title ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    note.GetPlainText().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(FilteredNotes));
                }
            }
        }

        public ICommand NewNoteCommand { get; }
        public ICommand ShowAllCommand { get; }
        public ICommand HideAllCommand { get; }
        public ICommand BringNotesOnTopCommand { get; }
        public ICommand ToggleStartupCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand ExitCommand { get; }

        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (_isStartupEnabled == value)
                {
                    return;
                }

                _isStartupEnabled = value;
                StartupService.SetStartup(value);
                OnPropertyChanged();
            }
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set
            {
                if (SetProperty(ref _startMinimized, value))
                {
                    _appStateService.SetStartMinimized(value);
                }
            }
        }

        public bool ShowUiOnStartup
        {
            get => _showUiOnStartup;
            set
            {
                if (SetProperty(ref _showUiOnStartup, value))
                {
                    _appStateService.SetShowUiOnStartup(value);
                }
            }
        }

        public bool ShowTrayIcon
        {
            get => _showTrayIcon;
            set
            {
                if (SetProperty(ref _showTrayIcon, value))
                {
                    _appStateService.SetShowTrayIcon(value);
                    _trayService?.SetVisible(value);
                }
            }
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set
            {
                if (SetProperty(ref _showInTaskbar, value))
                {
                    _appStateService.SetShowInTaskbar(value);
                    _windowManager?.SetShowInTaskbar(value);
                }
            }
        }

        public ICommand ToggleShowUiOnStartupCommand { get; }
        public ICommand ToggleShowTrayIconCommand { get; }
        public ICommand ToggleShowInTaskbarCommand { get; }
        public ICommand SetThemeCommand { get; }

        public string ThemeMode
        {
            get => _themeMode ?? "System";
            set
            {
                if (value == _themeMode) return;
                _themeMode = value ?? "System";
                _appStateService.SetThemeMode(_themeMode);
                _windowManager?.ApplyTheme(_themeMode);
                OnPropertyChanged();
            }
        }

        private void ToggleStartup()
        {
            IsStartupEnabled = !IsStartupEnabled;
        }

        public void ShowInstructions()
        {
            _windowManager?.ShowInstructions();
        }

        private void LoadNotes()
        {
            var savedNotes = _storageService.LoadNotes();
            if (!savedNotes.Any())
            {
                return;
            }

            foreach (var noteModel in savedNotes)
            {
                AddNoteViewModel(noteModel, openWindow: true);
            }
        }

        public void CreateWelcomeNote()
        {
            ShowInstructions();
        }

        private void CreateNewNote(bool openWindow = true)
        {
            var newNote = new NoteModel();
            AddNoteViewModel(newNote, openWindow);
            SaveNotesImmediately();
        }

        public void DuplicateNote(NoteViewModel source)
        {
            if (source == null)
            {
                return;
            }

            var duplicate = new NoteModel
            {
                Title = BuildDuplicateTitle(source.Title),
                Content = source.Content,
                X = source.X + 24,
                Y = source.Y + 24,
                Width = source.Width,
                Height = source.Height,
                Opacity = source.Opacity,
                TitleBarColor = source.TitleBarColor,
                BodyColor = source.BodyColor,
                IsPinned = source.IsPinned,
                TitleFontSize = source.TitleFontSize,
                BodyFontSize = source.BodyFontSize,
                IsCrystalClear = source.IsCrystalClear,
                CustomTextColor = source.CustomTextColor,
                TextOpacity = source.TextOpacity
            };

            AddNoteViewModel(duplicate, openWindow: true);
            SaveNotesImmediately();
        }

        private static string BuildDuplicateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Copy of Note";
            }

            return $"Copy of {title}";
        }

        private void AddNoteViewModel(NoteModel model, bool openWindow = true)
        {
            var viewModel = new NoteViewModel(model, this);
            viewModel.DeleteRequested += async (s, e) =>
            {
                try
                {
                    bool confirmed = true;
                    if (_windowManager != null)
                    {
                        confirmed = await _windowManager.ConfirmDeleteAsync(viewModel.Title);
                    }

                    if (confirmed)
                    {
                        DeleteNote(viewModel);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Delete error: {ex}");
                }
            };
            viewModel.SaveRequested += (s, e) => QueueSave();
            viewModel.PropertyChanged += NoteViewModel_PropertyChanged;

            _notes.Add(viewModel);
            if (openWindow)
            {
                OpenNote(viewModel);
            }
            OnPropertyChanged(nameof(FilteredNotes));
        }

        public void OpenNote(NoteViewModel viewModel)
        {
            _windowManager?.OpenNote(viewModel);
        }

        public void CloseNote(Guid noteId)
        {
            _windowManager?.CloseNote(noteId);
        }

        public void DeleteNote(NoteViewModel viewModel)
        {
            _windowManager?.CloseNote(viewModel.Id);
            _notes.Remove(viewModel);
            SaveNotesImmediately();
            OnPropertyChanged(nameof(FilteredNotes));
        }

        public void ShowDashboard()
        {
            _windowManager?.ShowDashboard();
        }

        public void HideDashboard()
        {
            _windowManager?.HideDashboard();
        }

        public void ShowAllNotes()
        {
            ShowAllNotes(minimizeDashboard: false);
        }

        public void ShowAllNotes(bool minimizeDashboard)
        {
            _windowManager?.ShowAllNotes(minimizeDashboard);
        }

        public void HideAllNotes()
        {
            _windowManager?.HideAllNotes();
        }

        public void BringNotesOnTop()
        {
            _windowManager?.BringNotesOnTop();
        }

        public void QueueSave()
        {
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        public void SaveNotesImmediately()
        {
            _saveDebounceTimer.Stop();

            if (_windowManager != null)
            {
                _windowManager.RunOnUiThread(PerformSave);
            }
            else
            {
                PerformSave();
            }
        }

        private void PerformSave()
        {
            _saveDebounceTimer.Stop();

            // 1. Snapshot and clone on the UI thread: isolated from UI modifications
            var models = _notes.Select(n => n.GetModel().Clone()).ToList();

            // 2. Serialize on the UI thread in the same execution path: immutable snapshot
            string json = JsonConvert.SerializeObject(models, Formatting.Indented);

            // 3. Persist to storage safely using the immutable serialized JSON
            _storageService.SaveNotesJson(json, models.Count);
        }

        private void Notes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FilteredNotes));
        }

        private void NoteViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NoteViewModel.Title) or nameof(NoteViewModel.Content))
            {
                OnPropertyChanged(nameof(FilteredNotes));
            }
        }
    }
}
