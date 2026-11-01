using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;

namespace PinNote.ViewModels
{
    /// <summary>Note sort order applied in the dashboard.</summary>
    public enum NoteSortMode
    {
        /// <summary>Order notes were loaded / created (insertion order).</summary>
        Default = 0,
        /// <summary>Alphabetical by title A → Z.</summary>
        TitleAscending = 1,
        /// <summary>Alphabetical by title Z → A.</summary>
        TitleDescending = 2,
        /// <summary>Most recently modified first.</summary>
        ModifiedNewest = 3,
        /// <summary>Least recently modified first.</summary>
        ModifiedOldest = 4,
        /// <summary>Newest-created first.</summary>
        CreatedNewest = 5,
        /// <summary>Oldest-created first.</summary>
        CreatedOldest = 6,
    }

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
        private NoteSortMode _sortMode = NoteSortMode.Default;

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

        public StorageService Storage => _storageService;

        public MainViewModel(IWindowManager? windowManager = null, StorageService? customStorageService = null)
        {
            _windowManager = windowManager;
            _showUiOnStartup = _appStateService.GetShowUiOnStartup();
            _showTrayIcon = _appStateService.GetShowTrayIcon();
            _showInTaskbar = _appStateService.GetShowInTaskbar();
            _storageService = customStorageService ?? new StorageService();
            _storageService.SaveFailed += (_, msg) => _windowManager?.ShowErrorMessage("Storage Error", $"Failed to save notes: {msg}");
            _storageService.LoadFailed += (_, msg) => _windowManager?.ShowErrorMessage("Storage Error", $"Failed to load notes: {msg}");

            // Restore persisted sort preference
            int savedSort = _appStateService.GetSortMode();
            if (Enum.IsDefined(typeof(NoteSortMode), savedSort))
            {
                _sortMode = (NoteSortMode)savedSort;
            }

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
                _windowManager?.FlushPendingEdits();
                SaveNotesImmediately();
                _windowManager?.Shutdown();
            });

            ToggleShowUiOnStartupCommand = new RelayCommand(_ => ShowUiOnStartup = !ShowUiOnStartup);
            ToggleShowTrayIconCommand = new RelayCommand(_ => ShowTrayIcon = !ShowTrayIcon);
            ToggleShowInTaskbarCommand = new RelayCommand(_ => ShowInTaskbar = !ShowInTaskbar);
            SetThemeCommand = new RelayCommand(p => ThemeMode = (p as string) ?? "System");

            ExportNotesCommand = new RelayCommand(async _ => await ExportNotesAsync());
            ImportNotesCommand = new RelayCommand(async _ => await ImportNotesAsync());
            BackupNotesCommand = new RelayCommand(_ => CreateBackup());
            SetSortModeCommand = new RelayCommand(p =>
            {
                if (p is string s && Enum.TryParse<NoteSortMode>(s, out var mode))
                {
                    SortMode = mode;
                }
            });

            // Load theme preference
            _themeMode = _appStateService.GetThemeMode();

            LoadNotes();
        }

        public ObservableCollection<NoteViewModel> Notes => _notes;

        /// <summary>
        /// Notes filtered by the current search query and sorted by the current <see cref="SortMode"/>.
        /// Pinned notes always appear first within each sort group.
        /// </summary>
        public IEnumerable<NoteViewModel> SortedFilteredNotes
        {
            get
            {
                IEnumerable<NoteViewModel> source = string.IsNullOrWhiteSpace(SearchText)
                    ? _notes
                    : _notes.Where(n => SearchService.Matches(n.Title, n.SearchableText, SearchText));

                // Pinned notes always lead; secondary key is the selected sort criterion
                return _sortMode switch
                {
                    NoteSortMode.TitleAscending =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase),
                    NoteSortMode.TitleDescending =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenByDescending(n => n.Title, StringComparer.CurrentCultureIgnoreCase),
                    NoteSortMode.ModifiedNewest =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenByDescending(n => n.ModifiedAt),
                    NoteSortMode.ModifiedOldest =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenBy(n => n.ModifiedAt),
                    NoteSortMode.CreatedNewest =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenByDescending(n => n.CreatedAt),
                    NoteSortMode.CreatedOldest =>
                        source.OrderByDescending(n => n.IsPinned)
                              .ThenBy(n => n.CreatedAt),
                    _ => // Default: pinned first, then insertion order
                        source.OrderByDescending(n => n.IsPinned),
                };
            }
        }

        /// <summary>Kept for backward compatibility with existing dashboard XAML bindings.</summary>
        public IEnumerable<NoteViewModel> FilteredNotes => SortedFilteredNotes;

        public NoteSortMode SortMode
        {
            get => _sortMode;
            set
            {
                if (SetProperty(ref _sortMode, value))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
                    OnPropertyChanged(nameof(FilteredNotes));
                    // Persist the preference
                    _appStateService.SetSortMode((int)value);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
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
        public ICommand ExportNotesCommand { get; }
        public ICommand ImportNotesCommand { get; }
        public ICommand BackupNotesCommand { get; }
        public ICommand SetSortModeCommand { get; }

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
                AddNoteViewModel(noteModel, openWindow: noteModel.IsVisible);
            }
        }

        public void CreateWelcomeNote()
        {
            ShowInstructions();
        }

        public void CreateNewNote(bool openWindow = true)
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
                NoteType = source.NoteType,
                ChecklistItems = source.ChecklistItems.Select(i => i.GetModel().Clone()).ToList(),
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
                TextOpacity = source.TextOpacity,
                IsVisible = true
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
            OnPropertyChanged(nameof(SortedFilteredNotes));
            OnPropertyChanged(nameof(FilteredNotes));
        }

        public void OpenNote(NoteViewModel viewModel)
        {
            viewModel.GetModel().IsVisible = true;
            _windowManager?.OpenNote(viewModel);
        }

        public void CloseNote(Guid noteId)
        {
            var vm = _notes.FirstOrDefault(n => n.Id == noteId);
            if (vm != null)
            {
                vm.GetModel().IsVisible = false;
            }
            _windowManager?.CloseNote(noteId);
            QueueSave();
        }

        public void DeleteNote(NoteViewModel viewModel)
        {
            _windowManager?.CloseNote(viewModel.Id);
            _notes.Remove(viewModel);
            SaveNotesImmediately();
            OnPropertyChanged(nameof(SortedFilteredNotes));
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

            long snapshotTime = DateTime.UtcNow.Ticks;

            // 1. Snapshot and clone on the UI thread: isolated from UI modifications
            var models = _notes.Select(n => n.GetModel().Clone()).ToList();

            // 2. Serialize on the UI thread in the same execution path: immutable snapshot
            string json = JsonConvert.SerializeObject(models, Formatting.Indented);

            // 3. Persist to storage safely using the immutable serialized JSON
            _storageService.SaveNotesJson(json, models.Count, snapshotTime);
        }

        public async Task ExportNotesAsync()
        {
            if (_windowManager == null) return;

            string defaultName = $"pinnote_backup_{DateTime.UtcNow:yyyyMMdd}.json";
            string filter = "JSON Notes Backup (*.json)|*.json|All Files (*.*)|*.*";
            string? filePath = await _windowManager.ShowSaveFileDialogAsync("Export Notes", defaultName, filter);

            if (string.IsNullOrWhiteSpace(filePath)) return;

            // Make sure current changes are committed
            _windowManager.FlushPendingEdits();

            var models = _notes.Select(n => n.GetModel().Clone()).ToList();
            var result = ImportExportService.ExportNotes(models, filePath);

            if (result.Success)
            {
                _windowManager.ShowMessage("Export Successful", result.Message);
            }
            else
            {
                _windowManager.ShowErrorMessage("Export Failed", result.Message);
            }
        }

        public async Task ImportNotesAsync()
        {
            if (_windowManager == null) return;

            string filter = "JSON Notes Backup (*.json)|*.json|All Files (*.*)|*.*";
            string? filePath = await _windowManager.ShowOpenFileDialogAsync("Import Notes", filter);

            if (string.IsNullOrWhiteSpace(filePath)) return;

            // Make sure current changes are committed
            _windowManager.FlushPendingEdits();

            var existingModels = _notes.Select(n => n.GetModel().Clone()).ToList();
            var result = ImportExportService.ImportNotes(filePath, existingModels, _storageService.StorageFolderPath);

            if (result.Success && result.ImportedNotes.Count > 0)
            {
                foreach (var noteModel in result.ImportedNotes)
                {
                    AddNoteViewModel(noteModel, openWindow: true);
                }

                SaveNotesImmediately();
                _windowManager.ShowMessage("Import Successful", result.Message);
            }
            else
            {
                _windowManager.ShowErrorMessage("Import Failed", result.Message);
            }
        }

        public void CreateBackup()
        {
            try
            {
                _windowManager?.FlushPendingEdits();
                SaveNotesImmediately();
                string backupPath = _storageService.CreateBackup("manual");
                _windowManager?.ShowMessage("Backup Created", $"Backup successfully saved to:\n{backupPath}");
            }
            catch (Exception ex)
            {
                _windowManager?.ShowErrorMessage("Backup Failed", ex.Message);
            }
        }

        public void HandleExternalCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            void Execute()
            {
                switch (command.ToUpperInvariant())
                {
                    case "NEW_NOTE":
                        CreateNewNote(openWindow: true);
                        _windowManager?.BringNotesOnTop();
                        break;
                    case "SHOW_ALL":
                        ShowAllNotes();
                        break;
                    case "HIDE_ALL":
                        HideAllNotes();
                        break;
                    case "SHOW_DASHBOARD":
                        ShowDashboard();
                        break;
                    case "BRING_ON_TOP":
                        BringNotesOnTop();
                        break;
                }
            }

            if (_windowManager != null)
            {
                _windowManager.RunOnUiThread(Execute);
            }
            else
            {
                Execute();
            }
        }

        private void Notes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SortedFilteredNotes));
            OnPropertyChanged(nameof(FilteredNotes));
        }

        private void NoteViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NoteViewModel.Title) or nameof(NoteViewModel.Content)
                or nameof(NoteViewModel.SearchableText) or nameof(NoteViewModel.IsPinned)
                or nameof(NoteViewModel.ModifiedAt))
            {
                OnPropertyChanged(nameof(SortedFilteredNotes));
                OnPropertyChanged(nameof(FilteredNotes));
            }
        }
    }
}
