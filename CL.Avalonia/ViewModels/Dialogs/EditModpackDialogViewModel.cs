using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CL.Core.Models;
using CL.Core.Services;
using CL.Core.Interfaces;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using Optifine.Installer;
using CmlLib.Core;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class EditModpackDialogViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogService;
    private readonly ModpackService _modpackService;
    private ModpackInfo _modpack = null!;

    public Action? CloseAction { get; set; }

    [RelayCommand]
    private void CloseWindow() => CloseAction?.Invoke();

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public ObservableCollection<EditModpackItemViewModel> ItemsList { get; } = new();

    [ObservableProperty]
    private bool _isVanilla;

    [ObservableProperty] private string _minecraftVersion = string.Empty;
    [ObservableProperty] private string _loaderType = string.Empty;
    [ObservableProperty] private string _loaderVersion = string.Empty;
    [ObservableProperty] private bool _isConsoleLogOpened;
    [ObservableProperty] private int _ram;
    [ObservableProperty] private int _maxRam = 8192;
    [ObservableProperty] private string _javaPath = string.Empty;
    [ObservableProperty] private int _windowWidth = 800;
    [ObservableProperty] private int _windowHeight = 600;
    [ObservableProperty] private bool _enterInServer;
    [ObservableProperty] private string _serverIp = string.Empty;
    [ObservableProperty] private Bitmap? _modpackIcon;

    public ObservableCollection<string> AvailableLoaders { get; } = new()
    { "Vanilla", "Forge", "Fabric", "Quilt", "NeoForge", "Optifine", "LiteLoader" };

    public ObservableCollection<string> AvailableMcVersions { get; } = new();
    public ObservableCollection<string> AvailableLoaderVersions { get; } = new();
    public ObservableCollection<string> AvailableJavaPaths { get; } = new();

    [ObservableProperty] private string _dialogTitle = string.Empty;
    [ObservableProperty] private string _tabMods = string.Empty;
    [ObservableProperty] private string _tabResourcePacks = string.Empty;
    [ObservableProperty] private string _tabShaders = string.Empty;
    [ObservableProperty] private string _tabMaps = string.Empty;
    [ObservableProperty] private string _tabOptions = string.Empty;
    [ObservableProperty] private string _searchPlaceholder = string.Empty;
    [ObservableProperty] private string _downloadBtnText = string.Empty;
    [ObservableProperty] private string _addFileBtnText = string.Empty;

    [ObservableProperty] private string _versionSectionTitle = string.Empty;
    [ObservableProperty] private string _mcVersionTitle = string.Empty;
    [ObservableProperty] private string _loaderTypeTitle = string.Empty;
    [ObservableProperty] private string _loaderVersionTitle = string.Empty;
    [ObservableProperty] private string _systemSectionTitle = string.Empty;
    [ObservableProperty] private string _logsTitle = string.Empty;
    [ObservableProperty] private string _ramTitle = string.Empty;
    [ObservableProperty] private string _javaPathTitle = string.Empty;
    [ObservableProperty] private string _javaPathDesc = string.Empty;
    [ObservableProperty] private string _gameSectionTitle = string.Empty;
    [ObservableProperty] private string _windowSizeTitle = string.Empty;
    [ObservableProperty] private string _autoJoinTitle = string.Empty;
    [ObservableProperty] private string _serverIpPlaceholder = string.Empty;
    [ObservableProperty] private string _resetOptionsTitle = string.Empty;
    [ObservableProperty] private string _resetOptionsDesc = string.Empty;
    [ObservableProperty] private string _resetBtnText = string.Empty;
    [ObservableProperty] private string _repairTitle = string.Empty;
    [ObservableProperty] private string _repairDesc = string.Empty;
    [ObservableProperty] private string _repairBtnText = string.Empty;
    [ObservableProperty] private string _customizationSectionTitle = string.Empty;
    [ObservableProperty] private string _modpackIconTitle = string.Empty;
    [ObservableProperty] private string _changeIconBtnText = string.Empty;
    [ObservableProperty] private string _browseBtnText = string.Empty;

    public EditModpackDialogViewModel(IDialogService dialogService, ModpackService modpackService)
    {
        _dialogService = dialogService;
        _modpackService = modpackService;
        ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        DialogTitle = LocalizationManager.GetString("Modpacks.ModpackEditTitle", "Налаштування збірки");
        TabMods = LocalizationManager.GetString("Modpacks.ModpackEditMods", "Моди");
        TabResourcePacks = LocalizationManager.GetString("Modpacks.ModpackEditResourcePacks", "Ресурспаки");
        TabShaders = LocalizationManager.GetString("Modpacks.ModpackEditShaders", "Шейдери");
        TabMaps = LocalizationManager.GetString("Modpacks.ModpackEditMaps", "Світи");
        TabOptions = LocalizationManager.GetString("Modpacks.ModpackEditOptions", "Опції");
        SearchPlaceholder = LocalizationManager.GetString("Modpacks.ModSearchPlaceholder", "Пошук файлів...");
        DownloadBtnText = LocalizationManager.GetString("Modpacks.ModpackDownloadBtn", "Завантажити");
        AddFileBtnText = LocalizationManager.GetString("Modpacks.ModpackAddFileBtn", "Додати файл");

        VersionSectionTitle = LocalizationManager.GetString("Modpacks.ModpackVersionTitle", "Версія");
        McVersionTitle = LocalizationManager.GetString("Modpacks.ModpackMcVersion", "Версія Minecraft");
        LoaderTypeTitle = LocalizationManager.GetString("Modpacks.ModpackLoaderType", "Тип Ядра");
        LoaderVersionTitle = LocalizationManager.GetString("Modpacks.ModpackLoaderVersion", "Версія Ядра");
        SystemSectionTitle = LocalizationManager.GetString("Modpacks.ModpackSystemTitle", "Система");
        LogsTitle = LocalizationManager.GetString("Modpacks.ModpackLogs", "Консоль розробника (Logs)");
        RamTitle = LocalizationManager.GetString("Modpacks.ModpackRam", "Оперативна пам’ять");
        JavaPathTitle = LocalizationManager.GetString("Modpacks.ModpackJavaPath", "Кастомний шлях до Java");
        JavaPathDesc = LocalizationManager.GetString("Modpacks.ModpackJavaPathDesc", "Залиште пустим для авто-пошуку");
        GameSectionTitle = LocalizationManager.GetString("Modpacks.ModpackGameTitle", "Гра");
        WindowSizeTitle = LocalizationManager.GetString("Modpacks.ModpackWindowSize", "Розмір вікна");
        AutoJoinTitle = LocalizationManager.GetString("Modpacks.ModpackAutoJoin", "Авто-вхід на сервер");
        ServerIpPlaceholder = LocalizationManager.GetString("Modpacks.ModpackServerIp", "IP Сервера");
        ResetOptionsTitle = LocalizationManager.GetString("Modpacks.ModpackResetOptionsTitle", "Скинути налаштування гри");
        ResetOptionsDesc = LocalizationManager.GetString("Modpacks.ModpackResetOptionsDesc", "Видаляє options.txt (допомагає, якщо крашиться гра)");
        ResetBtnText = LocalizationManager.GetString("Modpacks.ModpackResetBtn", "Скинути");
        RepairTitle = LocalizationManager.GetString("Modpacks.RepairModpackTitle", "Відновити збірку");
        RepairDesc = LocalizationManager.GetString("Modpacks.RepairModpackDesc", "Завантажує відсутні моди з маніфесту");
        RepairBtnText = LocalizationManager.GetString("Modpacks.RepairModpackBtn", "Відновити");
        CustomizationSectionTitle = LocalizationManager.GetString("Modpacks.ModpackCustomizationTitle", "Кастомізація");
        ModpackIconTitle = LocalizationManager.GetString("Modpacks.ModpackIcon", "Іконка збірки");
        ChangeIconBtnText = LocalizationManager.GetString("Modpacks.ModpackChangeBtn", "Змінити...");
        BrowseBtnText = LocalizationManager.GetString("Modpacks.ModpackBrowseBtn", "Огляд");
    }

    public async Task InitializeAsync(ModpackInfo modpack)
    {
        _modpack = modpack;
        MinecraftVersion = modpack.MinecraftVersion ?? "";
        LoaderType = modpack.LoaderType ?? "Vanilla";
        LoaderVersion = modpack.LoaderVersion ?? "";
        IsConsoleLogOpened = modpack.IsConsoleLogOpened;
        Ram = modpack.OPack > 0 ? modpack.OPack : 2048;
        MaxRam = 16384;
        JavaPath = modpack.JavaPath ?? "";
        WindowWidth = modpack.Wdith > 0 ? modpack.Wdith : 800;
        WindowHeight = modpack.Height > 0 ? modpack.Height : 600;
        EnterInServer = modpack.EnterInServer;
        ServerIp = modpack.ServerIP ?? "";

        CheckIsVanilla();
        LoadIcon();

        var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
        var launcher = new MinecraftLauncher(path);
        var allVersions = await launcher.GetAllVersionsAsync();
        foreach (var v in allVersions.Where(v => v.Type == "release").Select(v => v.Name))
            AvailableMcVersions.Add(v);

        var javas = JavaLocator.FindInstalled(SettingsManager.Default.PathLacunher);
        foreach (var j in javas) AvailableJavaPaths.Add(j);
        if (!string.IsNullOrEmpty(JavaPath) && !AvailableJavaPaths.Contains(JavaPath))
            AvailableJavaPaths.Add(JavaPath);

        if (!IsVanilla)
        {
            await LoadLoaderVersionsAsync();
            await RefreshListAsync();
        }
    }

    private void CheckIsVanilla()
    {
        IsVanilla = string.Equals(LoaderType, "Vanilla", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(LoaderType, "Vanila", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadIcon()
    {
        if (!string.IsNullOrEmpty(_modpack.UrlImage) && File.Exists(_modpack.UrlImage))
        {
            try
            {
                using var fs = File.OpenRead(_modpack.UrlImage);
                ModpackIcon = new Bitmap(fs);
            }
            catch { }
        }
    }

    [RelayCommand]
    private async Task DownloadModAsync()
    {
        AvaloniaSoundService.Click();
        var app = (App)global::Avalonia.Application.Current!;
        var desktop = (global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;

        var dialog = new Views.Dialogs.DownloadEditPackDialog();
        var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DownloadEditPackViewModel>(app.ServiceProvider!);

        vm.Initialize(_modpack, (byte)SelectedTabIndex);
        vm.CloseAction = () => dialog.Close();
        dialog.DataContext = vm;

        await dialog.ShowDialog(desktop.MainWindow!);

        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task SwitchTab(string indexStr)
    {
        int index = int.Parse(indexStr);

        if (IsVanilla && (index == 0 || index == 2))
        {
            _dialogService.ShowMessage(
                LocalizationManager.GetString("Modpacks.VanillaNoMods", "Ванільна версія не підтримує моди/шейдери."),
                "Увага");
            return;
        }

        SelectedTabIndex = index;
        if (index < 4)
            await RefreshListAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = RefreshListAsync();
    }

    private string GetModsBasePath()
    {
        string overridePath = Path.Combine(_modpack.Path, "override");
        string overridesPath = Path.Combine(_modpack.Path, "overrides");

        if (Directory.Exists(overridesPath))
            return overridesPath;
        if (Directory.Exists(overridePath))
            return overridePath;

        return _modpack.Path;
    }

    private async Task RefreshListAsync()
    {
        ItemsList.Clear();
        string folder = SelectedTabIndex switch
        {
            0 => "mods",
            1 => "resourcepacks",
            2 => "shaderpacks",
            3 => "saves",
            _ => "mods"
        };

        string baseModsPath = GetModsBasePath();
        string dir = Path.Combine(baseModsPath, folder);
        if (!Directory.Exists(dir)) return;

        string[] patterns = SelectedTabIndex switch
        {
            0 => new[] { "*.jar", "*.jar.disabled", "*.litemod", "*.litemod.disabled" },
            1 => new[] { "*.zip", "*.zip.disabled" },
            2 => new[] { "*.zip", "*.rar", "*.zip.disabled", "*.rar.disabled" },
            3 => new[] { "*.zip" },
            _ => new[] { "*.*" }
        };

        int tabIndex = SelectedTabIndex;

        string[] files = await Task.Run(() =>
        {
            if (tabIndex == 3)
            {
                var dirs = Directory.GetDirectories(dir);
                var zips = Directory.GetFiles(dir, "*.zip");
                return dirs.Concat(zips).ToArray();
            }
            else
            {
                return patterns.SelectMany(p => Directory.GetFiles(dir, p)).ToArray();
            }
        });

        var q = SearchQuery.ToLower();
        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            if (!string.IsNullOrEmpty(q) && !name.ToLower().Contains(q)) continue;

            bool isEnabled = !name.EndsWith(".disabled");
            var item = new EditModpackItemViewModel(OnItemToggled, OnItemDeleted)
            {
                Name = name.Replace(".disabled", ""),
                Description = isEnabled ? LocalizationManager.GetString("Modpacks.ModStateActive", "Активний")
                                        : LocalizationManager.GetString("Modpacks.ModStateDisabled", "Вимкнено"),
                FilePath = file,
                IsEnabled = isEnabled
            };
            ItemsList.Add(item);

            _ = Task.Run(async () =>
            {
                var meta = await ModMetadataReader.ReadMetadataAsync(file);
                if (meta != null)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!string.IsNullOrEmpty(meta.Name)) item.Name = meta.Name;
                        if (!string.IsNullOrEmpty(meta.Description)) item.Description = meta.Description;
                        if (meta.IconBytes != null)
                        {
                            try
                            {
                                using var ms = new System.IO.MemoryStream(meta.IconBytes);
                                item.Icon = new Bitmap(ms);
                            }
                            catch { }
                        }
                    });
                }
            });
        }
    }

    private void OnItemToggled(EditModpackItemViewModel item)
    {
        try
        {
            string oldPath = item.FilePath;
            string newPath;
            if (item.IsEnabled)
            {
                newPath = oldPath.EndsWith(".disabled") ? oldPath.Substring(0, oldPath.Length - 9) : oldPath;
                item.Description = LocalizationManager.GetString("Modpacks.ModStateActive", "Активний");
            }
            else
            {
                newPath = oldPath.EndsWith(".disabled") ? oldPath : oldPath + ".disabled";
                item.Description = LocalizationManager.GetString("Modpacks.ModStateDisabled", "Вимкнено");
            }

            if (oldPath != newPath && File.Exists(oldPath))
            {
                File.Move(oldPath, newPath);
                item.FilePath = newPath;
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage(ex.Message, "Помилка");
        }
    }

    private void OnItemDeleted(EditModpackItemViewModel item)
    {
        try
        {
            if (File.Exists(item.FilePath)) File.Delete(item.FilePath);
            else if (Directory.Exists(item.FilePath)) Directory.Delete(item.FilePath, true);
            ItemsList.Remove(item);
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage(ex.Message, "Помилка");
        }
    }

    private void SaveProperty(string propName, object value)
    {
        try
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "installed_modpacks.json");
            if (!File.Exists(jsonPath)) return;
            string json = File.ReadAllText(jsonPath);
            var modpacks = System.Text.Json.JsonSerializer.Deserialize<List<InstalledModpack>>(json);
            var target = modpacks?.FirstOrDefault(p => p.Name.Equals(_modpack.Name, StringComparison.OrdinalIgnoreCase));
            if (target == null) return;

            var property = typeof(InstalledModpack).GetProperty(propName);
            if (property == null || !property.CanWrite) return;

            property.SetValue(target, Convert.ChangeType(value, property.PropertyType));
            File.WriteAllText(jsonPath, System.Text.Json.JsonSerializer.Serialize(modpacks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            var localProp = typeof(ModpackInfo).GetProperty(propName);
            localProp?.SetValue(_modpack, Convert.ChangeType(value, localProp.PropertyType));
        }
        catch { }
    }

    partial void OnMinecraftVersionChanged(string value)
    {
        SaveProperty("MinecraftVersion", value);
        if (!IsVanilla) _ = LoadLoaderVersionsAsync();
    }
    partial void OnLoaderTypeChanged(string value)
    {
        SaveProperty("LoaderType", value);
        CheckIsVanilla();
        if (!IsVanilla) _ = LoadLoaderVersionsAsync();
    }
    partial void OnLoaderVersionChanged(string value) => SaveProperty("LoaderVersion", value);
    partial void OnIsConsoleLogOpenedChanged(bool value) => SaveProperty("IsConsoleLogOpened", value);
    partial void OnRamChanged(int value) => SaveProperty("OPack", value);
    partial void OnJavaPathChanged(string value) => SaveProperty("JavaPath", value);
    partial void OnWindowWidthChanged(int value) => SaveProperty("Wdith", value);
    partial void OnWindowHeightChanged(int value) => SaveProperty("Height", value);
    partial void OnEnterInServerChanged(bool value) => SaveProperty("EnterInServer", value);
    partial void OnServerIpChanged(string value) => SaveProperty("ServerIP", value);

    private async Task LoadLoaderVersionsAsync()
    {
        AvailableLoaderVersions.Clear();
        try
        {
            var versions = new List<string>();
            var mcVersion = MinecraftVersion;

            if (LoaderType == "Forge")
            {
                var fv = await new ForgeVersionLoader(WebHelper.Client).GetForgeVersions(mcVersion);
                versions = fv.Select(x => x.ForgeVersionName).ToList();
            }
            else if (LoaderType == "Fabric")
            {
                var fv = await new FabricInstaller(WebHelper.Client).GetLoaders(mcVersion);
                versions = fv.Select(x => x.Version).OfType<string>().ToList();
            }
            else if (LoaderType == "Quilt")
            {
                var qv = await new QuiltInstaller(WebHelper.Client).GetLoaders(mcVersion);
                versions = qv.Select(x => x.Version).OfType<string>().ToList();
            }
            else if (LoaderType == "NeoForge")
            {
                var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                var nf = await new NoAdNeoForgeInstaller(new MinecraftLauncher(path)).GetNeoForgeVersions(mcVersion);
                versions = nf.Select(x => x.VersionName).OfType<string>().ToList();
            }
            else if (LoaderType == "LiteLoader")
            {
                var ll = await new LiteLoaderInstaller(WebHelper.Client).GetAllLiteLoaders();
                versions = ll.Where(l => l.BaseVersion == mcVersion).Select(l => l.Version).OfType<string>().ToList();
            }
            else if (LoaderType == "Optifine")
            {
                var opt = await new OptifineInstaller(WebHelper.Client).GetOptifineVersionsAsync();
                versions = opt.Where(v => v.MinecraftVersion == mcVersion).Select(v => v.Version).ToList();
            }

            foreach (var v in versions) AvailableLoaderVersions.Add(v);

            if (AvailableLoaderVersions.Count > 0 && !AvailableLoaderVersions.Contains(LoaderVersion))
                LoaderVersion = AvailableLoaderVersions[0];
        }
        catch { }
    }

    [RelayCommand]
    private void ResetOptions()
    {
        string optPath = Path.Combine(_modpack.Path, "override", "options.txt");
        if (!File.Exists(optPath)) optPath = Path.Combine(_modpack.Path, "overrides", "options.txt");

        if (File.Exists(optPath))
        {
            File.Delete(optPath);
            _dialogService.ShowMessage("Налаштування успішно скинуто!", "Успіх");
        }
        else
        {
            _dialogService.ShowMessage("Файл options.txt не знайдено.", "Інформація");
        }
    }

    [RelayCommand]
    private void RepairModpack()
    {
        string[] paths = {
            Path.Combine(_modpack.Path, ".mods_installed"),
            Path.Combine(_modpack.Path, "override", ".mods_installed"),
            Path.Combine(_modpack.Path, "overrides", ".mods_installed")
        };
        foreach (var p in paths) { if (File.Exists(p)) File.Delete(p); }
        _dialogService.ShowMessage("Збірку буде перевірено та відновлено при наступному запуску!", "Успіх");
    }

    [RelayCommand]
    private async Task ChangeIcon()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Оберіть іконку",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } }
            });

            if (files.Count > 0)
            {
                string targetPath = Path.Combine(_modpack.Path, "icon.png");
                File.Copy(files[0].Path.LocalPath, targetPath, true);
                SaveProperty("UrlImage", targetPath);

                AvaloniaImageHelper.ClearCache();

                LoadIcon();
            }
        }
    }

    [RelayCommand]
    private void ClearJava()
    {
        JavaPath = string.Empty;
    }

    [RelayCommand]
    private async Task BrowseJavaAsync()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocalizationManager.GetString("Modpacks.SelectJavaTitle", "Оберіть виконуваний файл Java (javaw.exe або java)"),
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                var path = files[0].Path.LocalPath;
                if (!AvailableJavaPaths.Contains(path))
                {
                    AvailableJavaPaths.Add(path);
                }
                JavaPath = path;
            }
        }
    }

    [RelayCommand]
    private async Task AddFile()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Додати файли",
                AllowMultiple = true
            });

            if (files.Count > 0)
            {
                string folder = SelectedTabIndex switch { 0 => "mods", 1 => "resourcepacks", 2 => "shaderpacks", 3 => "saves", _ => "mods" };

                string baseModsPath = GetModsBasePath();
                string targetDir = Path.Combine(baseModsPath, folder);
                Directory.CreateDirectory(targetDir);

                foreach (var file in files)
                {
                    string targetFile = Path.Combine(targetDir, file.Name);
                    File.Copy(file.Path.LocalPath, targetFile, true);
                    if (SelectedTabIndex == 3 && file.Name.EndsWith(".zip"))
                        CL.Core.Helpers.ZipHelper.ExtractMap(targetFile, targetDir);
                }
                await RefreshListAsync();
            }
        }
    }

    public void Dispose()
    {
        CloseAction = null;
        ModpackIcon = null;
        ItemsList.Clear();
    }
}
