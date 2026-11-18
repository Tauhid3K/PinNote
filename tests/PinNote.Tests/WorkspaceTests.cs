using System;
using System.IO;
using System.Linq;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class WorkspaceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;
        private readonly MainViewModel _vm;

        public WorkspaceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_ws_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
            _vm = new MainViewModel(customStorageService: _storage);
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
        public void WorkspaceService_InitializesDefaultWorkspaces()
        {
            var wsService = new WorkspaceService(_testDir);
            var workspaces = wsService.LoadWorkspaces();

            Assert.NotEmpty(workspaces);
            Assert.Contains(workspaces, w => w.Name == "Default" && w.IsDefault);
            Assert.Contains(workspaces, w => w.Name == "University");
            Assert.Contains(workspaces, w => w.Name == "Personal");
            Assert.Contains(workspaces, w => w.Name == "Development");
            Assert.Contains(workspaces, w => w.Name == "Projects");
        }

        [Fact]
        public void MainViewModel_CreateAndRenameWorkspace_PersistsSuccessfully()
        {
            _vm.CreateWorkspace("Biology 101");
            var created = _vm.Workspaces.FirstOrDefault(w => w.Name == "Biology 101");
            Assert.NotNull(created);
            Assert.Equal("Biology 101", _vm.ActiveWorkspace?.Name);

            _vm.RenameWorkspace(created, "Advanced Biology");
            Assert.Equal("Advanced Biology", created.Name);
            Assert.Equal("Advanced Biology", _vm.ActiveWorkspace?.Name);
        }

        [Fact]
        public void MainViewModel_DeleteWorkspace_SafelyReassignsNotesToDefault()
        {
            _vm.CreateWorkspace("Temporary");
            var tempWs = _vm.Workspaces.First(w => w.Name == "Temporary");

            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();
            note.WorkspaceName = "Temporary";
            note.WorkspaceId = tempWs.Id;

            // Delete temporary workspace
            _vm.DeleteWorkspace(tempWs);

            Assert.DoesNotContain(_vm.Workspaces, w => w.Name == "Temporary");
            Assert.Equal("Default", note.WorkspaceName);
            Assert.Equal("Default", _vm.ActiveWorkspace?.Name);
        }

        [Fact]
        public void MainViewModel_SwitchWorkspace_FiltersNotesToActiveWorkspace()
        {
            _vm.CreateWorkspace("WS A");
            var wsA = _vm.Workspaces.First(w => w.Name == "WS A");

            _vm.CreateWorkspace("WS B");
            var wsB = _vm.Workspaces.First(w => w.Name == "WS B");

            _vm.CreateNewNote(openWindow: false);
            var nA = _vm.Notes.Last();
            nA.Title = "Note A";
            nA.WorkspaceName = "WS A";
            nA.WorkspaceId = wsA.Id;

            _vm.CreateNewNote(openWindow: false);
            var nB = _vm.Notes.Last();
            nB.Title = "Note B";
            nB.WorkspaceName = "WS B";
            nB.WorkspaceId = wsB.Id;

            _vm.ActiveWorkspace = wsA;
            var visibleA = _vm.SortedFilteredNotes.ToList();
            Assert.Contains(visibleA, n => n.Title == "Note A");
            Assert.DoesNotContain(visibleA, n => n.Title == "Note B");

            _vm.ActiveWorkspace = wsB;
            var visibleB = _vm.SortedFilteredNotes.ToList();
            Assert.Contains(visibleB, n => n.Title == "Note B");
            Assert.DoesNotContain(visibleB, n => n.Title == "Note A");
        }

        [Fact]
        public async System.Threading.Tasks.Task MainViewModel_ImportNotesWithCustomWorkspace_RegistersWorkspaceAndMakesNotesAccessible()
        {
            var exportFile = Path.Combine(_testDir, "custom_ws_export.json");
            var note = new NoteModel
            {
                Id = Guid.NewGuid(),
                Title = "Astronomy Log",
                WorkspaceName = "Astrophysics"
            };
            ImportExportService.ExportNotes(new System.Collections.Generic.List<NoteModel> { note }, exportFile);

            var mockWindow = new MockWindowManager(exportFile);
            var vm = new MainViewModel(mockWindow, _storage);

            await vm.ImportNotesAsync(ImportMode.Merge);

            // Workspace must now be registered in Workspaces collection
            Assert.Contains(vm.Workspaces, w => w.Name == "Astrophysics");
            // Note should belong to this registered workspace
            var importedNote = vm.Notes.FirstOrDefault(n => n.Title == "Astronomy Log");
            Assert.NotNull(importedNote);
            Assert.Equal("Astrophysics", importedNote.WorkspaceName);
            var ws = vm.Workspaces.First(w => w.Name == "Astrophysics");
            Assert.Equal(ws.Id, importedNote.WorkspaceId);

            // Switching to Astrophysics workspace shows note in filtered list
            vm.ActiveWorkspace = ws;
            Assert.Contains(vm.SortedFilteredNotes, n => n.Title == "Astronomy Log");
        }

        private class MockWindowManager : IWindowManager
        {
            private readonly string _fileToOpen;

            public MockWindowManager(string fileToOpen)
            {
                _fileToOpen = fileToOpen;
            }

            public void OpenNote(NoteViewModel viewModel) { }
            public void CloseNote(Guid noteId) { }
            public void BringNotesOnTop() { }
            public void HideAllNotes() { }
            public void ShowAllNotes(bool minimizeDashboard = false) { }
            public void ShowDashboard() { }
            public void HideDashboard() { }
            public void ShowInstructions() { }
            public bool ConfirmDelete(string noteTitle) => true;
            public System.Threading.Tasks.Task<bool> ConfirmDeleteAsync(string noteTitle) => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<bool> ConfirmActionAsync(string title, string message) => System.Threading.Tasks.Task.FromResult(true);
            public System.Threading.Tasks.Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultText = "") => System.Threading.Tasks.Task.FromResult<string?>(defaultText);
            public void RunOnUiThread(Action action) => action();
            public void Shutdown() { }
            public void ApplyTheme(string themeMode) { }
            public void SetShowInTaskbar(bool show) { }
            public void ShowErrorMessage(string title, string message) { }
            public void ShowMessage(string title, string message) { }
            public void FlushPendingEdits() { }
            public System.Threading.Tasks.Task<string?> ShowOpenFileDialogAsync(string title, string filter) => System.Threading.Tasks.Task.FromResult<string?>(_fileToOpen);
            public System.Threading.Tasks.Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filter) => System.Threading.Tasks.Task.FromResult<string?>(null);
        }
    }
}
