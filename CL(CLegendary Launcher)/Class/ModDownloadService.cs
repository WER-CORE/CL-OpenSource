using CL_CLegendary_Launcher_.Windows;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Files;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CL_CLegendary_Launcher_.Class
{
    public class ModSearchResult
    {
        public string ModId { get; set; }
        public string Slug { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string Author { get; set; }
        public string Downloads { get; set; }
        public string UpdatedDate { get; set; }
        public string CreatedDate { get; set; }
        public string Site { get; set; }
        public int CF_FileId { get; set; }
    }
    public class ModVersionInfo
    {
        [JsonProperty("project_id")] public string ModId { get; set; }
        [JsonProperty("id")] public string VersionId { get; set; }
        [JsonProperty("version_number")] public string VersionName { get; set; }
        public string FileName { get; set; }
        public string DownloadUrl { get; set; }
        public string Site { get; set; }
        [JsonProperty("game_versions")] public List<string> GameVersions { get; set; } = new List<string>();
        [JsonProperty("loaders")] public List<string> Loaders { get; set; } = new List<string>();
        [JsonProperty("version_type")] public string VersionType { get; set; }
    }
    public record DownloadProgressInfo(string FileName, int Percent, int FilesCompleted, int TotalFiles);

    public class ModDownloadService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private static readonly ApiClient _cfApiClient = new ApiClient(Secrets.CurseForgeKey);

        private static readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(3);
        private readonly JsonSerializerSettings _modrinthSettings;

        static ModDownloadService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CL-Legendary-Launcher/1.0");
        }

        public ModDownloadService()
        {
            _modrinthSettings = new JsonSerializerSettings();
            _modrinthSettings.Converters.Add(new ModrinthVersionConverter());
        }

        public string GetTargetFolderPath(InstalledModpack pack, byte modType)
        {
            string folderName = modType switch
            {
                1 => "shaderpacks",
                2 => "resourcepacks",
                3 => "saves",
                4 => "datapacks",
                _ => "mods"
            };
            return Path.Combine(pack.Path, folderName);
        }

        public async Task<List<ModSearchResult>> SearchModsAsync(string query, string site, string loader, int modType, int offset = 0)
        {
            if (site == "Modrinth") return await SearchModrinthAsync(query, loader, modType, offset);
            else return await SearchCurseForgeAsync(query, loader, modType, offset);
        }

        public async Task<List<ModVersionInfo>> GetVersionsAsync(ModSearchResult mod)
        {
            if (mod.Site == "Modrinth") return await GetModrinthVersionsAsync(mod.ModId);
            else return await GetCurseForgeVersionsAsync(int.Parse(mod.ModId));
        }

        public async Task DownloadModWithDependenciesAsync(
            ModVersionInfo version,
            int modType,
            string? customDestinationPath = null)
        {
            DowloadProgress progressWindow = null;
            var cts = new CancellationTokenSource();

            Application.Current.Dispatcher.Invoke(() =>
            {
                progressWindow = new DowloadProgress();
                progressWindow.CTS = cts;
                progressWindow.Show();
                progressWindow.DowloadProgressBarVersion(0, version.VersionName);
                progressWindow.DowloadProgressBarFileTask(0, 0, LocalizationManager.GetString("DownloadManager.AnalyzingDependencies", "Аналіз залежностей..."));
            });

            var progressReporter = new Progress<DownloadProgressInfo>(info =>
            {
                if (progressWindow == null) return;

                progressWindow.FileTXTName.Text = info.FileName;
                progressWindow.DowloadProgressBarFile(info.Percent);

                if (info.TotalFiles > 0)
                {
                    int totalPercent = (int)((double)info.FilesCompleted / info.TotalFiles * 100);
                    progressWindow.DowloadProgressBarVersion(totalPercent, version.VersionName);
                    progressWindow.DowloadProgressBarFileTask(info.TotalFiles, info.FilesCompleted, info.FileName);
                }
            });

            try
            {
                string modsFolder = string.IsNullOrEmpty(customDestinationPath)
                    ? Path.Combine(SettingsManager.Default.PathLacunher, modType switch { 1 => "shaderpacks", 2 => "resourcepacks", _ => "mods" })
                    : customDestinationPath;

                Directory.CreateDirectory(modsFolder);

                var filesToDownload = new HashSet<string> { version.DownloadUrl };

                if (modType == 0 && SettingsManager.Default.ModDep)
                {
                    var deps = await GetDependencyUrlsAsync(version);
                    if (deps != null)
                    {
                        foreach (var dep in deps) filesToDownload.Add(dep);
                    }
                }

                int totalFiles = filesToDownload.Count;
                int downloadedCount = 0;

                ((IProgress<DownloadProgressInfo>)progressReporter).Report(new DownloadProgressInfo("Start", 0, 0, totalFiles));

                var tasks = filesToDownload.Select(async url =>
                {
                    await _downloadSemaphore.WaitAsync(cts.Token);
                    try
                    {
                        string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                        string filePath = Path.Combine(modsFolder, fileName);

                        if (!System.IO.File.Exists(filePath))
                        {
                            await DownloadFileHelperAsync(url, filePath, cts.Token, percent =>
                            {
                                ((IProgress<DownloadProgressInfo>)progressReporter).Report(
                                    new DownloadProgressInfo(fileName, percent, downloadedCount, totalFiles));
                            });
                        }

                        Interlocked.Increment(ref downloadedCount);

                        ((IProgress<DownloadProgressInfo>)progressReporter).Report(
                            new DownloadProgressInfo(fileName, 100, downloadedCount, totalFiles));
                    }
                    finally
                    {
                        _downloadSemaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("DownloadManager.ErrorDesc", "Помилка: {0}"), ex.Message),
                    LocalizationManager.GetString("DownloadManager.ErrorTitle", "Помилка завантаження"),
                    MascotEmotion.Sad);
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => progressWindow?.Close());
                cts.Dispose();
            }
        }

        private async Task DownloadFileHelperAsync(string url, string path, CancellationToken token, Action<int> onProgress)
        {
            int maxRetries = 3;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);

            try
            {
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

                        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                            response.StatusCode == (System.Net.HttpStatusCode)429)
                        {
                            if (i == maxRetries - 1) response.EnsureSuccessStatusCode();
                            await Task.Delay(2000 * (i + 1), token);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();

                        long? totalBytes = response.Content.Headers.ContentLength;
                        using var contentStream = await response.Content.ReadAsStreamAsync(token);

                        string tempPath = path + ".tmp";

                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            long totalRead = 0;
                            int bytesRead;
                            long lastReportedBytes = 0;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue)
                                {
                                    if (totalRead - lastReportedBytes > 102400 || totalRead == totalBytes)
                                    {
                                        lastReportedBytes = totalRead;
                                        int percent = (int)((double)totalRead / totalBytes.Value * 100);
                                        onProgress?.Invoke(percent);
                                    }
                                }
                            }
                        }

                        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                        System.IO.File.Move(tempPath, path);
                        return;
                    }
                    catch (HttpRequestException)
                    {
                        if (i == maxRetries - 1) throw;
                        await Task.Delay(1000, token);
                    }
                    catch (Exception)
                    {
                        if (System.IO.File.Exists(path + ".tmp"))
                            System.IO.File.Delete(path + ".tmp");
                        throw;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #region Modrinth Logic
        private async Task<List<ModSearchResult>> SearchModrinthAsync(string query, string loader, int modType, int offset = 0)
        {
            string projectType = modType switch
            {
                1 => "shader",
                2 => "resourcepack",
                3 => null,
                4 => "datapacks",
                _ => "mod"
            };

            var facets = new List<string>();

            if (!string.IsNullOrEmpty(projectType) && modType != 4)
            {
                facets.Add($"[\"project_type:{projectType}\"]");
            }
            else if (modType == 4)
            {
                facets.Add("[\"categories:datapack\"]");
            }

            if (projectType == "mod" && !string.IsNullOrEmpty(loader) && modType != 4)
            {
                string loaderLower = loader.ToLower();
                if (loaderLower == "quilt") facets.Add("[\"categories:quilt\",\"categories:fabric\"]");
                else if (loaderLower == "neoforge") facets.Add("[\"categories:neoforge\",\"categories:forge\"]");
                else facets.Add($"[\"categories:{loaderLower}\"]");
            }

            string facetsJson = "[" + string.Join(",", facets) + "]";
            string index = string.IsNullOrWhiteSpace(query) ? "downloads" : "relevance";
            string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&index={index}&offset={offset}&facets={facetsJson}&limit=10";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var result = JObject.Parse(response);
                var hits = result["hits"] as JArray;

                var list = new List<ModSearchResult>();
                if (hits == null) return list;

                foreach (var mod in hits)
                {
                    list.Add(new ModSearchResult
                    {
                        ModId = mod["project_id"]?.ToString(),
                        Slug = mod["slug"]?.ToString(),
                        Title = mod["title"]?.ToString(),
                        Description = mod["description"]?.ToString(),
                        IconUrl = mod["icon_url"]?.ToString(),
                        Author = mod["author"]?.ToString(),
                        Downloads = mod["downloads"]?.ToString(),
                        UpdatedDate = mod["date_modified"]?.ToString(),
                        Site = "Modrinth"
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modrinth Search Error: {ex.Message}");
                return new List<ModSearchResult>();
            }
        }

        private async Task<List<ModVersionInfo>> GetModrinthVersionsAsync(string modId)
        {
            string url = $"https://api.modrinth.com/v2/project/{modId}/version";
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var versions = JsonConvert.DeserializeObject<List<ModVersionInfo>>(response, _modrinthSettings);

                if (versions == null) return new List<ModVersionInfo>();

                return versions
                    .Where(v => v.VersionType == "release" || v.VersionType == "beta")
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modrinth Versions Error: {ex.Message}");
                return new List<ModVersionInfo>();
            }
        }
        #endregion

        #region CurseForge Logic
        private async Task<List<ModSearchResult>> SearchCurseForgeAsync(string query, string loader, int modType, int offset = 0)
        {
            ModLoaderType? targetLoaderType = null;
            if (modType == 0)
            {
                targetLoaderType = loader switch
                {
                    "Forge" => ModLoaderType.Forge,
                    "Fabric" => ModLoaderType.Fabric,
                    "Quilt" => ModLoaderType.Quilt,
                    "NeoForge" => ModLoaderType.NeoForge,
                    _ => ModLoaderType.Any
                };
            }

            int classId = modType switch
            {
                1 => 6552,
                2 => 12,
                3 => 17,
                4 => 6945,
                _ => 6
            };

            var sortField = string.IsNullOrWhiteSpace(query) ? ModsSearchSortField.Popularity : ModsSearchSortField.Featured;
            string cleanQuery = query?.Trim();

            try
            {
                var response = await _cfApiClient.SearchModsAsync(
                    gameId: 432,
                    classId: classId,
                    searchFilter: cleanQuery,
                    modLoaderType: targetLoaderType,
                    pageSize: 10,
                    sortField: sortField,
                    index: offset
                );

                var list = new List<ModSearchResult>();
                if (response?.Data == null) return list;

                foreach (var mod in response.Data)
                {
                    list.Add(new ModSearchResult
                    {
                        ModId = mod.Id.ToString(),
                        Slug = mod.Slug,
                        Title = mod.Name,
                        Description = mod.Summary,
                        IconUrl = mod.Logo?.Url,
                        Author = mod.Authors?.FirstOrDefault()?.Name,
                        Downloads = mod.DownloadCount.ToString(),
                        UpdatedDate = mod.DateModified.ToString("g"),
                        Site = "CurseForge",
                        CF_FileId = mod.MainFileId
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CurseForge Search Error: {ex.Message}");
                return new List<ModSearchResult>();
            }
        }

        private async Task<List<ModVersionInfo>> GetCurseForgeVersionsAsync(int modId)
        {
            try
            {
                var response = await _cfApiClient.GetModFilesAsync(modId);
                var list = new List<ModVersionInfo>();
                if (response?.Data == null) return list;

                var releaseFiles = response.Data
                    .Where(file => file.ReleaseType == FileReleaseType.Release ||
                                   file.ReleaseType == FileReleaseType.Beta);

                foreach (var file in releaseFiles)
                {
                    list.Add(new ModVersionInfo
                    {
                        ModId = modId.ToString(),
                        VersionId = file.Id.ToString(),
                        VersionName = file.DisplayName,
                        FileName = file.FileName,
                        DownloadUrl = file.DownloadUrl,
                        Site = "CurseForge",
                        GameVersions = file.GameVersions.ToList(),
                        Loaders = file.GameVersions
                                      .Where(gv => gv == "Forge" || gv == "Fabric" || gv == "Quilt" || gv == "NeoForge")
                                      .Select(l => l.ToLower()).ToList(),
                        VersionType = file.ReleaseType.ToString()
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CurseForge Versions Error: {ex.Message}");
                return new List<ModVersionInfo>();
            }
        }
        #endregion

        #region Dependency Logic
        private async Task<List<string>> GetDependencyUrlsAsync(ModVersionInfo parentMod)
        {
            try
            {
                if (parentMod.Site == "Modrinth")
                    return await GetModrinthDependencyUrls(parentMod);
                else
                    return await GetCurseForgeDependencyUrls(parentMod);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Global Dependency Error: {ex.Message}");
                return new List<string>();
            }
        }

        private async Task<List<string>> GetModrinthDependencyUrls(ModVersionInfo parentMod)
        {
            var urls = new List<string>();
            string gameVersion = parentMod.GameVersions.FirstOrDefault();
            string loader = parentMod.Loaders.FirstOrDefault();

            if (string.IsNullOrEmpty(loader) || string.IsNullOrEmpty(gameVersion)) return urls;

            string url = $"https://api.modrinth.com/v2/version/{parentMod.VersionId}";
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var versionData = JObject.Parse(response);
                var dependencies = versionData["dependencies"] as JArray;

                if (dependencies != null)
                {
                    foreach (var dep in dependencies)
                    {
                        if (dep["dependency_type"]?.ToString() == "required")
                        {
                            string depProjectId = dep["project_id"]?.ToString();
                            if (depProjectId == null) continue;

                            string depUrl = $"https://api.modrinth.com/v2/project/{depProjectId}/version?loaders=[%22{loader}%22]&game_versions=[%22{gameVersion}%22]";
                            var depResponse = await _httpClient.GetStringAsync(depUrl);
                            var depVersions = JArray.Parse(depResponse);

                            if (depVersions.Count > 0)
                            {
                                var fileUrl = depVersions.OfType<JObject>()
                                    .SelectMany(v => v["files"] as JArray ?? new JArray())
                                    .FirstOrDefault(f => f["url"] != null)?["url"]?.ToString();

                                if (fileUrl != null) urls.Add(fileUrl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Modrinth Deps Error: {ex.Message}"); }
            return urls;
        }

        private async Task<List<string>> GetCurseForgeDependencyUrls(ModVersionInfo parentMod)
        {
            var urls = new List<string>();
            string gameVersion = parentMod.GameVersions.FirstOrDefault(v => v.StartsWith("1."));
            if (string.IsNullOrEmpty(gameVersion)) return urls;

            try
            {
                var fileData = await _cfApiClient.GetModFileAsync(int.Parse(parentMod.ModId), int.Parse(parentMod.VersionId));
                if (fileData?.Data?.Dependencies != null)
                {
                    foreach (var dep in fileData.Data.Dependencies)
                    {
                        if (dep.RelationType == FileRelationType.RequiredDependency)
                        {
                            string loader = parentMod.Loaders.FirstOrDefault();
                            var depFiles = (!string.IsNullOrEmpty(loader))
                                ? await _cfApiClient.GetModFilesAsync(modId: dep.ModId, gameVersion: gameVersion, modLoaderType: (ModLoaderType)Enum.Parse(typeof(ModLoaderType), loader, true))
                                : await _cfApiClient.GetModFilesAsync(modId: dep.ModId, gameVersion: gameVersion);

                            if (depFiles?.Data?.Count > 0)
                            {
                                var latestFile = depFiles.Data
                                    .Where(f => f.ReleaseType == FileReleaseType.Release || f.ReleaseType == FileReleaseType.Beta)
                                    .OrderByDescending(f => f.FileDate)
                                    .FirstOrDefault();

                                if (latestFile?.DownloadUrl != null) urls.Add(latestFile.DownloadUrl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CurseForge Deps Error: {ex.Message}"); }
            return urls;
        }
        #endregion

        #region Logic ModPack (Dependencies Info)
        public async Task<List<ModInfo>> GetDependenciesModInfoAsync(ModVersionInfo parentMod, string loaderType, int modTypeInt)
        {
            var dependenciesList = new List<ModInfo>();
            if (modTypeInt != 0) return dependenciesList;
            string typeStr = "mod";

            if (parentMod.Site == "Modrinth") return await GetModrinthDependenciesInfo(parentMod, loaderType, typeStr);
            else return await GetCurseForgeDependenciesInfo(parentMod, loaderType, typeStr);
        }

        private async Task<List<ModInfo>> GetModrinthDependenciesInfo(ModVersionInfo parentMod, string loader, string typeStr)
        {
            var list = new List<ModInfo>();
            string gameVersion = parentMod.GameVersions.FirstOrDefault();
            if (string.IsNullOrEmpty(loader) || string.IsNullOrEmpty(gameVersion)) return list;

            try
            {
                string url = $"https://api.modrinth.com/v2/version/{parentMod.VersionId}";
                var response = await _httpClient.GetStringAsync(url);
                var versionData = JObject.Parse(response);
                var dependencies = versionData["dependencies"] as JArray;

                if (dependencies != null)
                {
                    foreach (var dep in dependencies)
                    {
                        if (dep["dependency_type"]?.ToString() == "required")
                        {
                            string depProjectId = dep["project_id"]?.ToString();
                            if (string.IsNullOrEmpty(depProjectId)) continue;

                            string projectUrl = $"https://api.modrinth.com/v2/project/{depProjectId}";
                            var projResponse = await _httpClient.GetStringAsync(projectUrl);
                            var projData = JObject.Parse(projResponse);
                            string depName = projData["title"]?.ToString();
                            string depIcon = projData["icon_url"]?.ToString();

                            string verUrl = $"https://api.modrinth.com/v2/project/{depProjectId}/version?loaders=[%22{loader.ToLower()}%22]&game_versions=[%22{gameVersion}%22]";
                            var verResponse = await _httpClient.GetStringAsync(verUrl);
                            var verArray = JArray.Parse(verResponse);

                            if (verArray.Count > 0)
                            {
                                var bestVer = verArray[0];
                                var fileObj = (bestVer["files"] as JArray)?.FirstOrDefault(f => f["primary"]?.Value<bool>() == true)
                                              ?? (bestVer["files"] as JArray)?.FirstOrDefault();

                                if (fileObj != null)
                                {
                                    list.Add(new ModInfo
                                    {
                                        Name = depName,
                                        ProjectId = depProjectId,
                                        FileId = bestVer["id"]?.ToString(),
                                        Loader = loader,
                                        Version = gameVersion,
                                        Url = fileObj["url"]?.ToString(),
                                        LoaderType = loader,
                                        Type = typeStr,
                                        ImageURL = depIcon
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Modrinth ModPack Error: {ex.Message}"); }
            return list;
        }

        private async Task<List<ModInfo>> GetCurseForgeDependenciesInfo(ModVersionInfo parentMod, string loader, string typeStr)
        {
            var list = new List<ModInfo>();
            string gameVersion = parentMod.GameVersions.FirstOrDefault(v => v.StartsWith("1."));
            if (string.IsNullOrEmpty(gameVersion)) return list;

            try
            {
                var fileData = await _cfApiClient.GetModFileAsync(int.Parse(parentMod.ModId), int.Parse(parentMod.VersionId));
                if (fileData?.Data?.Dependencies == null) return list;

                ModLoaderType cfLoaderType = ModLoaderType.Any;
                Enum.TryParse(loader, true, out cfLoaderType);

                foreach (var dep in fileData.Data.Dependencies)
                {
                    if (dep.RelationType == FileRelationType.RequiredDependency)
                    {
                        var modInfo = await _cfApiClient.GetModAsync(dep.ModId);
                        if (modInfo?.Data == null) continue;

                        string depName = modInfo.Data.Name;
                        string depIcon = modInfo.Data.Logo?.Url;
                        CurseForge.APIClient.Models.Files.File latestFile = null;

                        var depFilesResponse = await _cfApiClient.GetModFilesAsync(modId: dep.ModId, gameVersion: gameVersion, modLoaderType: cfLoaderType);

                        if ((depFilesResponse?.Data == null || depFilesResponse.Data.Count == 0) && gameVersion.Count(c => c == '.') == 2)
                        {
                            string majorVersion = gameVersion.Substring(0, gameVersion.LastIndexOf('.'));
                            depFilesResponse = await _cfApiClient.GetModFilesAsync(modId: dep.ModId, gameVersion: majorVersion, modLoaderType: cfLoaderType);
                        }
                        if (depFilesResponse?.Data == null || depFilesResponse.Data.Count == 0)
                        {
                            depFilesResponse = await _cfApiClient.GetModFilesAsync(modId: dep.ModId, modLoaderType: cfLoaderType);
                        }

                        if (depFilesResponse?.Data != null && depFilesResponse.Data.Count > 0)
                        {
                            latestFile = depFilesResponse.Data
                                .Where(f => f.ReleaseType == FileReleaseType.Release || f.ReleaseType == FileReleaseType.Beta)
                                .OrderByDescending(f => f.FileDate)
                                .FirstOrDefault();
                        }
                        if (latestFile != null)
                        {
                            list.Add(new ModInfo
                            {
                                Name = depName,
                                ProjectId = dep.ModId.ToString(),
                                FileId = latestFile.Id.ToString(),
                                Loader = loader,
                                Version = gameVersion,
                                Url = latestFile.DownloadUrl,
                                LoaderType = loader,
                                Type = typeStr,
                                ImageURL = depIcon
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CurseForge ModPack Error: {ex.Message}"); }
            return list;
        }
        #endregion
    }

    public class ModrinthVersionConverter : JsonConverter<ModVersionInfo>
    {
        public override ModVersionInfo ReadJson(JsonReader reader, Type objectType, ModVersionInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            JObject item = JObject.Load(reader);
            var versionInfo = new ModVersionInfo();
            serializer.Populate(item.CreateReader(), versionInfo);

            var files = item["files"] as JArray;
            if (files != null && files.Count > 0)
            {
                string foundUrl = null;
                string foundFileName = null;
                foreach (JObject file in files)
                {
                    string url = file["url"]?.ToString();
                    if (string.IsNullOrEmpty(url)) continue;
                    string filename = file["filename"]?.ToString();
                    bool isPrimary = file["primary"]?.Value<bool>() ?? false;
                    if (isPrimary)
                    {
                        foundUrl = url; foundFileName = filename; break;
                    }
                    if (foundUrl == null)
                    {
                        foundUrl = url; foundFileName = filename;
                    }
                }
                versionInfo.DownloadUrl = foundUrl;
                versionInfo.FileName = foundFileName;
            }

            if (string.IsNullOrEmpty(versionInfo.FileName) && !string.IsNullOrEmpty(versionInfo.DownloadUrl))
            {
                if (Uri.TryCreate(versionInfo.DownloadUrl, UriKind.Absolute, out Uri uri))
                {
                    versionInfo.FileName = Path.GetFileName(uri.AbsolutePath);
                }
            }
            versionInfo.Site = "Modrinth";
            return versionInfo;
        }
        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, ModVersionInfo value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}