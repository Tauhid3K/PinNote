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

        [Fact]
        public void ImportNotes_MergeMode_KeepsExistingAndAddsNewNotes()
        {
            var noteAId = Guid.NewGuid();
            var existing = new List<NoteModel>
            {
                new() { Id = noteAId, Title = "Note A", Content = "Existing note" }
            };

            var noteBId = Guid.NewGuid();
            string exportFile = Path.Combine(_testDir, "merge_source.json");
            ImportExportService.ExportNotes(new List<NoteModel>
            {
                new() { Id = noteBId, Title = "Note B", Content = "New note" }
            }, exportFile);

            var result = ImportExportService.ImportNotes(exportFile, existing, _testDir, ImportMode.Merge);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(0, result.ReassignedIdCount);
            Assert.Equal(noteBId, result.ImportedNotes[0].Id);
        }

        [Fact]
        public void ImportNotes_ReplaceMode_PreparesOnlyImportedNotesAndCreatesReplaceBackup()
        {
            var existing = new List<NoteModel>
            {
                new() { Title = "Existing 1" },
                new() { Title = "Existing 2" }
            };

            var noteCId = Guid.NewGuid();
            string exportFile = Path.Combine(_testDir, "replace_source.json");
            ImportExportService.ExportNotes(new List<NoteModel>
            {
                new() { Id = noteCId, Title = "Note C", Content = "Replacement note" }
            }, exportFile);

            var result = ImportExportService.ImportNotes(exportFile, existing, _testDir, ImportMode.Replace);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(noteCId, result.ImportedNotes[0].Id);
            Assert.NotNull(result.BackupPath);
            Assert.Contains("before_replace", result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));

            string backupText = File.ReadAllText(result.BackupPath);
            Assert.Contains("Existing 1", backupText);
            Assert.Contains("Existing 2", backupText);
        }

        [Fact]
        public void ImportNotes_DuplicateIdsWithinFile_DeduplicatesSafely()
        {
            var duplicateId = Guid.NewGuid();
            string duplicateJson = JsonConvert.SerializeObject(new NoteExportPackage
            {
                SchemaVersion = 1,
                Notes = new List<NoteModel>
                {
                    new() { Id = duplicateId, Title = "First Copy" },
                    new() { Id = duplicateId, Title = "Second Copy" }
                }
            }, Formatting.Indented);

            string file = Path.Combine(_testDir, "duplicate_ids.json");
            File.WriteAllText(file, duplicateJson);

            var result = ImportExportService.ImportNotes(file, new List<NoteModel>(), _testDir, ImportMode.Replace);

            Assert.True(result.Success);
            Assert.Equal(2, result.ImportedCount);
            Assert.Equal(1, result.ReassignedIdCount);
            Assert.NotEqual(result.ImportedNotes[0].Id, result.ImportedNotes[1].Id);
        }

        [Fact]
        public void ImportNotes_UnsupportedFutureSchemaVersion_RejectsWithClearError()
        {
            string futureJson = JsonConvert.SerializeObject(new
            {
                schemaVersion = 99,
                application = "PinNote",
                notes = new[] { new { title = "From the Future" } }
            });

            string file = Path.Combine(_testDir, "future_schema.json");
            File.WriteAllText(file, futureJson);

            var result = ImportExportService.ImportNotes(file, new List<NoteModel>(), _testDir);

            Assert.False(result.Success);
            Assert.Contains("Unsupported package schema version 99", result.Message);
        }

        [Fact]
        public void ImportNotes_OlderSupportedSchemaVersion_MigratesSuccessfully()
        {
            // Legacy schema v1 note missing Tags and WorkspaceName
            string legacyJson = @"{
                ""schemaVersion"": 1,
                ""application"": ""PinNote"",
                ""notes"": [
                    {
                        ""id"": """ + Guid.NewGuid() + @""",
                        ""title"": ""Legacy Note"",
                        ""content"": ""Old content"",
                        ""schemaVersion"": 1
                    }
                ]
            }";

            string file = Path.Combine(_testDir, "legacy_note.json");
            File.WriteAllText(file, legacyJson);

            var result = ImportExportService.ImportNotes(file, new List<NoteModel>(), _testDir);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            var note = result.ImportedNotes[0];
            Assert.Equal("Legacy Note", note.Title);
            Assert.NotNull(note.Tags);
            Assert.Equal("Default", note.WorkspaceName);
            Assert.NotEqual(default, note.CreatedAt);
        }

        [Fact]
        public void ImportNotes_MissingNotesField_ReturnsError()
        {
            string missingNotesJson = @"{
                ""schemaVersion"": 1,
                ""application"": ""PinNote""
            }";

            string file = Path.Combine(_testDir, "missing_notes.json");
            File.WriteAllText(file, missingNotesJson);

            var result = ImportExportService.ImportNotes(file, new List<NoteModel>(), _testDir);

            Assert.False(result.Success);
            Assert.Contains("missing 'notes' array", result.Message);
        }

        [Fact]
        public void ImportNotes_MissingFieldsAndMalformedNotes_SanitizesSafely()
        {
            // Note with missing title, null tags, invalid width/opacity
            string malformedNoteJson = @"{
                ""schemaVersion"": 1,
                ""notes"": [
                    {
                        ""title"": """",
                        ""width"": 50,
                        ""opacity"": -2.0,
                        ""textOpacity"": 5.0
                    }
                ]
            }";

            string file = Path.Combine(_testDir, "malformed_note.json");
            File.WriteAllText(file, malformedNoteJson);

            var result = ImportExportService.ImportNotes(file, new List<NoteModel>(), _testDir);

            Assert.True(result.Success);
            Assert.Equal(1, result.ImportedCount);
            var note = result.ImportedNotes[0];
            Assert.Equal("Imported Note", note.Title);
            Assert.Equal(250, note.Width); // Sanitized from 50 to default 250
            Assert.Equal(1.0, note.Opacity); // Clamped to 1.0
            Assert.Equal(1.0, note.TextOpacity); // Clamped to 1.0
        }

        [Fact]
        public void ImportNotes_BackupRecovery_OriginalDataCanBeRestoredFromBackup()
        {
            var originalNotes = new List<NoteModel>
            {
                new() { Title = "Critical Note 1", Content = "Financial data" },
                new() { Title = "Critical Note 2", Content = "Passwords info" }
            };

            string importFile = Path.Combine(_testDir, "new_batch.json");
            ImportExportService.ExportNotes(new List<NoteModel>
            {
                new() { Title = "Unrelated note" }
            }, importFile);

            var importResult = ImportExportService.ImportNotes(importFile, originalNotes, _testDir, ImportMode.Replace);
            Assert.True(importResult.Success);
            Assert.NotNull(importResult.BackupPath);
            Assert.True(File.Exists(importResult.BackupPath));

            // Now simulate recovering by importing the backup file
            var recoverResult = ImportExportService.ImportNotes(importResult.BackupPath, new List<NoteModel>(), _testDir, ImportMode.Replace);
            Assert.True(recoverResult.Success);
            Assert.Equal(2, recoverResult.ImportedCount);
            Assert.Contains(recoverResult.ImportedNotes, n => n.Title == "Critical Note 1");
            Assert.Contains(recoverResult.ImportedNotes, n => n.Title == "Critical Note 2");
        }
    }
}
