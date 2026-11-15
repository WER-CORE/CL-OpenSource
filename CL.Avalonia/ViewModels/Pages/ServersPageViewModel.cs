using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Models;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

using CL.Core.Models.P2P;
using CL.Core.Services.P2P;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class ServersPageViewModel : ObservableObject, ITrimmablePage
{
    private readonly ServerListService _serverListService;
    private readonly LastActionService? _lastActionService;
    private readonly NewsService? _newsService;
    private readonly GameSessionManager? _sessionManager;

    public void TrimMemory()
    {
        IsNewsOverlayVisible = false;
        IsFundraiserOverlayVisible = false;
        _searchCts?.Cancel();
    }

    public void Unload()
    {
        TrimMemory();
        foreach (var card in ServerCards)
        {
            card.Dispose();
        }
        ServerCards.Clear();
        AllServers.Clear();
        DisplayedServers.Clear();
        _isInitialized = false;

        _lanDiscoveryTimeoutCts?.Cancel();
        if (!IsHostingActive)
        {
            _ = _p2pManager.StopDiscoveryAsync();
            DiscoveredLanWorlds.Clear();
        }
    }

    private CancellationTokenSource? _searchCts;
    private bool _isInitialized;
    private const int ItemsPerPage = 5;

    public ObservableCollection<ServerInfo> AllServers { get; } = new();
    public ObservableCollection<ServerInfo> DisplayedServers { get; } = new();
    public ObservableCollection<ServerCardViewModel> ServerCards { get; } = new();
    public ObservableCollection<FundraiserData3rd> Fundraisers { get; } = new();
    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    public ObservableCollection<ServerInfo> CurrentPageServers => DisplayedServers;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _canGoNext;
    [ObservableProperty] private bool _canGoPrev;
    [ObservableProperty] private bool _isPaginationVisible;
    [ObservableProperty] private string _pageInfo = "Сторінка 1 з 1";

    public string PageInfoText => PageInfo;

    [ObservableProperty] private bool _isServersLoading;
    [ObservableProperty] private bool _isNewsLoading;
    [ObservableProperty] private bool _isFundsLoading;

    public bool IsLoading => IsServersLoading;

    [ObservableProperty] private bool _isFundraiserOverlayVisible;
    [ObservableProperty] private bool _isNewsOverlayVisible;
    [ObservableProperty] private NewsItem? _selectedNewsItem;
    public ObservableCollection<NewsCardViewModel> NewsCards { get; } = new();
    private readonly P2PManagerService _p2pManager;

    [ObservableProperty] private bool _isPublicServersTab = true;
    [ObservableProperty] private bool _isP2PTab;

    public ObservableCollection<P2PLanWorld> DiscoveredLanWorlds { get; } = new();
    [ObservableProperty] private P2PLanWorld? _selectedLanWorld;
    [ObservableProperty] private string _lanWorldPlaceholder = LocalizationManager.GetString("P2P.SearchingLanWorlds", "Пошук LAN-світів у мережі...");
    [ObservableProperty] private bool _isLanWorldEmptyWarningVisible;
    [ObservableProperty] private string _lanWorldEmptyWarningText = LocalizationManager.GetString("P2P.NoLanWorldsFoundAfterWait", "За 5 секунд пошуку активних LAN-світів не знайдено. Відкрийте світ для LAN у грі або введіть порт вручну.");
    private CancellationTokenSource? _lanDiscoveryTimeoutCts;
    [ObservableProperty] private string _manualPortInput = string.Empty;
    [ObservableProperty] private bool _isDiscoveryRefreshing;
    [ObservableProperty] private bool _isHostingActive;
    [ObservableProperty] private string _hostRoomCode = string.Empty;
    [ObservableProperty] private string _hostDirectAddress = string.Empty;
    [ObservableProperty] private bool _hasDirectAddress;
    [ObservableProperty] private string _hostWorldName = string.Empty;
    [ObservableProperty] private string _hostStatus = LocalizationManager.GetString("P2P.WaitingLan", "Очікування запуску або відкриття LAN-світу у грі...");
    [ObservableProperty] private bool _requireApproval;
    [ObservableProperty] private bool _allowOfflinePlayers = true;
    [ObservableProperty] private bool _isHostBusy;
    [ObservableProperty] private string _p2pDiagnosticLogText = string.Empty;
    private readonly System.Collections.Generic.Queue<string> _p2pLogLines = new();
    public ObservableCollection<P2PPlayer> ConnectedPlayers { get; } = new();

    [ObservableProperty] private string _inputRoomCode = string.Empty;
    [ObservableProperty] private bool _isGuestChecking;
    [ObservableProperty] private bool _isGuestRoomFound;
    [ObservableProperty] private string _guestWorldName = string.Empty;
    [ObservableProperty] private string _guestGameVersion = string.Empty;
    [ObservableProperty] private string _guestLoaderType = string.Empty;
    [ObservableProperty] private string _guestStatusMessage = string.Empty;
    [ObservableProperty] private bool _isGuestLaunching;
    private P2PRoomPayload? _guestPayload;

    public ServersPageViewModel() : this(
        ResolveService<ServerListService>() ?? new ServerListService(),
        ResolveService<LastActionService>(),
        ResolveService<NewsService>(),
        ResolveService<GameSessionManager>(),
        ResolveService<P2PManagerService>() ?? new P2PManagerService())
    {
    }

    public ServersPageViewModel(
        ServerListService serverListService,
        LastActionService? lastActionService,
        NewsService? newsService,
        GameSessionManager? sessionManager,
        P2PManagerService? p2pManager = null)
    {
        _serverListService = serverListService;
        _lastActionService = lastActionService;
        _newsService = newsService;
        _sessionManager = sessionManager;
        _p2pManager = p2pManager ?? ResolveService<P2PManagerService>() ?? new P2PManagerService();

        _p2pManager.WorldDiscovered -= OnP2PWorldDiscovered;
        _p2pManager.WorldDiscovered += OnP2PWorldDiscovered;

        _p2pManager.PlayerConnected -= OnP2PPlayerConnected;
        _p2pManager.PlayerConnected += OnP2PPlayerConnected;

        _p2pManager.PlayerDisconnected -= OnP2PPlayerDisconnected;
        _p2pManager.PlayerDisconnected += OnP2PPlayerDisconnected;

        _p2pManager.SessionStateChanged -= OnP2PSessionStateChanged;
        _p2pManager.SessionStateChanged += OnP2PSessionStateChanged;

        _p2pManager.DiagnosticLogged -= OnP2PDiagnosticLogged;
        _p2pManager.DiagnosticLogged += OnP2PDiagnosticLogged;

        _p2pManager.ApprovalRequested = OnP2PApprovalRequested;
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageConfigurationChanged;
    }

    private void OnP2PDiagnosticLogged(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Dispatcher.UIThread.Post(() =>
        {
            if (_p2pLogLines.Count >= 150)
                _p2pLogLines.Dequeue();
            _p2pLogLines.Enqueue(line);
            P2pDiagnosticLogText = string.Join(Environment.NewLine, _p2pLogLines);
        });
    }

    [RelayCommand]
    private void ClearP2PLogs()
    {
        _p2pLogLines.Clear();
        P2pDiagnosticLogText = string.Empty;
    }

    private void OnLanguageConfigurationChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LanWorldEmptyWarningText = LocalizationManager.GetString("P2P.NoLanWorldsFoundAfterWait", "За 5 секунд пошуку активних LAN-світів не знайдено. Відкрийте світ для LAN у грі або введіть порт вручну.");
            if (DiscoveredLanWorlds.Count == 0)
            {
                LanWorldPlaceholder = IsLanWorldEmptyWarningVisible
                    ? LocalizationManager.GetString("P2P.NoLanWorldsFoundShort", "Світів LAN не знайдено")
                    : LocalizationManager.GetString("P2P.SearchingLanWorlds", "Пошук LAN-світів у мережі...");
            }
            if (!IsHostingActive && !IsHostBusy)
            {
                HostStatus = IsLanWorldEmptyWarningVisible
                    ? LanWorldEmptyWarningText
                    : LocalizationManager.GetString("P2P.WaitingLan", "Очікування запуску або відкриття LAN-світу у грі...");
            }
        });
    }

    private static T? ResolveService<T>() where T : class
    {
        var app = Application.Current as App;
        return app?.ServiceProvider?.GetService(typeof(T)) as T
               ?? ServiceLocator.Current?.GetService<T>();
    }

    partial void OnIsServersLoadingChanged(bool value) => OnPropertyChanged(nameof(IsLoading));
    partial void OnPageInfoChanged(string value) => OnPropertyChanged(nameof(PageInfoText));

    partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(200, token);
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentPage = 1;
                    UpdateDisplayedServers();
                });
            }
        }, token);
    }

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized || IsServersLoading) return;
        await LoadServersAsync();
    }

    [RelayCommand]
    public async Task LoadServersAsync()
    {
        if (IsServersLoading) return;
        IsServersLoading = true;
        _isInitialized = true;
        IsPaginationVisible = false;

        try
        {
            string serverUrl = Secrets._serversUrl ?? "https://cl-launcher.app/api/servers";
            var serversData = await _serverListService.GetServersAsync(serverUrl, null).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AllServers.Clear();

                if (serversData is { Count: > 0 })
                {
                    foreach (var s in serversData)
                    {
                        AllServers.Add(s);
                    }
                    CurrentPage = 1;
                    UpdateDisplayedServers();
                }
                else
                {
                    DisplayedServers.Clear();
                    ServerCards.Clear();
                    ShowNotification(
                        LocalizationManager.GetString("Servers.EmptyTitle", "Пусто"),
                        LocalizationManager.GetString("Servers.EmptyDesc", "На жаль, за вашим запитом сервери не знайдено."));
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FetchError: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ShowNotification(
                    LocalizationManager.GetString("Servers.FetchErrorTitle", "Помилка"),
                    string.Format(LocalizationManager.GetString("Servers.FetchErrorDesc", "Помилка при завантаженні серверів: {0}"), ex.Message));
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsServersLoading = false);
        }
    }

    private void UpdateDisplayedServers()
    {
        string query = SearchQuery?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? AllServers.ToList()
            : AllServers.Where(s =>
                (s.Name != null && s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Ip != null && s.Ip.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Version != null && s.Version.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        TotalPages = Math.Max(1, (int)Math.Ceiling((double)filtered.Count / ItemsPerPage));
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

        foreach (var card in ServerCards) card.Dispose();
        ServerCards.Clear();
        DisplayedServers.Clear();

        var pageItems = filtered.Skip((CurrentPage - 1) * ItemsPerPage).Take(ItemsPerPage);
        foreach (var item in pageItems)
        {
            DisplayedServers.Add(item);

            var cardVm = new ServerCardViewModel(
                item,
                async (ver, ip, port) => await LaunchServerAsync(ver, ip, port, item.Name));

            ServerCards.Add(cardVm);
        }

        CanGoPrev = CurrentPage > 1;
        CanGoNext = CurrentPage < TotalPages;
        IsPaginationVisible = filtered.Count > 0;
        PageInfo = string.Format(LocalizationManager.GetString("Servers.PageInfo", "Сторінка {0} з {1}"), CurrentPage, TotalPages);
    }

    [RelayCommand]
    public void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdateDisplayedServers();
        }
    }

    [RelayCommand]
    public void PrevPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdateDisplayedServers();
        }
    }

    [RelayCommand]
    private void OpenAllMcSite()
    {
        AvaloniaSoundService.Click();
        WebHelper.OpenUrl("https://allmc.in.ua/");
    }

    public async Task LaunchServerAsync(string version, string ip, int port, string serverName)
    {
        try
        {
            var accountService = ResolveService<AccountService>();

            if (accountService != null && (accountService.CurrentSession == null || accountService.CurrentProfile == null))
            {
                var profiles = accountService.GetProfiles();
                if (profiles is { Count: > 0 })
                {
                    int savedIndex = SettingsManager.Default.SelectIndexAccount;
                    var prof = (savedIndex >= 0 && savedIndex < profiles.Count) ? profiles[savedIndex] : profiles[0];
                    await accountService.SelectProfileAsync(prof);
                }
            }

            if (accountService?.CurrentSession == null || accountService.CurrentProfile == null)
            {
                var dialogService = ResolveService<IDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowMessageAsync(
                        LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Агов! Будь ласка, оберіть акаунт перед початком гри!"),
                        LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Акаунт не обраний"),
                        MascotEmotion.Alert);
                }
                return;
            }

            if (_lastActionService != null)
            {
                var action = new Dictionary<string, string>
                {
                    ["type"] = "server",
                    ["name"] = serverName ?? string.Empty,
                    ["version"] = version ?? string.Empty,
                    ["ip"] = ip ?? string.Empty,
                    ["port"] = port.ToString()
                };
                await _lastActionService.AddLastActionAsync(action);
            }

            var launchService = ResolveService<GameLaunchService>();
            if (launchService == null) return;

            var settings = SettingsManager.Default;
            int ram = settings.OP > 0 ? settings.OP : 2048;

            var config = await Task.Run(() => new LaunchConfiguration
            {
                MinimumRamMb = ram,
                MaximumRamMb = ram,
                ScreenWidth = settings.width > 0 ? settings.width : 854,
                ScreenHeight = settings.height > 0 ? settings.height : 480,
                IsFullscreen = settings.FullScreen,
                IsOffline = settings.OfflineModLauncher || !NetworkInterface.GetIsNetworkAvailable(),
                Session = accountService.CurrentSession,
                AccountType = accountService.CurrentProfile.TypeAccount,
                ServerIp = ip,
                ServerPort = port,
                ServerName = serverName
            });

            await launchService.LaunchGameAsync(LoaderType.Vanilla, version, null, config);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch server: {ex.Message}");
            ShowNotification(LocalizationManager.GetString("Dialogs.Error", "Помилка"), ex.Message);
        }
    }

    [RelayCommand]
    public void OpenUrl(string type)
    {
        switch (type)
        {
            case "bug":
                WebHelper.OpenUrl("https://discord.com/channels/1195118159187939458/1195494058571866172");
                break;
            case "youtube":
                WebHelper.OpenUrl("https://wiki.cl-launcher.app/wiki/%D0%93%D0%BE%D0%BB%D0%BE%D0%B2%D0%BD%D0%B0_%D1%81%D1%82%D0%BE%D1%80%D1%96%D0%BD%D0%BA%D0%B0");
                break;
            case "github":
                WebHelper.OpenUrl("https://github.com/WER-CORE/CL-OpenSource");
                break;
            case "support":
                WebHelper.OpenUrl("https://send.monobank.ua/jar/APZdKjTvuT");
                break;
        }
    }

    public void OpenExternalServerLink(string? url, string emptyKey, string emptyFallback)
    {
        if (!string.IsNullOrEmpty(url) && url != "-")
        {
            WebHelper.OpenUrl(url);
        }
        else
        {
            ShowNotification(
                LocalizationManager.GetString("Dialogs.Oops", "Упс"),
                LocalizationManager.GetString(emptyKey, emptyFallback));
        }
    }

    [RelayCommand]
    private async Task LoadFundraisersAsync()
    {
        IsFundraiserOverlayVisible = true;
        IsFundsLoading = true;
        Fundraisers.Clear();

        try
        {
            string json = await WebHelper.Client.GetStringAsync(Secrets.FundraiserURL);
            var dataList = await Task.Run(() => JsonConvert.DeserializeObject<List<FundraiserData3rd>>(json));
            var activeFunds = dataList?.Where(d => d.isActive).ToList();

            if (activeFunds is { Count: > 0 })
            {
                var imgTasks = activeFunds
                    .Where(f => !string.IsNullOrEmpty(f.imageUrl))
                    .Select(async f =>
                    {
                        try { f.ImageBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(f.imageUrl, 86); }
                        catch { }
                    });

                await Task.WhenAll(imgTasks);

                foreach (var fund in activeFunds)
                {
                    Fundraisers.Add(fund);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FundNetworkError: {ex.Message}");
        }
        finally
        {
            IsFundsLoading = false;
        }
    }

    [RelayCommand]
    private void CloseFundraisers() => IsFundraiserOverlayVisible = false;

    [RelayCommand]
    private async Task LoadNewsAsync()
    {
        IsNewsOverlayVisible = true;
        if (NewsItems.Count > 0 || _newsService == null) return;

        IsNewsLoading = true;
        try
        {
            var newsItems = await _newsService.GetNewsAsync().ConfigureAwait(false);
            if (newsItems != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var item in newsItems) NewsItems.Add(item);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NewsLoadError: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsNewsLoading = false);
        }
    }

    [RelayCommand]
    private void SelectNews(NewsItem? item) => SelectedNewsItem = item;

    [RelayCommand]
    private void CloseNews()
    {
        SelectedNewsItem = null;
        IsNewsOverlayVisible = false;
    }

    private static void ShowNotification(string title, string message)
    {
        var dialogService = ResolveService<IDialogService>();
        if (dialogService != null)
        {
            _ = dialogService.ShowMessageAsync(message, title, MascotEmotion.Confused);
        }
    }

    [RelayCommand]
    public void SelectPublicTab()
    {
        IsPublicServersTab = true;
        IsP2PTab = false;
    }

    [RelayCommand]
    public async Task SelectP2PTab()
    {
        IsPublicServersTab = false;
        IsP2PTab = true;
        await InitP2PAsync();
    }

    private void StartLanDiscoveryCountdown()
    {
        _lanDiscoveryTimeoutCts?.Cancel();
        if (DiscoveredLanWorlds.Count > 0 || IsHostingActive)
        {
            IsLanWorldEmptyWarningVisible = false;
            return;
        }

        IsLanWorldEmptyWarningVisible = false;
        LanWorldPlaceholder = LocalizationManager.GetString("P2P.SearchingLanWorlds", "Пошук LAN-світів у мережі...");
        HostStatus = LocalizationManager.GetString("P2P.WaitingLan", "Очікування запуску або відкриття LAN-світу у грі...");

        _lanDiscoveryTimeoutCts = new CancellationTokenSource();
        var token = _lanDiscoveryTimeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, token);
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (DiscoveredLanWorlds.Count == 0 && !IsHostingActive)
                        {
                            LanWorldPlaceholder = LocalizationManager.GetString("P2P.NoLanWorldsFoundShort", "Світів LAN не знайдено");
                            LanWorldEmptyWarningText = LocalizationManager.GetString("P2P.NoLanWorldsFoundAfterWait", "За 5 секунд пошуку активних LAN-світів не знайдено. Відкрийте світ для LAN у грі або введіть порт вручну.");
                            IsLanWorldEmptyWarningVisible = true;
                            if (!IsHostBusy)
                            {
                                HostStatus = LanWorldEmptyWarningText;
                            }
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task InitP2PAsync()
    {
        if (!IsHostingActive)
        {
            StartLanDiscoveryCountdown();
            await _p2pManager.StartDiscoveryAsync();
        }
    }

    private void OnP2PWorldDiscovered(P2PLanWorld world)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lanDiscoveryTimeoutCts?.Cancel();
            IsLanWorldEmptyWarningVisible = false;
            var existing = DiscoveredLanWorlds.FirstOrDefault(w => w.Port == world.Port);
            if (existing == null)
            {
                DiscoveredLanWorlds.Add(world);
                if (SelectedLanWorld == null) SelectedLanWorld = world;
            }
            else
            {
                int index = DiscoveredLanWorlds.IndexOf(existing);
                DiscoveredLanWorlds[index] = world;
                if (SelectedLanWorld?.Port == world.Port) SelectedLanWorld = world;
            }
            if (!IsHostingActive && !IsHostBusy)
            {
                HostStatus = LocalizationManager.GetString("P2P.WaitingLan", "Очікування запуску або відкриття LAN-світу у грі...");
            }
        });
    }

    [RelayCommand]
    public async Task RefreshDiscoveryAsync()
    {
        if (IsDiscoveryRefreshing) return;
        IsDiscoveryRefreshing = true;
        try
        {
            StartLanDiscoveryCountdown();
            await _p2pManager.RefreshDiscoveryAsync();

            var active = _p2pManager.DiscoveredWorlds.ToList();

            for (int i = DiscoveredLanWorlds.Count - 1; i >= 0; i--)
            {
                var current = DiscoveredLanWorlds[i];
                if (!active.Any(a => a.Port == current.Port))
                {
                    DiscoveredLanWorlds.RemoveAt(i);
                }
            }

            foreach (var world in active)
            {
                var existing = DiscoveredLanWorlds.FirstOrDefault(w => w.Port == world.Port);
                if (existing == null)
                {
                    DiscoveredLanWorlds.Add(world);
                }
                else
                {
                    int idx = DiscoveredLanWorlds.IndexOf(existing);
                    DiscoveredLanWorlds[idx] = world;
                }
            }

            if (SelectedLanWorld == null || !DiscoveredLanWorlds.Any(w => w.Port == SelectedLanWorld.Port))
            {
                SelectedLanWorld = DiscoveredLanWorlds.FirstOrDefault();
            }

            if (DiscoveredLanWorlds.Count > 0)
            {
                _lanDiscoveryTimeoutCts?.Cancel();
                IsLanWorldEmptyWarningVisible = false;
            }
        }
        finally
        {
            IsDiscoveryRefreshing = false;
        }
    }

    [RelayCommand]
    public async Task AddManualPortAsync()
    {
        if (int.TryParse(ManualPortInput?.Trim(), out int port) && port > 0 && port <= 65535)
        {
            var world = await _p2pManager.ScanOrAddPortAsync(port);
            if (world != null)
            {
                _lanDiscoveryTimeoutCts?.Cancel();
                IsLanWorldEmptyWarningVisible = false;
                SelectedLanWorld = world;
                ManualPortInput = string.Empty;
                AvaloniaNotificationService.ShowNotification(
                    LocalizationManager.GetString("Generic.Success", "Успіх"),
                    string.Format(LocalizationManager.GetString("P2P.PortAddedSuccess", "Світ на порті {0} успішно додано!"), port),
                    sec: 2);
            }
            else
            {
                ShowNotification(
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    string.Format(LocalizationManager.GetString("P2P.PortAddError", "Не вдалося підключитися до порту {0}. Переконайтеся, що світ відкрито для LAN у грі."), port));
            }
        }
    }

    private void OnP2PPlayerConnected(P2PPlayer player)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ConnectedPlayers.Any(p => p.ConnectionId == player.ConnectionId))
            {
                ConnectedPlayers.Add(player);
                AvaloniaNotificationService.ShowNotification(
                    LocalizationManager.GetString("P2P.PlayerJoinedTitle", "Гравець приєднався"),
                    string.Format(LocalizationManager.GetString("P2P.PlayerJoined", "{0} увійшов у гру!"), player.PlayerName),
                    sec: 3);
            }
        });
    }

    private void OnP2PPlayerDisconnected(P2PPlayer player)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = ConnectedPlayers.FirstOrDefault(p => p.ConnectionId == player.ConnectionId);
            if (existing != null)
            {
                ConnectedPlayers.Remove(existing);
                AvaloniaNotificationService.ShowNotification(
                    LocalizationManager.GetString("P2P.PlayerLeftTitle", "Гравець вийшов"),
                    string.Format(LocalizationManager.GetString("P2P.PlayerLeft", "{0} покинув гру."), player.PlayerName),
                    sec: 3);
            }
        });
    }

    private async Task<bool> OnP2PApprovalRequested(P2PPlayer player)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialogService = ResolveService<IDialogService>();
            if (dialogService == null) return true;

            return await dialogService.AskQuestionAsync(
                string.Format(LocalizationManager.GetString("P2P.ApprovalPromptDesc", "Гравець {0} хоче приєднатися до вашої гри.\n\nДозволити вхід?"), player.PlayerName),
                LocalizationManager.GetString("P2P.ApprovalPromptTitle", "Запит на підключення (P2P)"),
                MascotEmotion.Normal);
        });
    }

    private void OnP2PSessionStateChanged(P2PRoomSession? session)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsHostingActive = session != null && session.IsActive;
            if (session != null && session.IsActive)
            {
                HostRoomCode = session.RoomCode;
                HostDirectAddress = !string.IsNullOrEmpty(session.PublicDomain)
                    ? session.PublicDomain
                    : (session.PublicPort == 25565 ? session.HostAddress : $"{session.HostAddress}:{session.PublicPort}");
                HasDirectAddress = !string.IsNullOrWhiteSpace(HostDirectAddress);
                HostWorldName = session.WorldName;
                string loaderInfo = !string.IsNullOrEmpty(session.LoaderType) && session.LoaderType != "Vanilla"
                    ? (!string.IsNullOrEmpty(session.LoaderVersion) ? $" ({session.LoaderType} {session.LoaderVersion})" : $" ({session.LoaderType})")
                    : string.Empty;
                HostStatus = string.Format(LocalizationManager.GetString("P2P.ActiveHostingWaiting", "Спільна гра активна! Версія: {0}. Очікування друзів."), $"{session.GameVersion}{loaderInfo}");
            }
            else
            {
                HostRoomCode = string.Empty;
                HostDirectAddress = string.Empty;
                HasDirectAddress = false;
                HostWorldName = string.Empty;
                HostStatus = LocalizationManager.GetString("P2P.WaitingLan", "Очікування запуску або відкриття LAN-світу у грі...");
                ConnectedPlayers.Clear();
            }
        });
    }

    [RelayCommand]
    public async Task StartHostingAsync()
    {
        var targetWorld = SelectedLanWorld ?? DiscoveredLanWorlds.FirstOrDefault();
        if (targetWorld == null)
        {
            ShowNotification(
                LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                LocalizationManager.GetString("P2P.NeedOpenLanFirst", "Спочатку відкрийте світ для мережі в грі Minecraft (Відкрити для LAN)."));
            return;
        }

        IsHostBusy = true;
        HostStatus = LocalizationManager.GetString("P2P.OpeningRoomStatus", "Визначення версії та відкриття кімнати...");
        try
        {
            var accountService = ResolveService<AccountService>();
            string hostNick = accountService?.CurrentProfile?.NameAccount ?? accountService?.CurrentSession?.Username ?? string.Empty;

            string gameVersion = targetWorld.GameVersion ?? "1.20.1";
            string loaderType = targetWorld.LoaderType ?? "Vanilla";
            string loaderVersion = targetWorld.LoaderVersion ?? GameLaunchService.ActiveLoaderVersion ?? string.Empty;
            string? modsDirectory = targetWorld.ModsDirectory;

            if (string.IsNullOrWhiteSpace(modsDirectory) && !string.IsNullOrEmpty(GameLaunchService.ActiveGamePath))
            {
                string candidateMods = System.IO.Path.Combine(GameLaunchService.ActiveGamePath, "mods");
                if (System.IO.Directory.Exists(candidateMods)) modsDirectory = candidateMods;
            }

            var pingResult = await MinecraftPingHelper.TryGetServerInfoAsync(targetWorld.Port);
            if (pingResult != null && !string.IsNullOrWhiteSpace(pingResult.Value.Version))
            {
                gameVersion = pingResult.Value.Version;
                if (!string.IsNullOrEmpty(pingResult.Value.Loader) && pingResult.Value.Loader != "Vanilla")
                {
                    loaderType = pingResult.Value.Loader;
                }
            }
            else if (!string.IsNullOrEmpty(GameLaunchService.ActiveMinecraftVersion))
            {
                gameVersion = GameLaunchService.ActiveMinecraftVersion;
                loaderType = GameLaunchService.ActiveLoaderType.ToString();
            }

            if (loaderType == "Vanilla" && GameLaunchService.ActiveLoaderType != LoaderType.Vanilla)
            {
                loaderType = GameLaunchService.ActiveLoaderType.ToString();
            }

            if (string.IsNullOrEmpty(loaderVersion) && !string.IsNullOrEmpty(GameLaunchService.ActiveLoaderVersion))
            {
                loaderVersion = GameLaunchService.ActiveLoaderVersion;
            }

            if (string.IsNullOrEmpty(targetWorld.ModpackName) && !string.IsNullOrEmpty(GameLaunchService.ActiveModpackName))
            {
                targetWorld.ModpackName = GameLaunchService.ActiveModpackName;
            }

            if (!IsP2PVersionSupported(gameVersion))
            {
                ShowNotification(
                    LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                    LocalizationManager.GetString("P2P.UnsupportedVersion", "Спільна гра підтримується для версій Minecraft 1.7.2 та новіших."));
                HostStatus = LocalizationManager.GetString("P2P.UnsupportedVersionStatus", "Версія не підтримується для спільної гри (потрібна 1.7.2+).");
                return;
            }

            var session = await _p2pManager.StartHostingAsync(targetWorld, gameVersion, loaderType, loaderVersion, modsDirectory, RequireApproval, hostNick);
            if (session == null)
            {
                HostStatus = LocalizationManager.GetString("P2P.OpenRoomFailedStatus", "Не вдалося відкрити кімнату. Перевірте з'єднання з мережею.");
            }
            else if (AllowOfflinePlayers && accountService?.CurrentProfile?.TypeAccount == AccountType.Microsoft)
            {
                HostStatus = LocalizationManager.GetString("P2P.ActiveHostingOfflineTip", "Спільна гра активна! Порада: для входу друзів без ліцензії хосту рекомендовано відкривати світ з офлайн-акаунта.");
            }
        }
        catch (Exception ex)
        {
            HostStatus = $"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}";
        }
        finally
        {
            IsHostBusy = false;
        }
    }

    [RelayCommand]
    public async Task StopHostingAsync()
    {
        IsHostBusy = true;
        try
        {
            await _p2pManager.StopHostingAsync();

            if (IsGuestRoomFound)
            {
                IsGuestRoomFound = false;
                _guestPayload = null;
                GuestStatusMessage = LocalizationManager.GetString("P2P.RoomClosed", "Кімнату закрито (хост зупинив спільну гру).");
            }
        }
        finally
        {
            IsHostBusy = false;
        }
    }

    [RelayCommand]
    public async Task CopyRoomCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(HostRoomCode)) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(HostRoomCode);
            AvaloniaNotificationService.ShowNotification(
                LocalizationManager.GetString("Generic.Success", "Успіх"),
                LocalizationManager.GetString("P2P.RoomCodeCopied", "Код скопійовано"),
                sec: 2);
        }
    }

    [RelayCommand]
    public async Task CopyDirectAddressAsync()
    {
        if (string.IsNullOrWhiteSpace(HostDirectAddress)) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
        {
            await clipboard.SetTextAsync(HostDirectAddress);
            AvaloniaNotificationService.ShowNotification(
                LocalizationManager.GetString("Generic.Success", "Успіх"),
                LocalizationManager.GetString("P2P.DirectAddressCopied", "IP скопійовано"),
                sec: 2);
        }
    }

    [RelayCommand]
    public void KickPlayer(P2PPlayer? player)
    {
        if (player != null && !string.IsNullOrEmpty(player.ConnectionId))
        {
            _p2pManager.KickPlayer(player.ConnectionId);
        }
    }

    [RelayCommand]
    public async Task CheckRoomCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(InputRoomCode)) return;

        IsGuestChecking = true;
        GuestStatusMessage = LocalizationManager.GetString("P2P.SearchingRoom", "Перевірка зв'язку з хостом...");
        try
        {
            var (payload, success, error) = await _p2pManager.ValidateRoomCodeAsync(InputRoomCode);
            if (success && payload != null)
            {
                if (!IsP2PVersionSupported(payload.GameVersion))
                {
                    IsGuestRoomFound = false;
                    _guestPayload = null;
                    GuestStatusMessage = LocalizationManager.GetString("P2P.UnsupportedHostVersionStatus", "Версія хоста не підтримується для спільної гри (потрібна 1.7.2+).");
                    ShowNotification(
                        LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                        LocalizationManager.GetString("P2P.UnsupportedVersion", "Спільна гра підтримується для версій Minecraft 1.7.2 та новіших."));
                    return;
                }

                _guestPayload = payload;
                GuestWorldName = payload.WorldName;
                GuestGameVersion = payload.GameVersion;
                GuestLoaderType = !string.IsNullOrEmpty(payload.LoaderVersion)
                    ? $"{payload.LoaderType} {payload.LoaderVersion}"
                    : payload.LoaderType;
                IsGuestRoomFound = true;

                string loaderInfo = !string.IsNullOrEmpty(payload.LoaderType) && payload.LoaderType != "Vanilla"
                    ? (!string.IsNullOrEmpty(payload.LoaderVersion) ? $" ({payload.LoaderType} {payload.LoaderVersion})" : $" ({payload.LoaderType})")
                    : string.Empty;

                if (!string.IsNullOrEmpty(payload.ModpackName))
                {
                    GuestStatusMessage = string.Format(
                        LocalizationManager.GetString("P2P.JoiningModpackSuccess", "Знайдено світ: {0} (Збірка: {1}, {2})"),
                        payload.WorldName,
                        payload.ModpackName,
                        $"{payload.GameVersion}{loaderInfo}");
                }
                else
                {
                    GuestStatusMessage = string.Format(
                        LocalizationManager.GetString("P2P.JoiningRoomSuccess", "Знайдено світ: {0} (версія {1})"),
                        payload.WorldName,
                        $"{payload.GameVersion}{loaderInfo}");
                }
            }
            else
            {
                IsGuestRoomFound = false;
                _guestPayload = null;
                GuestStatusMessage = error ?? LocalizationManager.GetString("P2P.RoomNotFound", "Невірний або недійсний код кімнати.");
            }
        }
        finally
        {
            IsGuestChecking = false;
        }
    }

    [RelayCommand]
    public async Task JoinAndPlayAsync()
    {
        if (_guestPayload == null) return;

        if (!IsP2PVersionSupported(_guestPayload.GameVersion))
        {
            ShowNotification(
                LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                LocalizationManager.GetString("P2P.UnsupportedVersion", "Спільна гра підтримується для версій Minecraft 1.7.2 та новіших."));
            return;
        }

        IsGuestLaunching = true;
        GuestStatusMessage = LocalizationManager.GetString("P2P.LaunchingClientStatus", "Запуск клієнта та підключення до світу...");
        try
        {
            var accountService = ResolveService<AccountService>();
            var launchService = ResolveService<GameLaunchService>();
            if (accountService == null || launchService == null) return;

            var settings = SettingsManager.Default;
            int ram = Math.Clamp(settings.OP > 0 ? settings.OP : 2048, 1024, 32768);

            var guestSession = accountService.CurrentSession;
            var guestAccountType = accountService.CurrentProfile?.TypeAccount ?? AccountType.Offline;
            if (guestAccountType == AccountType.Offline && guestSession != null && string.IsNullOrEmpty(guestSession.UserType))
            {
                guestSession.UserType = "legacy";
            }

            var config = new LaunchConfiguration
            {
                MinimumRamMb = ram,
                MaximumRamMb = ram,
                ScreenWidth = settings.width > 0 ? settings.width : 854,
                ScreenHeight = settings.height > 0 ? settings.height : 480,
                IsFullscreen = settings.FullScreen,
                IsOffline = settings.OfflineModLauncher || !NetworkInterface.GetIsNetworkAvailable(),
                Session = guestSession,
                AccountType = guestAccountType,
                ServerIp = _guestPayload.HostAddress,
                ServerPort = _guestPayload.Port,
                ServerName = _guestPayload.WorldName,
                JoinServer = true,
                AllowOfflineLan = AllowOfflinePlayers
            };

            if (!string.IsNullOrEmpty(_guestPayload.ModpackName))
            {
                var modpackService = ResolveService<ModpackService>();
                var installedModpacks = modpackService?.LoadInstalledModpacks();
                var matchingPack = installedModpacks?.FirstOrDefault(p =>
                    string.Equals(p.Name, _guestPayload.ModpackName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(_guestPayload.GameVersion) || string.Equals(p.MinecraftVersion, _guestPayload.GameVersion, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(_guestPayload.LoaderType) || string.Equals(p.LoaderType, _guestPayload.LoaderType, StringComparison.OrdinalIgnoreCase)));

                if (matchingPack != null)
                {
                    matchingPack.EnterInServer = true;
                    matchingPack.ServerIP = $"{_guestPayload.HostAddress}:{_guestPayload.Port}";

                    string mcVer = !string.IsNullOrWhiteSpace(matchingPack.MinecraftVersion) && !matchingPack.MinecraftVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                        ? matchingPack.MinecraftVersion
                        : _guestPayload.GameVersion;
                    string loaderVer = !string.IsNullOrWhiteSpace(matchingPack.LoaderVersion) && !matchingPack.LoaderVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                        ? matchingPack.LoaderVersion
                        : _guestPayload.LoaderVersion;
                    string loaderType = !string.IsNullOrWhiteSpace(matchingPack.LoaderType) && !matchingPack.LoaderType.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                        ? matchingPack.LoaderType
                        : _guestPayload.LoaderType;

                    modpackService!.PlayModPack(
                        mcVer,
                        loaderVer,
                        loaderType,
                        matchingPack.Name,
                        matchingPack.Path,
                        matchingPack.PathJson,
                        matchingPack.TypeSite,
                        matchingPack.JavaPath,
                        config
                    );
                    return;
                }
                else
                {
                    if (string.Equals(_guestPayload.ModpackTypeSite, "Custom", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowNotification(
                            LocalizationManager.GetString("P2P.CustomModpackTitle", "Користувацька збірка"),
                            string.Format(
                                LocalizationManager.GetString("P2P.CustomModpackDesc", "Збірку «{0}» ({1}, {2}) створено вручну в CL.\n\nЇї немає в публічних каталогах Modrinth або CurseForge. Будь ласка, перенесіть файли збірки з комп'ютера хоста та імпортуйте їх у вкладці «Збірки»."),
                                _guestPayload.ModpackName,
                                _guestPayload.GameVersion,
                                _guestPayload.LoaderType));
                        GuestStatusMessage = LocalizationManager.GetString("P2P.CustomModpackStatus", "Для входу потрібна локальна збірка хоста.");
                        return;
                    }

                    var dialogService = ResolveService<IDialogService>();
                    string question = string.Format(
                        LocalizationManager.GetString("P2P.ModpackInstallQuestion", "Збірку «{0}» ({1}, {2}) не знайдено серед встановлених.\n\nЗавантажити та встановити її автоматично для входу в кімнату?"),
                        _guestPayload.ModpackName,
                        _guestPayload.GameVersion,
                        _guestPayload.LoaderType);
                    bool shouldDownload = dialogService != null && await dialogService.AskQuestionAsync(
                        question,
                        LocalizationManager.GetString("P2P.ModpackInstallTitle", "Встановлення збірки"),
                        MascotEmotion.Confused);

                    if (shouldDownload && modpackService != null)
                    {
                        GuestStatusMessage = string.Format(
                            LocalizationManager.GetString("P2P.ModpackDownloading", "Завантаження збірки «{0}»..."),
                            _guestPayload.ModpackName);
                        var installedPack = await modpackService.AutoDownloadAndInstallModpackAsync(
                            _guestPayload.ModpackName,
                            _guestPayload.GameVersion,
                            _guestPayload.LoaderType,
                            (status, _) =>
                            {
                                Dispatcher.UIThread.Post(() => GuestStatusMessage = status);
                            },
                            typeSite: _guestPayload.ModpackTypeSite
                        );

                        if (installedPack != null)
                        {
                            installedPack.EnterInServer = true;
                            installedPack.ServerIP = $"{_guestPayload.HostAddress}:{_guestPayload.Port}";

                            string mcVer = !string.IsNullOrWhiteSpace(installedPack.MinecraftVersion) && !installedPack.MinecraftVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                                ? installedPack.MinecraftVersion
                                : _guestPayload.GameVersion;
                            string loaderVer = !string.IsNullOrWhiteSpace(installedPack.LoaderVersion) && !installedPack.LoaderVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                                ? installedPack.LoaderVersion
                                : _guestPayload.LoaderVersion;
                            string loaderType = !string.IsNullOrWhiteSpace(installedPack.LoaderType) && !installedPack.LoaderType.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                                ? installedPack.LoaderType
                                : _guestPayload.LoaderType;

                            GuestStatusMessage = LocalizationManager.GetString("P2P.ModpackLaunching", "Запуск завантаженої збірки...");
                            modpackService.PlayModPack(
                                mcVer,
                                loaderVer,
                                loaderType,
                                installedPack.Name,
                                installedPack.Path,
                                installedPack.PathJson,
                                installedPack.TypeSite,
                                installedPack.JavaPath,
                                config
                            );
                            return;
                        }
                        else
                        {
                            ShowNotification(
                                LocalizationManager.GetString("P2P.ModpackDownloadFailTitle", "Помилка завантаження"),
                                string.Format(
                                    LocalizationManager.GetString("P2P.ModpackDownloadFailDesc", "Не вдалося автоматично знайти або завантажити збірку «{0}».\n\nБудь ласка, встановіть або імпортуйте її у вкладці «Збірки»."),
                                    _guestPayload.ModpackName));
                            return;
                        }
                    }
                    else
                    {
                        GuestStatusMessage = LocalizationManager.GetString("P2P.JoinCancelledNoModpack", "Приєднання скасовано: відсутня потрібна збірка.");
                        return;
                    }
                }
            }

            string version = !string.IsNullOrEmpty(_guestPayload.GameVersion) ? _guestPayload.GameVersion : "1.20.1";
            var loader = LoaderType.Vanilla;
            if (string.Equals(_guestPayload.LoaderType, "Fabric", StringComparison.OrdinalIgnoreCase)) loader = LoaderType.Fabric;
            else if (string.Equals(_guestPayload.LoaderType, "Forge", StringComparison.OrdinalIgnoreCase)) loader = LoaderType.Forge;
            else if (string.Equals(_guestPayload.LoaderType, "NeoForge", StringComparison.OrdinalIgnoreCase)) loader = LoaderType.NeoForge;

            await launchService.LaunchGameAsync(loader, version, _guestPayload.LoaderVersion, config);
        }
        catch (Exception ex)
        {
            GuestStatusMessage = string.Format(LocalizationManager.GetString("P2P.ConnectError", "Помилка підключення: {0}"), ex.Message);
        }
        finally
        {
            IsGuestLaunching = false;
        }
    }

    public static bool IsP2PVersionSupported(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return true;
        version = version.Trim().ToLowerInvariant();

        if (version.StartsWith("a") || version.StartsWith("b") || version.StartsWith("c") ||
            version.Contains("alpha") || version.Contains("beta") || version.Contains("infdev") || version.Contains("indev"))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(version, @"^1\.(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int minor))
        {
            return minor >= 7;
        }

        return true;
    }
}
