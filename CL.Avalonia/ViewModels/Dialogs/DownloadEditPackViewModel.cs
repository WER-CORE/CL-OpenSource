using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class DownloadEditPackViewModel : ObservableObject, IDisposable
{
    private readonly ModDownloadService _modDownloadService;
    private static ApiClient? _cfApiClientInstance;

    [ObservableProperty] private ModpackInfo _currentModpack = new();
    [ObservableProperty] private byte _selectMod = 0;
    [ObservableProperty] private string _loaderNow = "Forge";
    [ObservableProperty] private string _siteDownload = "Modrinth";
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _currentLoaderVersion = "Unknown";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPage))]
    private int _currentPage = 0;
    public int DisplayPage => CurrentPage + 1;
    private const int PageSize = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModrinthOpacity))]
    private bool _isModrinthSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurseForgeOpacity))]
    private bool _isCurseForgeSelected = false;

    public double ModrinthOpacity => IsModrinthSelected ? 1.0 : 0.5;
    public double CurseForgeOpacity => IsCurseForgeSelected ? 1.0 : 0.5;

    [ObservableProperty] private bool _isModrinthVisible = true;

    [ObservableProperty] private string _windowTitle = "Менеджер завантаження";
    [ObservableProperty] private string _headerTitle = "";
    [ObservableProperty] private string _queueTitle = "До черги:";
    [ObservableProperty] private string _installModTitle = "Встановлення мода";
    [ObservableProperty] private string _searchPlaceholder = "Пошук...";
    [ObservableProperty] private string _mcVersionTitle = "Версія Minecraft";
    [ObservableProperty] private string _loaderTitle = "Завантажувач";
    [ObservableProperty] private string _installSelectedContentBtnText = "ВСТАНОВИТИ ОБРАНИЙ КОНТЕНТ";
    [ObservableProperty] private string _addToQueueBtnText = "Додати до черги";

    [ObservableProperty] private ObservableCollection<string> _availableVersions = new();
    [ObservableProperty] private string? _selectedVersion;

    [ObservableProperty] private bool _isLoadingMods = false;
    [ObservableProperty] private ObservableCollection<DownloadEditPackItemViewModel> _modsList = new();
    [ObservableProperty] private ObservableCollection<DownloadEditPackQueueItemViewModel> _queueList = new();

    [ObservableProperty] private bool _isVersionSelectionOpen = false;
    [ObservableProperty] private ObservableCollection<string> _modVersions = new();
    [ObservableProperty] private int _selectedModVersionIndex = -1;
    [ObservableProperty] private DownloadEditPackItemViewModel? _currentlySelectingMod;

    [ObservableProperty] private bool _isPrevPageEnabled = false;
    [ObservableProperty] private bool _isNextPageEnabled = false;

    private List<string> _fileUrlDownload = new();
    private List<string> _versionIds = new();

    public Action? CloseAction { get; set; }
    public event Action? ScrollToTopRequested;

    [RelayCommand]
    private void CloseWindow() => CloseAction?.Invoke();

    public DownloadEditPackViewModel()
    {
        _modDownloadService = new ModDownloadService();
        ApplyLocalization();
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public void Initialize(ModpackInfo modpack, byte selectMod)
    {
        CurrentModpack = modpack;
        SelectMod = selectMod;

        if (SelectMod == 3)
        {
            IsModrinthVisible = false;
            SelectCurseForgeCommand.Execute(null);
        }

        ApplyLocalization();
        LoadVariableCurrentPack();
        LoadQueueFromJson();
        UpdateModsListAsync();
    }

    public void ApplyLocalization()
    {
        WindowTitle = LocalizationManager.GetString("DownloadManager.Title", "Менеджер завантаження");
        var modsType = SelectMod switch
        {
            0 => LocalizationManager.GetString("DownloadManager.TypeMods", "модів"),
            1 => LocalizationManager.GetString("DownloadManager.TypeResourcePacks", "ресурс-паків"),
            2 => LocalizationManager.GetString("DownloadManager.TypeShaders", "шейдерів"),
            _ => LocalizationManager.GetString("DownloadManager.TypeMods", "модів")
        };
        HeaderTitle = string.Format(LocalizationManager.GetString("DownloadManager.EditPackTitle", "Завантаження {0} у збірку {1}"), modsType, CurrentModpack?.Name ?? "");
        QueueTitle = LocalizationManager.GetString("DownloadManager.QueueTitle", "До черги:");
        InstallModTitle = LocalizationManager.GetString("DownloadManager.InstallModTitle", "Встановлення мода");
        SearchPlaceholder = LocalizationManager.GetString("Modpacks.ModSearchPlaceholder", "Пошук...");
        McVersionTitle = LocalizationManager.GetString("Modpacks.ModpackMcVersion", "Версія Minecraft");
        LoaderTitle = LocalizationManager.GetString("Modpacks.ModpackLoaderType", "Завантажувач");
        InstallSelectedContentBtnText = LocalizationManager.GetString("DownloadManager.InstallSelectedContent", "ВСТАНОВИТИ ОБРАНИЙ КОНТЕНТ");
        AddToQueueBtnText = LocalizationManager.GetString("DownloadManager.AddToQueue", "Додати до черги");
    }

    private void LoadVariableCurrentPack()
    {
        if (CurrentModpack != null)
        {
            LoadLoaderVersion(CurrentModpack.Name);
            AvailableVersions.Clear();
            if (!string.IsNullOrEmpty(CurrentModpack.MinecraftVersion))
            {
                AvailableVersions.Add(CurrentModpack.MinecraftVersion);
                SelectedVersion = CurrentModpack.MinecraftVersion;
            }
            LoaderNow = CurrentModpack.LoaderType ?? "Forge";
        }
    }

    private void LoadLoaderVersion(string modpackName)
    {
        try
        {
            string path = ModpackPaths.InstalledModpacksJson;
            if (!File.Exists(path))
            {
                string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");
                if (File.Exists(legacyPath)) path = legacyPath;
            }
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            JArray modpacks = JArray.Parse(json);
            var found = modpacks.FirstOrDefault(x => (string?)x["Name"] == modpackName);

            if (found != null)
            {
                string? loaderVersion = (string?)found["LoaderVersion"];
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    CurrentLoaderVersion = loaderVersion;
                }
            }
        }
        catch { }
    }

    private async Task<ApiClient?> GetCfClientAsync()
    {
        if (_cfApiClientInstance != null) return _cfApiClientInstance;
        try
        {
            var client = WebHelper.Client;
            client.DefaultRequestHeaders.Remove("x-launcher-secret");
            client.DefaultRequestHeaders.Add("x-launcher-secret", "CL-Super-Secret-2026");
            var response = await client.GetAsync(Secrets.CurseForgeKey);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                string? key = data["key"]?.ToString();
                if (key != null)
                {
                    _cfApiClientInstance = new ApiClient(key);
                    return _cfApiClientInstance;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не вдалося отримати ключ CurseForge: {ex.Message}");
        }
        return null;
    }

    [RelayCommand]
    private async Task UpdateModsListAsync()
    {
        if (string.IsNullOrEmpty(SelectedVersion) || (CurrentModpack.LoaderType != "Vanila" && string.IsNullOrEmpty(CurrentLoaderVersion)))
            return;

        IsLoadingMods = true;
        ModsList.Clear();
        IsPrevPageEnabled = CurrentPage > 0;

        try
        {
            if (SiteDownload == "Modrinth")
            {
                int offset = CurrentPage * PageSize;
                string query = Uri.EscapeDataString(SearchQuery);
                string loader = LoaderNow.ToLower();
                var urls = new[] {
                    $"https://api.modrinth.com/v2/search?query={query}&facets=[[\"categories:{loader}\"],[\"project_type:mod\"]]&limit={PageSize}&offset={offset}",
                    $"https://api.modrinth.com/v2/search?query={query}&facets=[[\"project_type:resourcepack\"]]&limit={PageSize}&offset={offset}",
                    $"https://api.modrinth.com/v2/search?query={query}&facets=[[\"project_type:shader\"]]&limit={PageSize}&offset={offset}",
                    $"https://api.modrinth.com/v2/search?query={query}&facets=[[\"project_type:modpack\"]]&limit={PageSize}&offset={offset}",
                };

                var response = await WebHelper.Client.GetStringAsync(urls[SelectMod]);
                dynamic? result = JsonConvert.DeserializeObject(response);

                if (result != null)
                {
                    IsNextPageEnabled = result["hits"].Count >= PageSize;
                    foreach (var mod in result["hits"])
                    {
                        string loaderType = SelectMod switch { 0 => "mod", 1 => "resourcepack", 2 => "shader", 3 => "map", _ => "mod" };
                        var item = CreateItemFromModrinth(mod, loaderType);
                        ModsList.Add(item);
                        _ = LoadItemImageAsync(item, item.IconUrl);
                    }
                }
            }
            else if (SiteDownload == "CurseForge")
            {
                int classId = SelectMod switch { 0 => 6, 1 => 12, 2 => 6552, 3 => 17, _ => 6 };
                ModLoaderType? modLoaderType = null;

                if (CurrentModpack.LoaderType != "Vanila" && SelectMod == 0)
                {
                    modLoaderType = LoaderNow switch
                    {
                        "Forge" => ModLoaderType.Forge,
                        "Fabric" => ModLoaderType.Fabric,
                        "Quilt" => ModLoaderType.Quilt,
                        "NeoForge" => ModLoaderType.NeoForge,
                        "LiteLoader" => ModLoaderType.LiteLoader,
                        "Optifine" => ModLoaderType.Any,
                        _ => null
                    };
                }

                var cfApi = await GetCfClientAsync();
                if (cfApi == null) return;

                int index = CurrentPage * PageSize;
                var searchResponse = await cfApi.SearchModsAsync(
                    gameId: 432,
                    classId: classId,
                    modLoaderType: modLoaderType,
                    gameVersion: SelectedVersion,
                    pageSize: PageSize,
                    searchFilter: SearchQuery,
                    index: index,
                    sortField: ModsSearchSortField.Popularity
                );

                if (searchResponse?.Data != null)
                {
                    IsNextPageEnabled = searchResponse.Data.Count >= PageSize;
                    foreach (var mod in searchResponse.Data)
                    {
                        var item = CreateItemFromCurseForge(mod, SelectMod);
                        ModsList.Add(item);
                        _ = LoadItemImageAsync(item, item.IconUrl);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                ((App)global::Avalonia.Application.Current!).ServiceProvider!);
            dialog.ShowMessage($"Не вдалося завантажити список модів.\n{ex.Message}", "Помилка списку");
        }
        finally
        {
            IsLoadingMods = false;
            ScrollToTopRequested?.Invoke();
        }
    }

    private DownloadEditPackItemViewModel CreateItemFromCurseForge(Mod mod, int type)
    {
        string detailsUrl = mod.Links?.WebsiteUrl ?? $"https://www.curseforge.com/minecraft/modpacks/{mod.Slug}";
        var item = new DownloadEditPackItemViewModel(detailsUrl, OnModAddClicked)
        {
            Name = mod.Name ?? "",
            ProjectId = mod.Id.ToString(),
            Slug = mod.Slug ?? "",
            Type = type,
            Loader = LoaderNow,
            GameVersion = SelectedVersion ?? "",
            IconUrl = mod.Logo?.Url ?? ""
        };
        return item;
    }

    private DownloadEditPackItemViewModel CreateItemFromModrinth(dynamic mod, string loaderType)
    {
        string detailsUrl = $"https://modrinth.com/{loaderType}/{mod["slug"]}";
        var item = new DownloadEditPackItemViewModel(detailsUrl, OnModAddClicked)
        {
            Name = mod["title"]?.ToString() ?? "",
            ProjectId = mod["project_id"]?.ToString() ?? "",
            Slug = mod["slug"]?.ToString() ?? "",
            Type = SelectMod,
            Loader = LoaderNow,
            GameVersion = SelectedVersion ?? "",
            IconUrl = mod["icon_url"]?.ToString() ?? ""
        };
        return item;
    }

    private async Task LoadItemImageAsync(DownloadEditPackItemViewModel item, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(url, 32);
            if (bitmap != null)
            {
                Dispatcher.UIThread.Post(() => item.IconBitmap = bitmap);
            }
        }
        catch { }
    }

    private void OnModAddClicked(DownloadEditPackItemViewModel item)
    {
        CurrentlySelectingMod = item;
        IsVersionSelectionOpen = true;

        var info = new ModInfo
        {
            Name = item.Name,
            ProjectId = item.ProjectId,
            Type = SelectMod == 0 ? "mod" : SelectMod == 1 ? "resourcepack" : SelectMod == 2 ? "shader" : SelectMod == 3 ? "map" : "mod",
            ImageURL = item.IconUrl,
            Slug = item.Slug,
            Loader = LoaderNow,
            LoaderType = SelectMod == 0 ? "mod" : "other",
            Version = SelectedVersion
        };

        if (SiteDownload == "Modrinth")
        {
            info.Url = $"https://api.modrinth.com/v2/project/{info.ProjectId}/version";
            _ = GetCompatibleVersionsModrinthAsync(info);
        }
        else
        {
            _ = GetLatestCompatibleModVersionsCurseForgeAsync(info);
        }
    }

    private async Task GetCompatibleVersionsModrinthAsync(ModInfo mod)
    {
        ModVersions.Clear();
        _fileUrlDownload.Clear();
        _versionIds.Clear();
        SelectedModVersionIndex = -1;

        if (string.IsNullOrEmpty(SelectedVersion)) return;

        try
        {
            var response = await WebHelper.Client.GetAsync(mod.Url);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            var versionsArray = JArray.Parse(responseBody);

            if (versionsArray != null && versionsArray.Count > 0)
            {
                foreach (var version in versionsArray)
                {
                    var gameVersions = version["game_versions"]?.ToObject<List<string>>();
                    var loaders = version["loaders"]?.ToObject<List<string>>();

                    bool isCompatible = mod.Type == "mod"
                        ? gameVersions != null && gameVersions.Contains(SelectedVersion) && loaders != null && loaders.Any(l => l.Equals(mod.Loader, StringComparison.OrdinalIgnoreCase))
                        : gameVersions != null && gameVersions.Contains(SelectedVersion);

                    if (isCompatible)
                    {
                        ModVersions.Add(version["version_number"]?.ToString() ?? "Unknown");
                        _versionIds.Add(version["id"]?.ToString() ?? "");

                        var files = version["files"] as JArray;
                        string? fileUrl = files?[0]?["url"]?.ToString();
                        _fileUrlDownload.Add(fileUrl ?? "");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing Modrinth versions: {ex.Message}");
        }
    }

    private async Task GetLatestCompatibleModVersionsCurseForgeAsync(ModInfo mod)
    {
        ModVersions.Clear();
        _fileUrlDownload.Clear();
        _versionIds.Clear();
        SelectedModVersionIndex = -1;

        if (string.IsNullOrEmpty(SelectedVersion)) return;

        var cfApi = await GetCfClientAsync();
        if (cfApi == null) return;

        try
        {
            if (!int.TryParse(mod.ProjectId, out int modId)) return;

            ModLoaderType? modLoaderType = null;
            if (SelectMod == 0)
            {
                modLoaderType = LoaderNow switch
                {
                    "Fabric" => ModLoaderType.Fabric,
                    "Quilt" => ModLoaderType.Quilt,
                    "NeoForge" => ModLoaderType.NeoForge,
                    _ => ModLoaderType.Forge
                };
            }

            var response = await cfApi.GetModFilesAsync(modId, null, modLoaderType);
            if (response?.Data == null) return;

            var compatibleFiles = response.Data.Where(file =>
                file.GameVersions.Any(v => v.Equals(SelectedVersion, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            foreach (var file in compatibleFiles)
            {
                ModVersions.Add($"{file.DisplayName}");
                _versionIds.Add(file.Id.ToString());

                string url = file.DownloadUrl;
                if (string.IsNullOrEmpty(url))
                {
                    string typePath = mod.Type switch { "shader" => "shaders", "resourcepack" => "texture-packs", "map" => "worlds", _ => "mc-mods" };
                    url = $"https://www.curseforge.com/minecraft/{typePath}/{mod.Slug}/download/{file.Id}";
                }
                _fileUrlDownload.Add(url);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CurseForge Version Fetch Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Search()
    {
        CurrentPage = 0;
        UpdateModsListAsync();
    }

    [RelayCommand]
    private void SelectModrinth()
    {
        AvaloniaSoundService.Click();
        if (SiteDownload == "Modrinth") return;
        IsModrinthSelected = true;
        IsCurseForgeSelected = false;
        SiteDownload = "Modrinth";
        CurrentPage = 0;
        UpdateModsListAsync();
    }

    [RelayCommand]
    private void SelectCurseForge()
    {
        AvaloniaSoundService.Click();
        if (SiteDownload == "CurseForge") return;
        IsModrinthSelected = false;
        IsCurseForgeSelected = true;
        SiteDownload = "CurseForge";
        CurrentPage = 0;
        UpdateModsListAsync();
    }

    [RelayCommand]
    private void PrevPage()
    {
        AvaloniaSoundService.Click();
        if (CurrentPage > 0)
        {
            CurrentPage--;
            ScrollToTopRequested?.Invoke();
            UpdateModsListAsync();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        AvaloniaSoundService.Click();
        CurrentPage++;
        ScrollToTopRequested?.Invoke();
        UpdateModsListAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        CurrentPage = 0;
        _ = UpdateModsListAsync();
    }

    [RelayCommand]
    private void CloseVersionSelection()
    {
        AvaloniaSoundService.Click();
        IsVersionSelectionOpen = false;
        _fileUrlDownload.Clear();
    }

    [RelayCommand]
    private async Task AddSelectedVersionToQueueAsync()
    {
        AvaloniaSoundService.Click();

        if (SelectedModVersionIndex < 0 || SelectedModVersionIndex >= _fileUrlDownload.Count || CurrentlySelectingMod == null)
        {
            var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                ((App)global::Avalonia.Application.Current!).ServiceProvider!);
            dialog.ShowMessage(LocalizationManager.GetString("Modpacks.SelectVersionAlertDesc", "Тицьни на потрібну версію зі списку!"), LocalizationManager.GetString("Modpacks.SelectVersionAlertTitle", "Обери версію"));
            IsVersionSelectionOpen = false;
            return;
        }

        string currentFileId = (SelectedModVersionIndex < _versionIds.Count) ? _versionIds[SelectedModVersionIndex] : "";

        var modInfo = new ModInfo
        {
            Name = $"{CurrentlySelectingMod.Name}:{ModVersions[SelectedModVersionIndex]}",
            ProjectId = CurrentlySelectingMod.ProjectId,
            FileId = currentFileId,
            Loader = CurrentLoaderVersion,
            Version = SelectedVersion,
            Url = _fileUrlDownload[SelectedModVersionIndex],
            LoaderType = LoaderNow,
            Type = SelectMod switch { 0 => "mod", 1 => "resourcepack", 2 => "shader", 3 => "map", _ => "mod" },
            ImageURL = CurrentlySelectingMod.IconUrl,
            Slug = CurrentlySelectingMod.Slug
        };

        SaveModToModpackJson(modInfo, silent: false);
        IsVersionSelectionOpen = false;

        if (SelectMod == 0 && !string.IsNullOrEmpty(currentFileId) && SiteDownload == "CurseForge")
        {
            try
            {
                var verInfo = new ModVersionInfo
                {
                    ModId = modInfo.ProjectId,
                    VersionId = modInfo.FileId,
                    Site = SiteDownload,
                    GameVersions = new List<string> { modInfo.Version ?? "" }
                };

                var dependencies = await _modDownloadService.GetDependenciesModInfoAsync(verInfo, LoaderNow, 0);
                if (dependencies != null && dependencies.Count > 0)
                {
                    int addedCount = 0;
                    foreach (var dep in dependencies)
                    {
                        if (dep.ProjectId != modInfo.ProjectId)
                        {
                            SaveModToModpackJson(dep, silent: true);
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                            ((App)global::Avalonia.Application.Current!).ServiceProvider!);
                        dialog.ShowMessage(string.Format(LocalizationManager.GetString("Modpacks.ModpackDepsAdded", "Автоматично додано {0} залежних модів (API/Lib)."), addedCount), "Успіх");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка залежностей: {ex.Message}");
            }
        }
    }

    private void SaveModToModpackJson(ModInfo mod, bool silent = false)
    {
        string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name ?? "Unknown");
        Directory.CreateDirectory(modpackFolder);
        string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

        List<ModInfo> modList = new();
        if (File.Exists(jsonPath))
        {
            try { modList = JsonSerializer.Deserialize<List<ModInfo>>(File.ReadAllText(jsonPath)) ?? new List<ModInfo>(); }
            catch { modList = new List<ModInfo>(); }
        }

        if (!modList.Any(m => m.Name == mod.Name || (!string.IsNullOrEmpty(m.ProjectId) && m.ProjectId == mod.ProjectId)))
        {
            modList.Add(mod);

            var queueItem = new DownloadEditPackQueueItemViewModel(OnQueueItemDeleteClicked)
            {
                Name = mod.Name ?? "",
                ModInfo = mod
            };

            QueueList.Add(queueItem);
            _ = LoadQueueItemImageAsync(queueItem, mod.ImageURL ?? "");

            string newJson = JsonSerializer.Serialize(modList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, newJson);

            if (!silent)
            {
                var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                    ((App)global::Avalonia.Application.Current!).ServiceProvider!);
                dialog.ShowMessage(string.Format(LocalizationManager.GetString("Modpacks.ModAddedSuccessDesc", "Ура! Мод \"{0}\" успішно додано!"), mod.Name), "Готово!");
            }
        }
        else
        {
            if (!silent)
            {
                var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                    ((App)global::Avalonia.Application.Current!).ServiceProvider!);
                dialog.ShowMessage(string.Format(LocalizationManager.GetString("Modpacks.ModAlreadyInListDesc", "Мод \"{0}\" вже є в списку."), mod.Name), "Вже є");
            }
        }
    }

    private async Task LoadQueueItemImageAsync(DownloadEditPackQueueItemViewModel item, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            var bitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(url, 32);
            if (bitmap != null)
            {
                Dispatcher.UIThread.Post(() => item.IconBitmap = bitmap);
            }
        }
        catch { }
    }

    private void OnQueueItemDeleteClicked(DownloadEditPackQueueItemViewModel item)
    {
        QueueList.Remove(item);

        string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name ?? "Unknown");
        string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

        if (File.Exists(jsonPath))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ModInfo>>(File.ReadAllText(jsonPath)) ?? new List<ModInfo>();
                list = list.Where(m => m.Name != item.Name).ToList();
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    private void LoadQueueFromJson()
    {
        string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name ?? "Unknown");
        string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

        if (File.Exists(jsonPath))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ModInfo>>(File.ReadAllText(jsonPath)) ?? new List<ModInfo>();
                foreach (var mod in list)
                {
                    var queueItem = new DownloadEditPackQueueItemViewModel(OnQueueItemDeleteClicked)
                    {
                        Name = mod.Name ?? "",
                        ModInfo = mod
                    };
                    QueueList.Add(queueItem);
                    _ = LoadQueueItemImageAsync(queueItem, mod.ImageURL ?? "");
                }
            }
            catch { }
        }
    }

    [RelayCommand]
    private async Task InstallSelectedContentAsync()
    {
        AvaloniaSoundService.Click();

        string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name ?? "Unknown");
        string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

        if (!File.Exists(jsonPath) || QueueList.Count == 0)
        {
            var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(
                ((App)global::Avalonia.Application.Current!).ServiceProvider!);
            dialog.ShowMessage(LocalizationManager.GetString("Modpacks.NoModsToDownloadDesc", "Немає модів для завантаження."), "Пусто");
            return;
        }

        var app = (App)global::Avalonia.Application.Current!;
        var desktop = (global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;

        var progressDialog = new Views.Dialogs.DownloadProgressDialog();
        var progressVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DownloadProgressViewModel>(app.ServiceProvider!);
        progressDialog.SetViewModel(progressVm);
        _ = progressDialog.ShowDialog(desktop.MainWindow!);

        try
        {
            string json = await File.ReadAllTextAsync(jsonPath);
            var mods = JsonConvert.DeserializeObject<List<ModInfo>>(json) ?? new List<ModInfo>();

            int total = mods.Count;
            int completed = 0;

            foreach (var mod in mods)
            {
                string subFolder = mod.Type switch
                {
                    "mod" => "mods",
                    "shader" => "shaderpacks",
                    "resourcepack" => "resourcepacks",
                    "map" => "saves",
                    _ => "mods"
                };

                string basePath = CurrentModpack.Path ?? "";
                string targetDir = Path.Combine(basePath, subFolder);

                bool hasOverrides = Directory.Exists(Path.Combine(basePath, "overrides"));
                bool hasOverride = Directory.Exists(Path.Combine(basePath, "override"));

                if (hasOverrides) targetDir = Path.Combine(basePath, "overrides", subFolder);
                else if (hasOverride) targetDir = Path.Combine(basePath, "override", subFolder);
                else if (Directory.Exists(Path.Combine(basePath, ".minecraft"))) targetDir = Path.Combine(basePath, ".minecraft", subFolder);

                Directory.CreateDirectory(targetDir);
                string fileName = Path.GetFileName(mod.Url ?? "");
                string filePath = Path.Combine(targetDir, fileName);

                progressVm.DowloadProgressBarFileTask(total, completed, fileName);

                if (!File.Exists(filePath))
                {
                    bool success = await DownloadFileWithProgressAsync(mod.Url ?? "", filePath, progressVm);
                    if (success && mod.Type == "map" && filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        CL.Core.Helpers.ZipHelper.ExtractMap(filePath, targetDir);
                    }
                    if (!success && !string.IsNullOrEmpty(mod.Url))
                    {

                        UrlOpener.Open(mod.Url);
                    }
                }
                completed++;
            }

            File.Delete(jsonPath);
            progressVm.CloseAction?.Invoke();

            var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(app.ServiceProvider!);
            dialog.ShowMessage(LocalizationManager.GetString("Modpacks.AllModsDownloadedDesc", "Всі моди успішно завантажені!"), "Готово!");

            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            progressVm.CloseAction?.Invoke();
            var dialog = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<CL.Core.Interfaces.IDialogService>(app.ServiceProvider!);
            dialog.ShowMessage($"Помилка: {ex.Message}", "Збій");
        }
    }

    private async Task<bool> DownloadFileWithProgressAsync(string url, string savePath, DownloadProgressViewModel progressVm)
    {
        try
        {
            using var response = await WebHelper.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[8192];
            int bytesRead;
            long totalRead = 0;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                totalRead += bytesRead;
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                if (totalBytes > 0)
                {
                    int percent = (int)(totalRead * 100 / totalBytes);
                    Dispatcher.UIThread.Post(() => progressVm.DowloadProgressBarFile(percent));
                }
            }
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
        CloseAction = null;
        ScrollToTopRequested = null;
    }
}
