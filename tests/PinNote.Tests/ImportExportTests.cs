using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PinNote.Models;
using PinNote.Services;
using Xunit;

namespace PinNote.Tests
{
    public class ImportExportTests : IDisposable
    {
        private readonly string _testDir;

        public ImportExportTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_import_export_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
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
        public void ExportNotes_CreatesValidPackageWithMetadata()
        {
            var notes = new List<NoteModel>
            {
                new() { Title = "Export Me", Content = "Some content", NoteType = NoteType.Text },
                new()
                {
                    Title = "Export Checklist",
                    NoteType = NoteType.Checklist,
                    ChecklistItems = new List<ChecklistItemModel> { new() { Text = "Check 1" } }
                }
            };

            string exportFile = Path.Combine(_testDir, "exported.json");
            var result = ImportExportService.ExportNotes(notes, exportFile);

            Assert.True(result.Success);
            Assert.Equal(2, result.ExportedCount);
            Assert.True(File.Exists(exportFile));

            string json = File.ReadAllText(exportFile);
            var package = JsonConvert.DeserializeObject<NoteExportPackage>(json);

            Assert.NotNull(package);
            Assert.Equal(1, package.SchemaVersion);
            Assert.Equal("PinNote", package.Application);
            Assert.Equal(2, package.Notes.Count);
        }

        [Fact]
        public void ImportNotes_FromExportPackage_SuccessfullyRestoresNotes()
        {
            var noteId = Guid.NewGuid();
            var notes = new List<NoteModel>
            {
                new() { Id = noteId, Title = "Import Note", Content = "Restored content" }
            };

            string exportFile = Path.Combine(_testDir, "exported.json");
            ImportExportService.ExportNotes(notes, exportFile);

            var existingNotes = new List<NoteModel>(); // no collision
            var result = ImportExportService.ImportNotes(exportFile, existingNotes, _testDir);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(0, result.ReassignedIdCount);
            Assert.Equal(noteId, result.ImportedNotes[0].Id);
            Assert.Equal("Import Note", result.ImportedNotes[0].Title);
        }

        [Fact]
        public void ImportNotes_WhenIdCollidesWithExistingNote_ReassignsIdToProtectExistingData()
        {
            var sharedId = Guid.NewGuid();
            var exportedNotes = new List<NoteModel>
            {
                new() { Id = sharedId, Title = "Conflicting Note", Content = "Imported copy" }
            };

            string exportFile = Path.Combine(_testDir, "exported.json");
            ImportExportService.ExportNotes(exportedNotes, exportFile);

            var existingNotes = new List<NoteModel>
            {
                new() { Id = sharedId, Title = "Existing Original Note", Content = "Original content" }
            };

            var result = ImportExportService.ImportNotes(exportFile, existingNotes, _testDir);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(1, result.ReassignedIdCount); // Reassigned to prevent collision
            Assert.NotEqual(sharedId, result.ImportedNotes[0].Id); // ID changed
            Assert.Equal("Conflicting Note", result.ImportedNotes[0].Title);
        }

        [Fact]
        public void ImportNotes_CreatesPreImportBackup()
        {
            var existingNotes = new List<NoteModel>
            {
                new() { Title = "Preserve Me", Content = "Crucial data" }
            };

            string exportFile = Path.Combine(_testDir, "import_source.json");
            ImportExportService.ExportNotes(new List<NoteModel> { new() { Title = "New Note" } }, exportFile);

            var result = ImportExportService.ImportNotes(exportFile, existingNotes, _testDir);

            Assert.True(result.Success);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));

            string backupContent = File.ReadAllText(result.BackupPath);
            Assert.Contains("Preserve Me", backupContent);
        }

        [Fact]
        public void ImportNotes_WithCorruptedFile_ReturnsErrorAndDoesNotCrash()
        {
            string corruptFile = Path.Combine(_testDir, "corrupted.json");
            File.WriteAllText(corruptFile, "{ incomplete json object");

            var result = ImportExportService.ImportNotes(corruptFile, new List<NoteModel>(), _testDir);

            Assert.False(result.Success);
            Assert.Contains("Invalid JSON", result.Message);
        }
    }
}
