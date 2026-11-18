using System;
using System.Threading.Tasks;

namespace PinNote.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public interface IUpdateCheckService
    {
        Version GetCurrentVersion();
        Task<UpdateInfo> CheckForUpdatesAsync();
    }
}
