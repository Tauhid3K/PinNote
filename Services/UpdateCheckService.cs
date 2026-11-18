using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PinNote.Services
{
    public class UpdateCheckService : IUpdateCheckService
    {
        public const string DefaultGitHubRepo = "Tauhid3K/PinNote";
        private static readonly HttpClient HttpClient = new();

        public string Repository { get; set; } = DefaultGitHubRepo;
        public Version? ForcedCurrentVersion { get; set; }
        public bool IncludePrereleases { get; set; } = false;
        public Func<string, Task<string>>? CustomHttpHandler { get; set; }

        public Version GetCurrentVersion()
        {
            if (ForcedCurrentVersion != null) return ForcedCurrentVersion;
            var asmVer = Assembly.GetExecutingAssembly().GetName().Version;
            if (asmVer != null && (asmVer.Major > 0 || asmVer.Minor > 0 || asmVer.Build > 0))
            {
                return asmVer;
            }
            return new Version(1, 1, 0);
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var info = new UpdateInfo();
            Version current = GetCurrentVersion();
            info.CurrentVersion = FormatVersion(current);

            try
            {
                string jsonString = string.Empty;

                if (CustomHttpHandler != null)
                {
                    jsonString = await CustomHttpHandler($"https://api.github.com/repos/{Repository}/releases");
                }
                else
                {
                    // Try fetching releases list (which includes releases, pre-releases, and tags)
                    string url = $"https://api.github.com/repos/{Repository}/releases";
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.UserAgent.ParseAdd("PinNote-DesktopApp");
                        request.Headers.Accept.ParseAdd("application/vnd.github+json");

                        using var response = await HttpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            jsonString = await response.Content.ReadAsStringAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateCheckService /releases error: {ex.Message}");
                    }

                    // Fallback to /releases/latest if releases array was not retrieved
                    if (string.IsNullOrWhiteSpace(jsonString))
                    {
                        try
                        {
                            string fallbackUrl = $"https://api.github.com/repos/{Repository}/releases/latest";
                            using var req = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
                            req.Headers.UserAgent.ParseAdd("PinNote-DesktopApp");
                            req.Headers.Accept.ParseAdd("application/vnd.github+json");

                            using var res = await HttpClient.SendAsync(req);
                            if (res.IsSuccessStatusCode)
                            {
                                jsonString = await res.Content.ReadAsStringAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UpdateCheckService /releases/latest fallback error: {ex.Message}");
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return info;
                }

                string trimmed = jsonString.Trim();
                Version? remoteVer = null;
                string? htmlUrl = null;
                string? body = null;

                if (trimmed.StartsWith("["))
                {
                    var array = JArray.Parse(trimmed);
                    foreach (var item in array)
                    {
                        bool isPrerelease = item["prerelease"]?.Value<bool>() ?? false;
                        bool isDraft = item["draft"]?.Value<bool>() ?? false;
                        if (isDraft) continue;
                        if (isPrerelease && !IncludePrereleases) continue;

                        string? tag = item["tag_name"]?.ToString();
                        string? name = item["name"]?.ToString();

                        if (!IncludePrereleases && (IsPrereleaseIdentifier(tag) || IsPrereleaseIdentifier(name)))
                        {
                            continue;
                        }

                        var v = ParseVersion(tag) ?? ParseVersion(name);
                        if (v != null && (remoteVer == null || v > remoteVer))
                        {
                            remoteVer = v;
                            htmlUrl = item["html_url"]?.ToString();
                            body = item["body"]?.ToString();
                        }
                    }
                }
                else if (trimmed.StartsWith("{"))
                {
                    var json = JObject.Parse(trimmed);
                    bool isPrerelease = json["prerelease"]?.Value<bool>() ?? false;
                    bool isDraft = json["draft"]?.Value<bool>() ?? false;
                    if (isDraft || (isPrerelease && !IncludePrereleases))
                    {
                        return info;
                    }

                    string? tag = json["tag_name"]?.ToString();
                    string? name = json["name"]?.ToString();

                    if (!IncludePrereleases && (IsPrereleaseIdentifier(tag) || IsPrereleaseIdentifier(name)))
                    {
                        return info;
                    }

                    remoteVer = ParseVersion(tag) ?? ParseVersion(name);
                    htmlUrl = json["html_url"]?.ToString();
                    body = json["body"]?.ToString();
                }

                if (remoteVer != null)
                {
                    info.LatestVersion = FormatVersion(remoteVer);
                    info.ReleaseUrl = htmlUrl ?? $"https://github.com/{Repository}/releases";
                    info.ReleaseNotes = body ?? string.Empty;

                    if (remoteVer > current)
                    {
                        info.IsUpdateAvailable = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCheckService error: {ex.Message}");
            }

            return info;
        }

        public static Version? ParseVersion(string? versionStr)
        {
            if (string.IsNullOrWhiteSpace(versionStr)) return null;

            versionStr = versionStr.Trim();
            if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionStr = versionStr.Substring(1).Trim();
            }

            int dash = versionStr.IndexOf('-');
            if (dash > 0)
            {
                versionStr = versionStr.Substring(0, dash);
            }

            if (Version.TryParse(versionStr, out var v))
            {
                return v;
            }

            string[] parts = versionStr.Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out int major))
            {
                return new Version(major, 0);
            }

            return null;
        }

        public static bool IsPrereleaseIdentifier(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Contains("-beta", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-alpha", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-rc", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-preview", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-dev", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("-nightly", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatVersion(Version v)
        {
            if (v.Build > 0 || v.Revision > 0)
            {
                return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
            }
            return $"{v.Major}.{v.Minor}.0";
        }

        public static void OpenReleaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        Process.Start("xdg-open", url);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to open URL with xdg-open: {ex.Message}");
                    }
                }
            }
        }
    }
}
