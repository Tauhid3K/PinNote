using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        public List<WorkspaceModel> ImportedWorkspaces { get; set; } = new();
        public string? BackupPath { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static class ImportExportService
    {
        public const string CurrentAppVersion = "1.1.0";
        public const int CurrentSchemaVersion = 1;
        public const int MaxSupportedSchemaVersion = 2;

        public static ExportResult ExportNotes(IEnumerable<NoteModel> notes, string destinationPath, IEnumerable<WorkspaceModel>? workspaces = null)
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
                    AppVersion = CurrentAppVersion,
                    ExportedAt = DateTime.UtcNow,
                    Notes = noteList,
                    Workspaces = workspaces?.Select(w => w.Clone()).ToList() ?? new List<WorkspaceModel>()
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

        public static async Task<ExportResult> ExportNotesAsync(IEnumerable<NoteModel> notes, string destinationPath, IEnumerable<WorkspaceModel>? workspaces = null)
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
                    AppVersion = CurrentAppVersion,
                    ExportedAt = DateTime.UtcNow,
                    Notes = noteList,
                    Workspaces = workspaces?.Select(w => w.Clone()).ToList() ?? new List<WorkspaceModel>()
                };

                string json = await Task.Run(() => JsonConvert.SerializeObject(package, Formatting.Indented));

                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tempPath = $"{destinationPath}.tmp_{Guid.NewGuid():N}";
                try
                {
                    await File.WriteAllTextAsync(tempPath, json, System.Text.Encoding.UTF8);
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
            string storageFolderPath,
            ImportMode mode = ImportMode.Merge)
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

                return ProcessImportJson(json, existingNotes, storageFolderPath, mode, isAsync: false).GetAwaiter().GetResult();
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

        public static async Task<ImportResult> ImportNotesAsync(
            string sourceFilePath,
            IEnumerable<NoteModel> existingNotes,
            string storageFolderPath,
            ImportMode mode = ImportMode.Merge)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                {
                    return new ImportResult { Success = false, Message = $"File not found: '{sourceFilePath}'" };
                }

                string json = await File.ReadAllTextAsync(sourceFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ImportResult { Success = false, Message = "The selected import file is empty." };
                }

                return await ProcessImportJson(json, existingNotes, storageFolderPath, mode, isAsync: true);
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

        private static async Task<ImportResult> ProcessImportJson(
            string json,
            IEnumerable<NoteModel> existingNotes,
            string storageFolderPath,
            ImportMode mode,
            bool isAsync)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ImportResult { Success = false, Message = "The selected import file is empty." };
            }

            JToken token;
            try
            {
                token = JToken.Parse(json);
            }
            catch (Exception ex)
            {
                return new ImportResult { Success = false, Message = $"Invalid JSON format in import file: {ex.Message}" };
            }

            int packageSchemaVersion = 1;
            JArray? notesArray = null;
            var candidateWorkspaces = new List<WorkspaceModel>();

            if (token is JObject obj)
            {
                if (obj.TryGetValue("schemaVersion", StringComparison.OrdinalIgnoreCase, out var verToken) ||
                    obj.TryGetValue("SchemaVersion", StringComparison.OrdinalIgnoreCase, out verToken))
                {
                    try
                    {
                        packageSchemaVersion = verToken.Value<int>();
                    }
                    catch
                    {
                        packageSchemaVersion = 1;
                    }
                }

                if (packageSchemaVersion > MaxSupportedSchemaVersion)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = $"Unsupported package schema version {packageSchemaVersion}. " +
                                  $"This file was created by a newer version of PinNote (maximum supported version is {MaxSupportedSchemaVersion}) and cannot be imported."
                    };
                }

                if (packageSchemaVersion < 1)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = $"Invalid package schema version {packageSchemaVersion}."
                    };
                }

                if (obj.TryGetValue("workspaces", StringComparison.OrdinalIgnoreCase, out var wsToken) ||
                    obj.TryGetValue("Workspaces", StringComparison.OrdinalIgnoreCase, out wsToken))
                {
                    if (wsToken is JArray wsArr)
                    {
                        foreach (var wsItem in wsArr)
                        {
                            if (wsItem is JObject wsObj)
                            {
                                try
                                {
                                    var ws = wsObj.ToObject<WorkspaceModel>();
                                    if (ws != null && !string.IsNullOrWhiteSpace(ws.Name))
                                    {
                                        if (ws.Id == Guid.Empty) ws.Id = Guid.NewGuid();
                                        ws.Name = ws.Name.Trim();
                                        candidateWorkspaces.Add(ws);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                if (obj.TryGetValue("notes", StringComparison.OrdinalIgnoreCase, out var nToken) ||
                    obj.TryGetValue("Notes", StringComparison.OrdinalIgnoreCase, out nToken))
                {
                    notesArray = nToken as JArray;
                }
                else
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = "No valid notes found in the import package (missing 'notes' array)."
                    };
                }
            }
            else if (token is JArray arr)
            {
                // Raw list of notes from a direct notes.json backup
                notesArray = arr;
                packageSchemaVersion = 1;
            }
            else
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "Invalid import file format: Root must be a JSON object or array."
                };
            }

            if (notesArray == null || notesArray.Count == 0)
            {
                return new ImportResult { Success = false, Message = "No valid notes found in the import file." };
            }

            var candidateNotes = new List<NoteModel>();
            int parseErrors = 0;

            foreach (var item in notesArray)
            {
                if (item is not JObject noteObj)
                {
                    parseErrors++;
                    continue;
                }

                try
                {
                    var note = noteObj.ToObject<NoteModel>();
                    if (note == null)
                    {
                        parseErrors++;
                        continue;
                    }
                    candidateNotes.Add(note);
                }
                catch
                {
                    parseErrors++;
                }
            }

            if (candidateNotes.Count == 0)
            {
                return new ImportResult { Success = false, Message = "No valid notes could be parsed from the import file." };
            }

            // Upgrade/migrate older notes to current schema
            DataMigrationService.Migrate(candidateNotes, storageFolderPath);

            // Create a pre-import backup of existing notes before any changes
            string backupPath = string.Empty;
            try
            {
                if (!Directory.Exists(storageFolderPath))
                {
                    Directory.CreateDirectory(storageFolderPath);
                }
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string backupPrefix = mode == ImportMode.Replace ? "notes_backup_before_replace" : "notes_backup_before_import";
                backupPath = Path.Combine(storageFolderPath, $"{backupPrefix}_{timestamp}.json");
                var existingList = existingNotes?.ToList() ?? new List<NoteModel>();
                string backupJson = JsonConvert.SerializeObject(existingList, Formatting.Indented);
                if (isAsync)
                {
                    await File.WriteAllTextAsync(backupPath, backupJson);
                }
                else
                {
                    File.WriteAllText(backupPath, backupJson);
                }

                if (!File.Exists(backupPath))
                {
                    throw new IOException($"Safety backup file '{backupPath}' could not be verified on disk.");
                }
            }
            catch (Exception backupEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create pre-import backup: {backupEx}");
                if (mode == ImportMode.Replace)
                {
                    return new ImportResult
                    {
                        Success = false,
                        Message = $"Failed to create safety backup prior to replacing notes: {backupEx.Message}. Replace operation aborted to protect your notes."
                    };
                }
            }

            // Deduplicate IDs and validate/sanitize fields
            var seenIds = mode == ImportMode.Merge
                ? new HashSet<Guid>(existingNotes?.Select(n => n.Id) ?? Enumerable.Empty<Guid>())
                : new HashSet<Guid>();

            int reassignedCount = 0;
            var validatedNotes = new List<NoteModel>();

            foreach (var note in candidateNotes)
            {
                if (note == null) continue;

                var cloned = note.Clone();

                // If Id is empty or already in use, assign a new unique ID
                if (cloned.Id == Guid.Empty || seenIds.Contains(cloned.Id))
                {
                    cloned.Id = Guid.NewGuid();
                    reassignedCount++;
                }

                // Sanitize dimensions and values
                if (cloned.Width < 160) cloned.Width = 250;
                if (cloned.Height < 120) cloned.Height = 250;
                if (cloned.Opacity <= 0 || cloned.Opacity > 1.0) cloned.Opacity = 1.0;
                if (cloned.TextOpacity <= 0 || cloned.TextOpacity > 1.0) cloned.TextOpacity = 1.0;
                if (string.IsNullOrWhiteSpace(cloned.Title)) cloned.Title = "Imported Note";
                if (cloned.Content == null) cloned.Content = string.Empty;
                if (cloned.Tags == null) cloned.Tags = new List<string>();
                if (string.IsNullOrWhiteSpace(cloned.WorkspaceName)) cloned.WorkspaceName = "Default";
                cloned.WorkspaceName = cloned.WorkspaceName.Trim();
                if (cloned.ChecklistItems == null) cloned.ChecklistItems = new List<ChecklistItemModel>();
                if (cloned.CreatedAt == default) cloned.CreatedAt = DateTime.UtcNow;
                if (cloned.ModifiedAt == default) cloned.ModifiedAt = cloned.CreatedAt;

                seenIds.Add(cloned.Id);
                validatedNotes.Add(cloned);
            }

            // Ensure every distinct workspace referenced by notes exists in candidateWorkspaces
            var distinctWorkspaceNames = validatedNotes
                .Select(n => string.IsNullOrWhiteSpace(n.WorkspaceName) ? "Default" : n.WorkspaceName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var wsName in distinctWorkspaceNames)
            {
                if (!candidateWorkspaces.Any(w => string.Equals(w.Name, wsName, StringComparison.OrdinalIgnoreCase)))
                {
                    var sampleNote = validatedNotes.FirstOrDefault(n => string.Equals(n.WorkspaceName, wsName, StringComparison.OrdinalIgnoreCase));
                    candidateWorkspaces.Add(new WorkspaceModel
                    {
                        Id = sampleNote?.WorkspaceId ?? Guid.NewGuid(),
                        Name = wsName,
                        IsDefault = string.Equals(wsName, "Default", StringComparison.OrdinalIgnoreCase),
                        Order = candidateWorkspaces.Count
                    });
                }
            }

            string detail = reassignedCount > 0
                ? $" ({reassignedCount} ID(s) re-assigned to prevent conflicts)"
                : string.Empty;

            string actionWord = mode == ImportMode.Replace ? "replaced" : "imported";
            return new ImportResult
            {
                Success = true,
                ImportedCount = validatedNotes.Count,
                ReassignedIdCount = reassignedCount,
                ImportedNotes = validatedNotes,
                ImportedWorkspaces = candidateWorkspaces,
                BackupPath = backupPath,
                Message = $"Successfully {actionWord} {validatedNotes.Count} notes{detail}."
            };
        }
    }
}
