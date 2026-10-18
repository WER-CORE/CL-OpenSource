using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
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
    private readonly Action<ServerCardViewModel, ServerInfo>? _onInfoClick;

    public ServerInfo? ServerData { get; }

    public event EventHandler? PlayClicked;
    public event EventHandler? InfoClicked;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _onlinePlayers = "-";
    [ObservableProperty] private Bitmap? _icon;
    [ObservableProperty] private Bitmap? _backgroundBitmap;

    public void Dispose()
    {
        Icon = null;
        BackgroundBitmap = null;
    }

    [ObservableProperty] private IBrush? _borderBrush;
    [ObservableProperty] private Thickness _borderThickness = new(0);
    [ObservableProperty] private BoxShadows _boxShadow;
    [ObservableProperty] private IBrush _titleForeground = Brushes.White;

    [ObservableProperty] private string _visitPageText = "Перейти на сторінку";
    [ObservableProperty] private string _infoTooltip = "Інформація про сервер";
    [ObservableProperty] private string _onlineTooltip = "Онлайн";
    [ObservableProperty] private string _copyIpTooltip = "Натисніть щоб скопіювати";
    [ObservableProperty] private string _playTooltip = "Натисніть щоб почати грати";
    [ObservableProperty] private string _playBtnText = "Грати";

    public ServerCardViewModel()
    {
        ApplyLocalization();
    }

    public ServerCardViewModel(
        ServerInfo serverData,
        Action<string, string, int>? onPlayClick = null,
        Action<ServerCardViewModel, ServerInfo>? onInfoClick = null) : this()
    {
        ServerData = serverData;
        _onPlayClick = onPlayClick;
        _onInfoClick = onInfoClick;

        Title = string.IsNullOrEmpty(serverData.Name)
            ? LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер")
            : serverData.Name;

        Description = string.Format(
            LocalizationManager.GetString("Servers.TypeVersionFormat", "Тип: {0}\nВерсія: {1}"),
            serverData.Type,
            serverData.Version);

        IpAddress = serverData.Ip;

        ApplyServerStyling(serverData);

        _ = LoadAssetsAsync(serverData);
        _ = PingServerAsync(serverData.Ip, serverData.Port);
    }

    public void ApplyLocalization()
    {
        InfoTooltip = LocalizationManager.GetString("Servers.ServerItemInfoTooltip", "Інформація про сервер");
        VisitPageText = LocalizationManager.GetString("Servers.ServerPageBtn", "Перейти на сторінку");
        OnlineTooltip = LocalizationManager.GetString("Servers.ServerOnlineTooltip", "Онлайн");
        CopyIpTooltip = LocalizationManager.GetString("Servers.ServerCopyIpTooltip", "Натисніть щоб скопіювати");
        PlayTooltip = LocalizationManager.GetString("Servers.ServerPlayBtn", "Натисніть щоб почати грати");
        PlayBtnText = LocalizationManager.GetString("Servers.ServerPlayBtn", "Грати");

        if (ServerData != null)
        {
            if (string.IsNullOrEmpty(ServerData.Name))
                Title = LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер");

            Description = string.Format(
                LocalizationManager.GetString("Servers.TypeVersionFormat", "Тип: {0}\nВерсія: {1}"),
                ServerData.Type,
                ServerData.Version);
        }
    }

    private void ApplyServerStyling(ServerInfo serverData)
    {
        if (serverData.Priority >= 100 && serverData.NeonEffect && !string.IsNullOrEmpty(serverData.BorderColorHex))
        {
            var hex = serverData.BorderColorHex.StartsWith('#')
                ? serverData.BorderColorHex
                : "#" + serverData.BorderColorHex;

            if (Color.TryParse(hex, out var glowColor))
            {
                BorderThickness = new Thickness(2);
                BorderBrush = new SolidColorBrush(glowColor);
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    Color = glowColor,
                    Blur = 15,
                    Spread = 0
                });
            }
        }

        if (serverData.Priority >= 20 && !string.IsNullOrEmpty(serverData.TextColorHex))
        {
            var hex = serverData.TextColorHex.StartsWith('#')
                ? serverData.TextColorHex
                : "#" + serverData.TextColorHex;

            if (Color.TryParse(hex, out var textColor))
            {
                TitleForeground = new SolidColorBrush(textColor);
            }
        }
    }

    private async Task LoadAssetsAsync(ServerInfo serverData)
    {
        if (!string.IsNullOrEmpty(serverData.LogoUrl))
        {
            Icon = await AvaloniaImageHelper.LoadOptimizedImageAsync(serverData.LogoUrl, 90);
        }

        if (serverData.Priority > 0 && !string.IsNullOrEmpty(serverData.BgUrl))
        {
            BackgroundBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(serverData.BgUrl, 885);
        }
    }

    private async Task PingServerAsync(string ip, int port)
    {
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
}
