using Avalonia.Input.Platform;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class ServerCardViewModel : ObservableObject, IDisposable
{
    private readonly Action<string, string, int>? _onPlayClick;

    public ServerInfo? ServerData { get; }

    public event EventHandler? PlayClicked;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _versionText = string.Empty;
    [ObservableProperty] private string _onlinePlayers = "-";
    [ObservableProperty] private Bitmap? _icon;

    [ObservableProperty] private bool _hasAllMc;
    [ObservableProperty] private bool _hasSite;
    [ObservableProperty] private bool _hasDiscord;

    [ObservableProperty] private string _playBtnText = "Грати";
    [ObservableProperty] private string _playTooltip = "Натисніть щоб почати грати";
    [ObservableProperty] private string _allMcTooltip = "Перейти на сторінку сервера на AllMC.in.ua";
    [ObservableProperty] private string _siteTooltip = "Офіційний сайт сервера";
    [ObservableProperty] private string _discordTooltip = "Discord спільнота сервера";
    [ObservableProperty] private string _copyIpTooltip = "Натисніть щоб скопіювати адресу";
    [ObservableProperty] private string _onlineTooltip = "Гравців онлайн";
    [ObservableProperty] private string _versionTooltip = "Версія Minecraft";

    public string FullAddress => (ServerData != null && ServerData.Port != 25565)
        ? $"{ServerData.Ip}:{ServerData.Port}"
        : (ServerData?.Ip ?? string.Empty);

    public ServerCardViewModel()
    {
        ApplyLocalization();
    }

    public ServerCardViewModel(
        ServerInfo serverData,
        Action<string, string, int>? onPlayClick = null) : this()
    {
        ServerData = serverData;
        _onPlayClick = onPlayClick;

        Title = string.IsNullOrEmpty(serverData.Name)
            ? LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер")
            : serverData.Name;

        IpAddress = FullAddress;
        VersionText = string.IsNullOrEmpty(serverData.Version) ? "-" : serverData.Version;
        OnlinePlayers = serverData.OnlinePlayers > 0 ? serverData.OnlinePlayers.ToString() : "-";

        HasAllMc = !string.IsNullOrWhiteSpace(serverData.AllMcUrl);
        HasSite = !string.IsNullOrWhiteSpace(serverData.SiteLink);
        HasDiscord = !string.IsNullOrWhiteSpace(serverData.DiscordLink);

        _ = LoadAssetsAsync(serverData);
        _ = PingServerAsync(serverData.Ip, serverData.Port);
    }

    public void Dispose()
    {
        Icon = null;
    }

    public void ApplyLocalization()
    {
        PlayBtnText = LocalizationManager.GetString("Servers.ServerPlayBtn", "Грати");
        PlayTooltip = LocalizationManager.GetString("Servers.ServerPlayTooltip", "Натисніть щоб почати грати");
        AllMcTooltip = LocalizationManager.GetString("Servers.ServerAllMcTooltip", "Перейти на сторінку сервера на AllMC.in.ua");
        SiteTooltip = LocalizationManager.GetString("Servers.ServerSiteTooltip", "Офіційний сайт сервера");
        DiscordTooltip = LocalizationManager.GetString("Servers.ServerDiscordTooltip", "Discord спільнота сервера");
        CopyIpTooltip = LocalizationManager.GetString("Servers.ServerCopyIpTooltip", "Натисніть щоб скопіювати адресу");
        OnlineTooltip = LocalizationManager.GetString("Servers.ServerOnlineTooltip", "Гравців онлайн");
        VersionTooltip = LocalizationManager.GetString("Servers.ServerVersionTooltip", "Версія Minecraft");

        if (ServerData != null && string.IsNullOrEmpty(ServerData.Name))
        {
            Title = LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер");
        }
    }

    private async Task LoadAssetsAsync(ServerInfo serverData)
    {
        if (!string.IsNullOrEmpty(serverData.LogoUrl))
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync(serverData.LogoUrl, 80);
        }
    }

    private async Task PingServerAsync(string ip, int port)
    {
        var status = await Task.Run(() =>
        {
            try
            {
                var serverStatus = new MCQuery.MCServer(ip, port);
                return serverStatus.Status(500);
            }
            catch
            {
                return null;
            }
        });

        if (status?.Players != null)
        {
            OnlinePlayers = $"{status.Players.Online}/{status.Players.Max}";
        }
    }

    [RelayCommand]
    private async Task CopyIpAsync()
    {
        AvaloniaSoundService.Click();
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            {
                await clipboard.SetTextAsync(FullAddress);
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void Play()
    {
        AvaloniaSoundService.Click();
        if (ServerData != null)
        {
            _onPlayClick?.Invoke(ServerData.Version, ServerData.Ip, ServerData.Port);
        }
        PlayClicked?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenAllMc()
    {
        SafeOpenUrl(ServerData?.AllMcUrl);
    }

    [RelayCommand]
    private void OpenSite()
    {
        SafeOpenUrl(ServerData?.SiteLink);
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        SafeOpenUrl(ServerData?.DiscordLink);
    }

    private static void SafeOpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            AvaloniaSoundService.Click();
            WebHelper.OpenUrl(uri.AbsoluteUri);
        }
    }
}
