using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PinNote.Models;

namespace PinNote.Services
{
    public class StorageService
    {
        private readonly string _filePath;

        public StorageService()
        {
            string folderPath = GetStorageFolderPath();
            _filePath = Path.Combine(folderPath, "notes.json");
        }

        public List<NoteModel> LoadNotes()
        {
            if (!File.Exists(_filePath))
            {
                return new List<NoteModel>();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<List<NoteModel>>(json) ?? new List<NoteModel>();
            }
            catch
            {
                return new List<NoteModel>();
            }
        }

        public void SaveNotes(List<NoteModel> notes)
        {
            try
            {
                string json = JsonConvert.SerializeObject(notes, Formatting.Indented);
                WriteAtomically(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving notes: {ex.Message}");
            }
        }

        private static void WriteAtomically(string path, string content)
        {
            string tempPath = $"{path}.tmp";
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, path, true);
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
