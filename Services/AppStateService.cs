using System;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;

namespace PinNote.Services
{
    public class AppStateService
    {
        private static readonly object _stateLock = new();
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
            lock (_stateLock)
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppStateService.LoadState error: {ex}");
                    return new AppState();
                }
            }
        }

        private void SaveState(AppState state)
        {
            lock (_stateLock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                    WriteAtomically(_filePath, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppStateService.SaveState error: {ex}");
                    // Ignore persistence errors and keep app startup resilient.
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
                        System.Threading.Thread.Sleep(30);
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

        private class AppState
        {
            public bool HasShownWelcomeNotification { get; set; }
            public bool HasShownFirstRunUi { get; set; }
            public bool ShowUiOnStartup { get; set; } = true;
            public bool StartMinimized { get; set; } = false;
            public bool ShowTrayIcon { get; set; } = true;
            public bool ShowInTaskbar { get; set; } = false;

            // Theme: "System" = follow OS, "Light" or "Dark"
            public string ThemeMode { get; set; } = "System";

            // Custom colors stored as integer ARGB values (matching System.Drawing color format used by ColorDialog.CustomColors)
            public int[] CustomColors { get; set; } = Array.Empty<int>();

            // Dashboard window state
            public double? DashboardX { get; set; }
            public double? DashboardY { get; set; }
            public double DashboardWidth { get; set; } = 800;
            public double DashboardHeight { get; set; } = 500;

            // Sort preference (matches NoteSortMode enum int values)
            public int SortMode { get; set; } = 0;

            // Wayland shortcut notice — shown once so we don't spam the user on every startup
            public bool HasShownWaylandShortcutNotice { get; set; } = false;
        }

        public (double? X, double? Y, double Width, double Height) GetDashboardState()
        {
            AppState state = LoadState();
            return (state.DashboardX, state.DashboardY, state.DashboardWidth, state.DashboardHeight);
        }

        public void SetDashboardState(double? x, double? y, double width, double height)
        {
            AppState state = LoadState();
            state.DashboardX = x;
            state.DashboardY = y;
            state.DashboardWidth = width;
            state.DashboardHeight = height;
            SaveState(state);
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

        public bool GetShowTrayIcon()
        {
            AppState state = LoadState();
            return state.ShowTrayIcon;
        }

        public void SetShowTrayIcon(bool value)
        {
            AppState state = LoadState();
            state.ShowTrayIcon = value;
            SaveState(state);
        }

        public bool GetShowInTaskbar()
        {
            AppState state = LoadState();
            return state.ShowInTaskbar;
        }

        public void SetShowInTaskbar(bool value)
        {
            AppState state = LoadState();
            state.ShowInTaskbar = value;
            SaveState(state);
        }

        public string GetThemeMode()
        {
            AppState state = LoadState();
            return state.ThemeMode ?? "System";
        }

        public int[] GetCustomColors()
        {
            AppState state = LoadState();
            return state.CustomColors ?? Array.Empty<int>();
        }

        public void SetCustomColors(int[] colors)
        {
            AppState state = LoadState();
            state.CustomColors = colors ?? Array.Empty<int>();
            SaveState(state);
        }

        public void SetThemeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "System";
            }

            AppState state = LoadState();
            state.ThemeMode = mode;
            SaveState(state);
        }

        public int GetSortMode()
        {
            AppState state = LoadState();
            return state.SortMode;
        }

        public void SetSortMode(int sortMode)
        {
            AppState state = LoadState();
            state.SortMode = sortMode;
            SaveState(state);
        }

        /// <summary>
        /// Returns true the first time this is called on a Wayland session, so the app can show
        /// a one-time notification about manual keyboard-shortcut binding.
        /// </summary>
        public bool ShouldShowWaylandShortcutNotice()
        {
            AppState state = LoadState();
            if (state.HasShownWaylandShortcutNotice)
            {
                return false;
            }

            state.HasShownWaylandShortcutNotice = true;
            SaveState(state);
            return true;
        }
    }
}
