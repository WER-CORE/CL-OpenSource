using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;

namespace CL.Core.Services
{
    public class UpdateService
    {
        private readonly string _tempZipPath = Path.Combine(Path.GetTempPath(), "launcher_update.zip");
        private readonly ITaskProgressService _progressService;
        private readonly IDispatcherService _dispatcherService;

        public UpdateService()
        {
            _progressService = ServiceLocator.Current.GetService<ITaskProgressService>();
            _dispatcherService = ServiceLocator.Current.GetService<IDispatcherService>();
        }

        public async Task<UpdateInfo> CheckForUpdateAsync(string updateUrl)
        {
            try
            {
                string json = await WebHelper.Client.GetStringAsync(updateUrl);
                return JsonSerializer.Deserialize<UpdateInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<UpdateInfo> CheckForUpdateFromGitHubAsync(string repoOwnerAndName, bool isPreview)
        {
            try
            {
                string url = $"https://api.github.com/repos/{repoOwnerAndName}/releases";
                string json = await WebHelper.Client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    bool isPre = rel.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
                    if (!isPreview && isPre) continue;

                    string tagName = rel.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(tagName)) continue;

                    var updateInfo = new UpdateInfo
                    {
                        Version = tagName.TrimStart('v', 'V')
                    };

                    if (rel.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                            string downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? "" : "";

                            if (name.EndsWith("-x64.AppImage", StringComparison.OrdinalIgnoreCase))
                                updateInfo.UrlLinuxX64 = downloadUrl;
                            else if (name.EndsWith("-arm64.AppImage", StringComparison.OrdinalIgnoreCase))
                                updateInfo.UrlLinuxArm64 = downloadUrl;
                            else if (name.Contains("Windows") || name.EndsWith("win-x64.zip", StringComparison.OrdinalIgnoreCase))
                                updateInfo.UrlDefault = downloadUrl;
                            else if (name.Contains("osx-arm64") || name.Contains("mac-arm64"))
                                updateInfo.UrlOsxArm64 = downloadUrl;
                            else if (name.Contains("osx-x64") || name.Contains("mac-x64"))
                                updateInfo.UrlOsxX64 = downloadUrl;
                        }
                    }

                    if (string.IsNullOrEmpty(updateInfo.UrlDefault) && rel.TryGetProperty("html_url", out var htmlProp))
                    {
                        updateInfo.UrlDefault = htmlProp.GetString() ?? "";
                    }

                    return updateInfo;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public bool IsUpdateAvailable(string newVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(newVersion) || string.IsNullOrEmpty(currentVersion)) return false;

            string cleanNew = newVersion.TrimStart('v', 'V').TrimEnd('r', 'R').Trim();
            string cleanCurrent = currentVersion.TrimStart('v', 'V').TrimEnd('r', 'R').Trim();

            if (TryComparePreviewVersions(cleanNew, cleanCurrent, out int cmp))
            {
                return cmp > 0;
            }

            bool v1Success = Version.TryParse(cleanNew, out Version vRemote);
            bool v2Success = Version.TryParse(cleanCurrent, out Version vLocal);

            return v1Success && v2Success ? vRemote > vLocal : !string.Equals(cleanNew, cleanCurrent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryComparePreviewVersions(string v1, string v2, out int result)
        {
            result = 0;
            try
            {
                var p1 = ParseVersionWithPreview(v1);
                var p2 = ParseVersionWithPreview(v2);

                for (int i = 0; i < Math.Max(p1.main.Length, p2.main.Length); i++)
                {
                    int num1 = i < p1.main.Length ? p1.main[i] : 0;
                    int num2 = i < p2.main.Length ? p2.main[i] : 0;
                    if (num1 != num2)
                    {
                        result = num1.CompareTo(num2);
                        return true;
                    }
                }

                result = p1.preview.CompareTo(p2.preview);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static (int[] main, double preview) ParseVersionWithPreview(string v)
        {
            int idx = v.IndexOf("-preview", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string mainStr = v.Substring(0, idx);
                string prevStr = v.Substring(idx + 8);
                string[] parts = mainStr.Split('.');
                int[] nums = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out nums[i]);
                double.TryParse(prevStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double prevNum);
                return (nums, prevNum);
            }
            else
            {
                string[] parts = v.Split('.');
                int[] nums = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out nums[i]);
                return (nums, 999999.0);
            }
        }

        public string GetCorrectDownloadUrl(UpdateInfo info)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X86 && !string.IsNullOrEmpty(info.UrlX86))
                    return info.UrlX86;
                return info.UrlDefault;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64 && !string.IsNullOrEmpty(info.UrlLinuxArm64))
                    return info.UrlLinuxArm64;
                return info.UrlLinuxX64;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64 && !string.IsNullOrEmpty(info.UrlOsxArm64))
                    return info.UrlOsxArm64;
                return info.UrlOsxX64;
            }

            return info.UrlDefault;
        }

        public string GetDownloadedFolderName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.OSArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
            }
            return "win-x64";
        }

        public async Task DownloadUpdateAsync(string url, IProgress<(double percent, string sizeText)> progress = null)
        {
            using var response = await WebHelper.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var fileStream = new FileStream(_tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var contentStream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            long lastReportTime = 0;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                long now = Environment.TickCount64;
                if (totalBytes > 0 && (now - lastReportTime > 50 || totalRead == totalBytes))
                {
                    lastReportTime = now;
                    double percent = (double)totalRead / totalBytes * 100;
                    string message = $"{totalRead / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB";
                    progress?.Report((percent, message));
                }
            }
        }

        public void ExtractUpdate(string destination)
        {
            if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);

            if (File.Exists(_tempZipPath))
            {
                ZipFile.ExtractToDirectory(_tempZipPath, destination, true);
                try { File.Delete(_tempZipPath); } catch { }
            }
        }

        public void MigrateUserData(string sourceBasePath, string destBasePath, string downloadedFolderName)
        {
            string targetDataRoot = Path.Combine(destBasePath, downloadedFolderName);
            if (!Directory.Exists(targetDataRoot))
            {
                targetDataRoot = destBasePath;
            }

            string sourceDataFolder = Path.Combine(sourceBasePath, "Data");
            string destDataFolder = Path.Combine(targetDataRoot, "Data");

            if (Directory.Exists(sourceDataFolder))
            {
                CopyDirectorySmart(sourceDataFolder, destDataFolder);
            }

            try
            {
                string newUserSavesPath = Path.Combine(destDataFolder, "UserSaves.json");
                if (!Directory.Exists(destDataFolder)) Directory.CreateDirectory(destDataFolder);
                string jsonSettings = JsonSerializer.Serialize(SettingsManager.Default, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(newUserSavesPath, jsonSettings);
            }
            catch { }

            string[] configFiles = Directory.GetFiles(sourceBasePath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in configFiles)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".runtimeconfig.json") || fileName.EndsWith(".deps.json")) continue;
                string destFile = Path.Combine(targetDataRoot, fileName);
                try { File.Copy(file, destFile, true); } catch { }
            }
        }

        private void CopyDirectorySmart(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    bool overwrite = file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
                    file.CopyTo(targetFilePath, overwrite);
                }
                catch (IOException) { }
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                try
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectorySmart(subDir.FullName, newDestinationDir);
                }
                catch { }
            }
        }
    }
}
