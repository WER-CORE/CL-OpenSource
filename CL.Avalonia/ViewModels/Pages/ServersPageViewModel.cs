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

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class ServersPageViewModel : ObservableObject
{
    private readonly ServerListService _serverListService;
    private readonly LastActionService? _lastActionService;
    private readonly NewsService? _newsService;
    private readonly GameSessionManager? _sessionManager;

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
    public ServersPageViewModel() : this(
        ResolveService<ServerListService>() ?? new ServerListService(),
        ResolveService<LastActionService>(),
        ResolveService<NewsService>(),
        ResolveService<GameSessionManager>())
    {
    }

    public ServersPageViewModel(
        ServerListService serverListService,
        LastActionService? lastActionService,
        NewsService? newsService,
        GameSessionManager? sessionManager)
    {
        _serverListService = serverListService;
        _lastActionService = lastActionService;
        _newsService = newsService;
        _sessionManager = sessionManager;
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
                ServerPort = (short)port,
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
}
