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

        public bool IsUpdateAvailable(string newVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(newVersion) || string.IsNullOrEmpty(currentVersion)) return false;

            string cleanNew = newVersion.Replace("v", "").Replace("R", "").Trim();
            string cleanCurrent = currentVersion.Replace("v", "").Replace("R", "").Trim();

            bool v1Success = Version.TryParse(cleanNew, out Version vRemote);
            bool v2Success = Version.TryParse(cleanCurrent, out Version vLocal);

            return v1Success && v2Success ? vRemote > vLocal : !string.Equals(cleanNew, cleanCurrent, StringComparison.OrdinalIgnoreCase);
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
