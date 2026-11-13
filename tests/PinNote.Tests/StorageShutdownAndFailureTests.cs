using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    /// <summary>
    /// Tests for storage concurrency edge-cases, failed writes, and
    /// application-shutdown flushing — scenarios required by Bug 1 fix verification.
    /// </summary>
    public class StorageShutdownAndFailureTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public StorageShutdownAndFailureTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_shutdown_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
        }

        // ─── Failed writes must not corrupt previously-saved data ───────────────

        [Fact]
        public void SaveNotes_WhenWriteFails_FiresSaveFailedEvent_AndPreservesOriginalData()
        {
            // Seed valid data in original storage
            var good = new List<NoteModel> { new() { Title = "Good Note", Content = "Initial" } };
            _storage.SaveNotes(good);

            // Create a separate test directory where the target notes.json is blocked by a directory of the same name
            string blockedDir = Path.Combine(_testDir, "blocked_dir");
            Directory.CreateDirectory(blockedDir);
            Directory.CreateDirectory(Path.Combine(blockedDir, "notes.json"));

            var blockedStorage = new StorageService(blockedDir);
            string? failMsg = null;
            blockedStorage.SaveFailed += (_, msg) => failMsg = msg;

            // Attempting to save to a path where notes.json is a directory causes File.Move to fail
            blockedStorage.SaveNotes(new List<NoteModel> { new() { Title = "Failing Note" } });

            // SaveFailed event must have been raised
            Assert.NotNull(failMsg);

            // The original storage data must remain completely intact
            var loaded = _storage.LoadNotes();
            Assert.Single(loaded);
            Assert.Equal("Good Note", loaded[0].Title);
            Assert.Equal("Initial", loaded[0].Content);
        }

        // ─── Shutdown with pending changes: FlushPendingSave persists data ───────

        [Fact]
        public async Task FlushPendingSave_WithPendingAsyncWrite_PersistsLatestContent()
        {
            // Simulate: user makes an edit, an async save is queued but not yet
            // committed, then the application shuts down and calls FlushPendingSave.

            var notes = new List<NoteModel>
            {
                new() { Title = "Shutdown Test Note", Content = "Must survive shutdown" }
            };

            // Queue an async save
            _ = _storage.SaveNotesAsync(notes);

            // Application shutdown: flush pending saves within timeout
            await _storage.FlushPendingSaveAsync();
            _storage.FlushPendingSave(5000);

            // Simulate restart
            var restarted = new StorageService(_testDir);
            var loaded = restarted.LoadNotes();

            Assert.Single(loaded);
            Assert.Equal("Shutdown Test Note", loaded[0].Title);
            Assert.Equal("Must survive shutdown", loaded[0].Content);
        }

        [Fact]
        public async Task SaveNotesAsync_MultipleRapidEdits_OnlyHighestVersionSurvivesRestart()
        {
            // Reproduce Bug 1 scenario:
            //  1. Rapid succession of edits each producing a snapshot.
            //  2. All writes eventually committed.
            //  3. Only the latest (highest-versioned) content survives restart.

            const int iterCount = 15;
            var tasks = new List<Task>();

            for (int i = 1; i <= iterCount; i++)
            {
                int idx = i;
                long version = StorageService.GetNextSnapshotVersion();
                var snap = new List<NoteModel>
                {
                    new() { Title = $"Edit {idx}", Content = $"Body {idx}" }
                };
                tasks.Add(_storage.SaveNotesAsync(snap, snapshotTimestamp: version));
            }

            await Task.WhenAll(tasks);
            await _storage.FlushPendingSaveAsync();

            // Restart
            var afterRestart = new StorageService(_testDir);
            var loaded = await afterRestart.LoadNotesAsync();

            Assert.Single(loaded);
            // Highest-versioned snapshot (iteration iterCount) must have won.
            Assert.Equal($"Edit {iterCount}", loaded[0].Title);
            Assert.Equal($"Body {iterCount}", loaded[0].Content);
        }

        [Fact]
        public async Task SaveNotesAsync_SnapshotB_DuringWriteA_IsNotRejected()
        {
            // Reproduce the specific race described in Bug 1:
            // Snapshot A starts writing; snapshot B is captured while A is in flight.
            // After both complete, snapshot B (higher version) must be on disk.

            long vA = StorageService.GetNextSnapshotVersion();
            var snapshotA = new List<NoteModel> { new() { Title = "Version A" } };
            var writeA = _storage.SaveNotesAsync(snapshotA, snapshotTimestamp: vA);

            // B is captured while A is in flight
            long vB = StorageService.GetNextSnapshotVersion();
            Assert.True(vB > vA, "vB must be strictly greater than vA");

            var snapshotB = new List<NoteModel> { new() { Title = "Version B" } };
            var writeB = _storage.SaveNotesAsync(snapshotB, snapshotTimestamp: vB);

            await Task.WhenAll(writeA, writeB);
            await _storage.FlushPendingSaveAsync();

            var restarted = new StorageService(_testDir);
            var loaded = await restarted.LoadNotesAsync();

            Assert.Single(loaded);
            Assert.Equal("Version B", loaded[0].Title);
        }

        [Fact]
        public void SaveNotes_OldSnapshotAfterNewWrite_IsRejectedAndLatestSurvives()
        {
            // Write newer snapshot first
            long newer = StorageService.GetNextSnapshotVersion();
            _storage.SaveNotes(new List<NoteModel> { new() { Title = "Newest" } }, newer);

            // Then try to write an older snapshot (simulates a delayed in-flight write)
            long older = newer - 1000;
            _storage.SaveNotes(new List<NoteModel> { new() { Title = "Stale" } }, older);

            var loaded = _storage.LoadNotes();
            Assert.Single(loaded);
            Assert.Equal("Newest", loaded[0].Title);
        }

        // ─── Backup is updated before overwriting primary ────────────────────────

        [Fact]
        public void SaveNotes_CreatesBackupBeforeOverwriting_PrimaryAndBackupBothValid()
        {
            // First save: creates the primary file
            var first = new List<NoteModel> { new() { Title = "First Save" } };
            _storage.SaveNotes(first);

            // Second save: should update backup to contain "First Save" data
            var second = new List<NoteModel> { new() { Title = "Second Save" } };
            _storage.SaveNotes(second);

            // Primary must contain the new data
            Assert.True(File.Exists(_storage.FilePath));
            var primary = JsonConvert.DeserializeObject<List<NoteModel>>(File.ReadAllText(_storage.FilePath));
            Assert.NotNull(primary);
            Assert.Equal("Second Save", primary![0].Title);

            // Backup must contain the previous data (created before overwriting)
            Assert.True(File.Exists(_storage.BackupFilePath));
            var backup = JsonConvert.DeserializeObject<List<NoteModel>>(File.ReadAllText(_storage.BackupFilePath));
            Assert.NotNull(backup);
            Assert.Equal("First Save", backup![0].Title);
        }

        // ─── MainViewModel shutdown flush ────────────────────────────────────────

        [Fact]
        public void MainViewModel_Shutdown_PendingChangesArePersisted()
        {
            // Simulate: a note is edited and a debounced save is queued.
            // The ExitCommand (or equivalent shutdown sequence) must flush
            // the pending save before the application exits.

            var storage = new StorageService(_testDir);
            var vm = new MainViewModel(customStorageService: storage);

            vm.CreateNewNote(openWindow: false);
            var note = vm.Notes.Last();
            note.Title = "Pending Change Before Shutdown";
            note.Content = "Must survive shutdown";

            // Queue a save (as the debounce timer would)
            vm.QueueSave();

            // Simulate shutdown sequence: flush pending edits and save
            vm.SaveNotesImmediately();
            storage.FlushPendingSave();

            // Restart
            var restarted = new StorageService(_testDir);
            var loaded = restarted.LoadNotes();

            var found = loaded.FirstOrDefault(n => n.Title == "Pending Change Before Shutdown");
            Assert.NotNull(found);
            Assert.Equal("Must survive shutdown", found!.Content);
        }
    }
}
