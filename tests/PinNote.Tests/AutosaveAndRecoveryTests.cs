using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class AutosaveAndRecoveryTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storageService;

        public AutosaveAndRecoveryTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _storageService = new StorageService(_testDir);
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
        public void SaveNotes_And_LoadNotes_RoundtripSuccessfully()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Test Note 1", Content = "Content 1", X = 120, Y = 150 },
                new() { Title = "Test Note 2", Content = "Content 2", IsPinned = true }
            };

            _storageService.SaveNotes(notes);

            var loaded = _storageService.LoadNotes();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("Test Note 1", loaded[0].Title);
            Assert.Equal("Content 1", loaded[0].Content);
            Assert.Equal(120, loaded[0].X);
            Assert.Equal(150, loaded[0].Y);
            Assert.Equal("Test Note 2", loaded[1].Title);
            Assert.True(loaded[1].IsPinned);
        }

        [Fact]
        public void LoadNotes_WhenPrimaryMissing_RestoresFromBackup()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Backup Note", Content = "From backup" }
            };

            string backupPath = _storageService.BackupFilePath;
            string backupJson = JsonConvert.SerializeObject(notes);
            File.WriteAllText(backupPath, backupJson);

            // Ensure primary does not exist
            if (File.Exists(_storageService.FilePath))
            {
                File.Delete(_storageService.FilePath);
            }

            var loaded = _storageService.LoadNotes();

            Assert.Single(loaded);
            Assert.Equal("Backup Note", loaded[0].Title);
        }

        [Fact]
        public void LoadNotes_WhenPrimaryCorrupted_PreservesCorruptFileAndRestoresBackup()
        {
            var validNotes = new List<NoteModel>
            {
                new() { Title = "Recovered Note", Content = "Good data" }
            };

            // Write valid backup
            File.WriteAllText(_storageService.BackupFilePath, JsonConvert.SerializeObject(validNotes));

            // Write corrupted primary
            File.WriteAllText(_storageService.FilePath, "{ invalid json content @#$%^&*");

            var loaded = _storageService.LoadNotes();

            // Should have restored from backup
            Assert.Single(loaded);
            Assert.Equal("Recovered Note", loaded[0].Title);

            // Should have created a .corrupt preservation file
            var corruptFiles = Directory.GetFiles(_testDir, "notes.json.corrupt.*");
            Assert.NotEmpty(corruptFiles);
        }

        [Fact]
        public void SaveNotes_GuardsAgainstWipingData_WhenInitialLoadFailed()
        {
            // Simulate corrupted file with no backup
            File.WriteAllText(_storageService.FilePath, "CORRUPT JSON");
            if (File.Exists(_storageService.BackupFilePath)) File.Delete(_storageService.BackupFilePath);

            // Attempt load - will fail and set _hasLoadError = true
            var loaded = _storageService.LoadNotes();
            Assert.Empty(loaded);
            Assert.True(_storageService.HasLoadError);

            // Try to save empty list
            _storageService.SaveNotes(new List<NoteModel>());

            // Primary file should NOT be replaced by empty list "[]"
            string fileContent = File.ReadAllText(_storageService.FilePath);
            Assert.Equal("CORRUPT JSON", fileContent);
        }

        [Fact]
        public void CreateBackup_GeneratesTimestampedBackupFile()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Important Note", Content = "Do not lose" }
            };
            _storageService.SaveNotes(notes);

            string backupPath = _storageService.CreateBackup("test_tag");

            Assert.True(File.Exists(backupPath));
            Assert.Contains("test_tag", backupPath);
            string backupContent = File.ReadAllText(backupPath);
            Assert.Contains("Important Note", backupContent);
        }

        [Fact]
        public void MainViewModel_SaveNotesImmediately_PersistsAllNotes()
        {
            var storage = new StorageService(_testDir);
            var mainVm = new MainViewModel(customStorageService: storage);

            mainVm.CreateNewNote(openWindow: false);
            mainVm.Notes[0].Title = "Immediate Save Test";
            mainVm.Notes[0].Content = "Updated content";

            mainVm.SaveNotesImmediately();

            var loaded = storage.LoadNotes();
            Assert.Single(loaded);
            Assert.Equal("Immediate Save Test", loaded[0].Title);
            Assert.Equal("Updated content", loaded[0].Content);
        }
    }
}
