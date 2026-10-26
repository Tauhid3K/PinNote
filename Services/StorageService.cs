using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using PinNote.Models;

namespace PinNote.Services
{
    public class StorageService
    {
        private static readonly object _fileLock = new();
        private readonly string _filePath;
        private readonly string _backupFilePath;
        private bool _hasLoadError;

        public event EventHandler<string>? SaveFailed;
        public event EventHandler<string>? LoadFailed;

        public StorageService()
        {
            string folderPath = GetStorageFolderPath();
            _filePath = Path.Combine(folderPath, "notes.json");
            _backupFilePath = Path.Combine(folderPath, "notes.json.bak");
        }

        public List<NoteModel> LoadNotes()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    if (File.Exists(_backupFilePath))
                    {
                        try
                        {
                            string backupJson = File.ReadAllText(_backupFilePath);
                            var backupNotes = JsonConvert.DeserializeObject<List<NoteModel>>(backupJson);
                            if (backupNotes != null)
                            {
                                return backupNotes;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"StorageService.LoadNotes backup error: {ex}");
                        }
                    }

                    return new List<NoteModel>();
                }

                try
                {
                    string json = File.ReadAllText(_filePath);
                    var notes = JsonConvert.DeserializeObject<List<NoteModel>>(json);
                    if (notes != null)
                    {
                        _hasLoadError = false;
                        return notes;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"StorageService.LoadNotes primary read error: {ex}");
                    _hasLoadError = true;

                    // Attempt to restore from backup
                    if (File.Exists(_backupFilePath))
                    {
                        try
                        {
                            string backupJson = File.ReadAllText(_backupFilePath);
                            var backupNotes = JsonConvert.DeserializeObject<List<NoteModel>>(backupJson);
                            if (backupNotes != null)
                            {
                                Debug.WriteLine("StorageService.LoadNotes: Restored notes from backup.");
                                return backupNotes;
                            }
                        }
                        catch (Exception backupEx)
                        {
                            Debug.WriteLine($"StorageService.LoadNotes backup read error: {backupEx}");
                        }
                    }

                    LoadFailed?.Invoke(this, ex.Message);
                    NotificationService.ShowNotification("PinNote Storage Warning", $"Failed to load notes: {ex.Message}");

                    // Preserve corrupt file for user recovery rather than silently losing data
                    try
                    {
                        string corruptPath = $"{_filePath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Copy(_filePath, corruptPath, true);
                    }
                    catch { }
                }

                return new List<NoteModel>();
            }
        }

        public void SaveNotes(List<NoteModel> notes)
        {
            string json = JsonConvert.SerializeObject(notes ?? new List<NoteModel>(), Formatting.Indented);
            SaveNotesJson(json, notes?.Count ?? 0);
        }

        public void SaveNotesJson(string json, int noteCount)
        {
            lock (_fileLock)
            {
                // Safety guard: if initial load failed and we have an empty list, do not wipe the existing file
                if (_hasLoadError && noteCount == 0)
                {
                    Debug.WriteLine("StorageService.SaveNotes skipped: guarding against wiping notes after load failure.");
                    return;
                }

                try
                {
                    // Update backup before overwriting
                    if (File.Exists(_filePath))
                    {
                        try
                        {
                            File.Copy(_filePath, _backupFilePath, true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"StorageService.SaveNotes backup creation error: {ex}");
                        }
                    }

                    WriteAtomically(_filePath, json);
                    _hasLoadError = false;
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

        private static void WriteAtomically(string path, string content)
        {
            string tempPath = $"{path}.tmp";
            try
            {
                File.WriteAllText(tempPath, content);

                int retries = 3;
                while (true)
                {
                    try
                    {
                        File.Move(tempPath, path, true);
                        break;
                    }
                    catch (IOException) when (--retries > 0)
                    {
                        Thread.Sleep(30);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }

                throw new IOException($"Atomic write failed for '{path}': {ex.Message}", ex);
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
