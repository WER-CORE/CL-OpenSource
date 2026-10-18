using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class PartnerServerCardViewModel : ObservableObject, IDisposable
{
    private readonly Action<string, string, int>? _onPlayClick;
    private readonly Action<PartnerServerCardViewModel, ServerInfo>? _onInfoClick;

    public ServerInfo? ServerData { get; }

    public event EventHandler? PlayClicked;
    public event EventHandler? InfoClicked;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _onlinePlayers = "0/0";
    [ObservableProperty] private Bitmap? _iconBitmap;
    [ObservableProperty] private Bitmap? _backgroundBitmap;

    public void Dispose()
    {
        IconBitmap = null;
        BackgroundBitmap = null;
    }

    [ObservableProperty] private string _infoTooltip = string.Empty;
    [ObservableProperty] private string _visitPageText = string.Empty;
    [ObservableProperty] private string _onlineTooltip = string.Empty;
    [ObservableProperty] private string _copyIpTooltip = string.Empty;
    [ObservableProperty] private string _playTooltip = string.Empty;
    [ObservableProperty] private string _playBtnText = string.Empty;

    public PartnerServerCardViewModel()
    {
        ApplyLocalization();
    }

    public PartnerServerCardViewModel(
        ServerInfo serverData,
        Action<string, string, int>? onPlayClick = null,
        Action<PartnerServerCardViewModel, ServerInfo>? onInfoClick = null) : this()
    {
        ServerData = serverData;
        _onPlayClick = onPlayClick;
        _onInfoClick = onInfoClick;

        Title = serverData.Name ?? string.Empty;
        IpAddress = serverData.Ip ?? string.Empty;

        _ = LoadAssetsAsync(serverData);
        _ = PingServerAsync(serverData.Ip, serverData.Port);
    }

    public void ApplyLocalization()
    {
        InfoTooltip = LocalizationManager.GetString("Servers.PartnerInfoTooltip", "Інформація про сервер");
        VisitPageText = LocalizationManager.GetString("Servers.PartnerVisitPage", "Перейти на сторінку");
        OnlineTooltip = LocalizationManager.GetString("Servers.PartnerOnlineTooltip", "Онлайн");
        CopyIpTooltip = LocalizationManager.GetString("Servers.PartnerIpTooltip", "Натисніть щоб скопіювати");
        PlayTooltip = LocalizationManager.GetString("Servers.PartnerPlayTooltip", "Натисніть щоб почати грати");
        PlayBtnText = LocalizationManager.GetString("Servers.PartnerPlayBtn", "Грати");
    }

    private async Task LoadAssetsAsync(ServerInfo serverData)
    {
        if (!string.IsNullOrEmpty(serverData.LogoUrl))
        {
            IconBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(serverData.LogoUrl, 64);
        }

        if (!string.IsNullOrEmpty(serverData.BgUrl))
        {
            BackgroundBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(serverData.BgUrl, 315);
        }
    }

    private async Task PingServerAsync(string? ip, int port)
    {
        if (string.IsNullOrEmpty(ip))
        {
            OnlinePlayers = "-";
            return;
        }

        OnlinePlayers = await Task.Run(() =>
        {
            try
            {
                var serverStatus = new MCQuery.MCServer(ip, port);
                var status = serverStatus.Status(500);
                return $"{status.Players.Online}/{status.Players.Max}";
            }
            catch
            {
                return "-";
            }
        });
    }

    [RelayCommand]
    private void OpenInfo()
    {
        AvaloniaSoundService.Click();
        if (ServerData != null)
        {
            _onInfoClick?.Invoke(this, ServerData);
        }
        InfoClicked?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CopyIpAsync()
    {
        AvaloniaSoundService.Click();
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
            {
                await clipboard.SetTextAsync(IpAddress);
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
    private void OpenOnline()
    {
    }
}
