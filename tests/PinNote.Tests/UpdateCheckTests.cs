using System;
using System.IO;
using System.Threading.Tasks;
using PinNote.Services;
using PinNote.ViewModels;
using Xunit;

namespace PinNote.Tests
{
    public class UpdateCheckTests
    {
        [Theory]
        [InlineData("v1.0.5", 1, 0, 5)]
        [InlineData("V2.1.0", 2, 1, 0)]
        [InlineData("1.0.4.1", 1, 0, 4)]
        [InlineData("v3.0-rc1", 3, 0, 0)]
        public void ParseVersion_ValidStrings_ParsesCorrectly(string input, int major, int minor, int build)
        {
            var parsed = UpdateCheckService.ParseVersion(input);
            Assert.NotNull(parsed);
            Assert.Equal(major, parsed.Major);
            Assert.Equal(minor, parsed.Minor);
            if (build > 0) Assert.Equal(build, parsed.Build);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("not-a-version")]
        public void ParseVersion_InvalidStrings_ReturnsNull(string? input)
        {
            var parsed = UpdateCheckService.ParseVersion(input);
            Assert.Null(parsed);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_NewerVersionAvailable_ReportsUpdate()
        {
            var service = new UpdateCheckService
            {
                ForcedCurrentVersion = new Version(1, 0, 4),
                CustomHttpHandler = _ => Task.FromResult(@"{
                    ""tag_name"": ""v1.0.5"",
                    ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5"",
                    ""body"": ""New features added.""
                }")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.0.5", result.LatestVersion);
            Assert.Equal("1.0.4", result.CurrentVersion);
            Assert.Equal("https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5", result.ReleaseUrl);
            Assert.Equal("New features added.", result.ReleaseNotes);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_SameOrOlderVersion_ReportsNoUpdate()
        {
            var service = new UpdateCheckService
            {
                ForcedCurrentVersion = new Version(1, 0, 4),
                CustomHttpHandler = _ => Task.FromResult(@"{
                    ""tag_name"": ""v1.0.4"",
                    ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.4"",
                    ""body"": ""Same version.""
                }")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.False(result.IsUpdateAvailable);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_NetworkError_HandlesGracefully()
        {
            var service = new UpdateCheckService
            {
                CustomHttpHandler = _ => throw new IOException("Network unreachable")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.False(result.IsUpdateAvailable);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_Prerelease_IgnoredWhenIncludePrereleasesFalse()
        {
            var service = new UpdateCheckService
            {
                ForcedCurrentVersion = new Version(1, 0, 4),
                IncludePrereleases = false,
                CustomHttpHandler = _ => Task.FromResult(@"[
                    {
                        ""tag_name"": ""v2.0.0-beta1"",
                        ""prerelease"": true,
                        ""draft"": false,
                        ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v2.0.0-beta1"",
                        ""body"": ""Beta preview.""
                    },
                    {
                        ""tag_name"": ""v1.0.5"",
                        ""prerelease"": false,
                        ""draft"": false,
                        ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5"",
                        ""body"": ""Stable release.""
                    }
                ]")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.0.5", result.LatestVersion);
            Assert.Equal("https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5", result.ReleaseUrl);
            Assert.Equal("Stable release.", result.ReleaseNotes);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_Prerelease_AllowedWhenIncludePrereleasesTrue()
        {
            var service = new UpdateCheckService
            {
                ForcedCurrentVersion = new Version(1, 0, 4),
                IncludePrereleases = true,
                CustomHttpHandler = _ => Task.FromResult(@"[
                    {
                        ""tag_name"": ""v2.0.0-beta1"",
                        ""prerelease"": true,
                        ""draft"": false,
                        ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v2.0.0-beta1"",
                        ""body"": ""Beta preview.""
                    },
                    {
                        ""tag_name"": ""v1.0.5"",
                        ""prerelease"": false,
                        ""draft"": false,
                        ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5"",
                        ""body"": ""Stable release.""
                    }
                ]")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("2.0.0", result.LatestVersion);
            Assert.Equal("https://github.com/Tauhid3K/PinNote/releases/tag/v2.0.0-beta1", result.ReleaseUrl);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_SinglePrerelease_IgnoredByDefault()
        {
            var service = new UpdateCheckService
            {
                ForcedCurrentVersion = new Version(1, 0, 4),
                IncludePrereleases = false,
                CustomHttpHandler = _ => Task.FromResult(@"{
                    ""tag_name"": ""v1.1.0-rc1"",
                    ""prerelease"": true,
                    ""draft"": false,
                    ""html_url"": ""https://github.com/Tauhid3K/PinNote/releases/tag/v1.1.0-rc1"",
                    ""body"": ""Release candidate.""
                }")
            };

            var result = await service.CheckForUpdatesAsync();

            Assert.False(result.IsUpdateAvailable);
        }

        [Fact]
        public async Task MainViewModel_WithAvailableUpdate_ExposesUpdateProperties()
        {
            var mockService = new MockUpdateCheckService(new UpdateInfo
            {
                IsUpdateAvailable = true,
                LatestVersion = "1.0.5",
                CurrentVersion = "1.0.4",
                ReleaseUrl = "https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5"
            });

            var tempDir = Path.Combine(Path.GetTempPath(), "PinNote_UpdateTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                var storage = new StorageService(tempDir);
                var vm = new MainViewModel(null, storage, mockService);

                await vm.CheckForUpdatesAsync();

                Assert.True(vm.IsUpdateAvailable);
                Assert.Equal("1.0.5", vm.LatestVersion);
                Assert.Equal("Update v1.0.5", vm.UpdateBadgeText);
                Assert.Equal("https://github.com/Tauhid3K/PinNote/releases/tag/v1.0.5", vm.UpdateUrl);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        private class MockUpdateCheckService : IUpdateCheckService
        {
            private readonly UpdateInfo _info;

            public MockUpdateCheckService(UpdateInfo info)
            {
                _info = info;
            }

            public Version GetCurrentVersion()
            {
                return UpdateCheckService.ParseVersion(_info.CurrentVersion) ?? new Version(1, 1, 0);
            }

            public Task<UpdateInfo> CheckForUpdatesAsync()
            {
                return Task.FromResult(_info);
            }
        }
    }
}
