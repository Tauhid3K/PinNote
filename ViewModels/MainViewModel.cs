using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PinNote.Models;
using PinNote.Services;
using PinNote.UI;

namespace PinNote.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AppStateService _appStateService = new();
        private bool _showUiOnStartup;
        private readonly StorageService _storageService;
        private readonly ObservableCollection<NoteViewModel> _notes;
        private readonly Dictionary<Guid, NoteWindow> _openWindows;
        private MainDashboardWindow? _dashboardWindow;
        private string _searchText = string.Empty;
        private bool _isStartupEnabled;
        private bool _startMinimized;
        private string _themeMode;

        public MainViewModel()
        {
            _showUiOnStartup = _appStateService.GetShowUiOnStartup();
            _storageService = new StorageService();
            _notes = new ObservableCollection<NoteViewModel>();
            _notes.CollectionChanged += Notes_CollectionChanged;
            _openWindows = new Dictionary<Guid, NoteWindow>();
            _isStartupEnabled = StartupService.IsStartupEnabled();
            _startMinimized = _appStateService.GetStartMinimized();

            NewNoteCommand = new RelayCommand(_ => CreateNewNote());
            ShowAllCommand = new RelayCommand(_ => ShowAllNotes());
            HideAllCommand = new RelayCommand(_ => HideAllNotes());
            BringNotesOnTopCommand = new RelayCommand(_ => BringNotesOnTop());
            ToggleStartupCommand = new RelayCommand(_ => ToggleStartup());
            SettingsCommand = new RelayCommand(_ => ShowAllNotes());
            HelpCommand = new RelayCommand(_ => ShowInstructions());
            ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());

            ToggleShowUiOnStartupCommand = new RelayCommand(_ => ShowUiOnStartup = !ShowUiOnStartup);
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
                // Use a snapshot list to ensure WPF receives a stable collection and guard against null Title/Content.
                return _notes.Where(note =>
                    (note.Title ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (note.Content ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
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

        public ICommand ToggleShowUiOnStartupCommand { get; }
        public ICommand SetThemeCommand { get; }

        public string ThemeMode
        {
            get => _themeMode ?? "System";
            set
            {
                if (value == _themeMode) return;
                _themeMode = value ?? "System";
                _appStateService.SetThemeMode(_themeMode);
                // Apply immediately via App
                try
                {
                    if (Application.Current is App app)
                    {
                        app.SetTheme(_themeMode);
                    }
                }
                catch { }
                OnPropertyChanged();
            }
        }

        private void ToggleStartup()
        {
            IsStartupEnabled = !IsStartupEnabled;
        }

        private InstructionsWindow? _instructionsWindow;

        public void ShowInstructions()
        {
            if (_instructionsWindow != null && _instructionsWindow.IsLoaded)
            {
                _instructionsWindow.Activate();
                return;
            }

            _instructionsWindow = new InstructionsWindow();
            _instructionsWindow.Show();
        }

        private void LoadNotes()
        {
            var savedNotes = _storageService.LoadNotes();

            // If there are no saved notes, start with an empty dashboard (do not auto-create a note).
            if (!savedNotes.Any())
            {
                return;
            }

            foreach (var noteModel in savedNotes)
            {
                // Open all notes on startup as requested
                AddNoteViewModel(noteModel, openWindow: true);
            }
        }

        public void CreateWelcomeNote()
        {
            // This method is now kept for internal compatibility if needed, 
            // but the primary onboarding is now handled by ShowInstructions().
            ShowInstructions();
        }

        private void CreateNewNote(bool openWindow = true)
        {
            var newNote = new NoteModel();
            AddNoteViewModel(newNote, openWindow);
            SaveNotes();
        }

        private void AddNoteViewModel(NoteModel model, bool openWindow = true)
        {
            var viewModel = new NoteViewModel(model, this);
            viewModel.DeleteRequested += (s, e) =>
            {
                try
                {
                    var title = string.IsNullOrWhiteSpace(viewModel.Title) ? "this note" : viewModel.Title;
                    var res = MessageBox.Show($"Are you sure you want to delete '{title}'?", "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        DeleteNote(viewModel);
                    }
                }
                catch
                {
                    // Fallback: delete without confirmation if UI fails
                    DeleteNote(viewModel);
                }
            };
            viewModel.SaveRequested += (s, e) => SaveNotes();
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
            if (_openWindows.TryGetValue(viewModel.Id, out var existingWindow))
            {
                if (!existingWindow.IsVisible)
                {
                    existingWindow.Show();
                }

                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.Activate();
                existingWindow.Topmost = true;
                existingWindow.Topmost = viewModel.IsPinned;
                return;
            }

            var window = new NoteWindow(viewModel);
            window.Closed += (_, _) => OnNoteWindowClosed(viewModel.Id);
            _openWindows[viewModel.Id] = window;
            window.Show();
            window.Activate();
        }

        private void DeleteNote(NoteViewModel viewModel)
        {
            if (_openWindows.TryGetValue(viewModel.Id, out var window))
            {
                window.Close();
                _openWindows.Remove(viewModel.Id);
            }

            _notes.Remove(viewModel);
            SaveNotes();
            OnPropertyChanged(nameof(FilteredNotes));
        }

        private void OnNoteWindowClosed(Guid noteId)
        {
            _openWindows.Remove(noteId);
        }

        public void ShowDashboard()
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new MainDashboardWindow(this);
            }

            _dashboardWindow.ShowInTaskbar = false;
            _dashboardWindow.Show();
            if (_dashboardWindow.WindowState == WindowState.Minimized)
            {
                _dashboardWindow.WindowState = WindowState.Normal;
            }

            _dashboardWindow.Activate();
        }

        public void ShowAllNotes()
        {
            ShowAllNotes(minimizeDashboard: false);
        }

        /// <summary>
        /// Show all note windows and the dashboard. Optionally minimize the dashboard window
        /// (useful for auto-start behavior where notes should appear but the main dashboard
        /// remains minimized).
        /// </summary>
        /// <param name="minimizeDashboard">If true, minimize the dashboard after showing notes.</param>
        public void ShowAllNotes(bool minimizeDashboard)
        {
            ShowDashboard();

            foreach (var window in _openWindows.Values)
            {
                window.Show();
                window.Activate();

                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
            }

            try
            {
                if (minimizeDashboard && _dashboardWindow != null)
                {
                    _dashboardWindow.WindowState = WindowState.Minimized;
                }
            }
            catch
            {
                // non-fatal
            }
        }

        private void HideAllNotes()
        {
            // Keep the main dashboard visible; only hide individual note windows.
            foreach (var window in _openWindows.Values)
            {
                window.Hide();
            }
        }

        private void BringNotesOnTop()
        {
            foreach (var window in _openWindows.Values)
            {
                window.Show();

                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Topmost = true;
                window.Topmost = window.DataContext is NoteViewModel vm && vm.IsPinned;
                window.Activate();
            }
        }

        private void SaveNotes()
        {
            var models = _notes.Select(n => n.GetModel()).ToList();
            _storageService.SaveNotes(models);
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
