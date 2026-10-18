using Avalonia;
using Avalonia.Threading;
using CL.Core.Helpers;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.Services;
using SharpCompress.Compressors.Arj;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ExCSS.RadialGradient;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class ModsPageViewModel : ObservableObject
{
    private readonly ModDownloadService? _modDownloadService;
    private readonly ModpackService? _modpackService;
    private readonly IDialogService? _dialogService;

    private const int ItemsPerPage = 10;
    private CancellationTokenSource? _searchCts;
    private List<ModVersionInfo>? _currentModVersions;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _siteMods = "Modrinth";
    [ObservableProperty] private string _versionType = "Fabric";
    [ObservableProperty] private string _modType = "Collection";
    [ObservableProperty] private byte _selectedCategory = 0;

    [ObservableProperty] private int _currentPage = 0;
    [ObservableProperty] private string _pageNumberText = "1";
    [ObservableProperty] private bool _canGoPrevPage;
    [ObservableProperty] private bool _canGoNextPage;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isResultsEmpty;
    [ObservableProperty] private bool _isInstallerOpen;
    [ObservableProperty] private bool _isDownloadingMod;
    [ObservableProperty] private bool _canDownloadSelectedMod;

    public bool IsCollectionMode => ModType == "Collection";
    public bool IsStandardMode => ModType != "Collection";

    public ObservableCollection<ModSearchResult> SearchResults { get; } = new();
    public ObservableCollection<ModsListItemViewModel> ModCards { get; } = new();
    public ObservableCollection<InstalledModpack> InstalledModpacks { get; } = new();
    public ObservableCollection<string> GameVersions { get; } = new();
    public ObservableCollection<string> ModFileVersions { get; } = new();

    public Action<ModSearchResult>? OnModDownloadRequested { get; set; }

    [ObservableProperty] private InstalledModpack? _selectedModpack;
    [ObservableProperty] private string? _selectedGameVersion;
    [ObservableProperty] private string? _selectedModFileVersion;

    public ModsPageViewModel() : this(
        ResolveService<ModDownloadService>(),
        ResolveService<ModpackService>(),
        ResolveService<IDialogService>())
    {
    }

    public ModsPageViewModel(
        ModDownloadService? modDownloadService,
        ModpackService? modpackService,
        IDialogService? dialogService)
    {
        _modDownloadService = modDownloadService;
        _modpackService = modpackService;
        _dialogService = dialogService;
    }

    private static T? ResolveService<T>() where T : class
    {
        var app = Application.Current as App;
        return app?.ServiceProvider?.GetService(typeof(T)) as T
               ?? ServiceLocator.Current?.GetService<T>();
    }

    partial void OnSearchTextChanged(string value)
    {
        string defaultSearch = LocalizationManager.GetString("Generic.Search", "Пошук...");
        string trimmed = value?.Trim() ?? string.Empty;

        if (trimmed == defaultSearch || trimmed == "Пошук") return;

        CurrentPage = 0;
        TriggerSearch(debounceMs: 300);
    }

    partial void OnModTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCollectionMode));
        OnPropertyChanged(nameof(IsStandardMode));
    }

    partial void OnSelectedModpackChanged(InstalledModpack? value) => FilterVersionsForModpack(value);
    partial void OnSelectedGameVersionChanged(string? value) => FilterVersionsForGameVersion(value);
    partial void OnSelectedModFileVersionChanged(string? value) => UpdateCanDownloadState();

    private void UpdateCanDownloadState()
    {
        CanDownloadSelectedMod = IsCollectionMode
            ? SelectedModpack != null && !string.IsNullOrEmpty(SelectedModFileVersion)
            : !string.IsNullOrEmpty(SelectedGameVersion) && !string.IsNullOrEmpty(SelectedModFileVersion);
    }

    public void TriggerSearch(int debounceMs = 0)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            if (debounceMs > 0)
                await Task.Delay(debounceMs, token);

            if (!token.IsCancellationRequested)
                await SearchModsInternalAsync(token);
        }, token);
    }

    [RelayCommand]
    public async Task SearchModsAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        await SearchModsInternalAsync(_searchCts.Token);
    }

    private async Task SearchModsInternalAsync(CancellationToken token)
    {
        if (_modDownloadService == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            IsResultsEmpty = false;
            SearchResults.Clear();
            ModCards.Clear();
        });

        try
        {
            string query = SearchText?.Trim() ?? string.Empty;
            int offset = CurrentPage * ItemsPerPage;

            var results = await _modDownloadService.SearchModsAsync(
                query, SiteMods, VersionType, SelectedCategory, offset
            ).ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PageNumberText = (CurrentPage + 1).ToString();
                CanGoPrevPage = CurrentPage > 0;
                CanGoNextPage = results != null && results.Count >= ItemsPerPage;

                if (results == null || results.Count == 0)
                {
                    IsResultsEmpty = true;
                    CanGoNextPage = false;
                    return;
                }

                foreach (var mod in results)
                {
                    SearchResults.Add(mod);

                    var cardVm = new ModsListItemViewModel(
                        mod,
                        onDownloadClick: m => OnModDownloadRequested?.Invoke(m),
                        onOpenUrlClick: m =>
                        {
                            AvaloniaSoundService.Click();
                            OpenModInBrowser(m);
                        });

                    ModCards.Add(cardVm);
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowMessageAsync(
                        LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                        $"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}",
                        MascotEmotion.Sad);
                }
            });
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNextPage) return;
        CurrentPage++;
        TriggerSearch();
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage <= 0) return;
        CurrentPage--;
        TriggerSearch();
    }

    [RelayCommand]
    public void SetCategory(byte categoryId)
    {
        if (SelectedCategory == categoryId) return;
        SelectedCategory = categoryId;
        CurrentPage = 0;
        TriggerSearch();
    }

    [RelayCommand]
    public void SetProvider(string provider)
    {
        if (SiteMods == provider) return;
        SiteMods = provider;
        CurrentPage = 0;
        TriggerSearch();
    }

    [RelayCommand]
    public void SetLoader(string loader)
    {
        if (VersionType == loader) return;
        VersionType = loader;
        CurrentPage = 0;
        TriggerSearch();
    }

    [RelayCommand]
    public void SetModTargetType(string targetType)
    {
        if (ModType == targetType) return;
        ModType = targetType;
        CurrentPage = 0;
        TriggerSearch();
    }

    public async Task<bool> PrepareModInstallerAsync(ModSearchResult mod)
    {
        if (_modDownloadService == null || _modpackService == null) return false;

        GameVersions.Clear();
        ModFileVersions.Clear();
        InstalledModpacks.Clear();
        _currentModVersions = null;
        CanDownloadSelectedMod = false;

        try
        {
            var allVersions = await _modDownloadService.GetVersionsAsync(mod).ConfigureAwait(false);

            if (allVersions == null || allVersions.Count == 0)
            {
                await ShowDialogAsync(
                    LocalizationManager.GetString("Mods.ArchiveEmptyTitle", "Пусто"),
                    LocalizationManager.GetString("Mods.ArchiveEmptyDesc", "Дивина! Я перерила усі архіви, але не знайшла жодного файлу для цього проекту."),
                    MascotEmotion.Confused);
                return false;
            }

            _currentModVersions = allVersions;

            if (IsCollectionMode)
            {
                var modpacks = await Task.Run(() => _modpackService.LoadInstalledModpacks()).ConfigureAwait(false);
                if (modpacks == null || modpacks.Count == 0)
                {
                    await ShowDialogAsync(
                        LocalizationManager.GetString("Mods.NoModpacksTitle", "Немає збірок"),
                        LocalizationManager.GetString("Mods.NoModpacksDesc", "У вас ще немає жодної створеної збірки! Спочатку створіть збірку, щоб я могла додати туди цей мод."),
                        MascotEmotion.Normal);
                    return false;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var pack in modpacks) InstalledModpacks.Add(pack);
                    SelectedModpack = InstalledModpacks.FirstOrDefault();
                });
            }
            else
            {
                var gameVersions = _currentModVersions
                    .SelectMany(v => v.GameVersions)
                    .Distinct()
                    .Where(v => !string.IsNullOrEmpty(v) && char.IsDigit(v[0]))
                    .OrderByDescending(v => v)
                    .ToList();

                if (gameVersions.Count == 0)
                {
                    await ShowDialogAsync(
                        LocalizationManager.GetString("Mods.NoVersionsTitle", "Версії відсутні"),
                        LocalizationManager.GetString("Mods.NoVersionsDesc", "Я знайшла файли, але жоден з них не має чіткої версії гри (можливо, мод застарів)."),
                        MascotEmotion.Confused);
                    return false;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var gv in gameVersions) GameVersions.Add(gv);
                    SelectedGameVersion = GameVersions.FirstOrDefault();
                });
            }

            IsInstallerOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            await ShowDialogAsync(
                LocalizationManager.GetString("Dialogs.Oops", "Збій"),
                $"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}",
                MascotEmotion.Sad);
            return false;
        }
    }

    private void FilterVersionsForModpack(InstalledModpack? selectedPack)
    {
        ModFileVersions.Clear();
        if (selectedPack == null || _currentModVersions == null)
        {
            CanDownloadSelectedMod = false;
            return;
        }

        string targetVer = selectedPack.MinecraftVersion;
        string targetLoader = selectedPack.LoaderType;

        var compatible = _currentModVersions
            .Where(v => v.GameVersions.Contains(targetVer))
            .ToList();

        if (SelectedCategory == 0)
        {
            compatible = compatible
                .Where(v => v.Loaders.Any(l => l.Equals(targetLoader, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (compatible.Count > 0)
        {
            foreach (var v in compatible) ModFileVersions.Add(v.VersionName);
            SelectedModFileVersion = ModFileVersions[0];
            CanDownloadSelectedMod = true;
        }
        else
        {
            CanDownloadSelectedMod = false;
            string error = string.Format(
                LocalizationManager.GetString("Modpacks.IncompatibleDesc",
                    "Ех, не вийде. Я перевірила всі файли, але не знайшла версії мода, яка б підійшла для збірки '{0}'.\n\nЗбірка вимагає Minecraft {1} ({2}), а цей мод, схоже, не оновлено під такі параметри."),
                selectedPack.Name, targetVer, targetLoader);

            _ = ShowDialogAsync(
                LocalizationManager.GetString("Modpacks.IncompatibleTitle", "Несумісність"),
                error,
                MascotEmotion.Sad);
        }
    }

    private void FilterVersionsForGameVersion(string? selectedGameVersion)
    {
        ModFileVersions.Clear();
        if (string.IsNullOrEmpty(selectedGameVersion) || _currentModVersions == null)
        {
            CanDownloadSelectedMod = false;
            return;
        }

        var query = _currentModVersions.Where(v => v.GameVersions.Contains(selectedGameVersion));

        if (SelectedCategory == 0)
        {
            query = query.Where(v => v.Loaders.Any(l => l.Equals(VersionType, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var fileVer in query.OrderByDescending(v => v.VersionName))
        {
            ModFileVersions.Add(fileVer.VersionName);
        }

        SelectedModFileVersion = ModFileVersions.FirstOrDefault();
        CanDownloadSelectedMod = ModFileVersions.Count > 0;
    }

    public async Task<bool> DownloadSelectedModAsync(string? customFolderPath = null)
    {
        if (_modDownloadService == null || _currentModVersions == null || string.IsNullOrEmpty(SelectedModFileVersion))
            return false;

        var selectedVersionInfo = _currentModVersions.FirstOrDefault(v => v.VersionName == SelectedModFileVersion);
        if (selectedVersionInfo == null) return false;

        string targetPath;

        if (IsCollectionMode)
        {
            if (SelectedModpack == null)
            {
                await ShowDialogAsync(
                    LocalizationManager.GetString("Modpacks.SelectTargetTitle", "Куди качати?"),
                    LocalizationManager.GetString("Modpacks.SelectTargetDesc", "Оберіть збірку!"),
                    MascotEmotion.Alert);
                return false;
            }

            string subFolder = SelectedCategory switch
            {
                1 => "shaderpacks",
                2 => "resourcepacks",
                3 => "saves",
                4 => "datapacks",
                _ => "mods"
            };

            targetPath = Path.Combine(SelectedModpack.Path, "override", subFolder);
            if (!Directory.Exists(targetPath))
                targetPath = Path.Combine(SelectedModpack.Path, "overrides", subFolder);

            if (!Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(SelectedModpack.Path, "override", subFolder);
                Directory.CreateDirectory(targetPath);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(customFolderPath)) return false;
            targetPath = customFolderPath;
        }

        IsDownloadingMod = true;
        CanDownloadSelectedMod = false;

        try
        {
            await _modDownloadService.DownloadModWithDependenciesAsync(
                selectedVersionInfo, SelectedCategory, targetPath
            ).ConfigureAwait(false);

            if (SelectedCategory == 3)
            {
                string downloadedFile = Path.Combine(targetPath, selectedVersionInfo.FileName);
                if (File.Exists(downloadedFile) && Path.GetExtension(downloadedFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() => ZipHelper.ExtractMap(downloadedFile, targetPath)).ConfigureAwait(false);
                    await ShowDialogAsync(
                        LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                        LocalizationManager.GetString("Mods.MapInstalled", "Мапа завантажена та розпакована!"),
                        MascotEmotion.Normal);
                }
            }
            else
            {
                await ShowDialogAsync(
                    LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                    string.Format(LocalizationManager.GetString("Mods.FileInstalled", "Файл встановлено у {0}!"), Path.GetFileName(targetPath)),
                    MascotEmotion.Normal);
            }

            return true;
        }
        catch (Exception ex)
        {
            await ShowDialogAsync(
                LocalizationManager.GetString("DownloadManager.ErrorTitle", "Помилка завантаження"),
                $"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}",
                MascotEmotion.Sad);
            return false;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsDownloadingMod = false;
                UpdateCanDownloadState();
            });
        }
    }

    public void OpenModInBrowser(ModSearchResult mod)
    {
        string baseUrl = mod.Site == "Modrinth" ? "https://modrinth.com" : "https://www.curseforge.com/minecraft";
        string category = mod.Site == "Modrinth"
            ? SelectedCategory switch
            {
                1 => "shader",
                2 => "resourcepack",
                4 => "datapack",
                _ => "mod"
            }
            : SelectedCategory switch
            {
                1 => "shaders",
                2 => "texture-packs",
                3 => "worlds",
                4 => "data-packs",
                _ => "mc-mods"
            };

        WebHelper.OpenUrl($"{baseUrl}/{category}/{mod.Slug}");
    }

    private async Task ShowDialogAsync(string title, string message, MascotEmotion emotion)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_dialogService != null)
                await _dialogService.ShowMessageAsync(title, message, emotion);
        });
    }
}
