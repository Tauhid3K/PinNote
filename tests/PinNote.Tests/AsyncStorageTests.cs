using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PinNote.Models;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    public class AsyncStorageTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public AsyncStorageTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_async_test_{Guid.NewGuid():N}");
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
        public async Task LoadNotesAsync_And_SaveNotesAsync_RoundtripSuccessfully()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Async Note 1", Content = "Async Content 1" },
                new() { Title = "Async Note 2", Content = "Async Content 2", IsPinned = true }
            };

            await _storage.SaveNotesAsync(notes);
            var loaded = await _storage.LoadNotesAsync();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("Async Note 1", loaded[0].Title);
            Assert.Equal("Async Note 2", loaded[1].Title);
            Assert.True(loaded[1].IsPinned);
        }

        [Fact]
        public async Task SaveNotesAsync_StaleSnapshot_DoesNotOverwriteNewerContent()
        {
            long t1 = DateTime.UtcNow.Ticks;
            long t2 = t1 + 1000;

            var older = new List<NoteModel> { new() { Title = "Older" } };
            var newer = new List<NoteModel> { new() { Title = "Newer" } };

            await _storage.SaveNotesAsync(newer, snapshotTimestamp: t2);
            await _storage.SaveNotesAsync(older, snapshotTimestamp: t1);

            var loaded = await _storage.LoadNotesAsync();
            Assert.Single(loaded);
            Assert.Equal("Newer", loaded[0].Title);
        }

        [Fact]
        public async Task CreateBackupAsync_CreatesValidBackupFile()
        {
            var notes = new List<NoteModel> { new() { Title = "Backup Test" } };
            await _storage.SaveNotesAsync(notes);

            string backupPath = await _storage.CreateBackupAsync("test");

            Assert.True(File.Exists(backupPath));
            string json = await File.ReadAllTextAsync(backupPath);
            Assert.Contains("Backup Test", json);
        }

        [Fact]
        public async Task FlushPendingSave_WaitsForInFlightSaveCompletion()
        {
            var notes = new List<NoteModel> { new() { Title = "In Flight" } };
            var saveTask = _storage.SaveNotesAsync(notes);

            await _storage.FlushPendingSaveAsync();
            _storage.FlushPendingSave();

            Assert.True(File.Exists(_storage.FilePath));
            var loaded = _storage.LoadNotes();
            Assert.Single(loaded);
            Assert.Equal("In Flight", loaded[0].Title);
        }

        [Fact]
        public async Task ExportNotesAsync_And_ImportNotesAsync_WorkCorrectly()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Export Async", Content = "Body" }
            };

            string exportFile = Path.Combine(_testDir, "export_async.json");
            var exportRes = await ImportExportService.ExportNotesAsync(notes, exportFile);

            Assert.True(exportRes.Success);
            Assert.True(File.Exists(exportFile));

            var importRes = await ImportExportService.ImportNotesAsync(exportFile, new List<NoteModel>(), _testDir);

            Assert.True(importRes.Success);
            Assert.Single(importRes.ImportedNotes);
            Assert.Equal("Export Async", importRes.ImportedNotes[0].Title);
        }

        [Fact]
        public async Task SaveNotesAsync_NewerSnapshotDuringOngoingWrite_IsAcceptedAndPersisted()
        {
            // Acceptance Criteria:
            // 1. Capture snapshot A and begin writing it.
            // 2. Modify note and capture snapshot B before A finishes.
            // 3. Complete A's write.
            // 4. Verify B is still accepted and eventually persisted.
            // 5. Reload and confirm latest content is present.

            long versionA = StorageService.GetNextSnapshotVersion();
            var snapshotA = new List<NoteModel>
            {
                new() { Title = "Note Version A", Content = "Initial Content" }
            };

            // Start write A
            var writeATask = _storage.SaveNotesAsync(snapshotA, snapshotTimestamp: versionA);

            // While A is in-flight, capture snapshot B
            long versionB = StorageService.GetNextSnapshotVersion();
            Assert.True(versionB > versionA);

            var snapshotB = new List<NoteModel>
            {
                new() { Title = "Note Version B", Content = "Updated Content" }
            };

            // Queue write B
            var writeBTask = _storage.SaveNotesAsync(snapshotB, snapshotTimestamp: versionB);

            // Await both writes to finish
            await Task.WhenAll(writeATask, writeBTask);
            await _storage.FlushPendingSaveAsync();

            // Verify with a freshly loaded storage service (simulating app reload)
            var reloadedStorage = new StorageService(_testDir);
            var loadedNotes = await reloadedStorage.LoadNotesAsync();

            Assert.Single(loadedNotes);
            Assert.Equal("Note Version B", loadedNotes[0].Title);
            Assert.Equal("Updated Content", loadedNotes[0].Content);
        }

        [Fact]
        public async Task SaveNotesAsync_RapidEditsAndConcurrentSaves_PersistsLatestContent()
        {
            // Rapid concurrent edits
            const int editCount = 20;
            var tasks = new List<Task>();

            for (int i = 1; i <= editCount; i++)
            {
                int index = i;
                long version = StorageService.GetNextSnapshotVersion();
                var snapshot = new List<NoteModel>
                {
                    new() { Title = $"Note Iteration {index}", Content = $"Content {index}" }
                };

                tasks.Add(_storage.SaveNotesAsync(snapshot, snapshotTimestamp: version));
            }

            await Task.WhenAll(tasks);
            await _storage.FlushPendingSaveAsync();

            var reloaded = new StorageService(_testDir);
            var loaded = await reloaded.LoadNotesAsync();

            Assert.Single(loaded);
            // The final persisted note must be the highest iteration
            Assert.Equal($"Note Iteration {editCount}", loaded[0].Title);
            Assert.Equal($"Content {editCount}", loaded[0].Content);
        }
    }
}
