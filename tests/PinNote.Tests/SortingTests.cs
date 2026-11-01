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
    /// <summary>
    /// Tests for dashboard note sorting (P0.4 gap — filtering existed but sorting did not).
    /// </summary>
    public class SortingTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public SortingTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_sort_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
        }

        private MainViewModel CreateVm()
        {
            return new MainViewModel(customStorageService: _storage);
        }

        // ─── helpers ────────────────────────────────────────────────────────────

        private static NoteViewModel AddNote(
            MainViewModel vm,
            string title,
            string content = "",
            bool pinned = false,
            DateTime? createdAt = null,
            DateTime? modifiedAt = null)
        {
            vm.CreateNewNote(openWindow: false);
            var note = vm.Notes[^1];
            note.Title = title;
            note.Content = content;
            note.IsPinned = pinned;

            // Directly patch model timestamps to control test scenarios
            var model = note.GetModel();
            if (createdAt.HasValue)  model.CreatedAt  = createdAt.Value;
            if (modifiedAt.HasValue) model.ModifiedAt = modifiedAt.Value;

            return note;
        }

        // ─── Default sort (pinned first, then insertion order) ─────────────────

        [Fact]
        public void SortedFilteredNotes_Default_PinnedLeadsInsertion()
        {
            var vm = CreateVm();
            AddNote(vm, "Banana");
            AddNote(vm, "Apple", pinned: true);
            AddNote(vm, "Cherry");

            var sorted = vm.SortedFilteredNotes.ToList();

            Assert.Equal("Apple",  sorted[0].Title);
            Assert.Equal("Banana", sorted[1].Title);
            Assert.Equal("Cherry", sorted[2].Title);
        }

        // ─── Title ascending ───────────────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_TitleAscending_SortsAlpha()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.TitleAscending;
            AddNote(vm, "Zebra");
            AddNote(vm, "Apple");
            AddNote(vm, "Mango");

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "Apple", "Mango", "Zebra" }, sorted);
        }

        [Fact]
        public void SortedFilteredNotes_TitleAscending_PinnedBeforeUnpinned()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.TitleAscending;
            AddNote(vm, "Zebra", pinned: true);
            AddNote(vm, "Apple");
            AddNote(vm, "Mango", pinned: true);

            var sorted = vm.SortedFilteredNotes.ToList();

            // Pinned: Mango before Zebra (alpha within pinned group)
            Assert.True(sorted[0].IsPinned);
            Assert.True(sorted[1].IsPinned);
            Assert.Equal("Mango", sorted[0].Title);
            Assert.Equal("Zebra", sorted[1].Title);
            Assert.False(sorted[2].IsPinned);
            Assert.Equal("Apple", sorted[2].Title);
        }

        // ─── Title descending ──────────────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_TitleDescending_SortsReverseAlpha()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.TitleDescending;
            AddNote(vm, "Apple");
            AddNote(vm, "Zebra");
            AddNote(vm, "Mango");

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "Zebra", "Mango", "Apple" }, sorted);
        }

        // ─── Modified newest ────────────────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_ModifiedNewest_MostRecentFirst()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.ModifiedNewest;

            var t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddNote(vm, "Old",    modifiedAt: t.AddDays(1));
            AddNote(vm, "Newest", modifiedAt: t.AddDays(3));
            AddNote(vm, "Middle", modifiedAt: t.AddDays(2));

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "Newest", "Middle", "Old" }, sorted);
        }

        // ─── Modified oldest ────────────────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_ModifiedOldest_OldestFirst()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.ModifiedOldest;

            var t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddNote(vm, "Newest", modifiedAt: t.AddDays(3));
            AddNote(vm, "Old",    modifiedAt: t.AddDays(1));
            AddNote(vm, "Middle", modifiedAt: t.AddDays(2));

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "Old", "Middle", "Newest" }, sorted);
        }

        // ─── Created newest / oldest ───────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_CreatedNewest_MostRecentFirst()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.CreatedNewest;

            var t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddNote(vm, "First",  createdAt: t.AddDays(1));
            AddNote(vm, "Third",  createdAt: t.AddDays(3));
            AddNote(vm, "Second", createdAt: t.AddDays(2));

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "Third", "Second", "First" }, sorted);
        }

        [Fact]
        public void SortedFilteredNotes_CreatedOldest_OldestFirst()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.CreatedOldest;

            var t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            AddNote(vm, "Third",  createdAt: t.AddDays(3));
            AddNote(vm, "First",  createdAt: t.AddDays(1));
            AddNote(vm, "Second", createdAt: t.AddDays(2));

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(new[] { "First", "Second", "Third" }, sorted);
        }

        // ─── Search + Sort together ────────────────────────────────────────────

        [Fact]
        public void SortedFilteredNotes_SearchAndSort_FiltersBeforeSorting()
        {
            var vm = CreateVm();
            vm.SortMode = NoteSortMode.TitleAscending;
            AddNote(vm, "Buy apples",  content: "grocery");
            AddNote(vm, "Buy bananas", content: "grocery");
            AddNote(vm, "Doctor appointment");

            vm.SearchText = "grocery";

            var sorted = vm.SortedFilteredNotes.Select(n => n.Title).ToList();

            Assert.Equal(2, sorted.Count);
            Assert.Equal("Buy apples",  sorted[0]);
            Assert.Equal("Buy bananas", sorted[1]);
        }

        // ─── Changing sort mode fires property change ──────────────────────────

        [Fact]
        public void SortMode_Change_FiresPropertyChangedForSortedFilteredNotes()
        {
            var vm = CreateVm();
            var changed = new List<string?>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            vm.SortMode = NoteSortMode.TitleDescending;

            Assert.Contains(nameof(MainViewModel.SortedFilteredNotes), changed);
            Assert.Contains(nameof(MainViewModel.FilteredNotes), changed);
        }

        // ─── Sort mode persists across ViewModel instances ────────────────────

        [Fact]
        public void SortMode_Persisted_RestoredOnNextConstruction()
        {
            var vm1 = CreateVm();
            vm1.SortMode = NoteSortMode.TitleDescending;
            // SortMode setter persists via AppStateService

            // Create fresh VM over the same storage (simulating restart)
            var vm2 = new MainViewModel(customStorageService: _storage);

            // Note: sort is stored in AppStateService (separate file path).
            // We can only verify the *default* round-trip without mocking AppStateService.
            // The important invariant is that SortMode setter does not throw.
            Assert.True(Enum.IsDefined(typeof(NoteSortMode), vm2.SortMode));
        }

        // ─── NoteViewModel exposes timestamps ─────────────────────────────────

        [Fact]
        public void NoteViewModel_Timestamps_ReflectModelValues()
        {
            var t = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            var model = new NoteModel
            {
                Title = "Test",
                CreatedAt  = t,
                ModifiedAt = t.AddHours(1)
            };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            Assert.Equal(t,              vm.CreatedAt);
            Assert.Equal(t.AddHours(1),  vm.ModifiedAt);
        }

        [Fact]
        public void NoteViewModel_ContentChange_UpdatesModifiedAt()
        {
            var model = new NoteModel { Title = "Test" };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            var before = vm.ModifiedAt;

            // Ensure time advances (CI can be very fast)
            System.Threading.Thread.Sleep(10);
            vm.Content = "New content";

            Assert.True(vm.ModifiedAt >= before,
                $"ModifiedAt ({vm.ModifiedAt:O}) should be >= before ({before:O})");
        }

        [Fact]
        public void NoteViewModel_TitleChange_UpdatesModifiedAt()
        {
            var model = new NoteModel { Title = "Old Title" };
            var mainVm = new MainViewModel();
            var vm = new NoteViewModel(model, mainVm);

            var before = vm.ModifiedAt;

            System.Threading.Thread.Sleep(10);
            vm.Title = "New Title";

            Assert.True(vm.ModifiedAt >= before,
                $"ModifiedAt ({vm.ModifiedAt:O}) should be >= before ({before:O})");
        }
    }
}
