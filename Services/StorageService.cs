using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using PinNote.Models;

namespace PinNote.Services
{
    public class StorageService
    {
        private static readonly object _fileLock = new();
        private readonly string _folderPath;
        private readonly string _filePath;
        private readonly string _backupFilePath;
        private bool _hasLoadError;
        private long _lastSavedTimestamp;

        public event EventHandler<string>? SaveFailed;
        public event EventHandler<string>? LoadFailed;
        public event EventHandler<string>? LoadSucceeded;

        public string StorageFolderPath => _folderPath;
        public string FilePath => _filePath;
        public string BackupFilePath => _backupFilePath;
        public bool HasLoadError => _hasLoadError;

        public StorageService(string? customFolderPath = null)
        {
            _folderPath = !string.IsNullOrWhiteSpace(customFolderPath)
                ? customFolderPath
                : GetStorageFolderPath();

            EnsureDirectoryExists(_folderPath);
            _filePath = Path.Combine(_folderPath, "notes.json");
            _backupFilePath = Path.Combine(_folderPath, "notes.json.bak");
        }

        public List<NoteModel> LoadNotes()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    // If primary does not exist, check if a backup exists to restore from
                    if (File.Exists(_backupFilePath))
                    {
                        try
                        {
                            string backupJson = File.ReadAllText(_backupFilePath);
                            var backupNotes = JsonConvert.DeserializeObject<List<NoteModel>>(backupJson);
                            if (backupNotes != null)
                            {
                                Debug.WriteLine("StorageService.LoadNotes: Restored notes from backup because primary was missing.");
                                _hasLoadError = false;
                                LoadSucceeded?.Invoke(this, "Restored notes from backup file.");
                                return backupNotes;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"StorageService.LoadNotes backup recovery error: {ex}");
                        }
                    }

                    _hasLoadError = false;
                    return new List<NoteModel>();
                }

                try
                {
                    string json = File.ReadAllText(_filePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        // File is empty - try backup
                        return TryRecoverFromBackup("Primary notes.json is empty.");
                    }

                    var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);
                    if (notes != null)
                    {
                        _hasLoadError = false;
                        LoadSucceeded?.Invoke(this, $"Successfully loaded {notes.Count} notes.");
                        return notes;
                    }

                    return TryRecoverFromBackup("Deserialization returned null.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"StorageService.LoadNotes primary read error: {ex}");
                    _hasLoadError = true;

                    // Preserve corrupt file for recovery rather than losing data
                    try
                    {
                        string corruptPath = Path.Combine(_folderPath, $"notes.json.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}");
                        if (File.Exists(_filePath))
                        {
                            File.Copy(_filePath, corruptPath, true);
                            Debug.WriteLine($"StorageService.LoadNotes: Preserved corrupt file at {corruptPath}");
                        }
                    }
                    catch (Exception copyEx)
                    {
                        Debug.WriteLine($"Failed to preserve corrupt file: {copyEx}");
                    }

                    // Attempt to restore from backup
                    var recovered = TryRecoverFromBackup(ex.Message);
                    if (recovered.Count > 0)
                    {
                        return recovered;
                    }

                    LoadFailed?.Invoke(this, ex.Message);
                    NotificationService.ShowNotification("PinNote Storage Warning", $"Failed to load notes: {ex.Message}");
                    return new List<NoteModel>();
                }
            }
        }

        private List<NoteModel> TryRecoverFromBackup(string reason)
        {
            if (File.Exists(_backupFilePath))
            {
                try
                {
                    string backupJson = File.ReadAllText(_backupFilePath);
                    if (!string.IsNullOrWhiteSpace(backupJson))
                    {
                        var backupNotes = JsonConvert.DeserializeObject<List<NoteModel>>(backupJson);
                        if (backupNotes != null)
                        {
                            Debug.WriteLine($"StorageService.LoadNotes: Restored {backupNotes.Count} notes from backup. Reason: {reason}");
                            _hasLoadError = false;
                            LoadSucceeded?.Invoke(this, $"Restored {backupNotes.Count} notes from backup.");
                            return backupNotes;
                        }
                    }
                }
                catch (Exception backupEx)
                {
                    Debug.WriteLine($"StorageService.LoadNotes backup read error: {backupEx}");
                }
            }

            return new List<NoteModel>();
        }

        public void SaveNotes(List<NoteModel> notes, long snapshotTimestamp = 0)
        {
            string json = JsonConvert.SerializeObject(notes ?? new List<NoteModel>(), Formatting.Indented);
            SaveNotesJson(json, notes?.Count ?? 0, snapshotTimestamp);
        }

        public void SaveNotesJson(string json, int noteCount, long snapshotTimestamp = 0)
        {
            lock (_fileLock)
            {
                // Safety guard: if initial load failed and we have an empty list, do not wipe the existing file
                if (_hasLoadError && noteCount == 0)
                {
                    Debug.WriteLine("StorageService.SaveNotes skipped: guarding against wiping notes after load failure.");
                    return;
                }

                // Guard against stale async saves overwriting newer content
                if (snapshotTimestamp > 0 && snapshotTimestamp < _lastSavedTimestamp)
                {
                    Debug.WriteLine("StorageService.SaveNotes skipped: snapshot is older than last saved timestamp.");
                    return;
                }

                try
                {
                    EnsureDirectoryExists(_folderPath);

                    // Update backup before overwriting, but only if the current file exists and is non-empty
                    if (File.Exists(_filePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(_filePath);
                            if (fileInfo.Length > 2) // more than just "{}" or "[]"
                            {
                                File.Copy(_filePath, _backupFilePath, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"StorageService.SaveNotes backup creation error: {ex}");
                        }
                    }

                    WriteAtomically(_filePath, json);
                    _hasLoadError = false;
                    _lastSavedTimestamp = snapshotTimestamp > 0
                        ? Math.Max(snapshotTimestamp, DateTime.UtcNow.Ticks)
                        : DateTime.UtcNow.Ticks;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"StorageService.SaveNotes error: {ex}");
                    Console.WriteLine($"Error saving notes: {ex.Message}");
                    SaveFailed?.Invoke(this, ex.Message);
                    NotificationService.ShowNotification("PinNote Storage Warning", $"Failed to save notes: {ex.Message}");
                }
            }
        }

        public string CreateBackup(string? customTag = null)
        {
            lock (_fileLock)
            {
                EnsureDirectoryExists(_folderPath);
                string tag = string.IsNullOrWhiteSpace(customTag) ? "manual" : customTag.Trim();
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(_folderPath, $"notes_backup_{tag}_{timestamp}.json");

                if (File.Exists(_filePath))
                {
                    File.Copy(_filePath, backupPath, true);
                    return backupPath;
                }

                // If notes.json doesn't exist, write empty list
                File.WriteAllText(backupPath, "[]");
                return backupPath;
            }
        }

        private static void WriteAtomically(string path, string content)
        {
            string tempPath = $"{path}.tmp_{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(tempPath, content);

                int retries = 5;
                while (true)
                {
                    try
                    {
                        File.Move(tempPath, path, overwrite: true);
                        break;
                    }
                    catch (IOException) when (--retries > 0)
                    {
                        Thread.Sleep(40);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch { }

                throw new IOException($"Atomic write failed for '{path}': {ex.Message}", ex);
            }
        }

        private static void EnsureDirectoryExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        private static string GetStorageFolderPath()
        {
            string[] candidateRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetTempPath(),
            };

            foreach (var root in candidateRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                try
                {
                    string folderPath = Path.Combine(root, "PinNote");
                    Directory.CreateDirectory(folderPath);
                    return folderPath;
                }
                catch
                {
                    // Try the next location.
                }
            }

            string fallbackPath = Path.Combine(AppContext.BaseDirectory, "PinNoteData");
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }
    }
}
