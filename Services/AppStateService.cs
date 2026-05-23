using System;
using System.IO;
using Newtonsoft.Json;

namespace PinNote.Services
{
    public class AppStateService
    {
        private readonly string _filePath;

        public AppStateService()
        {
            string folderPath = GetStorageFolderPath();
            _filePath = Path.Combine(folderPath, "appstate.json");
        }

        public bool ShouldShowFirstRunNotification()
        {
            AppState state = LoadState();

            if (state.HasShownWelcomeNotification)
            {
                return false;
            }

            state.HasShownWelcomeNotification = true;
            SaveState(state);
            return true;
        }

        private AppState LoadState()
        {
            if (!File.Exists(_filePath))
            {
                return new AppState();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<AppState>(json) ?? new AppState();
            }
            catch
            {
                return new AppState();
            }
        }

        private void SaveState(AppState state)
        {
            try
            {
                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                WriteAtomically(_filePath, json);
            }
            catch
            {
                // Ignore persistence errors and keep app startup resilient.
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

        private class AppState
        {
            public bool HasShownWelcomeNotification { get; set; }
            public bool HasShownFirstRunUi { get; set; }
            public bool ShowUiOnStartup { get; set; } = true;
            public bool StartMinimized { get; set; } = false;
        }

        public bool ShouldShowFirstRunUi()
        {
            AppState state = LoadState();

            if (state.HasShownFirstRunUi)
            {
                return false;
            }

            state.HasShownFirstRunUi = true;
            SaveState(state);
            return true;
        }

        public bool GetShowUiOnStartup()
        {
            AppState state = LoadState();
            return state.ShowUiOnStartup;
        }

        public void SetShowUiOnStartup(bool value)
        {
            AppState state = LoadState();
            state.ShowUiOnStartup = value;
            SaveState(state);
        }

        public bool GetStartMinimized()
        {
            AppState state = LoadState();
            return state.StartMinimized;
        }

        public void SetStartMinimized(bool value)
        {
            AppState state = LoadState();
            state.StartMinimized = value;
            SaveState(state);
        }
    }
}
