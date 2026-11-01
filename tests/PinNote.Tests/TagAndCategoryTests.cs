using System;
using System.IO;
using System.Linq;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class TagAndCategoryTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;
        private readonly MainViewModel _vm;

        public TagAndCategoryTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_tag_test_{Guid.NewGuid():N}");
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
        public void NoteViewModel_AddAndRemoveTags_UpdatesTagsAndSearchableText()
        {
            _vm.CreateNewNote(openWindow: false);
            var note = _vm.Notes.Last();

            note.AddTag("Math");
            note.AddTag("Calculus");

            Assert.Equal(2, note.Tags.Count);
            Assert.Contains("Math", note.Tags);
            Assert.Contains("Calculus", note.Tags);
            Assert.Contains("Math", note.SearchableText);

            note.RemoveTag("Math");
            Assert.Single(note.Tags);
            Assert.DoesNotContain("Math", note.Tags);
        }

        [Fact]
        public void MainViewModel_FilterByTagAndCategory_FiltersCorrectly()
        {
            _vm.CreateNewNote(openWindow: false);
            var n1 = _vm.Notes.Last();
            n1.Title = "Note 1";
            n1.Category = "Study";
            n1.AddTag("Physics");

            _vm.CreateNewNote(openWindow: false);
            var n2 = _vm.Notes.Last();
            n2.Title = "Note 2";
            n2.Category = "Personal";
            n2.AddTag("Errands");

            _vm.CreateNewNote(openWindow: false);
            var n3 = _vm.Notes.Last();
            n3.Title = "Note 3";
            n3.Category = "Study";
            n3.AddTag("Exam");

            // Filter by category
            _vm.SelectedCategoryFilter = "Study";
            var studyNotes = _vm.SortedFilteredNotes.ToList();
            Assert.Equal(2, studyNotes.Count);
            Assert.All(studyNotes, n => Assert.Equal("Study", n.Category));

            // Filter by tag
            _vm.SelectedCategoryFilter = null;
            _vm.SelectedTagFilter = "Physics";
            var physicsNotes = _vm.SortedFilteredNotes.ToList();
            Assert.Single(physicsNotes);
            Assert.Equal("Note 1", physicsNotes[0].Title);

            // Clear filters
            _vm.ClearFilters();
            Assert.Equal(3, _vm.SortedFilteredNotes.Count());
        }

        [Fact]
        public void MainViewModel_RenameTag_UpdatesAllNotesWithOldTag()
        {
            _vm.CreateNewNote(openWindow: false);
            var n1 = _vm.Notes.Last();
            n1.AddTag("OldTag");

            _vm.CreateNewNote(openWindow: false);
            var n2 = _vm.Notes.Last();
            n2.AddTag("OldTag");

            _vm.RenameTag("OldTag", "NewTag");

            Assert.Contains("NewTag", n1.Tags);
            Assert.DoesNotContain("OldTag", n1.Tags);
            Assert.Contains("NewTag", n2.Tags);
            Assert.DoesNotContain("OldTag", n2.Tags);
        }

        [Fact]
        public void MainViewModel_DeleteTag_RemovesFromAllNotes()
        {
            _vm.CreateNewNote(openWindow: false);
            var n1 = _vm.Notes.Last();
            n1.AddTag("ToDelete");

            _vm.DeleteTag("ToDelete");

            Assert.DoesNotContain("ToDelete", n1.Tags);
            Assert.DoesNotContain("ToDelete", _vm.AllTags);
        }
    }
}
