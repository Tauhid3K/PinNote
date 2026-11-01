using System;
using System.IO;
using System.Threading.Tasks;
using PinNote.Models;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class QuickNoteAndSingleInstanceTests : IDisposable
    {
        private readonly string _testIdentifier;
        private readonly string _testDir;

        public QuickNoteAndSingleInstanceTests()
        {
            _testIdentifier = $"test_{Guid.NewGuid():N}";
            _testDir = Path.Combine(Path.GetTempPath(), $"pinnote_quicknote_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void SingleInstanceService_FirstInstance_ReportsTrue()
        {
            using var instance1 = new SingleInstanceService(_testIdentifier);
            Assert.True(instance1.IsFirstInstance);

            using var instance2 = new SingleInstanceService(_testIdentifier);
            Assert.False(instance2.IsFirstInstance);
        }

        [Fact]
        public void MainViewModel_HandleExternalCommand_CreatesNoteOnNewNoteCommand()
        {
            var storage = new StorageService(_testDir);
            var mainVm = new MainViewModel(customStorageService: storage);
            int initialCount = mainVm.Notes.Count;

            mainVm.HandleExternalCommand("NEW_NOTE");

            Assert.Equal(initialCount + 1, mainVm.Notes.Count);
        }

        [Fact]
        public async Task SingleInstanceService_ServerAndClient_TransfersCommands()
        {
            using var server = new SingleInstanceService(_testIdentifier);
            Assert.True(server.IsFirstInstance);

            string receivedCommand = string.Empty;
            var tcs = new TaskCompletionSource<string>();

            server.CommandReceived += cmd =>
            {
                receivedCommand = cmd;
                tcs.TrySetResult(cmd);
            };

            server.StartServer();
            await Task.Delay(200); // Give server time to bind

            bool sent = await SingleInstanceService.SendCommandToRunningInstanceAsync("NEW_NOTE", _testIdentifier);
            Assert.True(sent);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(3000));
            Assert.Equal(tcs.Task, completedTask);
            Assert.Equal("NEW_NOTE", receivedCommand);
        }

        [Fact]
        public void LinuxGlobalShortcutService_Dispose_CanBeCalledMultipleTimesWithoutThrowing()
        {
            var service = new LinuxGlobalShortcutService();
            // Should be completely safe to call Unregister and Dispose repeatedly
            service.Unregister();
            service.Dispose();
            service.Dispose();
        }

        [Fact]
        public void LinuxGlobalShortcutService_WaylandDetection_MatchesEnvironment()
        {
            var service = new LinuxGlobalShortcutService();
            bool expectedWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                                   string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

            Assert.Equal(expectedWayland, service.IsWayland);
        }
    }
}
