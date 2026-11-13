using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class VisibilityAndFilterTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public VisibilityAndFilterTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_visibility_filter_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void NoteVisibility_ClosedNote_PersistsInvisible_AndDoesNotReopenOnRestart()
        {
            // Acceptance criteria P0.3:
            // 1. Open note.
            // 2. Close it using its X button (CloseNote).
            // 3. Restart PinNote.
            // 4. Confirm closed note does not unexpectedly reopen.
            // 5. Open another note and verify it reappears after restart if left visible.

            var mockWindowManager = new MockWindowManager();
            var vm = new MainViewModel(windowManager: mockWindowManager, customStorageService: _storage);

            vm.CreateNewNote(openWindow: true);
            var noteA = vm.Notes.Last();
            noteA.Title = "Note A";

            vm.CreateNewNote(openWindow: true);
            var noteB = vm.Notes.Last();
            noteB.Title = "Note B";

            Assert.True(noteA.IsVisible);
            Assert.True(noteB.IsVisible);
            Assert.Equal(2, mockWindowManager.OpenedNotes.Count);

            // User closes Note A
            vm.CloseNote(noteA.Id);
            Assert.False(noteA.IsVisible);

            // Save immediately before restart
            vm.SaveNotesImmediately();

            // Simulate app restart
            var restartedMockWindowManager = new MockWindowManager();
            var restartedVm = new MainViewModel(windowManager: restartedMockWindowManager, customStorageService: _storage);
            restartedVm.RestoreVisibleNotes();

            var loadedA = restartedVm.Notes.First(n => n.Title == "Note A");
            var loadedB = restartedVm.Notes.First(n => n.Title == "Note B");

            // Note A must remain closed (invisible) and not reopened
            Assert.False(loadedA.IsVisible);
            Assert.DoesNotContain(loadedA.Id, restartedMockWindowManager.OpenedNotes);

            // Note B was left visible and must be reopened
            Assert.True(loadedB.IsVisible);
            Assert.Contains(loadedB.Id, restartedMockWindowManager.OpenedNotes);

            // Re-open Note A explicitly
            restartedVm.OpenNote(loadedA);
            Assert.True(loadedA.IsVisible);
            restartedVm.SaveNotesImmediately();

            // Next restart, both reopen
            var thirdMockWindowManager = new MockWindowManager();
            var thirdVm = new MainViewModel(windowManager: thirdMockWindowManager, customStorageService: _storage);
            thirdVm.RestoreVisibleNotes();

            Assert.Equal(2, thirdMockWindowManager.OpenedNotes.Count);
        }

        [Fact]
        public void RemindersFilter_WpfBindingAndFiltering_WorksConsistently()
        {
            // Acceptance criteria P0.4:
            // - The Reminders filter works in WPF (ShowRemindersOnlyFilter).
            // - Property change notifications work for both properties.
            // - Switching filters does not unexpectedly reset sorting or search.

            var vm = new MainViewModel(customStorageService: _storage);

            vm.CreateNewNote(openWindow: false);
            var note1 = vm.Notes.Last();
            note1.Title = "Note With Reminder";
            note1.ReminderDateTime = DateTime.UtcNow.AddHours(2);

            vm.CreateNewNote(openWindow: false);
            var note2 = vm.Notes.Last();
            note2.Title = "Regular Note";

            // Set search and sort
            vm.SearchText = "Note";
            vm.SortMode = NoteSortMode.TitleAscending;

            var changedProperties = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
            };

            // Test setting ShowRemindersOnlyFilter (WPF binding)
            vm.ShowRemindersOnlyFilter = true;

            Assert.True(vm.ShowRemindersOnly);
            Assert.True(vm.ShowRemindersOnlyFilter);
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnly), changedProperties);
            Assert.Contains(nameof(MainViewModel.ShowRemindersOnlyFilter), changedProperties);
            Assert.Contains(nameof(MainViewModel.SortedFilteredNotes), changedProperties);

            // Filter must now only show note1
            var filtered = vm.SortedFilteredNotes.ToList();
            Assert.Single(filtered);
            Assert.Equal("Note With Reminder", filtered[0].Title);

            // Verify search and sort were NOT reset
            Assert.Equal("Note", vm.SearchText);
            Assert.Equal(NoteSortMode.TitleAscending, vm.SortMode);

            // Uncheck reminders filter
            vm.ShowRemindersOnly = false;
            Assert.False(vm.ShowRemindersOnlyFilter);
            Assert.Equal(2, vm.SortedFilteredNotes.Count());
        }

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
