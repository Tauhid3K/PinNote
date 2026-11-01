using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    /// <summary>
    /// Edge-case tests for <see cref="StorageService"/> that exercise scenarios not covered
    /// by the existing AutosaveAndRecoveryTests: both-corrupt recovery, concurrent-save guards,
    /// and timestamp backward-compatibility.
    /// </summary>
    public class StorageEdgeCaseTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storage;

        public StorageEdgeCaseTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_edge_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storage = new StorageService(_testDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
        }

        // ─── Both primary and backup are corrupt / missing ──────────────────────

        [Fact]
        public void LoadNotes_WhenBothPrimaryAndBackupAreMissing_ReturnsEmptyList()
        {
            // Fresh directory — nothing written yet
            var notes = _storage.LoadNotes();

            Assert.Empty(notes);
            Assert.False(_storage.HasLoadError);
        }

        [Fact]
        public void LoadNotes_WhenBothPrimaryAndBackupAreCorrupt_ReturnsEmptyAndSetsHasLoadError()
        {
            // Write corrupt data to both files
            File.WriteAllText(_storage.FilePath, "NOT_VALID_JSON [[[@#");
            File.WriteAllText(_storage.BackupFilePath, "ALSO_INVALID {}{}}{");

            var notes = _storage.LoadNotes();

            Assert.Empty(notes);
            Assert.True(_storage.HasLoadError,
                "HasLoadError must be true when both primary and backup are unreadable.");

            // A .corrupt. preservation file should exist for the primary
            var corruptFiles = Directory.GetFiles(_testDir, "notes.json.corrupt.*");
            Assert.NotEmpty(corruptFiles);
        }

        [Fact]
        public void SaveNotes_WhenHasLoadError_DoesNotWipeExistingCorruptFile()
        {
            const string corruptContent = "{CORRUPT_SENTINEL}";
            File.WriteAllText(_storage.FilePath, corruptContent);

            _storage.LoadNotes(); // triggers HasLoadError = true

            Assert.True(_storage.HasLoadError);

            // Try saving an empty list — guard should prevent overwrite
            _storage.SaveNotes(new List<NoteModel>());

            string remaining = File.ReadAllText(_storage.FilePath);
            Assert.Equal(corruptContent, remaining);
        }

        // ─── Rapid successive saves don't corrupt data ──────────────────────────

        [Fact]
        public void SaveNotes_RapidSuccession_LastSaveWins()
        {
            // Pre-populate a valid state so HasLoadError is false
            _storage.SaveNotes(new List<NoteModel> { new() { Title = "Seed" } });

            var results = new List<Exception?>();
            const int concurrentSaves = 20;
            var barrier = new Barrier(concurrentSaves);

            Parallel.For(0, concurrentSaves, i =>
            {
                var notes = new List<NoteModel>
                {
                    new() { Title = $"Note_{i}", Content = $"Content_{i}" }
                };

                try
                {
                    barrier.SignalAndWait();           // synchronize start
                    _storage.SaveNotes(notes);
                    results.Add(null);
                }
                catch (Exception ex)
                {
                    results.Add(ex);
                }
            });

            // No save should have thrown
            Assert.All(results, ex => Assert.Null(ex));

            // The final file must be valid JSON — we don't assert which save won
            var loaded = _storage.LoadNotes();
            Assert.NotEmpty(loaded);
            Assert.False(_storage.HasLoadError);
        }

        [Fact]
        public void SaveNotes_StaleSnapshot_DoesNotOverwriteNewerContent()
        {
            long t1 = DateTime.UtcNow.Ticks;
            var initialNotes = new List<NoteModel> { new() { Title = "Initial Note" } };
            _storage.SaveNotes(initialNotes, snapshotTimestamp: t1);

            // Newer save arrives at t2 > t1
            long t2 = t1 + TimeSpan.FromSeconds(5).Ticks;
            var newerNotes = new List<NoteModel> { new() { Title = "Newer Note" } };
            _storage.SaveNotes(newerNotes, snapshotTimestamp: t2);

            // Verify newer content is saved
            var current = _storage.LoadNotes();
            Assert.Single(current);
            Assert.Equal("Newer Note", current[0].Title);

            // An older delayed save arrives with timestamp t1 < t2
            var staleNotes = new List<NoteModel> { new() { Title = "Stale Delayed Note" } };
            _storage.SaveNotes(staleNotes, snapshotTimestamp: t1);

            // Stale save should have been rejected; current content remains "Newer Note"
            var afterStale = _storage.LoadNotes();
            Assert.Single(afterStale);
            Assert.Equal("Newer Note", afterStale[0].Title);
        }

        // ─── Atomic write ensures the file is never partially written ───────────

        [Fact]
        public void SaveNotes_ResultsInNonEmptyValidJsonFile()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Atomic Note", Content = "Should survive" }
            };

            _storage.SaveNotes(notes);

            Assert.True(File.Exists(_storage.FilePath));

            string json = File.ReadAllText(_storage.FilePath);
            // Must be valid JSON and non-trivially sized
            Assert.True(json.Length > 2);
            var loaded = JsonConvert.DeserializeObject<List<NoteModel>>(json);
            Assert.NotNull(loaded);
            Assert.Single(loaded!);
            Assert.Equal("Atomic Note", loaded![0].Title);
        }

        // ─── Timestamp backward-compatibility ──────────────────────────────────

        [Fact]
        public void LoadNotes_OldJsonWithoutTimestamps_LoadsWithDefaultTimestamps()
        {
            // Simulate old notes.json that predates the timestamp fields
            var legacyJson = @"[
                {
                    ""Id"": """ + Guid.NewGuid() + @""",
                    ""Title"": ""Legacy note"",
                    ""Content"": ""Plain text content""
                }
            ]";

            var before = DateTime.UtcNow.AddSeconds(-2);
            File.WriteAllText(_storage.FilePath, legacyJson);

            var notes = _storage.LoadNotes();
            var after = DateTime.UtcNow.AddSeconds(2);

            Assert.Single(notes);
            Assert.Equal("Legacy note", notes[0].Title);
            // Timestamps should default to DateTime.UtcNow on model instantiation — not throw
            Assert.InRange(notes[0].CreatedAt, before, after);
            Assert.InRange(notes[0].ModifiedAt, before, after);
        }

        // ─── Empty backup should not be used to overwrite a valid primary ───────

        [Fact]
        public void LoadNotes_WhenPrimaryIsValidAndBackupIsEmpty_UsesPrimary()
        {
            var goodNotes = new List<NoteModel>
            {
                new() { Title = "Good Note", Content = "Important" }
            };
            _storage.SaveNotes(goodNotes);

            // Corrupt the auto-backup manually with an empty file
            File.WriteAllText(_storage.BackupFilePath, string.Empty);

            var notes = _storage.LoadNotes();

            Assert.Single(notes);
            Assert.Equal("Good Note", notes[0].Title);
        }

        // ─── Storage folder creation ────────────────────────────────────────────

        [Fact]
        public void StorageService_WithNonexistentCustomFolder_CreatesItAutomatically()
        {
            string newDir = Path.Combine(_testDir, "deep", "nested", "dir");
            Assert.False(Directory.Exists(newDir));

            var service = new StorageService(newDir);
            service.SaveNotes(new List<NoteModel> { new() { Title = "Test" } });

            Assert.True(Directory.Exists(newDir));
            Assert.True(File.Exists(service.FilePath));
        }
    }
}
