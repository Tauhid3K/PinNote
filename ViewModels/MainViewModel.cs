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
        private readonly WorkspaceService _workspaceService;
        private readonly TemplateService _templateService;
        private readonly ReminderService _reminderService;
        private readonly ObservableCollection<NoteViewModel> _notes;
        private readonly ObservableCollection<WorkspaceModel> _workspaces = new();
        private readonly ObservableCollection<string> _allTags = new();
        private readonly ObservableCollection<NoteTemplateModel> _templates = new();
        private WorkspaceModel? _activeWorkspace;
        private string? _selectedTagFilter;
        private string? _selectedCategoryFilter;
        private bool _showRemindersOnly;
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
            _saveDebounceTimer.Elapsed += (_, _) => TriggerDebouncedSave();

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
                _storageService.FlushPendingSave();
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

            _workspaceService = new WorkspaceService(_storageService.StorageFolderPath);
            foreach (var ws in _workspaceService.LoadWorkspaces())
            {
                _workspaces.Add(ws);
            }
            _activeWorkspace = _workspaces.FirstOrDefault(w => w.IsDefault) ?? _workspaces.FirstOrDefault();

            _templateService = new TemplateService(_storageService.StorageFolderPath);
            foreach (var t in _templateService.LoadTemplates())
            {
                _templates.Add(t);
            }

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CreateWorkspaceCommand = new RelayCommand(p => CreateWorkspace(p as string));
            RenameWorkspaceCommand = new RelayCommand(p =>
            {
                if (p is Tuple<WorkspaceModel, string> tuple)
                {
                    RenameWorkspace(tuple.Item1, tuple.Item2);
                }
            });
            DeleteWorkspaceCommand = new RelayCommand(p => DeleteWorkspace(p as WorkspaceModel));
            SwitchWorkspaceCommand = new RelayCommand(p => SwitchWorkspace(p as WorkspaceModel));

            RenameTagCommand = new RelayCommand(p =>
            {
                if (p is Tuple<string, string> tuple)
                {
                    RenameTag(tuple.Item1, tuple.Item2);
                }
            });
            DeleteTagCommand = new RelayCommand(p => DeleteTag(p as string));

            NewNoteFromTemplateCommand = new RelayCommand(p => CreateNoteFromTemplate(p as NoteTemplateModel));
            CreateTemplateFromNoteCommand = new RelayCommand(p => CreateTemplateFromNote(p as NoteViewModel));
            DuplicateTemplateCommand = new RelayCommand(p => DuplicateTemplate(p as NoteTemplateModel));
            DeleteTemplateCommand = new RelayCommand(p => DeleteTemplate(p as NoteTemplateModel));
            ToggleRemindersFilterCommand = new RelayCommand(_ => ShowRemindersOnly = !ShowRemindersOnly);

            _reminderService = new ReminderService(this);
            _reminderService.Start();

            LoadNotes();
            NotifyTagsChanged();
        }

        public ObservableCollection<NoteViewModel> Notes => _notes;

        /// <summary>
        /// Notes filtered by the current search query, tags, category, and workspace,
        /// sorted by the current <see cref="SortMode"/>.
        /// Pinned notes always appear first within each sort group.
        /// </summary>
        public IEnumerable<NoteViewModel> SortedFilteredNotes
        {
            get
            {
                IEnumerable<NoteViewModel> source = _notes;

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    source = source.Where(n => SearchService.Matches(n.Title, n.SearchableText, SearchText));
                }

                if (!string.IsNullOrWhiteSpace(SelectedTagFilter) && SelectedTagFilter != "All Tags")
                {
                    source = source.Where(n => n.Tags.Any(t => string.Equals(t, SelectedTagFilter, StringComparison.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && SelectedCategoryFilter != "All Categories")
                {
                    source = source.Where(n => string.Equals(n.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
                }

                if (_activeWorkspace != null && !string.IsNullOrWhiteSpace(_activeWorkspace.Name) && _activeWorkspace.Name != "All Workspaces")
                {
                    source = source.Where(n => string.Equals(n.WorkspaceName, _activeWorkspace.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (_showRemindersOnly)
                {
                    source = source.Where(n => n.HasReminder);
                }

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

        public ObservableCollection<WorkspaceModel> Workspaces => _workspaces;

        public WorkspaceModel? ActiveWorkspace
        {
            get => _activeWorkspace;
            set
            {
                if (SetProperty(ref _activeWorkspace, value))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
                    OnPropertyChanged(nameof(FilteredNotes));
                }
            }
        }

        public ObservableCollection<string> AllTags => _allTags;
        public IReadOnlyList<string> Categories => DataMigrationService.DefaultCategories;

        public string? SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                if (SetProperty(ref _selectedTagFilter, value))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
                    OnPropertyChanged(nameof(FilteredNotes));
                }
            }
        }

        public string? SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
                    OnPropertyChanged(nameof(FilteredNotes));
                }
            }
        }

        public bool ShowRemindersOnly
        {
            get => _showRemindersOnly;
            set
            {
                if (SetProperty(ref _showRemindersOnly, value))
                {
                    OnPropertyChanged(nameof(SortedFilteredNotes));
                    OnPropertyChanged(nameof(FilteredNotes));
                }
            }
        }

        public ObservableCollection<NoteTemplateModel> Templates => _templates;
        public ReminderService Reminders => _reminderService;

        public ICommand ClearFiltersCommand { get; }
        public ICommand CreateWorkspaceCommand { get; }
        public ICommand RenameWorkspaceCommand { get; }
        public ICommand DeleteWorkspaceCommand { get; }
        public ICommand SwitchWorkspaceCommand { get; }
        public ICommand RenameTagCommand { get; }
        public ICommand DeleteTagCommand { get; }
        public ICommand NewNoteFromTemplateCommand { get; }
        public ICommand CreateTemplateFromNoteCommand { get; }
        public ICommand DuplicateTemplateCommand { get; }
        public ICommand DeleteTemplateCommand { get; }
        public ICommand ToggleRemindersFilterCommand { get; }

        public void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedTagFilter = null;
            SelectedCategoryFilter = null;
            ShowRemindersOnly = false;
        }

        public void NotifyTagsChanged()
        {
            var tags = _notes.SelectMany(n => n.Tags)
                             .Where(t => !string.IsNullOrWhiteSpace(t))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                             .ToList();

            _allTags.Clear();
            foreach (var tag in tags)
            {
                _allTags.Add(tag);
            }
            OnPropertyChanged(nameof(AllTags));
        }

        public void CreateWorkspace(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string clean = name.Trim();
            if (_workspaces.Any(w => string.Equals(w.Name, clean, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var ws = new WorkspaceModel
            {
                Id = Guid.NewGuid(),
                Name = clean,
                IsDefault = false,
                Order = _workspaces.Count
            };

            _workspaces.Add(ws);
            _workspaceService.SaveWorkspaces(_workspaces.ToList());
            ActiveWorkspace = ws;
        }

        public void RenameWorkspace(WorkspaceModel? ws, string? newName)
        {
            if (ws == null || string.IsNullOrWhiteSpace(newName)) return;
            string clean = newName.Trim();
            string oldName = ws.Name;
            ws.Name = clean;

            foreach (var note in _notes.Where(n => string.Equals(n.WorkspaceName, oldName, StringComparison.OrdinalIgnoreCase) || n.WorkspaceId == ws.Id))
            {
                note.WorkspaceName = clean;
            }

            _workspaceService.SaveWorkspaces(_workspaces.ToList());
            SaveNotesImmediately();
            OnPropertyChanged(nameof(ActiveWorkspace));
            OnPropertyChanged(nameof(SortedFilteredNotes));
        }

        public void DeleteWorkspace(WorkspaceModel? ws)
        {
            if (ws == null || ws.IsDefault) return;

            var defaultWs = _workspaces.FirstOrDefault(w => w.IsDefault) ?? _workspaces.FirstOrDefault();
            string targetName = defaultWs?.Name ?? "Default";
            Guid? targetId = defaultWs?.Id ?? WorkspaceService.DefaultWorkspaceId;

            foreach (var note in _notes.Where(n => string.Equals(n.WorkspaceName, ws.Name, StringComparison.OrdinalIgnoreCase) || n.WorkspaceId == ws.Id))
            {
                note.WorkspaceName = targetName;
                note.WorkspaceId = targetId;
            }

            _workspaces.Remove(ws);
            _workspaceService.SaveWorkspaces(_workspaces.ToList());
            SaveNotesImmediately();

            if (ActiveWorkspace == ws)
            {
                ActiveWorkspace = defaultWs;
            }
        }

        public void SwitchWorkspace(WorkspaceModel? ws)
        {
            if (ws == null) return;
            ActiveWorkspace = ws;

            if (_windowManager != null)
            {
                foreach (var note in _notes)
                {
                    if (string.Equals(note.WorkspaceName, ws.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (note.GetModel().IsVisible)
                        {
                            _windowManager.OpenNote(note);
                        }
                    }
                    else
                    {
                        _windowManager.CloseNote(note.Id);
                    }
                }
            }
        }

        public void RenameTag(string? oldTag, string? newTag)
        {
            if (string.IsNullOrWhiteSpace(oldTag) || string.IsNullOrWhiteSpace(newTag)) return;
            string oldClean = oldTag.Trim();
            string newClean = newTag.Trim();

            bool changed = false;
            foreach (var note in _notes)
            {
                if (note.Tags.Contains(oldClean, StringComparer.OrdinalIgnoreCase))
                {
                    note.RemoveTag(oldClean);
                    note.AddTag(newClean);
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyTagsChanged();
                SaveNotesImmediately();
            }
        }

        public void DeleteTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            string clean = tag.Trim();

            bool changed = false;
            foreach (var note in _notes)
            {
                if (note.Tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
                {
                    note.RemoveTag(clean);
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyTagsChanged();
                SaveNotesImmediately();
            }
        }

        public void CreateNoteFromTemplate(NoteTemplateModel? template)
        {
            if (template == null) return;
            var noteModel = template.CreateNote();
            if (ActiveWorkspace != null && !string.IsNullOrWhiteSpace(ActiveWorkspace.Name) && ActiveWorkspace.Name != "All Workspaces")
            {
                noteModel.WorkspaceName = ActiveWorkspace.Name;
                noteModel.WorkspaceId = ActiveWorkspace.Id;
            }
            AddNoteViewModel(noteModel, openWindow: true);
            SaveNotesImmediately();
        }

        public void CreateTemplateFromNote(NoteViewModel? note)
        {
            if (note == null) return;
            var template = new NoteTemplateModel
            {
                Id = Guid.NewGuid(),
                Name = $"{note.Title} Template",
                Description = $"Custom template created from '{note.Title}'",
                Content = note.Content,
                NoteType = note.NoteType,
                ChecklistItems = note.ChecklistItems.Select(i => i.GetModel().Clone()).ToList(),
                Tags = note.Tags.ToList(),
                Category = note.Category,
                IsBuiltIn = false
            };

            _templates.Add(template);
            _templateService.SaveCustomTemplates(_templates);
        }

        public void DuplicateTemplate(NoteTemplateModel? source)
        {
            if (source == null) return;
            var dup = _templateService.DuplicateTemplate(source);
            _templates.Add(dup);
            _templateService.SaveCustomTemplates(_templates);
        }

        public void DeleteTemplate(NoteTemplateModel? template)
        {
            if (template == null || template.IsBuiltIn) return;
            _templates.Remove(template);
            _templateService.SaveCustomTemplates(_templates);
        }

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
            var newNote = new NoteModel { SchemaVersion = 2 };
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
                SchemaVersion = 2,
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
                IsVisible = true,
                Tags = source.Tags.ToList(),
                Category = source.Category,
                WorkspaceId = source.WorkspaceId,
                WorkspaceName = source.WorkspaceName,
                DueDate = source.DueDate,
                IsMarkdownPreview = source.IsMarkdownPreview
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

        private void TriggerDebouncedSave()
        {
            _saveDebounceTimer.Stop();

            if (_windowManager != null)
            {
                _windowManager.RunOnUiThread(PerformAsyncBackgroundSave);
            }
            else
            {
                PerformAsyncBackgroundSave();
            }
        }

        private void PerformAsyncBackgroundSave()
        {
            long snapshotTime = DateTime.UtcNow.Ticks;
            var models = _notes.Select(n => n.GetModel().Clone()).ToList();

            Task.Run(async () =>
            {
                string json = JsonConvert.SerializeObject(models, Formatting.Indented);
                await _storageService.SaveNotesJsonAsync(json, models.Count, snapshotTime);
            });
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
            _storageService.FlushPendingSave();
        }

        public async Task SaveNotesImmediatelyAsync()
        {
            _saveDebounceTimer.Stop();

            long snapshotTime = DateTime.UtcNow.Ticks;
            List<NoteModel>? models = null;

            if (_windowManager != null)
            {
                _windowManager.RunOnUiThread(() =>
                {
                    models = _notes.Select(n => n.GetModel().Clone()).ToList();
                });
            }
            else
            {
                models = _notes.Select(n => n.GetModel().Clone()).ToList();
            }

            if (models != null)
            {
                string json = await Task.Run(() => JsonConvert.SerializeObject(models, Formatting.Indented));
                await _storageService.SaveNotesJsonAsync(json, models.Count, snapshotTime);
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
            var result = await ImportExportService.ExportNotesAsync(models, filePath);

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
            var result = await ImportExportService.ImportNotesAsync(filePath, existingModels, _storageService.StorageFolderPath);

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
            NotifyTagsChanged();
            OnPropertyChanged(nameof(SortedFilteredNotes));
            OnPropertyChanged(nameof(FilteredNotes));
        }

        private void NoteViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NoteViewModel.Title) or nameof(NoteViewModel.Content)
                or nameof(NoteViewModel.SearchableText) or nameof(NoteViewModel.IsPinned)
                or nameof(NoteViewModel.ModifiedAt) or nameof(NoteViewModel.Category)
                or nameof(NoteViewModel.WorkspaceName) or nameof(NoteViewModel.Tags)
                or nameof(NoteViewModel.ReminderDateTime) or nameof(NoteViewModel.IsReminderDismissed))
            {
                OnPropertyChanged(nameof(SortedFilteredNotes));
                OnPropertyChanged(nameof(FilteredNotes));
            }
        }
    }
}
