using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PinNote.Models;

namespace PinNote.Services
{
    public static class DataMigrationService
    {
        public const int CurrentSchemaVersion = 2;

        public static readonly IReadOnlyList<string> DefaultCategories = new[]
        {
            "Study",
            "Personal",
            "Work",
            "Projects"
        };

        public static bool NeedsMigration(IEnumerable<NoteModel> notes)
        {
            if (notes == null) return false;

            return notes.Any(n =>
                n.SchemaVersion < CurrentSchemaVersion ||
                n.Tags == null ||
                string.IsNullOrWhiteSpace(n.WorkspaceName) ||
                n.CreatedAt == default ||
                n.ModifiedAt == default);
        }

        public static bool Migrate(List<NoteModel> notes, string? storageFolderPath = null)
        {
            if (notes == null || !NeedsMigration(notes))
            {
                return false;
            }

            // Create pre-migration backup if storage folder path is available
            if (!string.IsNullOrWhiteSpace(storageFolderPath))
            {
                try
                {
                    string primaryFile = Path.Combine(storageFolderPath, "notes.json");
                    if (File.Exists(primaryFile))
                    {
                        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                        string backupPath = Path.Combine(storageFolderPath, $"notes_migration_backup_v1_{timestamp}.json");
                        File.Copy(primaryFile, backupPath, overwrite: true);
                        Debug.WriteLine($"DataMigrationService: Created migration backup at {backupPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DataMigrationService backup error: {ex}");
                }
            }

            bool migratedAny = false;
            foreach (var note in notes)
            {
                if (note == null) continue;

                if (note.Tags == null)
                {
                    note.Tags = new List<string>();
                    migratedAny = true;
                }

                if (string.IsNullOrWhiteSpace(note.WorkspaceName))
                {
                    note.WorkspaceName = "Default";
                    migratedAny = true;
                }

                if (note.CreatedAt == default)
                {
                    note.CreatedAt = DateTime.UtcNow;
                    migratedAny = true;
                }

                if (note.ModifiedAt == default)
                {
                    note.ModifiedAt = note.CreatedAt;
                    migratedAny = true;
                }

                if (note.ChecklistItems == null)
                {
                    note.ChecklistItems = new List<ChecklistItemModel>();
                    migratedAny = true;
                }

                if (note.SchemaVersion < CurrentSchemaVersion)
                {
                    note.SchemaVersion = CurrentSchemaVersion;
                    migratedAny = true;
                }
            }

            return migratedAny;
        }
    }
}
