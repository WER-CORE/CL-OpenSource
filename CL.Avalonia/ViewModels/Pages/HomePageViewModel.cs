using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class HomePageViewModel : ObservableObject
{
    private readonly VersionService? _versionService;
    private readonly GameLaunchService? _gameLaunchService;
    private readonly GameSessionManager? _gameSessionManager;
    private readonly AccountService? _accountService;
    private readonly ServerListService? _serverListService;
    private readonly LastActionService? _lastActionService;

    private CancellationTokenSource? _vanillaSearchCts;
    private CancellationTokenSource? _optifineSearchCts;
    private bool _latestVersionFetched;

    public AccountService? AccountService => _accountService;

    private MSession? _currentSession;
    public MSession? CurrentSession
    {
        get => _accountService?.CurrentSession ?? _currentSession;
        set => SetProperty(ref _currentSession, value);
    }

    private AccountType _currentAccountType;
    public AccountType CurrentAccountType
    {
        get => _accountService?.CurrentProfile?.TypeAccount ?? _currentAccountType;
        set => SetProperty(ref _currentAccountType, value);
    }

    [ObservableProperty] private bool _showReleases = true;
    [ObservableProperty] private bool _showSnapshots;
    [ObservableProperty] private bool _showBeta;
    [ObservableProperty] private bool _showAlpha;

    [ObservableProperty] private string _vanillaSearchText = string.Empty;
    [ObservableProperty] private string _optifineSearchText = string.Empty;

    public ObservableCollection<string> VanillaVersions { get; } = new();
    public ObservableCollection<string> OptifineMinecraftVersions { get; } = new();
    public ObservableCollection<string> OptifineModVersions { get; } = new();
    public ObservableCollection<VersionLogItem> ChangelogItems { get; } = new();
    public ObservableCollection<PartnerServerCardViewModel> PartnerCards { get; } = new();
    public event Action<ServerInfo>? ServerInfoRequested;
    public ObservableCollection<HistoryActionItemViewModel> RecentActions { get; } = new();

    [ObservableProperty] private string? _selectedVanillaVersion;
    [ObservableProperty] private string? _selectedOptifineMinecraftVersion;
    [ObservableProperty] private string? _selectedOptifineModVersion;
    [ObservableProperty] private VersionLogItem? _selectedChangelogItem;
    [ObservableProperty] private bool _hasMoreChangelogItems = true;
    [ObservableProperty] private bool _isLoadingMoreChangelog;

    [ObservableProperty] private byte _selectedLoaderType;
    [ObservableProperty] private bool _isVersionSelectorOpen;

    [ObservableProperty] private string _latestVanillaRelease = "1.21.1";
    [ObservableProperty] private string _playButtonText = "ОБЕРІТЬ ВЕРСІЮ";

    public HomePageViewModel() : this(
        ResolveService<VersionService>(),
        ResolveService<GameLaunchService>(),
        ResolveService<GameSessionManager>(),
        ResolveService<AccountService>(),
        ResolveService<ServerListService>(),
        ResolveService<LastActionService>())
    {
    }

    public HomePageViewModel(
        VersionService? versionService,
        GameLaunchService? gameLaunchService,
        GameSessionManager? gameSessionManager,
        AccountService? accountService,
        ServerListService? serverListService = null,
        LastActionService? lastActionService = null)
    {
        _versionService = versionService;
        _gameLaunchService = gameLaunchService;
        _gameSessionManager = gameSessionManager;
        _accountService = accountService;
        _serverListService = serverListService ?? ResolveService<ServerListService>();
        _lastActionService = lastActionService ?? ResolveService<LastActionService>();

        if (_accountService != null)
        {
            _accountService.AccountChanged += OnAccountChanged;
        }

        if (_lastActionService != null)
        {
            _lastActionService.OnActionsUpdated += UpdateRecentActions;
        }

        _ = InitializeAsync();
    }

    private static T? ResolveService<T>() where T : class
    {
        var app = Application.Current as App;
        return app?.ServiceProvider?.GetService(typeof(T)) as T
               ?? ServiceLocator.Current?.GetService<T>();
    }

    private async Task InitializeAsync()
    {
        await EnsureAccountSelectedAsync();
        _ = LoadChangelogAsync();
        _ = LoadPartnerServersAsync();

        if (_lastActionService != null)
        {
            _ = _lastActionService.LoadLastActionsFromJsonAsync();
        }

        int savedType = SettingsManager.Default.LastSelectedType;
        if (savedType == 1)
        {
            SelectedLoaderType = 1;
            _ = LoadVanillaVersionsAsync();
        }
        else if (savedType == 5)
        {
            SelectedLoaderType = 5;
            _ = LoadOptifineVersionsAsync();
        }
    }

    private void OnAccountChanged(ProfileItem? profile, MSession? session)
    {
        OnPropertyChanged(nameof(CurrentSession));
        OnPropertyChanged(nameof(CurrentAccountType));
    }

    #region Обробники змін властивостей ( замість PropertyChanged += async )

    partial void OnShowReleasesChanged(bool value) => TriggerVanillaReload();
    partial void OnShowSnapshotsChanged(bool value) => TriggerVanillaReload();
    partial void OnShowBetaChanged(bool value) => TriggerVanillaReload();
    partial void OnShowAlphaChanged(bool value) => TriggerVanillaReload();

    partial void OnVanillaSearchTextChanged(string value) => TriggerVanillaReload(debounceMs: 250);
    partial void OnOptifineSearchTextChanged(string value) => TriggerOptifineReload(debounceMs: 250);

    partial void OnSelectedVanillaVersionChanged(string? value)
    {
        if (value != null) UpdatePlayButtonText(1, value);
    }

    partial void OnSelectedOptifineMinecraftVersionChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _ = LoadOptifineModVersionsAsync();
    }

    partial void OnSelectedOptifineModVersionChanged(string? value)
    {
        if (value != null) UpdatePlayButtonText(5, value);
    }

    private void TriggerVanillaReload(int debounceMs = 0)
    {
        _vanillaSearchCts?.Cancel();
        _vanillaSearchCts = new CancellationTokenSource();
        var token = _vanillaSearchCts.Token;

        _ = Task.Run(async () =>
        {
            if (debounceMs > 0)
                await Task.Delay(debounceMs, token);

            if (!token.IsCancellationRequested)
                await LoadVanillaVersionsAsync(token);
        }, token);
    }

    private void TriggerOptifineReload(int debounceMs = 0)
    {
        _optifineSearchCts?.Cancel();
        _optifineSearchCts = new CancellationTokenSource();
        var token = _optifineSearchCts.Token;

        _ = Task.Run(async () =>
        {
            if (debounceMs > 0)
                await Task.Delay(debounceMs, token);

            if (!token.IsCancellationRequested)
                await LoadOptifineVersionsAsync(token);
        }, token);
    }

    #endregion

    #region Логіка Акаунтів та Конфігурації Запуску

    public async Task<bool> EnsureAccountSelectedAsync()
    {
        var accountService = _accountService ?? ResolveService<AccountService>();
        if (accountService == null) return false;

        if (accountService.CurrentSession == null || accountService.CurrentProfile == null)
        {
            var profiles = await Task.Run(() => accountService.GetProfiles());
            if (profiles is { Count: > 0 })
            {
                int savedIndex = SettingsManager.Default.SelectIndexAccount;
                var prof = (savedIndex >= 0 && savedIndex < profiles.Count) ? profiles[savedIndex] : profiles[0];
                await accountService.SelectProfileAsync(prof);
            }
        }

        OnPropertyChanged(nameof(CurrentSession));
        OnPropertyChanged(nameof(CurrentAccountType));

        return accountService.CurrentSession != null && !string.IsNullOrWhiteSpace(accountService.CurrentSession.Username);
    }

    public LaunchConfiguration CreateLaunchConfig(string? serverIp = null, int serverPort = 0, string? serverName = null)
    {
        var settings = SettingsManager.Default;
        int ram = settings.OP > 0 ? settings.OP : 2048;
        int width = settings.width > 0 ? settings.width : 854;
        int height = settings.height > 0 ? settings.height : 480;

        return new LaunchConfiguration
        {
            IsOffline = settings.OfflineModLauncher || !NetworkInterface.GetIsNetworkAvailable(),
            MinimumRamMb = ram,
            MaximumRamMb = ram,
            ScreenWidth = width,
            ScreenHeight = height,
            Session = _accountService?.CurrentSession ?? CurrentSession,
            AccountType = _accountService?.CurrentProfile?.TypeAccount ?? CurrentAccountType,
            IsFullscreen = settings.FullScreen,
            ServerIp = serverIp,
            ServerPort = serverPort,
            ServerName = serverName
        };
    }

    private void UpdatePlayButtonText(byte type, string? version)
    {
        SelectedLoaderType = type;

        if (string.IsNullOrEmpty(version))
        {
            PlayButtonText = LocalizationManager.GetString("GameLaunch.PlayBtnPlay", "ГРАТИ");
            return;
        }

        string template = LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})");

        if (type == 1)
        {
            PlayButtonText = string.Format(template, SelectedVanillaVersion);
            SettingsManager.Default.LastSelectedVersion = SelectedVanillaVersion;
            SettingsManager.Default.LastSelectedType = 1;
            SettingsManager.Save();
        }
        else if (type == 5)
        {
            string? mcVer = SelectedOptifineMinecraftVersion;
            string? modVer = SelectedOptifineModVersion;

            if (!string.IsNullOrEmpty(mcVer) && mcVer.Contains("optifine", StringComparison.OrdinalIgnoreCase))
            {
                PlayButtonText = string.Format(template, mcVer);
            }
            else if (!string.IsNullOrEmpty(modVer))
            {
                PlayButtonText = string.Format(template, modVer);
            }

            SettingsManager.Default.LastSelectedVersion = mcVer;
            SettingsManager.Default.LastSelectedModVersion = modVer;
            SettingsManager.Default.LastSelectedType = 5;
            SettingsManager.Save();
        }
    }

    #endregion

    #region Команди (RelayCommands)

    [RelayCommand]
    private async Task ToggleVersionSelectorAsync()
    {
        IsVersionSelectorOpen = !IsVersionSelectorOpen;

        if (IsVersionSelectorOpen)
        {
            if (_latestVersionFetched || SettingsManager.Default.OfflineModLauncher) return;

            try
            {
                string rootPath = SettingsManager.Default.PathLacunher;
                if (string.IsNullOrWhiteSpace(rootPath))
                    rootPath = PlatformPaths.DefaultLauncherPath();

                var launcher = new MinecraftLauncher(new MinecraftPath(rootPath));
                var versions = await launcher.GetAllVersionsAsync().ConfigureAwait(false);

                if (versions.LatestReleaseName != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => LatestVanillaRelease = versions.LatestReleaseName);
                    _latestVersionFetched = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get latest versions: {ex.Message}");
            }
        }
        else
        {
            RestoreSavedVersionState();
        }
    }

    public void RestoreSavedVersionState()
    {
        int savedType = SettingsManager.Default.LastSelectedType;
        string? savedVer = SettingsManager.Default.LastSelectedVersion;
        string? savedModVer = SettingsManager.Default.LastSelectedModVersion;

        if (savedType != 0 && !string.IsNullOrEmpty(savedVer))
        {
            SelectedLoaderType = (byte)savedType;
            string displayVer = (savedType == 5 && !string.IsNullOrEmpty(savedModVer)) ? savedModVer : savedVer;
            PlayButtonText = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), displayVer);
        }
        else
        {
            PlayButtonText = LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ");
            SelectedLoaderType = 0;
        }
    }

    [RelayCommand]
    private async Task SelectLoaderTypeAsync(string loaderTypeString)
    {
        if (loaderTypeString == "Vanilla")
        {
            SelectedLoaderType = 1;
            await LoadVanillaVersionsAsync();
        }
        else if (loaderTypeString == "Optifine")
        {
            SelectedLoaderType = 5;
            await LoadOptifineVersionsAsync();
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (SelectedLoaderType == 0)
            SelectedLoaderType = (byte)SettingsManager.Default.LastSelectedType;

        if (SelectedLoaderType == 0) return;

        await EnsureAccountSelectedAsync();
        var dialogService = ResolveService<IDialogService>();

        if (CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.Username))
        {
            if (dialogService != null)
            {
                await dialogService.ShowMessageAsync(
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Не обрано"),
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Ой! Схоже, нікого не обрано початково!\nБудь ласка, оберіть акаунт у списку акаунтів."),
                    MascotEmotion.Alert);
            }
            return;
        }

        if (SelectedLoaderType == 1 && string.IsNullOrEmpty(SelectedVanillaVersion))
        {
            SelectedVanillaVersion = SettingsManager.Default.LastSelectedVersion;
        }
        else if (SelectedLoaderType == 5 && string.IsNullOrEmpty(SelectedOptifineMinecraftVersion))
        {
            SelectedOptifineMinecraftVersion = SettingsManager.Default.LastSelectedVersion;
            SelectedOptifineModVersion = SettingsManager.Default.LastSelectedModVersion;
        }

        if (!await ValidateSelectedVersionAsync(dialogService)) return;

        var config = await Task.Run(() => CreateLaunchConfig());

        try
        {
            if (_gameLaunchService == null) return;

            if (SelectedLoaderType == 1 && !string.IsNullOrEmpty(SelectedVanillaVersion))
            {
                await _gameLaunchService.LaunchGameAsync(LoaderType.Vanilla, SelectedVanillaVersion, null, config);
            }
            else if (SelectedLoaderType == 5 && !string.IsNullOrEmpty(SelectedOptifineMinecraftVersion))
            {
                await _gameLaunchService.LaunchGameAsync(LoaderType.Optifine, SelectedOptifineMinecraftVersion, SelectedOptifineModVersion, config);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Game launch error: {ex.Message}");
        }
    }

    private async Task<bool> ValidateSelectedVersionAsync(IDialogService? dialogService)
    {
        if (SelectedLoaderType == 5)
        {
            bool isOfflinePrepacked = !string.IsNullOrEmpty(SelectedOptifineMinecraftVersion) &&
                                      SelectedOptifineMinecraftVersion.Contains("optifine", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(SelectedOptifineMinecraftVersion) || (!isOfflinePrepacked && string.IsNullOrEmpty(SelectedOptifineModVersion)))
            {
                if (dialogService != null)
                {
                    await dialogService.ShowMessageAsync(
                        LocalizationManager.GetString("GameLaunch.NoVersionSelectedTitle", "Версія не обрана"),
                        LocalizationManager.GetString("GameLaunch.OptifineNotSelectedDesc", "Секундочку! А що саме ми запускаємо?\nТи забув обрати версію Minecraft та OptiFine у списках.\n\nТицьни на потрібні версії, і погнали!"),
                        MascotEmotion.Alert);
                }
                return false;
            }
        }
        else if (SelectedLoaderType == 1 && string.IsNullOrEmpty(SelectedVanillaVersion))
        {
            if (dialogService != null)
            {
                await dialogService.ShowMessageAsync(
                    LocalizationManager.GetString("GameLaunch.NoVersionSelectedTitle", "Версія не обрана"),
                    LocalizationManager.GetString("GameLaunch.VanilaNotSelectedDesc", "Секундочку! А що саме ми запускаємо?\nТи забув обрати версію Minecraft у списку.\n\nТицьни на потрібну версію, і погнали!"),
                    MascotEmotion.Alert);
            }
            return false;
        }

        return true;
    }

    #endregion

    #region Завантаження Версій та Чейнджлогу

    private static Task<List<string>> GetLocalVersionsAsync(string? keyword = null)
    {
        return Task.Run(() =>
        {
            var localVersions = new List<string>();
            var path = new MinecraftPath(SettingsManager.Default.PathLacunher);

            if (!Directory.Exists(path.Versions)) return localVersions;

            foreach (var dir in Directory.EnumerateDirectories(path.Versions))
            {
                string verName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(keyword) || verName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    localVersions.Add(verName);
                }
            }
            return localVersions;
        });
    }

    private async Task LoadVanillaVersionsAsync(CancellationToken token = default)
    {
        if (_versionService == null) return;

        try
        {
            string search = VanillaSearchText;
            bool isOffline = SettingsManager.Default.OfflineModLauncher;
            List<string> resultList;

            if (isOffline)
            {
                resultList = await GetLocalVersionsAsync(search).ConfigureAwait(false);
                if (resultList.Count == 0)
                    resultList.Add(LocalizationManager.GetString("GameLaunch.OfflineNoVersions", "Немає офлайн версій"));
            }
            else
            {
                var filtered = await _versionService
                    .GetFilteredVersionsAsync(search, ShowReleases, ShowSnapshots, ShowBeta, ShowAlpha)
                    .ConfigureAwait(false);
                resultList = filtered.ToList();
            }

            if (token.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCollection(VanillaVersions, resultList);

                string? lastVer = SettingsManager.Default.LastSelectedVersion;
                SelectedVanillaVersion = (!string.IsNullOrEmpty(lastVer) && VanillaVersions.Contains(lastVer))
                    ? lastVer
                    : VanillaVersions.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading Vanilla versions: {ex.Message}");
        }
    }

    private async Task LoadOptifineVersionsAsync(CancellationToken token = default)
    {
        if (_versionService == null) return;

        try
        {
            string search = OptifineSearchText;
            bool isOffline = SettingsManager.Default.OfflineModLauncher;
            List<string> resultList;

            if (isOffline)
            {
                var locals = await GetLocalVersionsAsync(search).ConfigureAwait(false);
                resultList = locals.Where(v => v.Contains("optifine", StringComparison.OrdinalIgnoreCase)).ToList();

                if (resultList.Count == 0)
                    resultList.Add(LocalizationManager.GetString("GameLaunch.OptifineNoVersions", "Немає офлайн Optifine"));
            }
            else
            {
                var filtered = await _versionService
                    .GetFilteredVersionsAsync(search, true, false, false, false)
                    .ConfigureAwait(false);
                resultList = filtered.ToList();
            }

            if (token.IsCancellationRequested) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCollection(OptifineMinecraftVersions, resultList);

                string? lastVer = SettingsManager.Default.LastSelectedVersion;
                SelectedOptifineMinecraftVersion = (!string.IsNullOrEmpty(lastVer) && OptifineMinecraftVersions.Contains(lastVer))
                    ? lastVer
                    : OptifineMinecraftVersions.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading Optifine versions: {ex.Message}");
        }
    }

    private async Task LoadOptifineModVersionsAsync()
    {
        if (_versionService == null || string.IsNullOrEmpty(SelectedOptifineMinecraftVersion)) return;

        try
        {
            bool isOffline = SettingsManager.Default.OfflineModLauncher;
            if (isOffline || SelectedOptifineMinecraftVersion.Contains("optifine", StringComparison.OrdinalIgnoreCase))
            {
                string builtIn = LocalizationManager.GetString("GameLaunch.LocalBuiltIn", "Вбудовано");
                UpdateCollection(OptifineModVersions, new[] { builtIn });
                SelectedOptifineModVersion = builtIn;
                return;
            }

            var modVersions = await _versionService.GetOptifineVersionsAsync(SelectedOptifineMinecraftVersion).ConfigureAwait(false);
            var resultList = modVersions.Count > 0
                ? modVersions.ToList()
                : new List<string> { LocalizationManager.GetString("GameLaunch.NoAvailableVersions", "Недоступно") };

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCollection(OptifineModVersions, resultList);

                string? lastModVer = SettingsManager.Default.LastSelectedModVersion;
                SelectedOptifineModVersion = (!string.IsNullOrEmpty(lastModVer) && OptifineModVersions.Contains(lastModVer))
                    ? lastModVer
                    : OptifineModVersions.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCollection(OptifineModVersions, new[] { $"Помилка: {ex.Message}" });
            });
        }
    }

    private async Task LoadChangelogAsync()
    {
        if (SettingsManager.Default.OfflineModLauncher || _versionService == null) return;

        try
        {
            var logs = await _versionService.GetChangeLogAsync(skip: 0, take: 10).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCollection(ChangelogItems, logs);
                if (ChangelogItems.Count > 0)
                {
                    SelectedChangelogItem = ChangelogItems[0];
                }
                HasMoreChangelogItems = _versionService.HasMoreChangeLogs(ChangelogItems.Count);
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var dialogService = ResolveService<IDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowMessageAsync(
                        string.Format(LocalizationManager.GetString("MainScreen.ChangelogErrorDesc", "Вибач, але я не змогла дістати свіжий випуск новин (Changelog).\nМожливо, інтернет зник або сервери Mojang втомилися відповідати.\n\nОсь що трапилося: {0}"), ex.Message),
                        LocalizationManager.GetString("MainScreen.ChangelogErrorTitle", "Немає новин"),
                        MascotEmotion.Sad);
                }
            });
        }
    }

    [RelayCommand]
    public async Task LoadMoreChangelogAsync()
    {
        if (SettingsManager.Default.OfflineModLauncher || _versionService == null || IsLoadingMoreChangelog || !HasMoreChangelogItems)
            return;

        try
        {
            IsLoadingMoreChangelog = true;
            int currentCount = ChangelogItems.Count;
            var moreLogs = await _versionService.GetChangeLogAsync(skip: currentCount, take: 10).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in moreLogs)
                {
                    ChangelogItems.Add(item);
                }
                HasMoreChangelogItems = _versionService.HasMoreChangeLogs(ChangelogItems.Count);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading more changelog items: {ex.Message}");
        }
        finally
        {
            IsLoadingMoreChangelog = false;
        }
    }

    private static void UpdateCollection<T>(ObservableCollection<T> collection, IEnumerable<T> newItems)
    {
        collection.Clear();
        foreach (var item in newItems)
        {
            collection.Add(item);
        }
    }

    private async Task LoadPartnerServersAsync()
    {
        if (SettingsManager.Default.OfflineModLauncher || _serverListService == null) return;

        try
        {
            string serverUrl = Secrets._serversUrl ?? "https://cl-launcher.app/api/servers";
            var servers = await _serverListService.GetServersAsync(serverUrl, null).ConfigureAwait(false);
            var partners = servers?.Where(s => s.IsPartner).OrderByDescending(s => s.Priority).ToList();

            if (partners is { Count: > 0 })
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PartnerCards.Clear();
                    foreach (var p in partners)
                    {
                        PartnerCards.Add(new PartnerServerCardViewModel(
                            p,
                            (ver, ip, port) => _ = LaunchDirectGameAsync(LoaderType.Vanilla, ver, null, ip, port, p.Name),
                            (cardVm, info) => ServerInfoRequested?.Invoke(info)));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Partner servers load error: {ex.Message}");
        }
    }

    public void UpdateRecentActions(List<Dictionary<string, string>> actions)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RecentActions.Clear();
            if (actions == null) return;

            foreach (var action in actions)
            {
                try
                {
                    var item = new HistoryActionItemViewModel(
                        action,
                        onPlayServer: (ver, ip, port) => _ = LaunchDirectGameAsync(LoaderType.Vanilla, ver, null, ip, port),
                        onPlayOptifine: (ver, modVer) => _ = LaunchDirectGameAsync(LoaderType.Optifine, ver, modVer),
                        onPlayVanilla: ver => _ = LaunchDirectGameAsync(LoaderType.Vanilla, ver, null)
                    );
                    RecentActions.Insert(0, item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating history item: {ex.Message}");
                }
            }
        });
    }

    public async Task LaunchDirectGameAsync(LoaderType loader, string version, string? modVersion, string? ip = null, int port = 0, string? serverName = null)
    {
        if (_gameLaunchService == null) return;
        var config = CreateLaunchConfig(ip, port, serverName);
        await _gameLaunchService.LaunchGameAsync(loader, version, modVersion, config);
    }

    #endregion
}
