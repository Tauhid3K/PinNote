using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PinNote.Models;

namespace PinNote.Services
{
    public enum ImportMode
    {
        Merge = 0,
        Replace = 1
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public int ExportedCount { get; set; }
        public string? FilePath { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int ImportedCount { get; set; }
        public int ReassignedIdCount { get; set; }
        public List<NoteModel> ImportedNotes { get; set; } = new();
        public string? BackupPath { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static class ImportExportService
    {
        public const int CurrentSchemaVersion = 1;

        public static ExportResult ExportNotes(IEnumerable<NoteModel> notes, string destinationPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return new ExportResult { Success = false, Message = "Export file path cannot be empty." };
                }

                var noteList = notes?.Select(n => n.Clone()).ToList() ?? new List<NoteModel>();

                var package = new NoteExportPackage
                {
                    SchemaVersion = CurrentSchemaVersion,
                    Application = "PinNote",
                    AppVersion = "1.0.3",
                    ExportedAt = DateTime.UtcNow,
                    Notes = noteList
                };

                string json = JsonConvert.SerializeObject(package, Formatting.Indented);

                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Use atomic write (write to temp then rename) to avoid partial exports
                string tempPath = $"{destinationPath}.tmp_{Guid.NewGuid():N}";
                try
                {
                    File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
                    File.Move(tempPath, destinationPath, overwrite: true);
                }
                catch
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    throw;
                }

                return new ExportResult
                {
                    Success = true,
                    ExportedCount = noteList.Count,
                    FilePath = destinationPath,
                    Message = $"Successfully exported {noteList.Count} notes to '{Path.GetFileName(destinationPath)}'."
                };
            }
            catch (Exception ex)
            {
                return new ExportResult
                {
                    Success = false,
                    ExportedCount = 0,
                    Message = $"Export failed: {ex.Message}"
                };
            }
        }

        public static ImportResult ImportNotes(
            string sourceFilePath,
            IEnumerable<NoteModel> existingNotes,
            string storageFolderPath)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                {
                    return new ImportResult { Success = false, Message = $"File not found: '{sourceFilePath}'" };
                }

                string json = File.ReadAllText(sourceFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ImportResult { Success = false, Message = "The selected import file is empty." };
                }

                List<NoteModel>? candidateNotes = null;
                int schemaVersion = 1;

                // Try parsing as NoteExportPackage first
                try
                {
                    var token = JToken.Parse(json);
                    if (token is JObject obj)
                    {
                        if (obj.TryGetValue("schemaVersion", StringComparison.OrdinalIgnoreCase, out var verToken))
                        {
                            schemaVersion = verToken.Value<int>();
                        }

                        if (obj.TryGetValue("notes", StringComparison.OrdinalIgnoreCase, out var notesToken))
                        {
                            candidateNotes = notesToken.ToObject<List<NoteModel>>();
                        }
                    }
                    else if (token is JArray)
                    {
                        // Raw list of notes from a direct notes.json backup
                        candidateNotes = JsonConvert.DeserializeObject<List<NoteModel>>(json);
                    }
                }
                catch (JsonException ex)
                {
                    return new ImportResult { Success = false, Message = $"Invalid JSON format in import file: {ex.Message}" };
                }

                if (candidateNotes == null || candidateNotes.Count == 0)
                {
                    return new ImportResult { Success = false, Message = "No valid notes found in the import file." };
                }

                // Create a pre-import backup of existing notes before any changes
                string backupPath = string.Empty;
                try
                {
                    if (!Directory.Exists(storageFolderPath))
                    {
                        Directory.CreateDirectory(storageFolderPath);
                    }
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    backupPath = Path.Combine(storageFolderPath, $"notes_backup_before_import_{timestamp}.json");
                    var existingList = existingNotes?.ToList() ?? new List<NoteModel>();
                    File.WriteAllText(backupPath, JsonConvert.SerializeObject(existingList, Formatting.Indented));
                }
                catch (Exception backupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create pre-import backup: {backupEx}");
                }

                // Check for duplicate identifiers to prevent overwriting existing notes
                var existingIds = new HashSet<Guid>(existingNotes?.Select(n => n.Id) ?? Enumerable.Empty<Guid>());
                int reassignedCount = 0;
                var validatedNotes = new List<NoteModel>();

                foreach (var note in candidateNotes)
                {
                    if (note == null) continue;

                    var cloned = note.Clone();

                    // If Id is empty or already in use by an existing note, assign a new unique ID
                    if (cloned.Id == Guid.Empty || existingIds.Contains(cloned.Id))
                    {
                        cloned.Id = Guid.NewGuid();
                        reassignedCount++;
                    }

                    // Sanitize dimensions
                    if (cloned.Width < 160) cloned.Width = 250;
                    if (cloned.Height < 120) cloned.Height = 250;
                    if (cloned.Opacity <= 0 || cloned.Opacity > 1.0) cloned.Opacity = 1.0;
                    if (cloned.TextOpacity <= 0 || cloned.TextOpacity > 1.0) cloned.TextOpacity = 1.0;
                    if (string.IsNullOrEmpty(cloned.Title)) cloned.Title = "Imported Note";

                    existingIds.Add(cloned.Id);
                    validatedNotes.Add(cloned);
                }

                string detail = reassignedCount > 0
                    ? $" ({reassignedCount} IDs re-assigned to prevent conflicts)"
                    : string.Empty;

                return new ImportResult
                {
                    Success = true,
                    ImportedCount = validatedNotes.Count,
                    ReassignedIdCount = reassignedCount,
                    ImportedNotes = validatedNotes,
                    BackupPath = backupPath,
                    Message = $"Successfully imported {validatedNotes.Count} notes{detail}."
                };
            }
            catch (Exception ex)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = $"Import failed: {ex.Message}"
                };
            }
        }
    }
}
