using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PinNote.Models;

namespace PinNote.Services
{
    public class WorkspaceService
    {
        private static readonly object _fileLock = new();
        private readonly string _filePath;

        public static readonly Guid DefaultWorkspaceId = new("00000000-0000-0000-0000-000000000001");

        public WorkspaceService(string? customFolderPath = null)
        {
            string folder = !string.IsNullOrWhiteSpace(customFolderPath)
                ? customFolderPath
                : GetDefaultStorageFolder();

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _filePath = Path.Combine(folder, "workspaces.json");
        }

        public List<WorkspaceModel> LoadWorkspaces()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                {
                    var defaults = CreateDefaultWorkspaces();
                    SaveWorkspaces(defaults);
                    return defaults;
                }

                try
                {
                    string json = File.ReadAllText(_filePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        var defaults = CreateDefaultWorkspaces();
                        SaveWorkspaces(defaults);
                        return defaults;
                    }

                    var workspaces = JsonConvert.DeserializeObject<List<WorkspaceModel>>(json);
                    if (workspaces == null || workspaces.Count == 0)
                    {
                        var defaults = CreateDefaultWorkspaces();
                        SaveWorkspaces(defaults);
                        return defaults;
                    }

                    // Ensure a default workspace exists
                    if (!workspaces.Any(w => w.IsDefault))
                    {
                        workspaces.Insert(0, new WorkspaceModel
                        {
                            Id = DefaultWorkspaceId,
                            Name = "Default",
                            IsDefault = true,
                            Order = 0
                        });
                    }

                    return workspaces;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WorkspaceService.LoadWorkspaces error: {ex}");
                    return CreateDefaultWorkspaces();
                }
            }
        }

        public void SaveWorkspaces(List<WorkspaceModel> workspaces)
        {
            lock (_fileLock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(workspaces ?? CreateDefaultWorkspaces(), Formatting.Indented);
                    string tempPath = $"{_filePath}.tmp_{Guid.NewGuid():N}";
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _filePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WorkspaceService.SaveWorkspaces error: {ex}");
                }
            }
        }

        public static List<WorkspaceModel> CreateDefaultWorkspaces()
        {
            return new List<WorkspaceModel>
            {
                new() { Id = DefaultWorkspaceId, Name = "Default", IsDefault = true, Order = 0 },
                new() { Id = Guid.NewGuid(), Name = "University", IsDefault = false, Order = 1 },
                new() { Id = Guid.NewGuid(), Name = "Personal", IsDefault = false, Order = 2 },
                new() { Id = Guid.NewGuid(), Name = "Development", IsDefault = false, Order = 3 },
                new() { Id = Guid.NewGuid(), Name = "Projects", IsDefault = false, Order = 4 }
            };
        }

        private static string GetDefaultStorageFolder()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
            return Path.Combine(root, "PinNote");
        }
    }
}
