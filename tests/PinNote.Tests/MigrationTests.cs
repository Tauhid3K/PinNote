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
    /// <summary>
    /// Tests that verify backward-compatibility when loading note data from older
    /// versions of PinNote that may be missing fields introduced in later releases.
    /// </summary>
    public class MigrationTests
    {
        // ─── NoteType / ChecklistItems (added in P0.3) ─────────────────────────

        [Fact]
        public void OlderJson_WithoutNoteTypeField_DefaultsToTextNote()
        {
            string json = @"[{
                ""Id"": """ + Guid.NewGuid() + @""",
                ""Title"": ""Legacy"",
                ""Content"": ""Some plain text""
            }]";

            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);

            Assert.NotNull(notes);
            Assert.Single(notes!);
            Assert.Equal(NoteType.Text, notes![0].NoteType);
        }

        [Fact]
        public void OlderJson_WithoutChecklistItemsField_DefaultsToEmptyList()
        {
            string json = @"[{
                ""Id"": """ + Guid.NewGuid() + @""",
                ""Title"": ""Legacy"",
                ""Content"": ""Some text""
            }]";

            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);

            Assert.NotNull(notes);
            Assert.NotNull(notes![0].ChecklistItems);
            Assert.Empty(notes![0].ChecklistItems);
        }

        // ─── Timestamps (added in this P0 cycle) ──────────────────────────────

        [Fact]
        public void OlderJson_WithoutTimestamps_DefaultsToValidUtcTimestamp()
        {
            string json = @"[{
                ""Id"": """ + Guid.NewGuid() + @""",
                ""Title"": ""Old Note"",
                ""Content"": ""Content""
            }]";

            var before = DateTime.UtcNow.AddSeconds(-2);
            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);
            var after = DateTime.UtcNow.AddSeconds(2);

            Assert.NotNull(notes);
            // Defaults should be initialized to DateTime.UtcNow on construction, not throw or corrupt
            Assert.InRange(notes![0].CreatedAt, before, after);
            Assert.InRange(notes![0].ModifiedAt, before, after);
        }

        // ─── IsVisible field (added later) ────────────────────────────────────

        [Fact]
        public void OlderJson_WithoutIsVisibleField_DefaultsToTrue()
        {
            string json = @"[{
                ""Id"": """ + Guid.NewGuid() + @""",
                ""Title"": ""Old Note""
            }]";

            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);

            Assert.NotNull(notes);
            Assert.True(notes![0].IsVisible);
        }

        // ─── OpacityDefaults ─────────────────────────────────────────────────

        [Fact]
        public void OlderJson_WithoutOpacityFields_DefaultsToOne()
        {
            string json = @"[{
                ""Id"": """ + Guid.NewGuid() + @""",
                ""Title"": ""Old Note""
            }]";

            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);

            Assert.NotNull(notes);
            Assert.Equal(1.0, notes![0].Opacity);
            Assert.Equal(1.0, notes![0].TextOpacity);
        }

        // ─── Mixed schema (some notes with timestamps, some without) ──────────

        [Fact]
        public void MixedJson_SomeNotesWithTimestamps_LoadsAllCorrectly()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            string json = $@"[
                {{
                    ""Id"": ""{id1}"",
                    ""Title"": ""With timestamps"",
                    ""CreatedAt"": ""2024-01-01T00:00:00Z"",
                    ""ModifiedAt"": ""2024-06-01T00:00:00Z""
                }},
                {{
                    ""Id"": ""{id2}"",
                    ""Title"": ""Without timestamps""
                }}
            ]";

            var before = DateTime.UtcNow.AddSeconds(-2);
            var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);
            var after = DateTime.UtcNow.AddSeconds(2);

            Assert.NotNull(notes);
            Assert.Equal(2, notes!.Count);

            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notes[0].CreatedAt);
            Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), notes[0].ModifiedAt);

            // Legacy note: timestamps should default to current Utc timestamp on model instantiation
            Assert.InRange(notes[1].CreatedAt, before, after);
            Assert.InRange(notes[1].ModifiedAt, before, after);
        }

        // ─── StorageService preserves timestamps through round-trip ────────────

        [Fact]
        public void StorageService_RoundTrip_PreservesTimestamps()
        {
            string testDir = Path.Combine(Path.GetTempPath(), $"pinnote_mig_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            try
            {
                var service = new StorageService(testDir);

                var createdAt  = new DateTime(2024, 3, 15, 10, 0, 0, DateTimeKind.Utc);
                var modifiedAt = new DateTime(2024, 9, 20, 18, 30, 0, DateTimeKind.Utc);

                var notes = new List<NoteModel>
                {
                    new()
                    {
                        Title      = "Timestamped Note",
                        Content    = "Content",
                        CreatedAt  = createdAt,
                        ModifiedAt = modifiedAt
                    }
                };

                service.SaveNotes(notes);
                var loaded = service.LoadNotes();

                Assert.Single(loaded);
                Assert.Equal(createdAt,  loaded[0].CreatedAt);
                Assert.Equal(modifiedAt, loaded[0].ModifiedAt);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        // ─── NoteViewModel round-trip through MainViewModel ────────────────────

        [Fact]
        public void MainViewModel_LoadsLegacyNotes_WithoutThrowingOrLosingContent()
        {
            string testDir = Path.Combine(Path.GetTempPath(), $"pinnote_migvm_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            try
            {
                // Write legacy JSON without new fields
                string legacyJson = $@"[
                    {{
                        ""Id"": ""{Guid.NewGuid()}"",
                        ""Title"": ""Legacy Note A"",
                        ""Content"": ""Plain content""
                    }},
                    {{
                        ""Id"": ""{Guid.NewGuid()}"",
                        ""Title"": ""Legacy Note B"",
                        ""NoteType"": 0
                    }}
                ]";
                File.WriteAllText(Path.Combine(testDir, "notes.json"), legacyJson);

                var storage = new StorageService(testDir);
                var vm = new MainViewModel(customStorageService: storage);

                Assert.Equal(2, vm.Notes.Count);
                Assert.Equal("Legacy Note A", vm.Notes[0].Title);
                Assert.Equal("Legacy Note B", vm.Notes[1].Title);
                Assert.Equal(NoteType.Text, vm.Notes[0].NoteType);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        // ─── NoteModel.Clone preserves timestamps ─────────────────────────────

        [Fact]
        public void NoteModel_Clone_PreservesTimestamps()
        {
            var createdAt  = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var modifiedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            var original = new NoteModel
            {
                Title      = "Original",
                CreatedAt  = createdAt,
                ModifiedAt = modifiedAt
            };

            var clone = original.Clone();

            Assert.Equal(createdAt,  clone.CreatedAt);
            Assert.Equal(modifiedAt, clone.ModifiedAt);
            Assert.NotSame(original, clone);
        }
    }
}
