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
        SelectedServerBg = null;
        IsServerInfoPanelVisible = false;
        IsNewsOverlayVisible = false;
        IsFundraiserOverlayVisible = false;
        _searchCts?.Cancel();
        foreach (var card in ServerCards)
        {
            card.Dispose();
        }
        ServerCards.Clear();
        _isInitialized = false;


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
    public ObservableCollection<ServerInfo> PartnerServers { get; } = new();
    public ObservableCollection<FundraiserData3rd> Fundraisers { get; } = new();
    public ObservableCollection<NewsItem> NewsItems { get; } = new();
    public ObservableCollection<string> SelectedServerVersions { get; } = new();

    public ObservableCollection<ServerInfo> CurrentPageServers => DisplayedServers;

    public Action<ServerCardViewModel, ServerInfo>? OnServerInfoRequested { get; set; }

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

    [ObservableProperty] private ServerInfo? _selectedServer;
    [ObservableProperty] private bool _isServerInfoPanelVisible;
    [ObservableProperty] private string _selectedServerVersion = string.Empty;
    [ObservableProperty] private Bitmap? _selectedServerBg;

    [ObservableProperty] private bool _isFundraiserOverlayVisible;
    [ObservableProperty] private bool _isNewsOverlayVisible;
    [ObservableProperty] private NewsItem? _selectedNewsItem;
    public ObservableCollection<PartnerServerCardViewModel> PartnerServerCards { get; } = new();
    public ObservableCollection<NewsCardViewModel> NewsCards { get; } = new();
    private readonly P2PManagerService _p2pManager;

    [ObservableProperty] private bool _isPublicServersTab = true;
    [ObservableProperty] private bool _isP2PTab;

    public ObservableCollection<P2PLanWorld> DiscoveredLanWorlds { get; } = new();
    [ObservableProperty] private P2PLanWorld? _selectedLanWorld;
    [ObservableProperty] private string _manualPortInput = string.Empty;
    [ObservableProperty] private bool _isDiscoveryRefreshing;
    [ObservableProperty] private bool _isHostingActive;
    [ObservableProperty] private string _hostRoomCode = string.Empty;
    [ObservableProperty] private string _hostWorldName = string.Empty;
    [ObservableProperty] private string _hostStatus = "Очікування запуску або відкриття LAN-світу у грі...";
    [ObservableProperty] private bool _requireApproval;
    [ObservableProperty] private bool _allowOfflinePlayers = true;
    [ObservableProperty] private bool _isHostBusy;
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

        _p2pManager.ApprovalRequested = OnP2PApprovalRequested;
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
                PartnerServers.Clear();

                if (serversData is { Count: > 0 })
                {
                    foreach (var s in serversData.OrderByDescending(x => x.Priority))
                    {
                        if (s.IsPartner) PartnerServers.Add(s);
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
        DisplayedServers.Clear();
        ServerCards.Clear();
        string query = SearchQuery?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? AllServers.ToList()
            : AllServers.Where(s =>
                (s.Name != null && s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Description != null && s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Ip != null && s.Ip.Contains(query, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        TotalPages = Math.Max(1, (int)Math.Ceiling((double)filtered.Count / ItemsPerPage));
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
        DisplayedServers.Clear();
        foreach (var card in ServerCards) card.Dispose();
        ServerCards.Clear();

        var pageItems = filtered.Skip((CurrentPage - 1) * ItemsPerPage).Take(ItemsPerPage);
        foreach (var item in pageItems)
        {
            DisplayedServers.Add(item);

            var cardVm = new ServerCardViewModel(
                item,
                async (ver, ip, port) => await LaunchServerAsync(ver, ip, port, item.Name),
                (card, data) =>
                {
                    OnServerInfoRequested?.Invoke(card, data);
                    _ = LoadServerBackgroundAsync(data, card.Icon);
                });

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
    public void SelectServer(ServerInfo? server)
    {
        if (server == null) return;
        SelectedServer = server;
        SelectedServerVersions.Clear();

        if (server.VersionsSupported is { Count: > 0 })
        {
            foreach (var v in server.VersionsSupported) SelectedServerVersions.Add(v);
            SelectedServerVersion = SelectedServerVersions[0];
        }
        else
        {
            SelectedServerVersion = server.Version;
        }

        IsServerInfoPanelVisible = true;
    }

    public async Task LoadServerBackgroundAsync(ServerInfo data, Bitmap? fallbackIcon)
    {
        SelectServer(data);
        SelectedServerBg = null;

        if (data.Priority > 0 && !string.IsNullOrEmpty(data.BgUrl))
        {
            try
            {
                var bgImg = await AvaloniaImageHelper.LoadOptimizedImageAsync(data.BgUrl, 400).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => SelectedServerBg = bgImg ?? fallbackIcon);
            }
            catch
            {
                SelectedServerBg = fallbackIcon;
            }
        }
        else
        {
            SelectedServerBg = fallbackIcon;
        }
    }

    [RelayCommand]
    public void CloseServerInfo()
    {
        IsServerInfoPanelVisible = false;
        SelectedServer = null;
    }

    [RelayCommand]
    public async Task PlayServerAsync(ServerInfo? server)
    {
        if (server == null) return;
        string targetVersion = SelectedServerVersions.Any() ? SelectedServerVersion : server.Version;
        await LaunchServerAsync(targetVersion, server.Ip, server.Port, server.Name);
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
            case "discord":
                OpenExternalServerLink(SelectedServer?.DiscordLink, "Servers.ServerNoDiscord", "У цього сервера немає Discord каналу.");
                break;
            case "site":
                OpenExternalServerLink(SelectedServer?.SiteLink, "Servers.ServerNoSite", "Сайт не вказано.");
                break;
            case "donate":
                OpenExternalServerLink(SelectedServer?.DonateLink, "Servers.ServerNoDonate", "Посилання на донат відсутнє.");
                break;
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

    private async Task InitP2PAsync()
    {
        if (!IsHostingActive)
        {
            await _p2pManager.StartDiscoveryAsync();
        }
    }

    private void OnP2PWorldDiscovered(P2PLanWorld world)
    {
        Dispatcher.UIThread.Post(() =>
        {
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
        });
    }

    [RelayCommand]
    public async Task RefreshDiscoveryAsync()
    {
        if (IsDiscoveryRefreshing) return;
        IsDiscoveryRefreshing = true;
        try
        {
            await _p2pManager.RefreshDiscoveryAsync();
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
                SelectedLanWorld = world;
                ManualPortInput = string.Empty;
                AvaloniaNotificationService.ShowNotification("Успіх", $"Світ на порті {port} успішно додано!", sec: 2);
            }
            else
            {
                ShowNotification("Помилка", $"Не вдалося підключитися до порту {port}. Переконайтеся, що світ відкрито для LAN у грі.");
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
                AvaloniaNotificationService.ShowNotification("Гравець приєднався", $"{player.PlayerName} увійшов у гру!", sec: 3);
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
                AvaloniaNotificationService.ShowNotification("Гравець вийшов", $"{player.PlayerName} покинув гру.", sec: 3);
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
                $"Гравець {player.PlayerName} хоче приєднатися до вашої гри.\n\nДозволити вхід?",
                "Запит на підключення (P2P)",
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
                HostWorldName = session.WorldName;
                string loaderInfo = !string.IsNullOrEmpty(session.LoaderType) && session.LoaderType != "Vanilla"
                    ? (!string.IsNullOrEmpty(session.LoaderVersion) ? $" ({session.LoaderType} {session.LoaderVersion})" : $" ({session.LoaderType})")
                    : string.Empty;
                HostStatus = $"Спільна гра активна! Версія: {session.GameVersion}{loaderInfo}. Очікування друзів.";
            }
            else
            {
                HostRoomCode = string.Empty;
                HostWorldName = string.Empty;
                HostStatus = "Очікування запуску або відкриття LAN-світу у грі...";
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
            ShowNotification(LocalizationManager.GetString("Dialogs.Alert", "Увага"), "Спочатку відкрийте світ для мережі в грі Minecraft (Відкрити для LAN).");
            return;
        }

        IsHostBusy = true;
        HostStatus = "Визначення версії та відкриття кімнати...";
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

            var session = await _p2pManager.StartHostingAsync(targetWorld, gameVersion, loaderType, loaderVersion, modsDirectory, RequireApproval, hostNick);
            if (session == null)
            {
                HostStatus = "Не вдалося відкрити кімнату. Перевірте з'єднання з мережею.";
            }
            else if (AllowOfflinePlayers && accountService?.CurrentProfile?.TypeAccount == AccountType.Microsoft)
            {
                HostStatus = "Спільна гра активна! Порада: для входу друзів без ліцензії хосту рекомендовано відкривати світ з офлайн-акаунта.";
            }
        }
        catch (Exception ex)
        {
            HostStatus = $"Помилка: {ex.Message}";
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
                GuestStatusMessage = "Кімнату закрито (хост зупинив спільну гру).";
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
            AvaloniaNotificationService.ShowNotification("Успіх", "Код кімнати скопійовано в буфер обміну!", sec: 3);
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
        GuestStatusMessage = "Перевірка зв'язку з хостом...";
        try
        {
            var (payload, success, error) = await _p2pManager.ValidateRoomCodeAsync(InputRoomCode);
            if (success && payload != null)
            {
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
                    GuestStatusMessage = $"Знайдено світ: {payload.WorldName} (Збірка: {payload.ModpackName}, {payload.GameVersion}{loaderInfo})";
                }
                else
                {
                    GuestStatusMessage = $"Знайдено світ: {payload.WorldName} (версія {payload.GameVersion}{loaderInfo})";
                }
            }
            else
            {
                IsGuestRoomFound = false;
                _guestPayload = null;
                GuestStatusMessage = error ?? "Невірний або недійсний код кімнати.";
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

        IsGuestLaunching = true;
        GuestStatusMessage = "Запуск клієнта та підключення до світу...";
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
                        ShowNotification("Користувацька збірка",
                            $"Збірку «{_guestPayload.ModpackName}» ({_guestPayload.GameVersion}, {_guestPayload.LoaderType}) створено вручну в CL.\n\nЇї немає в публічних каталогах Modrinth або CurseForge. Будь ласка, перенесіть файли збірки з комп'ютера хоста та імпортуйте їх у вкладці «Збірки».");
                        GuestStatusMessage = "Для входу потрібна локальна збірка хоста.";
                        return;
                    }

                    var dialogService = ResolveService<IDialogService>();
                    string question = $"Збірку «{_guestPayload.ModpackName}» ({_guestPayload.GameVersion}, {_guestPayload.LoaderType}) не знайдено серед встановлених.\n\nЗавантажити та встановити її автоматично для входу в кімнату?";
                    bool shouldDownload = dialogService != null && await dialogService.AskQuestionAsync(question, "Встановлення збірки", MascotEmotion.Confused);

                    if (shouldDownload && modpackService != null)
                    {
                        GuestStatusMessage = $"Завантаження збірки «{_guestPayload.ModpackName}»...";
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

                            GuestStatusMessage = "Запуск завантаженої збірки...";
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
                            ShowNotification("Помилка завантаження",
                                $"Не вдалося автоматично знайти або завантажити збірку «{_guestPayload.ModpackName}».\n\nБудь ласка, встановіть або імпортуйте її у вкладці «Збірки».");
                            return;
                        }
                    }
                    else
                    {
                        GuestStatusMessage = "Приєднання скасовано: відсутня потрібна збірка.";
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
            GuestStatusMessage = $"Помилка підключення: {ex.Message}";
        }
        finally
        {
            IsGuestLaunching = false;
        }
    }
}
