using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    /// <summary>
    /// Additional tests for Bug 3 (note visibility persistence) and Bug 4 (WPF reminders filter).
    /// These extend the existing VisibilityAndFilterTests with deeper edge-case coverage.
    /// </summary>
    public class VisibilityPersistenceAndFilterTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public VisibilityPersistenceAndFilterTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_vis_filter_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
        }

        // ─── Bug 3: Note visibility persistence ─────────────────────────────────

        [Fact]
        public void CloseNote_SetsIsVisibleFalse_AndIsPersisted()
        {
            // Acceptance:
            //   1. A visible note's IsVisible is initially true.
            //   2. CloseNote sets IsVisible = false.
            //   3. After save and reload the note stays invisible.

            var mock = new MockWindowManager();
            var vm = new MainViewModel(windowManager: mock, customStorageService: _storage);

            vm.CreateNewNote(openWindow: true);
            var note = vm.Notes.Last();
            note.Title = "Close-Me Note";
            Assert.True(note.IsVisible);

            // Close (simulates X-button)
            vm.CloseNote(note.Id);
            Assert.False(note.IsVisible);

            // Persist and reload
            vm.SaveNotesImmediately();

            var reloadedStorage = new StorageService(_testDir);
            var reloaded = reloadedStorage.LoadNotes();
            var reloadedNote = reloaded.First(n => n.Title == "Close-Me Note");
            Assert.False(reloadedNote.IsVisible, "Closed note must remain invisible after reload.");
        }

        [Fact]
        public void CloseNote_OnlyClosedNote_IsInvisible_OtherNotesRemainVisible()
        {
            var mock = new MockWindowManager();
            var vm = new MainViewModel(windowManager: mock, customStorageService: _storage);

            vm.CreateNewNote(openWindow: true);
            var noteA = vm.Notes.Last();
            noteA.Title = "Note A – close me";

            vm.CreateNewNote(openWindow: true);
            var noteB = vm.Notes.Last();
            noteB.Title = "Note B – keep me";

            // Close only Note A
            vm.CloseNote(noteA.Id);

            Assert.False(noteA.IsVisible);
            Assert.True(noteB.IsVisible);

            vm.SaveNotesImmediately();

            // Restart
            var restartMock = new MockWindowManager();
            var restartVm = new MainViewModel(windowManager: restartMock, customStorageService: _storage);
            restartVm.RestoreVisibleNotes();

            var rA = restartVm.Notes.First(n => n.Title == "Note A – close me");
            var rB = restartVm.Notes.First(n => n.Title == "Note B – keep me");

            Assert.False(rA.IsVisible);
            Assert.True(rB.IsVisible);
            Assert.DoesNotContain(rA.Id, restartMock.OpenedNotes);
            Assert.Contains(rB.Id, restartMock.OpenedNotes);
        }

        [Fact]
        public void DeleteNote_RemovesFromStorage_NotJustHides()
        {
            var mock = new MockWindowManager();
            var vm = new MainViewModel(windowManager: mock, customStorageService: _storage);

            vm.CreateNewNote(openWindow: true);
            var note = vm.Notes.Last();
            note.Title = "Delete Me Completely";

            var id = note.Id;

            // Delete (not just close)
            vm.DeleteNote(note);
            Assert.DoesNotContain(vm.Notes, n => n.Id == id);

            vm.SaveNotesImmediately();

            var reloaded = new StorageService(_testDir).LoadNotes();
            Assert.DoesNotContain(reloaded, n => n.Id == id);
        }

        [Fact]
        public void NoteViewModel_IsVisible_CanBeReopenedAfterClose()
        {
            // After closing, a note can be explicitly reopened and will show visible again.
            var mock = new MockWindowManager();
            var vm = new MainViewModel(windowManager: mock, customStorageService: _storage);

            vm.CreateNewNote(openWindow: true);
            var note = vm.Notes.Last();
            note.Title = "Reopen Test";

            vm.CloseNote(note.Id);
            Assert.False(note.IsVisible);

            vm.OpenNote(note);
            Assert.True(note.IsVisible);
            Assert.Contains(note.Id, mock.OpenedNotes);
        }

        // ─── Bug 4: WPF reminders filter binding ────────────────────────────────

        [Fact]
        public void ShowRemindersOnlyFilter_SetViaAlias_UpdatesBothPropertiesAndFilter()
        {
            // WPF binds to ShowRemindersOnlyFilter; setting it must update ShowRemindersOnly
            // and trigger SortedFilteredNotes to re-evaluate.

            var vm = new MainViewModel(customStorageService: _storage);

            vm.CreateNewNote(openWindow: false);
            var withReminder = vm.Notes.Last();
            withReminder.Title = "Has Reminder";
            withReminder.ReminderDateTime = DateTime.UtcNow.AddHours(1);

            vm.CreateNewNote(openWindow: false);
            var noReminder = vm.Notes.Last();
            noReminder.Title = "No Reminder";

            var changedProps = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) changedProps.Add(e.PropertyName);
            };

            // Set via WPF alias
            vm.ShowRemindersOnlyFilter = true;

            Assert.True(vm.ShowRemindersOnly);
            Assert.True(vm.ShowRemindersOnlyFilter);
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnly), changedProps);
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnlyFilter), changedProps);
            Assert.Contains(nameof(MainViewModel.SortedFilteredNotes), changedProps);
            Assert.Contains(nameof(MainViewModel.FilteredNotes), changedProps);

            var filtered = vm.SortedFilteredNotes.ToList();
            Assert.Single(filtered);
            Assert.Equal("Has Reminder", filtered[0].Title);
        }

        [Fact]
        public void ShowRemindersOnly_SetDirectly_SetsAliasPropertyToo()
        {
            var vm = new MainViewModel(customStorageService: _storage);

            var changedProps = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) changedProps.Add(e.PropertyName);
            };

            vm.ShowRemindersOnly = true;

            // Both properties must be in sync
            Assert.True(vm.ShowRemindersOnly);
            Assert.True(vm.ShowRemindersOnlyFilter);

            // Both change notifications must fire
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnly), changedProps);
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnlyFilter), changedProps);
        }

        [Fact]
        public void RemindersFilter_Toggle_PreservesSearchAndSortState()
        {
            var vm = new MainViewModel(customStorageService: _storage);

            vm.CreateNewNote(openWindow: false);
            vm.Notes.Last().Title = "Alpha Reminder";
            vm.Notes.Last().ReminderDateTime = DateTime.UtcNow.AddHours(3);

            vm.CreateNewNote(openWindow: false);
            vm.Notes.Last().Title = "Beta Note";

            vm.SearchText = "a";
            vm.SortMode = NoteSortMode.TitleDescending;

            vm.ShowRemindersOnly = true;
            Assert.Equal("a", vm.SearchText, ignoreCase: false);
            Assert.Equal(NoteSortMode.TitleDescending, vm.SortMode);

            vm.ShowRemindersOnly = false;
            Assert.Equal("a", vm.SearchText, ignoreCase: false);
            Assert.Equal(NoteSortMode.TitleDescending, vm.SortMode);
        }

        [Fact]
        public void ClearFilters_ResetsRemindersFilterAndOthers()
        {
            var vm = new MainViewModel(customStorageService: _storage);

            vm.SearchText = "something";
            vm.ShowRemindersOnly = true;
            vm.SortMode = NoteSortMode.TitleAscending;

            vm.ClearFilters();

            Assert.Equal(string.Empty, vm.SearchText);
            Assert.False(vm.ShowRemindersOnly);
            Assert.False(vm.ShowRemindersOnlyFilter);
            // SortMode is intentionally NOT reset by ClearFilters
        }

        // ─── Helper ─────────────────────────────────────────────────────────────

        private class MockWindowManager : IWindowManager
        {
            public HashSet<Guid> OpenedNotes { get; } = new();

            public void OpenNote(NoteViewModel viewModel) => OpenedNotes.Add(viewModel.Id);
            public void CloseNote(Guid noteId) => OpenedNotes.Remove(noteId);
            public void BringNotesOnTop() { }
            public void HideAllNotes() { }
            public void ShowAllNotes(bool minimizeDashboard = false) { }
            public void ShowDashboard() { }
            public void HideDashboard() { }
            public void ShowInstructions() { }
            public bool ConfirmDelete(string noteTitle) => true;
            public Task<bool> ConfirmDeleteAsync(string noteTitle) => Task.FromResult(true);
            public Task<bool> ConfirmActionAsync(string title, string message) => Task.FromResult(true);
            public Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultText = "") => Task.FromResult<string?>(defaultText);
            public void RunOnUiThread(Action action) => action();
            public void Shutdown() { }
            public void ApplyTheme(string themeMode) { }
            public void SetShowInTaskbar(bool show) { }
            public void ShowErrorMessage(string title, string message) { }
            public void ShowMessage(string title, string message) { }
            public void FlushPendingEdits() { }
            public Task<string?> ShowOpenFileDialogAsync(string title, string filter) => Task.FromResult<string?>(null);
            public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filter) => Task.FromResult<string?>(null);
        }
    }
}
