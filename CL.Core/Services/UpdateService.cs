using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
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
        private readonly ITaskProgressService? _progressService;
        private readonly IDispatcherService? _dispatcherService;
        private readonly HttpClient? _httpClient;

        public UpdateService() : this(null)
        {
        }

        public UpdateService(HttpClient? httpClient)
        {
            _httpClient = httpClient;
            _progressService = TryGetOptionalService<ITaskProgressService>();
            _dispatcherService = TryGetOptionalService<IDispatcherService>();
        }

        private static T? TryGetOptionalService<T>() where T : class
        {
            try
            {
                return ServiceLocator.Current?.GetService<T>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private HttpClient Client => _httpClient ?? WebHelper.Client;

        public static UpdateChannel ResolveChannel(string? channelSetting)
        {
            if (string.Equals(channelSetting, "Preview", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channelSetting, "Testing", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateChannel.Testing;
            }
            return UpdateChannel.Stable;
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync(string updateUrl, CancellationToken cancellationToken = default)
        {
            if (!IsValidHttpsUrl(updateUrl)) return null;
            try
            {
                using var response = await Client.GetAsync(updateUrl, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json)) return null;

                return JsonSerializer.Deserialize<UpdateInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<UpdateInfo?> CheckForUpdateFromGitHubAsync(
            string repoOwnerAndName,
            bool isPreview,
            string? currentVersion = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repoOwnerAndName)) return null;

            try
            {
                string url = $"https://api.github.com/repos/{repoOwnerAndName}/releases";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                request.Headers.TryAddWithoutValidation("User-Agent", "CLegendary-Launcher-Updater");

                using var response = await Client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var releases = ParseGitHubReleases(json);
                var channel = isPreview ? UpdateChannel.Testing : UpdateChannel.Stable;
                string localVer = string.IsNullOrWhiteSpace(currentVersion) ? GetCurrentLauncherVersion() : currentVersion;

                return SelectBestRelease(releases, channel, localVer);
            }
            catch
            {
                return null;
            }
        }

        public static List<GitHubReleaseDto> ParseGitHubReleases(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<GitHubReleaseDto>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<GitHubReleaseDto>();

                return JsonSerializer.Deserialize<List<GitHubReleaseDto>>(json) ?? new List<GitHubReleaseDto>();
            }
            catch
            {
                return new List<GitHubReleaseDto>();
            }
        }

        public UpdateInfo? SelectBestRelease(
            IEnumerable<GitHubReleaseDto>? releases,
            UpdateChannel channel,
            string currentVersion,
            OSPlatform? platformOverride = null,
            Architecture? archOverride = null)
        {
            if (releases == null || string.IsNullOrWhiteSpace(currentVersion)) return null;

            bool requirePrerelease = channel == UpdateChannel.Testing;
            GitHubReleaseDto? bestRelease = null;
            UpdateInfo? bestInfo = null;

            foreach (var rel in releases)
            {
                if (rel == null || rel.Draft) continue;
                if (rel.Prerelease != requirePrerelease) continue;
                if (string.IsNullOrWhiteSpace(rel.TagName)) continue;

                string candidateVersion = NormalizeVersionTag(rel.TagName);
                if (string.IsNullOrWhiteSpace(candidateVersion)) continue;

                if (!IsUpdateAvailable(candidateVersion, currentVersion)) continue;

                var candidateInfo = BuildUpdateInfo(candidateVersion, rel.Assets);
                string platformUrl = GetCorrectDownloadUrl(candidateInfo, platformOverride, archOverride);
                if (!IsValidHttpsUrl(platformUrl)) continue;

                if (bestRelease == null || CompareVersions(candidateVersion, bestInfo!.Version) > 0)
                {
                    bestRelease = rel;
                    bestInfo = candidateInfo;
                }
            }

            return bestInfo;
        }

        private static UpdateInfo BuildUpdateInfo(string version, List<GitHubAssetDto>? assets)
        {
            var updateInfo = new UpdateInfo { Version = version };
            if (assets == null) return updateInfo;

            foreach (var asset in assets)
            {
                if (asset == null) continue;
                string name = asset.Name ?? string.Empty;
                string downloadUrl = asset.BrowserDownloadUrl ?? string.Empty;
                if (!IsValidHttpsUrl(downloadUrl)) continue;

                if (name.EndsWith("-x64.AppImage", StringComparison.OrdinalIgnoreCase))
                    updateInfo.UrlLinuxX64 = downloadUrl;
                else if (name.EndsWith("-arm64.AppImage", StringComparison.OrdinalIgnoreCase))
                    updateInfo.UrlLinuxArm64 = downloadUrl;
                else if (name.Contains("Windows", StringComparison.OrdinalIgnoreCase) &&
                         (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                          name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    updateInfo.UrlDefault = SelectPreferredAssetUrl(updateInfo.UrlDefault, name, downloadUrl);
                }
                else if (name.EndsWith("win-x64.zip", StringComparison.OrdinalIgnoreCase))
                    updateInfo.UrlDefault = SelectPreferredAssetUrl(updateInfo.UrlDefault, name, downloadUrl);
                else if ((name.Contains("osx-arm64", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("mac-arm64", StringComparison.OrdinalIgnoreCase)) &&
                         name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    updateInfo.UrlOsxArm64 = SelectPreferredAssetUrl(updateInfo.UrlOsxArm64, name, downloadUrl);
                }
                else if ((name.Contains("osx-x64", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("mac-x64", StringComparison.OrdinalIgnoreCase)) &&
                         name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    updateInfo.UrlOsxX64 = SelectPreferredAssetUrl(updateInfo.UrlOsxX64, name, downloadUrl);
                }
            }

            return updateInfo;
        }

        private static string SelectPreferredAssetUrl(string? currentUrl, string candidateName, string candidateUrl)
        {
            if (string.IsNullOrEmpty(currentUrl) ||
                candidateName.EndsWith("Portable.zip", StringComparison.OrdinalIgnoreCase))
            {
                return candidateUrl;
            }

            return currentUrl;
        }

        public static bool IsValidHttpsUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeVersionTag(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return string.Empty;
            string clean = version.Trim();
            if (clean.StartsWith("Rel_", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(4);
            clean = clean.TrimStart('v', 'V').Trim();
            if (clean.EndsWith("R", StringComparison.OrdinalIgnoreCase) &&
                clean.Length > 1 &&
                char.IsDigit(clean[clean.Length - 2]))
            {
                clean = clean.Substring(0, clean.Length - 1);
            }
            return clean;
        }

        public static string GetCurrentLauncherVersion(Assembly? assembly = null)
        {
            assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            try
            {
                var infoVerAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (!string.IsNullOrWhiteSpace(infoVerAttr?.InformationalVersion))
                {
                    string infoVer = infoVerAttr.InformationalVersion.Split('+')[0].Trim().TrimStart('v', 'V');
                    if (!string.IsNullOrEmpty(infoVer)) return infoVer;
                }
            }
            catch { }

            var ver = assembly.GetName().Version;
            return ver != null ? ver.ToString() : "1.1.0";
        }

        public bool IsUpdateAvailable(string newVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(newVersion) || string.IsNullOrWhiteSpace(currentVersion)) return false;
            return CompareVersions(newVersion, currentVersion) > 0;
        }

        public static int CompareVersions(string versionA, string versionB)
        {
            string cleanA = NormalizeVersionTag(versionA);
            string cleanB = NormalizeVersionTag(versionB);

            if (TryCompareStructuredVersions(cleanA, cleanB, out int cmp))
            {
                return cmp;
            }

            if (Version.TryParse(cleanA, out var vA) && Version.TryParse(cleanB, out var vB))
            {
                return vA.CompareTo(vB);
            }

            return string.Compare(cleanA, cleanB, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCompareStructuredVersions(string v1, string v2, out int result)
        {
            result = 0;
            try
            {
                var p1 = ParseVersionWithPrerelease(v1);
                var p2 = ParseVersionWithPrerelease(v2);

                if (p1.main.Length == 0 || p2.main.Length == 0) return false;

                int maxMain = Math.Max(p1.main.Length, p2.main.Length);
                for (int i = 0; i < maxMain; i++)
                {
                    int num1 = i < p1.main.Length ? p1.main[i] : 0;
                    int num2 = i < p2.main.Length ? p2.main[i] : 0;
                    if (num1 != num2)
                    {
                        result = num1.CompareTo(num2);
                        return true;
                    }
                }

                if (p1.isRelease != p2.isRelease)
                {
                    result = p1.isRelease ? 1 : -1;
                    return true;
                }

                if (p1.isRelease)
                {
                    result = 0;
                    return true;
                }

                int maxTokens = Math.Max(p1.prereleaseTokens.Length, p2.prereleaseTokens.Length);
                for (int i = 0; i < maxTokens; i++)
                {
                    if (i >= p1.prereleaseTokens.Length)
                    {
                        result = -1;
                        return true;
                    }
                    if (i >= p2.prereleaseTokens.Length)
                    {
                        result = 1;
                        return true;
                    }

                    var t1 = p1.prereleaseTokens[i];
                    var t2 = p2.prereleaseTokens[i];
                    int cmp = ComparePrereleaseTokens(t1, t2);
                    if (cmp != 0)
                    {
                        result = cmp;
                        return true;
                    }
                }

                result = 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ComparePrereleaseTokens(string a, string b)
        {
            bool aIsNum = int.TryParse(a, out int numA);
            bool bIsNum = int.TryParse(b, out int numB);

            if (aIsNum && bIsNum) return numA.CompareTo(numB);
            if (aIsNum != bIsNum) return aIsNum ? -1 : 1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static (int[] main, bool isRelease, string[] prereleaseTokens) ParseVersionWithPrerelease(string v)
        {
            int dashIdx = v.IndexOf('-');
            if (dashIdx >= 0)
            {
                string mainStr = v.Substring(0, dashIdx);
                string preStr = v.Substring(dashIdx + 1);
                return (ParseIntArray(mainStr), false, TokenizePrerelease(preStr));
            }

            return (ParseIntArray(v), true, Array.Empty<string>());
        }

        private static string[] TokenizePrerelease(string preStr)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(preStr)) return Array.Empty<string>();

            int i = 0;
            while (i < preStr.Length)
            {
                char ch = preStr[i];
                if (ch == '.' || ch == '-' || ch == '_' || char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }

                bool digit = char.IsDigit(ch);
                int start = i;
                while (i < preStr.Length && preStr[i] != '.' && preStr[i] != '-' && preStr[i] != '_' &&
                       !char.IsWhiteSpace(preStr[i]) && char.IsDigit(preStr[i]) == digit)
                {
                    i++;
                }

                tokens.Add(preStr.Substring(start, i - start));
            }

            return tokens.ToArray();
        }

        private static int[] ParseIntArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
            string[] raw = s.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();
            foreach (var r in raw)
            {
                if (int.TryParse(r, out int n)) list.Add(n);
            }
            return list.ToArray();
        }

        public string GetCorrectDownloadUrl(UpdateInfo info) =>
            GetCorrectDownloadUrl(info, null, null);

        public string GetCorrectDownloadUrl(UpdateInfo info, OSPlatform? platformOverride, Architecture? archOverride)
        {
            Architecture arch = archOverride ?? RuntimeInformation.OSArchitecture;
            bool isWindows = platformOverride.HasValue
                ? platformOverride.Value == OSPlatform.Windows
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux = platformOverride.HasValue
                ? platformOverride.Value == OSPlatform.Linux
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            bool isOsx = platformOverride.HasValue
                ? platformOverride.Value == OSPlatform.OSX
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            if (isWindows)
            {
                if (arch == Architecture.X86 && !string.IsNullOrEmpty(info.UrlX86))
                    return info.UrlX86;
                return info.UrlDefault;
            }
            if (isLinux)
            {
                if (arch == Architecture.Arm64 && !string.IsNullOrEmpty(info.UrlLinuxArm64))
                    return info.UrlLinuxArm64;
                return info.UrlLinuxX64;
            }
            if (isOsx)
            {
                if (arch == Architecture.Arm64 && !string.IsNullOrEmpty(info.UrlOsxArm64))
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

        public async Task DownloadUpdateAsync(
            string url,
            IProgress<(double percent, string sizeText)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidHttpsUrl(url))
                throw new InvalidOperationException("Недійсний або незахищений URL оновлення (очікується HTTPS).");

            try
            {
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var fileStream = new FileStream(_tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[8192];
                long totalRead = 0;
                int read;
                long lastReportTime = 0;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
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
            catch
            {
                try { if (File.Exists(_tempZipPath)) File.Delete(_tempZipPath); } catch { }
                throw;
            }
        }

        public void ExtractUpdate(string destination)
        {
            if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);

            if (!File.Exists(_tempZipPath))
                throw new FileNotFoundException("Файл оновлення не знайдено.", _tempZipPath);

            try
            {
                using (var archive = ZipFile.OpenRead(_tempZipPath))
                {
                    if (archive.Entries.Count == 0)
                        throw new InvalidDataException("Архів оновлення порожній.");
                }

                ZipFile.ExtractToDirectory(_tempZipPath, destination, true);
            }
            catch
            {
                try { if (File.Exists(_tempZipPath)) File.Delete(_tempZipPath); } catch { }
                throw;
            }

            try { File.Delete(_tempZipPath); } catch { }
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
