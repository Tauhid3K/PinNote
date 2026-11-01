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
    }
}
