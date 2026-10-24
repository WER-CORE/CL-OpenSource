using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using Newtonsoft.Json;
using Optifine.Installer;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class CreateModpackViewModel : ObservableObject, IDisposable
{
    private readonly ModDownloadService _modDownloadService;
    private readonly ModpackService _modpackService;
    private readonly IDialogService _dialogService;

    private const int ITEMS_PER_PAGE = 10;
    private CancellationTokenSource? _searchCts;
    private int _selectMod = 0;

    private readonly List<ModInfo> _tempModList = new();
    private readonly List<string> _iconUrls = new();
    private ModSearchResult? _currentModToInstall;
    private List<ModVersionInfo>? _currentAvailableVersions;

    public Action? CloseAction { get; set; }
    public event Action? ScrollToTopRequested;

    [ObservableProperty]
    private string _modpackName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _minecraftVersions = new();

    [ObservableProperty]
    private string? _selectedMinecraftVersion;

    [ObservableProperty]
    private ObservableCollection<string> _loaderTypes = new()
    {
        "Vanilla",
        "Forge",
        "NeoForge",
        "Fabric",
        "Quilt",
        "LiteLoader",
        "Optifine"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVanilla))]
    [NotifyPropertyChangedFor(nameof(CanInstallMods))]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    private string _selectedLoaderType = "Forge";

    public bool IsVanilla => SelectedLoaderType == "Vanilla";
    public bool CanInstallMods => !IsVanilla;
    public string DialogTitle => IsVanilla
        ? LocalizationManager.GetString("Modpacks.CreateVanillaTitle", "Створення збірки - Ванільної збірки (Ваніла)")
        : LocalizationManager.GetString("Modpacks.CreateModpackTitle", "Створення збірки - Модової збірки (Forge,Fabric,...)");

    [ObservableProperty] private string _modpackNameLabel = string.Empty;
    [ObservableProperty] private string _modpackNamePlaceholder = string.Empty;
    [ObservableProperty] private string _minecraftVersionLabel = string.Empty;
    [ObservableProperty] private string _loaderTypeLabel = string.Empty;
    [ObservableProperty] private string _loaderVersionLabel = string.Empty;
    [ObservableProperty] private string _tabModsText = string.Empty;
    [ObservableProperty] private string _tabResourcePacksText = string.Empty;
    [ObservableProperty] private string _tabShadersText = string.Empty;
    [ObservableProperty] private string _tabDatapacksText = string.Empty;
    [ObservableProperty] private string _tabMapsText = string.Empty;
    [ObservableProperty] private string _searchPlaceholder = string.Empty;
    [ObservableProperty] private string _searchToolTip = string.Empty;
    [ObservableProperty] private string _searchResultsTitle = string.Empty;
    [ObservableProperty] private string _selectedModsTitle = string.Empty;
    [ObservableProperty] private string _createButtonText = string.Empty;
    [ObservableProperty] private string _addButtonText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _loaderVersions = new();

    [ObservableProperty]
    private string? _selectedLoaderVersion;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedTab = "Mods";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModrinthOpacity))]
    private bool _isModrinthSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurseForgeOpacity))]
    private bool _isCurseForgeSelected = false;

    public double ModrinthOpacity => IsModrinthSelected ? 1.0 : 0.5;
    public double CurseForgeOpacity => IsCurseForgeSelected ? 1.0 : 0.5;

    [ObservableProperty]
    private bool _isMenuInstallerVisible = false;

    [ObservableProperty]
    private string _selectedModName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _selectedModVersions = new();

    [ObservableProperty]
    private string? _selectedModVersion;

    [ObservableProperty]
    private bool _isLoadingVersions = false;

    [ObservableProperty]
    private bool _isLoadingMods = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageNumber))]
    [NotifyPropertyChangedFor(nameof(HasPrevPage))]
    private int _currentPage = 0;

    public int PageNumber => CurrentPage + 1;
    public bool HasPrevPage => CurrentPage > 0;

    [ObservableProperty]
    private bool _hasNextPage = false;

    [ObservableProperty]
    private ObservableCollection<ModSearchCardViewModel> _searchResults = new();

    [ObservableProperty]
    private ObservableCollection<SelectedModItemViewModel> _selectedMods = new();

    public CreateModpackViewModel(ModDownloadService modDownloadService, ModpackService modpackService, IDialogService dialogService)
    {
        _modDownloadService = modDownloadService;
        _modpackService = modpackService;
        _dialogService = dialogService;

        ApplyLocalization();
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageConfigurationChanged;
        _ = LoadMinecraftVersionsAsync();
    }

    private void OnLanguageConfigurationChanged()
    {
        Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public void Cleanup()
    {
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageConfigurationChanged;
    }

    public void Dispose() => Cleanup();

    public void ApplyLocalization()
    {
        ModpackNameLabel = LocalizationManager.GetString("Modpacks.ModpackNameTitle", "Назва збірки");
        ModpackNamePlaceholder = LocalizationManager.GetString("Modpacks.ModpackNamePlaceholder", "Напишіть назву...");
        MinecraftVersionLabel = LocalizationManager.GetString("Modpacks.ModpackMcVersion", "Версія Minecraft");
        LoaderTypeLabel = LocalizationManager.GetString("Modpacks.LoaderTypeTitle", LocalizationManager.GetString("Modpacks.ModpackLoaderType", "Тип ядра"));
        LoaderVersionLabel = LocalizationManager.GetString("Modpacks.ModpackLoaderVersion", "Версія ядра");
        TabModsText = LocalizationManager.GetString("Modpacks.ModpackEditMods", "Моди");
        TabResourcePacksText = LocalizationManager.GetString("Modpacks.ModpackEditResourcePacks", "Ресурс-паки");
        TabShadersText = LocalizationManager.GetString("Modpacks.ModpackEditShaders", "Шейдери");
        TabDatapacksText = LocalizationManager.GetString("Modpacks.ModpackEditDatapacks", "Датапаки");
        TabMapsText = LocalizationManager.GetString("Modpacks.ModpackEditMaps", "Карти");
        SearchPlaceholder = LocalizationManager.GetString("Modpacks.ModSearchPlaceholder", "Пошук...");
        SearchToolTip = LocalizationManager.GetString("Modpacks.SearchToolTip", "Введіть назву для пошуку");
        SearchResultsTitle = LocalizationManager.GetString("Modpacks.SearchResultsTitle", "Результати пошуку");
        SelectedModsTitle = LocalizationManager.GetString("Modpacks.SelectedModsTitle", "Обрані моди (Ваша збірка)");
        CreateButtonText = LocalizationManager.GetString("Modpacks.CreateModpackBtn", "СТВОРИТИ ЗБІРКУ");
        AddButtonText = LocalizationManager.GetString("Modpacks.ModAddBtn", "Додати");
        OnPropertyChanged(nameof(DialogTitle));
    }

    private string GetEffectiveLauncherPath()
    {
        string path = SettingsManager.Default.PathLacunher;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = PlatformPaths.DefaultLauncherPath();
        }
        return path;
    }

    private async Task LoadMinecraftVersionsAsync()
    {
        IsLoadingVersions = true;
        try
        {
            MinecraftVersions.Clear();
            string launcherPath = GetEffectiveLauncherPath();
            var path = new MinecraftPath(launcherPath);
            var launcher = new MinecraftLauncher(path);

            if (SelectedLoaderType == "Fabric")
            {
                var fabricInstaller = new FabricInstaller(WebHelper.Client);
                var versions = await fabricInstaller.GetSupportedVersionNames();
                foreach (var version in versions) MinecraftVersions.Add(version);
            }
            else if (SelectedLoaderType == "Quilt")
            {
                var quiltInstaller = new QuiltInstaller(WebHelper.Client);
                var versions = await quiltInstaller.GetSupportedVersionNames();
                foreach (var version in versions) MinecraftVersions.Add(version);
            }
            else if (SelectedLoaderType == "LiteLoader")
            {
                var liteLoaderInstaller = new LiteLoaderInstaller(WebHelper.Client);
                var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                var gameVersions = loaders.Select(x => x.BaseVersion).OfType<string>().Distinct().OrderByDescending(v => Version.TryParse(v, out var ver) ? ver : new Version(0, 0, 0)).ToList();
                foreach (var version in gameVersions) MinecraftVersions.Add(version);
            }
            else if (SelectedLoaderType == "Optifine")
            {
                var optifineInstaller = new OptifineInstaller(WebHelper.Client);
                var optifineVersions = await optifineInstaller.GetOptifineVersionsAsync();
                var gameVersions = optifineVersions.Select(x => x.MinecraftVersion).Distinct().OrderByDescending(v => Version.TryParse(v, out var ver) ? ver : new Version(0, 0, 0)).ToList();
                foreach (var version in gameVersions) MinecraftVersions.Add(version);
            }
            else
            {
                var allVersions = await launcher.GetAllVersionsAsync();
                foreach (var ver in allVersions)
                {
                    if (ver.Type == "release")
                    {
                        if (SelectedLoaderType == "NeoForge" && ver.Name == "1.20.1") break;
                        if (SelectedLoaderType == "Forge" && ver.Name == "1.7.10") break;
                        MinecraftVersions.Add(ver.Name);
                    }
                }
            }

            if (MinecraftVersions.Count > 0)
                SelectedMinecraftVersion = MinecraftVersions[0];
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    partial void OnSelectedLoaderTypeChanged(string? oldValue, string newValue)
    {
        _tempModList.Clear();
        _iconUrls.Clear();
        RefreshSelectedModsUI();

        _ = LoadMinecraftVersionsAsync();

        if (IsVanilla && (SelectedTab == "Mods" || SelectedTab == "Shaders" || SelectedTab == "Datapacks"))
        {
            SelectTab("ResourcePacks");
        }
        else
        {
            CurrentPage = 0;
            _ = SearchModsAsync();
        }
    }

    partial void OnSelectedMinecraftVersionChanged(string? oldValue, string? newValue)
    {
        if (newValue != null)
        {
            _tempModList.Clear();
            _iconUrls.Clear();
            RefreshSelectedModsUI();

            _ = UpdateLoaderVersionsAsync();
            CurrentPage = 0;
            _ = SearchModsAsync();
        }
    }

    partial void OnSearchQueryChanged(string? oldValue, string newValue)
    {
        CurrentPage = 0;
        _ = SearchModsAsync();
    }

    private async Task UpdateLoaderVersionsAsync()
    {
        if (IsVanilla || string.IsNullOrEmpty(SelectedMinecraftVersion))
        {
            LoaderVersions.Clear();
            SelectedLoaderVersion = null;
            return;
        }

        LoaderVersions.Clear();
        IsLoadingVersions = true;

        try
        {
            string gameVersion = SelectedMinecraftVersion;

            if (SelectedLoaderType == "Forge")
            {
                var versionLoader = new ForgeVersionLoader(WebHelper.Client);
                var forgeList = await versionLoader.GetForgeVersions(gameVersion);
                foreach (var forge in forgeList) LoaderVersions.Add(forge.ForgeVersionName);
            }
            else if (SelectedLoaderType == "Fabric")
            {
                var fabricInstaller = new FabricInstaller(WebHelper.Client);
                var fabricVersions = await fabricInstaller.GetLoaders(gameVersion);
                foreach (var fabric in fabricVersions) if (!string.IsNullOrEmpty(fabric.Version)) LoaderVersions.Add(fabric.Version);
            }
            else if (SelectedLoaderType == "Quilt")
            {
                var quiltInstaller = new QuiltInstaller(WebHelper.Client);
                var quiltVersions = await quiltInstaller.GetLoaders(gameVersion);
                foreach (var quilt in quiltVersions) if (!string.IsNullOrEmpty(quilt.Version)) LoaderVersions.Add(quilt.Version);
            }
            else if (SelectedLoaderType == "NeoForge")
            {
                string launcherPath = GetEffectiveLauncherPath();
                var path = new MinecraftPath(launcherPath);
                var launcher = new MinecraftLauncher(path);
                var versionLoader = new NoAdNeoForgeInstaller(launcher);
                var neoForgeList = await versionLoader.GetNeoForgeVersions(gameVersion);
                foreach (var neo in neoForgeList) LoaderVersions.Add(neo.VersionName);
            }
            else if (SelectedLoaderType == "LiteLoader")
            {
                var liteLoaderInstaller = new LiteLoaderInstaller(WebHelper.Client);
                var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                foreach (var loader in loaders.Where(l => l.BaseVersion == gameVersion && !string.IsNullOrEmpty(l.Version))) LoaderVersions.Add(loader.Version!);
            }
            else if (SelectedLoaderType == "Optifine")
            {
                var loader = new OptifineInstaller(WebHelper.Client);
                var versions = await loader.GetOptifineVersionsAsync();
                foreach (var v in versions.Where(v => v.MinecraftVersion == gameVersion && !string.IsNullOrEmpty(v.Version))) LoaderVersions.Add(v.Version!);
            }

            if (LoaderVersions.Count > 0)
                SelectedLoaderVersion = LoaderVersions[0];
            else
                _dialogService?.ShowMessage("Версій ядра не знайдено", "Інформація", MascotEmotion.Alert);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateLoaderVersionsAsync Error: {ex.Message}");
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        AvaloniaSoundService.Click();
        SelectedTab = tabName;
        _selectMod = tabName switch
        {
            "Mods" => 0,
            "Shaders" => 1,
            "ResourcePacks" => 2,
            "Maps" => 3,
            "Datapacks" => 4,
            _ => 0
        };

        CurrentPage = 0;
        RefreshSelectedModsUI();
        _ = SearchModsAsync();
    }

    [RelayCommand]
    private void SelectProvider(string provider)
    {
        AvaloniaSoundService.Click();

        if (provider == "Modrinth")
        {
            if (_selectMod == 3)
            {
                _dialogService?.ShowMessage("На жаль, сайт Modrinth не має бази карт (світів). Шукайте їх через CurseForge!", "Увага", MascotEmotion.Confused);
                return;
            }

            if (IsModrinthSelected) return;

            IsModrinthSelected = true;
            IsCurseForgeSelected = false;
        }
        else if (provider == "CurseForge")
        {
            if (IsCurseForgeSelected) return;

            IsModrinthSelected = false;
            IsCurseForgeSelected = true;
        }

        CurrentPage = 0;
        _ = SearchModsAsync();
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (CurrentPage > 0)
        {
            AvaloniaSoundService.Click();
            CurrentPage--;
            ScrollToTopRequested?.Invoke();
            await SearchModsAsync();
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (HasNextPage)
        {
            AvaloniaSoundService.Click();
            CurrentPage++;
            ScrollToTopRequested?.Invoke();
            await SearchModsAsync();
        }
    }

    public async Task SearchModsAsync()
    {
        if (string.IsNullOrEmpty(SelectedMinecraftVersion) || (IsVanilla && _selectMod == 0)) return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        SearchResults.Clear();
        IsLoadingMods = true;

        try
        {
            string site = IsModrinthSelected ? "Modrinth" : "CurseForge";
            string loader = IsVanilla ? "Vanilla" : SelectedLoaderType;
            int offset = CurrentPage * ITEMS_PER_PAGE;

            var results = await _modDownloadService.SearchModsAsync(
                SearchQuery ?? string.Empty,
                site,
                loader,
                _selectMod,
                offset,
                SelectedMinecraftVersion
            );

            if (token.IsCancellationRequested) return;

            if (results != null && results.Count > 0)
            {
                HasNextPage = results.Count >= ITEMS_PER_PAGE;
                foreach (var mod in results)
                {
                    SearchResults.Add(new ModSearchCardViewModel(mod, OpenVersionSelectorAsync, _selectMod));
                }
            }
            else
            {
                HasNextPage = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching mods: {ex.Message}");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingMods = false;
                ScrollToTopRequested?.Invoke();
            }
        }
    }

    public async Task OpenVersionSelectorAsync(ModSearchResult mod)
    {
        AvaloniaSoundService.Click();

        if (string.IsNullOrEmpty(SelectedMinecraftVersion))
        {
            _dialogService?.ShowMessage(
                LocalizationManager.GetString("Modpacks.NeedMinecraftVersion", "Спочатку оберіть версію Minecraft!"),
                LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                MascotEmotion.Alert);
            return;
        }

        _currentModToInstall = mod;
        SelectedModName = mod.Title;
        SelectedModVersions.Clear();

        try
        {
            var allVersions = await _modDownloadService.GetVersionsAsync(
                mod,
                SelectedMinecraftVersion,
                _selectMod == 0 ? SelectedLoaderType : null
            );
            string gameVersion = SelectedMinecraftVersion;

            _currentAvailableVersions = allVersions
                .Where(v => (v.GameVersions.Contains(gameVersion) || v.GameVersions.Count == 0) &&
                            (_selectMod != 0 || IsLoaderCompatible(v.Loaders, SelectedLoaderType)))
                .ToList();

            if (_currentAvailableVersions.Count > 0)
            {
                foreach (var v in _currentAvailableVersions)
                {
                    SelectedModVersions.Add(v.VersionName);
                }
                SelectedModVersion = SelectedModVersions[0];
                IsMenuInstallerVisible = true;
            }
            else
            {
                _dialogService?.ShowMessage(
                    LocalizationManager.GetString("Modpacks.NoVersionsForModpack", "Не знайдено версій для вашої збірки."),
                    LocalizationManager.GetString("Dialogs.EmptyTitle", "Пусто"),
                    MascotEmotion.Confused);
                IsMenuInstallerVisible = false;
            }
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            IsMenuInstallerVisible = false;
        }
    }

    private static bool IsLoaderCompatible(List<string>? versionLoaders, string? targetLoader)
    {
        if (string.IsNullOrEmpty(targetLoader)) return true;
        if (versionLoaders == null || versionLoaders.Count == 0) return true;

        if (versionLoaders.Any(l => l.Equals(targetLoader, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (targetLoader.Equals("Quilt", StringComparison.OrdinalIgnoreCase) &&
            versionLoaders.Any(l => l.Equals("Fabric", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (targetLoader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) &&
            versionLoaders.Any(l => l.Equals("Forge", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    [RelayCommand]
    private async Task AddModAsync()
    {
        AvaloniaSoundService.Click();

        if (string.IsNullOrEmpty(SelectedModVersion) || _currentModToInstall == null || _currentAvailableVersions == null) return;

        var selectedVerInfo = _currentAvailableVersions.FirstOrDefault(v => v.VersionName == SelectedModVersion);
        if (selectedVerInfo == null) return;

        var modInfo = new ModInfo
        {
            Name = _currentModToInstall.Title,
            ProjectId = selectedVerInfo.ModId,
            FileId = selectedVerInfo.VersionId,
            Loader = SelectedLoaderType,
            Version = SelectedMinecraftVersion,
            Url = selectedVerInfo.DownloadUrl,
            LoaderType = SelectedLoaderType,
            Type = _selectMod switch { 0 => "mod", 1 => "shader", 2 => "resourcepack", 3 => "map", 4 => "datapack", _ => "mod" },
            ImageURL = _currentModToInstall.IconUrl,
            Slug = _currentModToInstall.Slug,
            FileName = selectedVerInfo.FileName
        };

        if (_tempModList.Any(m => m.ProjectId == modInfo.ProjectId))
        {
            _dialogService?.ShowMessage(
                LocalizationManager.GetString("Modpacks.ModInListAlready", "Цей мод вже є в списку."),
                LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                MascotEmotion.Alert);
            return;
        }

        _tempModList.Add(modInfo);
        if (!string.IsNullOrEmpty(modInfo.ImageURL))
        {
            _iconUrls.Add(modInfo.ImageURL);
        }

        RefreshSelectedModsUI();

        _dialogService?.ShowMessage(
            string.Format(LocalizationManager.GetString("Modpacks.ModAddedQueue", "Мод \"{0}\" додано до черги!"), modInfo.Name),
            LocalizationManager.GetString("Dialogs.Success", "Успіх"),
            MascotEmotion.Happy);

        IsMenuInstallerVisible = false;

        if (_selectMod == 0)
        {
            try
            {
                var dependencies = await _modDownloadService.GetDependenciesModInfoAsync(selectedVerInfo, SelectedLoaderType, 0);
                if (dependencies != null && dependencies.Count > 0)
                {
                    int addedCount = 0;
                    foreach (var dep in dependencies)
                    {
                        if (!_tempModList.Any(m => m.ProjectId == dep.ProjectId))
                        {
                            _tempModList.Add(dep);
                            if (!string.IsNullOrEmpty(dep.ImageURL)) _iconUrls.Add(dep.ImageURL);
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        RefreshSelectedModsUI();

                        _dialogService?.ShowMessage(
                            string.Format(LocalizationManager.GetString("Modpacks.ModpackDepsAdded", "Автоматично додано {0} залежних модів (наприклад, Sodium/FabricAPI)."), addedCount),
                            LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                            MascotEmotion.Happy);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка додавання залежностей: {ex.Message}");
            }
        }
    }

    private void RefreshSelectedModsUI()
    {
        SelectedMods.Clear();
        string currentTypeStr = _selectMod switch { 0 => "mod", 1 => "shader", 2 => "resourcepack", 3 => "map", 4 => "datapack", _ => "mod" };
        var filtered = _tempModList.Where(m => m.Type == currentTypeStr).ToList();

        foreach (var m in filtered)
        {
            SelectedMods.Add(new SelectedModItemViewModel(m, RemoveSelectedMod));
        }
    }

    private void RemoveSelectedMod(SelectedModItemViewModel item)
    {
        AvaloniaSoundService.Click();
        SelectedMods.Remove(item);
        var toRemove = _tempModList.FirstOrDefault(m => m.ProjectId == item.Mod.ProjectId);
        if (toRemove != null)
        {
            _tempModList.Remove(toRemove);
        }
    }

    [RelayCommand]
    private void CloseMenuInstaller()
    {
        AvaloniaSoundService.Click();
        IsMenuInstallerVisible = false;
    }

    [RelayCommand]
    private async Task CreateModpackAsync()
    {
        try
        {
            AvaloniaSoundService.Click();

            string name = ModpackName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                _dialogService?.ShowMessage(
                    LocalizationManager.GetString("Modpacks.ModpackNameEmpty", "Введіть назву збірки!"),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
                return;
            }

            if (!IsVanilla && string.IsNullOrEmpty(SelectedLoaderVersion))
            {
                _dialogService?.ShowMessage(
                    LocalizationManager.GetString("Modpacks.LoaderVersionNotSelected", "Дочекайтесь завантаження версії ядра (Loader Version)!"),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
                return;
            }

            string launcherRoot = GetEffectiveLauncherPath();
            string basePath = Path.Combine(launcherRoot, "CLModpack", name);
            string pathJson = Path.Combine(basePath, "modpack.json");

            Directory.CreateDirectory(basePath);

            string newJson = JsonConvert.SerializeObject(_tempModList, Formatting.Indented);
            await File.WriteAllTextAsync(pathJson, newJson);

            string iconDestPath = Path.Combine(basePath, "icon.png");
            string imageUrl = _iconUrls.FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrEmpty(imageUrl) && (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    byte[] iconBytes = await WebHelper.Client.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(iconDestPath, iconBytes);
                    imageUrl = iconDestPath;
                }
                catch
                {
                    imageUrl = "avares://CL.Avalonia/Icon/IconCL(Common).webp";
                }
            }
            else
            {
                imageUrl = "avares://CL.Avalonia/Icon/IconCL(Common).webp";
            }

            var modpack = new InstalledModpack
            {
                Name = name,
                TypeSite = "Custom",
                MinecraftVersion = SelectedMinecraftVersion ?? "Unknown",
                LoaderType = IsVanilla ? "Vanilla" : SelectedLoaderType,
                LoaderVersion = IsVanilla ? "Latest" : (SelectedLoaderVersion ?? "Unknown"),
                Path = basePath,
                PathJson = pathJson,
                UrlImage = imageUrl
            };

            _modpackService.AddModpack(modpack);

            _dialogService?.ShowMessage(
                IsVanilla
                    ? LocalizationManager.GetString("Modpacks.VanillaPackCreatedSuccess", "Ванільну збірку успішно створено!")
                    : LocalizationManager.GetString("Modpacks.ModpackCreatedSuccess", "Збірку успішно створено!"),
                LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                MascotEmotion.Happy);

            CloseDialog();
        }
        catch (Exception ex)
        {
            _dialogService?.ShowMessage(
                string.Format(LocalizationManager.GetString("Modpacks.ModpackCreateFatalError", "Критична помилка створення: {0}"), ex.Message),
                LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                MascotEmotion.Sad);
        }
    }

    [RelayCommand]
    private void CloseDialog()
    {
        AvaloniaSoundService.Click();
        CloseAction?.Invoke();
    }
}
