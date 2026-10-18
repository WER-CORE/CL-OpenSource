using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public class ModpackFileVersion
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string LoaderType { get; set; } = string.Empty;
}

public partial class DownloadModpackDialogViewModel : ObservableObject, IDisposable
{
    private readonly ModpackService _modpackService;
    private static ApiClient? _cfApiClientInstance;
    private const int ItemsPerPage = 10;
    private int _currentPage;
    private CancellationTokenSource? _searchCts;

    public Action? CloseAction { get; set; }
    public event Action? ScrollToTopRequested;

    [RelayCommand]
    private void CloseWindow() => CloseAction?.Invoke();

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedSite = "Modrinth";
    [ObservableProperty] private ObservableCollection<string> _mcVersions = new();
    [ObservableProperty] private string? _selectedMcVersion;
    [ObservableProperty] private ObservableCollection<string> _loaders = new() { "Forge", "Fabric", "Quilt", "NeoForge" };
    [ObservableProperty] private string _selectedLoader = "Forge";
    [ObservableProperty] private ObservableCollection<ModpackSearchResultViewModel> _modpacks = new();
    [ObservableProperty] private ModpackSearchResultViewModel? _selectedModpack;
    [ObservableProperty] private ObservableCollection<ModpackFileVersion> _packVersions = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    private ModpackFileVersion? _selectedPackVersion;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private bool _hasPrevPage;
    [ObservableProperty] private string _currentPageDisplay = "1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    private bool _isImporting;

    public bool CanInteract => !IsDownloading && !IsImporting;
    public bool CanDownload => CanInteract && SelectedPackVersion != null;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _downloadProgressText = "Завантажити";
    [ObservableProperty] private string _importButtonText = "Імпорт";

    [ObservableProperty] private string _windowTitle = string.Empty;
    [ObservableProperty] private string _searchPlaceholder = string.Empty;
    [ObservableProperty] private string _step1Title = string.Empty;
    [ObservableProperty] private string _loaderTitle = string.Empty;
    [ObservableProperty] private string _step2Title = string.Empty;
    [ObservableProperty] private string _step3Title = string.Empty;
    [ObservableProperty] private string _importOverlayText = string.Empty;

    public DownloadModpackDialogViewModel(ModpackService modpackService)
    {
        _modpackService = modpackService;
        ApplyLocalization();
        _ = LoadMcVersionsAsync();
    }

    public void ApplyLocalization()
    {
        WindowTitle = LocalizationManager.GetString("Modpacks.DownloadModpackWindowTitle", "CL - Завантаження/Імпорт збірок");
        ImportButtonText = LocalizationManager.GetString("Modpacks.ImportBtn", "Імпорт");
        DownloadProgressText = LocalizationManager.GetString("Modpacks.ModpackDownloadBtn", "Завантажити");
        SearchPlaceholder = LocalizationManager.GetString("Modpacks.SearchModpacksPlaceholder", "Пошук збірок...");
        Step1Title = LocalizationManager.GetString("Modpacks.McVersionTitle", "1. ВЕРСІЯ");
        LoaderTitle = LocalizationManager.GetString("Modpacks.LoaderTypeTitle", "ЯДРО");
        Step2Title = LocalizationManager.GetString("Modpacks.DownloadModpackHeader", "2. ВИБІР ЗБІРКИ");
        Step3Title = LocalizationManager.GetString("Modpacks.SelectVersionTitle", "3. ВЕРСІЯ ФАЙЛУ");
        ImportOverlayText = LocalizationManager.GetString("Modpacks.ModpackCielDownloading", "Триває імпорт архіву...");
    }

    private static async Task<ApiClient?> GetCfClientAsync()
    {
        if (_cfApiClientInstance != null) return _cfApiClientInstance;
        try
        {
            var client = WebHelper.Client;
            client.DefaultRequestHeaders.Remove("x-launcher-secret");
            client.DefaultRequestHeaders.Add("x-launcher-secret", "CL-Super-Secret-2026");
            var response = await client.GetAsync(Secrets.CurseForgeKey).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var data = JObject.Parse(json);
                string key = data["key"]?.ToString() ?? string.Empty;
                _cfApiClientInstance = new ApiClient(key);
                return _cfApiClientInstance;
            }
        }
        catch { }
        return null;
    }

    private async Task LoadMcVersionsAsync()
    {
        try
        {
            string rootPath = SettingsManager.Default.PathLacunher;
            if (string.IsNullOrWhiteSpace(rootPath)) rootPath = PlatformPaths.DefaultLauncherPath();

            var launcher = new MinecraftLauncher(new MinecraftPath(rootPath));
            var allVersions = await launcher.GetAllVersionsAsync().ConfigureAwait(false);

            IEnumerable<string>? versions = null;
            if (SelectedLoader == "Forge")
            {
                var minVer = new Version(1, 7, 10);
                versions = allVersions.Where(v => v.Type == "release" && ParseGameVersion(v.Name) >= minVer).Select(v => v.Name);
            }
            else if (SelectedLoader == "Fabric")
            {
                var fabricInstaller = new CmlLib.Core.ModLoaders.FabricMC.FabricInstaller(WebHelper.Client);
                versions = await fabricInstaller.GetSupportedVersionNames().ConfigureAwait(false);
            }
            else if (SelectedLoader == "Quilt")
            {
                var quiltInstaller = new CmlLib.Core.ModLoaders.QuiltMC.QuiltInstaller(WebHelper.Client);
                versions = await quiltInstaller.GetSupportedVersionNames().ConfigureAwait(false);
            }
            else if (SelectedLoader == "NeoForge")
            {
                var minVer = new Version(1, 20, 1);
                versions = allVersions.Where(v => v.Type == "release" && ParseGameVersion(v.Name) >= minVer).Select(v => v.Name);
            }

            if (versions != null)
            {
                var list = versions.ToList();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    McVersions = new ObservableCollection<string>(list);
                    if (SelectedMcVersion == null || !McVersions.Contains(SelectedMcVersion))
                    {
                        SelectedMcVersion = McVersions.FirstOrDefault();
                    }
                });
            }
        }
        catch { }
    }

    private static Version ParseGameVersion(string? versionStr)
    {
        if (string.IsNullOrEmpty(versionStr)) return new Version(0, 0, 0);
        try
        {
            string cleanStr = Regex.Match(versionStr, @"^[0-9\.]+").Value;
            var parts = cleanStr.Split('.');
            if (parts.Length == 2) cleanStr += ".0";
            if (Version.TryParse(cleanStr, out var version)) return version;
        }
        catch { }
        return new Version(0, 0, 0);
    }

    async partial void OnSelectedLoaderChanged(string value)
    {
        if (!CanInteract) return;
        _currentPage = 0;
        await LoadMcVersionsAsync();
        TriggerSearch();
    }

    partial void OnSelectedMcVersionChanged(string? value)
    {
        if (!CanInteract) return;
        _currentPage = 0;
        if (value != null) TriggerSearch();
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (!CanInteract) return;
        _currentPage = 0;
        TriggerSearch(debounceMs: 300);
    }

    async partial void OnSelectedModpackChanged(ModpackSearchResultViewModel? value)
    {
        if (!CanInteract) return;
        PackVersions.Clear();
        SelectedPackVersion = null;
        if (value != null)
        {
            await LoadVersionsForSelectedPackAsync(value);
        }
    }

    private void TriggerSearch(int debounceMs = 0)
    {
        if (!CanInteract) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            if (debounceMs > 0)
                await Task.Delay(debounceMs, token);

            if (!token.IsCancellationRequested && CanInteract)
                await SearchModsAsync(token);
        }, token);
    }

    [RelayCommand]
    private void SelectSite(string site)
    {
        if (!CanInteract) return;
        if (SelectedSite == site) return;
        SelectedSite = site;
        _currentPage = 0;
        TriggerSearch();
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (!CanInteract) return;
        if (_currentPage > 0)
        {
            _currentPage--;
            ScrollToTopRequested?.Invoke();
            TriggerSearch();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanInteract) return;
        if (HasNextPage)
        {
            _currentPage++;
            ScrollToTopRequested?.Invoke();
            TriggerSearch();
        }
    }

    private async Task SearchModsAsync(CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(SelectedMcVersion)) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            Modpacks.Clear();
        });

        try
        {
            if (SelectedSite == "Modrinth")
                await SearchModrinthAsync(token);
            else
                await SearchCurseForgeAsync(token);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = false;
                    UpdatePagination();
                    ScrollToTopRequested?.Invoke();
                });
            }
        }
    }

    private void UpdatePagination()
    {
        HasPrevPage = _currentPage > 0;
        CurrentPageDisplay = (_currentPage + 1).ToString();
    }

    private async Task SearchModrinthAsync(CancellationToken token)
    {
        try
        {
            int offset = _currentPage * ItemsPerPage;
            string safeSearch = string.IsNullOrWhiteSpace(SearchQuery) ? "" : SearchQuery;
            string safeLoader = SelectedLoader.ToLower();
            string version = SelectedMcVersion!;

            string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(safeSearch)}&facets=[[\"categories:{safeLoader}\"],[\"project_type:modpack\"],[\"versions:{version}\"]]&limit={ItemsPerPage}&offset={offset}&sort=downloads";

            string response = await WebHelper.Client.GetStringAsync(url, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            var result = JObject.Parse(response);
            if (result["hits"] is JArray hits)
            {
                var items = hits.Select(mod => new ModpackSearchResultViewModel(
                    mod["project_id"]?.ToString() ?? "",
                    mod["title"]?.ToString() ?? "",
                    mod["slug"]?.ToString() ?? "",
                    "Modrinth",
                    mod["icon_url"]?.ToString() ?? ""
                )).ToList();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var item in items) Modpacks.Add(item);
                    HasNextPage = hits.Count >= ItemsPerPage;
                });
            }
        }
        catch { }
    }

    private async Task SearchCurseForgeAsync(CancellationToken token)
    {
        try
        {
            var cfApi = await GetCfClientAsync().ConfigureAwait(false);
            if (cfApi == null || token.IsCancellationRequested) return;

            var modLoaderType = SelectedLoader switch
            {
                "Forge" => ModLoaderType.Forge,
                "Fabric" => ModLoaderType.Fabric,
                "Quilt" => ModLoaderType.Quilt,
                "NeoForge" => ModLoaderType.NeoForge,
                _ => ModLoaderType.Any
            };

            int index = _currentPage * ItemsPerPage;
            var searchResponse = await cfApi.SearchModsAsync(
                gameId: 432,
                searchFilter: SearchQuery,
                classId: 4471,
                pageSize: ItemsPerPage,
                index: index,
                sortField: ModsSearchSortField.Popularity,
                modLoaderType: modLoaderType,
                gameVersion: SelectedMcVersion
            ).ConfigureAwait(false);

            if (token.IsCancellationRequested || searchResponse.Data == null) return;

            var items = searchResponse.Data.Select(mod => new ModpackSearchResultViewModel(
                mod.Id.ToString(),
                mod.Name,
                mod.Slug,
                "CurseForge",
                mod.Logo?.Url ?? ""
            )).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in items) Modpacks.Add(item);
                HasNextPage = searchResponse.Data.Count >= ItemsPerPage;
            });
        }
        catch { }
    }

    private async Task LoadVersionsForSelectedPackAsync(ModpackSearchResultViewModel item)
    {
        try
        {
            var availableFiles = new List<ModpackFileVersion>();
            if (SelectedSite == "Modrinth")
            {
                string url = $"https://api.modrinth.com/v2/project/{item.ProjectId}/version";
                string response = await WebHelper.Client.GetStringAsync(url).ConfigureAwait(false);
                var versions = JArray.Parse(response);

                foreach (var ver in versions)
                {
                    var gameVersions = ver["game_versions"]?.ToObject<List<string>>() ?? new();
                    var loaders = ver["loaders"]?.ToObject<List<string>>() ?? new();

                    if (gameVersions.Contains(SelectedMcVersion!) && loaders.Contains(SelectedLoader.ToLower()))
                    {
                        if (ver["files"] is JArray files)
                        {
                            foreach (var file in files)
                            {
                                string fileName = file["filename"]?.ToString() ?? "";
                                bool isPrimary = file["primary"]?.ToObject<bool>() == true;
                                if (isPrimary || fileName.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
                                {
                                    availableFiles.Add(new ModpackFileVersion
                                    {
                                        Name = ver["name"]?.ToString() ?? fileName,
                                        DownloadUrl = file["url"]?.ToString() ?? "",
                                        FileName = fileName,
                                        GameVersion = SelectedMcVersion!,
                                        LoaderType = SelectedLoader
                                    });
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (int.TryParse(item.ProjectId, out int modId))
            {
                var cfApi = await GetCfClientAsync().ConfigureAwait(false);
                if (cfApi != null)
                {
                    var filesResponse = await cfApi.GetModFilesAsync(modId, pageSize: 50).ConfigureAwait(false);
                    if (filesResponse.Data != null)
                    {
                        foreach (var file in filesResponse.Data)
                        {
                            bool isCorrectVersion = file.GameVersions.Any(v => v == SelectedMcVersion);
                            bool isCorrectLoader = file.GameVersions.Any(v => v.Equals(SelectedLoader, StringComparison.OrdinalIgnoreCase));
                            if (isCorrectVersion && isCorrectLoader)
                            {
                                availableFiles.Add(new ModpackFileVersion
                                {
                                    Name = file.DisplayName,
                                    DownloadUrl = file.DownloadUrl,
                                    FileName = file.FileName,
                                    GameVersion = SelectedMcVersion!,
                                    LoaderType = SelectedLoader
                                });
                            }
                        }
                    }
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PackVersions = new ObservableCollection<ModpackFileVersion>(availableFiles);
                SelectedPackVersion = PackVersions.FirstOrDefault();
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (SelectedModpack == null || SelectedPackVersion == null || IsDownloading) return;

        IsDownloading = true;
        ProgressValue = 0;
        DownloadProgressText = "Підготовка... 0%";

        string baseDir = SettingsManager.Default.PathLacunher;
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = PlatformPaths.DefaultLauncherPath();

        string tempFolder = Path.Combine(baseDir, "CLModpack", $"{SelectedModpack.Name}_TempDownload");
        Directory.CreateDirectory(tempFolder);
        string zipFileName = string.IsNullOrWhiteSpace(SelectedPackVersion.FileName) ? $"{SelectedModpack.Name}.zip" : SelectedPackVersion.FileName;
        string zipFilePath = Path.Combine(tempFolder, zipFileName);

        try
        {

            using (var response = await WebHelper.Client.GetAsync(SelectedPackVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;
                await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int read;
                int lastPct = -1;

                while ((read = await contentStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes is > 0)
                    {
                        int pct = (int)((double)totalRead / totalBytes.Value * 100);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ProgressValue = pct * 0.5;
                                DownloadProgressText = $"Скачування: {pct}%";
                            });
                        }
                    }
                }
            }

            string extractPath = Path.Combine(baseDir, "CLModpack", SelectedModpack.Name);
            await ExtractArchiveWithProgressAsync(zipFilePath, extractPath, pct =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = 50 + (pct * 0.5);
                    DownloadProgressText = $"Розпакування: {pct}%";
                });
            }).ConfigureAwait(false);

            string pathJson = SelectedSite == "Modrinth"
                ? Path.Combine(extractPath, "modrinth.index.json")
                : Path.Combine(extractPath, "manifest.json");

            string loaderVersion = ExtractLoaderVersion(pathJson, SelectedSite, SelectedLoader);
            string finalIconPath = await DownloadPackIconAsync(SelectedModpack.OriginalIconUrl, extractPath).ConfigureAwait(false);

            var newPack = new InstalledModpack
            {
                Name = SelectedModpack.Name,
                TypeSite = SelectedSite,
                MinecraftVersion = SelectedPackVersion.GameVersion,
                LoaderType = SelectedLoader,
                LoaderVersion = loaderVersion,
                Path = extractPath,
                UrlImage = finalIconPath,
                PathJson = pathJson
            };

            SaveInstalledModpackToJson(newPack);

            await Dispatcher.UIThread.InvokeAsync(() => CloseAction?.Invoke());
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                AvaloniaDialogService.Show($"Помилка завантаження:\n{ex.Message}", "Помилка", MascotEmotion.Sad));
        }
        finally
        {
            try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsDownloading = false;
                ProgressValue = 0;
                DownloadProgressText = "Завантажити";
            });
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsImporting || IsDownloading) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return;

        var options = new FilePickerOpenOptions
        {
            Title = "Оберіть файл мод-паку",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Modpack Files")
                {
                    Patterns = new[] { "*.zip", "*.mrpack" }
                }
            }
        };

        var result = await mainWindow.StorageProvider.OpenFilePickerAsync(options);
        if (result == null || result.Count == 0) return;

        string archivePath = result[0].Path.LocalPath;

        IsImporting = true;
        IsDownloading = true;
        ProgressValue = 0;
        ImportButtonText = "0%";
        DownloadProgressText = "Імпорт: 0%";

        try
        {
            await ImportArchiveWithProgressAsync(archivePath, pct =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = pct;
                    ImportButtonText = $"{pct}%";
                    DownloadProgressText = $"Імпорт: {pct}%";
                });
            });

            AvaloniaDialogService.Show("Збірка успішно імпортована!", "Успіх", MascotEmotion.Happy);
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            AvaloniaDialogService.Show($"Не вдалося імпортувати файл:\n{ex.Message}", "Помилка імпорту", MascotEmotion.Sad);
        }
        finally
        {
            IsImporting = false;
            IsDownloading = false;
            ProgressValue = 0;
            ImportButtonText = "Імпорт";
            DownloadProgressText = "Завантажити";
        }
    }

    private async Task ImportArchiveWithProgressAsync(string archivePath, Action<int> onProgress)
    {
        string baseDir = SettingsManager.Default.PathLacunher;
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = PlatformPaths.DefaultLauncherPath();

        string packName = Path.GetFileNameWithoutExtension(archivePath);
        string extractPath = Path.Combine(baseDir, "CLModpack", packName);

        await ExtractArchiveWithProgressAsync(archivePath, extractPath, pct =>
        {
            onProgress((int)(pct * 0.9));
        }).ConfigureAwait(false);

        string clManifestPath = Path.Combine(extractPath, "modpack.json");
        string mrManifestPath = Path.Combine(extractPath, "modrinth.index.json");
        string cfManifestPath = Path.Combine(extractPath, "manifest.json");

        string mcVersion = SelectedMcVersion ?? "1.20.1";
        string loaderType = SelectedLoader ?? "Fabric";
        string loaderVersion = "Auto";
        string typeSite = "Custom";
        string pathJson = clManifestPath;

        if (File.Exists(clManifestPath))
        {
            try
            {
                var obj = JObject.Parse(await File.ReadAllTextAsync(clManifestPath).ConfigureAwait(false));
                packName = obj["Name"]?.ToString() ?? packName;
                mcVersion = obj["Minecraft"]?.ToString() ?? mcVersion;
                loaderType = obj["Loader"]?.ToString() ?? loaderType;
                loaderVersion = obj["LoaderVersion"]?.ToString() ?? "Auto";
                typeSite = "Custom";
                pathJson = clManifestPath;
            }
            catch { }
        }
        else if (File.Exists(mrManifestPath))
        {
            typeSite = "Modrinth";
            pathJson = mrManifestPath;
            try
            {
                var obj = JObject.Parse(await File.ReadAllTextAsync(mrManifestPath).ConfigureAwait(false));
                packName = obj["name"]?.ToString() ?? packName;
                var deps = obj["dependencies"] as JObject;
                if (deps != null)
                {
                    mcVersion = deps["minecraft"]?.ToString() ?? mcVersion;
                    foreach (var prop in deps.Properties())
                    {
                        if (prop.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase)) { loaderType = "Fabric"; loaderVersion = prop.Value.ToString(); }
                        else if (prop.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase)) { loaderType = "Quilt"; loaderVersion = prop.Value.ToString(); }
                        else if (prop.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) { loaderType = "NeoForge"; loaderVersion = prop.Value.ToString(); }
                        else if (prop.Name.Contains("forge", StringComparison.OrdinalIgnoreCase)) { loaderType = "Forge"; loaderVersion = prop.Value.ToString(); }
                    }
                }
            }
            catch { }
        }
        else if (File.Exists(cfManifestPath))
        {
            typeSite = "CurseForge";
            pathJson = cfManifestPath;
            try
            {
                var obj = JObject.Parse(await File.ReadAllTextAsync(cfManifestPath).ConfigureAwait(false));
                packName = obj["name"]?.ToString() ?? packName;
                mcVersion = obj["minecraft"]?["version"]?.ToString() ?? mcVersion;
                var loaderId = obj["minecraft"]?["modLoaders"]?.FirstOrDefault()?["id"]?.ToString();
                if (!string.IsNullOrEmpty(loaderId) && loaderId.Contains('-'))
                {
                    var parts = loaderId.Split('-', 2);
                    loaderType = char.ToUpper(parts[0][0]) + parts[0][1..];
                    loaderVersion = parts[1];
                }
            }
            catch { }
        }

        string iconPath = Path.Combine(extractPath, "icon.png");
        if (!File.Exists(iconPath))
            iconPath = "avares://CL.Avalonia/Icon/IconCL(Common).png";

        var importedPack = new InstalledModpack
        {
            Name = packName,
            TypeSite = typeSite,
            MinecraftVersion = mcVersion,
            LoaderType = loaderType,
            LoaderVersion = loaderVersion,
            Path = extractPath,
            UrlImage = iconPath,
            PathJson = pathJson
        };

        SaveInstalledModpackToJson(importedPack);
        onProgress(100);
    }

    private static Task ExtractArchiveWithProgressAsync(string archivePath, string extractPath, Action<int> onProgress)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(extractPath);

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                int totalEntries = Math.Max(1, archive.Entries.Count);
                int processed = 0;
                int lastPct = -1;
                string fullExtractRoot = Path.GetFullPath(extractPath);

                foreach (var entry in archive.Entries)
                {
                    processed++;
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                    if (!destinationPath.StartsWith(fullExtractRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        string? parentDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(parentDir))
                            Directory.CreateDirectory(parentDir);

                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }

                    int pct = (int)((double)processed / totalEntries * 100);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        onProgress(pct);
                    }
                }
            }

            var rootDir = new DirectoryInfo(extractPath);
            var subDirs = rootDir.GetDirectories();
            var files = rootDir.GetFiles();

            if (files.Length == 0 && subDirs.Length == 1)
            {
                var nestedDir = subDirs[0];
                foreach (var file in nestedDir.GetFiles())
                {
                    file.MoveTo(Path.Combine(extractPath, file.Name), true);
                }
                foreach (var dir in nestedDir.GetDirectories())
                {
                    string destDir = Path.Combine(extractPath, dir.Name);
                    if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                    dir.MoveTo(destDir);
                }
                nestedDir.Delete();
            }
        });
    }

    private static string ExtractLoaderVersion(string pathJson, string site, string selectedLoader)
    {
        if (!File.Exists(pathJson)) return "Auto";
        try
        {
            var jsonObj = JObject.Parse(File.ReadAllText(pathJson));
            if (site == "Modrinth" && jsonObj["dependencies"] is JObject deps)
            {
                foreach (var property in deps.Properties())
                {
                    if (property.Name.Contains(selectedLoader, StringComparison.OrdinalIgnoreCase))
                        return property.Value?.ToString() ?? "Auto";
                }
            }
            else if (site == "CurseForge" && jsonObj["minecraft"]?["modLoaders"] is JArray loadersArr)
            {
                var loaderInfo = loadersArr.FirstOrDefault(l => l["id"]?.ToString().StartsWith(selectedLoader, StringComparison.OrdinalIgnoreCase) == true);
                string? fullId = loaderInfo?["id"]?.ToString();
                if (!string.IsNullOrEmpty(fullId) && fullId.Contains('-'))
                    return fullId.Split('-')[1];
            }
        }
        catch { }
        return "Auto";
    }

    private static async Task<string> DownloadPackIconAsync(string? iconUrl, string extractPath)
    {
        const string defaultIcon = "avares://CL.Avalonia/Icon/IconCL(Common).png";
        if (string.IsNullOrEmpty(iconUrl)) return defaultIcon;

        try
        {
            string extension = Path.GetExtension(iconUrl);
            if (string.IsNullOrEmpty(extension) || extension.Length > 5) extension = ".png";
            string localFilePath = Path.Combine(extractPath, $"icon{extension}");
            byte[] imgBytes = await WebHelper.Client.GetByteArrayAsync(iconUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(localFilePath, imgBytes).ConfigureAwait(false);
            return localFilePath;
        }
        catch
        {
            return defaultIcon;
        }
    }

    private static void SaveInstalledModpackToJson(InstalledModpack newPack)
    {
        Directory.CreateDirectory(ModpackPaths.DataDirectory);
        string jsonSavePath = ModpackPaths.InstalledModpacksJson;
        var modpacks = new List<InstalledModpack>();

        if (File.Exists(jsonSavePath))
        {
            try
            {
                string existingJson = File.ReadAllText(jsonSavePath);
                modpacks = JsonConvert.DeserializeObject<List<InstalledModpack>>(existingJson) ?? new List<InstalledModpack>();
            }
            catch { }
        }

        modpacks.RemoveAll(m => string.Equals(m.Name, newPack.Name, StringComparison.OrdinalIgnoreCase));
        modpacks.Add(newPack);

        File.WriteAllText(jsonSavePath, JsonConvert.SerializeObject(modpacks, Formatting.Indented));
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        CloseAction = null;
        ScrollToTopRequested = null;
        Modpacks.Clear();
        SelectedModpack = null;
    }
}
