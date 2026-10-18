using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views.Controls;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class ServerPage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<ServerPage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    private ServersPageViewModel? _boundVm;

    public ServerPage()
    {
        InitializeComponent();

        if (DataContext is not ServersPageViewModel)
        {
            var sp = (Application.Current as App)?.ServiceProvider;
            DataContext = sp?.GetService(typeof(ServersPageViewModel)) as ServersPageViewModel ?? new ServersPageViewModel();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
            _boundVm.ServerCards.CollectionChanged -= OnServerCardsChanged;
            _boundVm.OnServerInfoRequested = null;
        }

        _boundVm = DataContext as ServersPageViewModel;

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
            _boundVm.ServerCards.CollectionChanged += OnServerCardsChanged;
            _boundVm.OnServerInfoRequested = (cardVm, info) => OpenServerInfoPanel(info, cardVm.OnlinePlayers, cardVm.Icon);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
        ApplyLocalization();
        if (_boundVm != null)
        {
            _ = _boundVm.EnsureInitializedAsync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => Dispatcher.UIThread.Post(ApplyLocalization);

    public void ApplyLocalization()
    {
        if (SearchSystemTXT != null) SearchSystemTXT.PlaceholderText = LocalizationManager.GetString("Generic.SearchServers", "Пошук серверів...");
        if (Loc_RadioNormalServers != null) Loc_RadioNormalServers.Content = LocalizationManager.GetString("Servers.NormalServers", "Звичайні");
        if (Loc_RadioModdedServers != null) Loc_RadioModdedServers.Content = LocalizationManager.GetString("Servers.ModdedServers", "Модові");
        if (OwnerButton != null) OwnerButton.Content = LocalizationManager.GetString("Servers.AddServer", "Додати свій сервер");
        if (Loc_TxtPlayServer != null) Loc_TxtPlayServer.Text = LocalizationManager.GetString("Servers.PlayOnServer", "ГРАТИ НА СЕРВЕРІ");
        if (DescriptionServer != null && string.IsNullOrWhiteSpace(DescriptionServer.Text))
            DescriptionServer.Text = LocalizationManager.GetString("Servers.Description", "Опис...");
    }

    private void OnServerCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollServersToTop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundVm == null) return;

        switch (e.PropertyName)
        {
            case nameof(ServersPageViewModel.IsLoading):
            case nameof(ServersPageViewModel.IsServersLoading):
                ServerSearchLoader.IsVisible = _boundVm.IsLoading;
                ServerCardSkeletonTemplate.IsVisible = _boundVm.IsLoading;
                break;

            case nameof(ServersPageViewModel.PageInfo):
            case nameof(ServersPageViewModel.PageInfoText):
                TxtPageInfo.Text = _boundVm.PageInfoText;
                break;

            case nameof(ServersPageViewModel.CanGoPrev):
                BtnPrevPage.IsEnabled = _boundVm.CanGoPrev;
                break;

            case nameof(ServersPageViewModel.CanGoNext):
                BtnNextPage.IsEnabled = _boundVm.CanGoNext;
                break;

            case nameof(ServersPageViewModel.IsPaginationVisible):
                PaginationPanel.IsVisible = _boundVm.IsPaginationVisible;
                break;

            case nameof(ServersPageViewModel.SelectedServerBg):
                BG.Source = _boundVm.SelectedServerBg;
                BG.IsVisible = _boundVm.SelectedServerBg != null;
                break;
        }
    }

    private void ScrollServersToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ServerList is { ItemCount: > 0 })
                ServerList.ScrollIntoView(0);

            var sv = ServerList?.FindDescendantOfType<ScrollViewer>();
            if (sv != null) sv.Offset = new Vector(0, 0);
        }, DispatcherPriority.Loaded);
    }

    public void BtnPrevPage_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.PrevPage();
    }

    public void BtnNextPage_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.NextPage();
    }

    public void SearchSystem_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_boundVm != null)
            _boundVm.SearchQuery = SearchSystemTXT.Text ?? string.Empty;
    }

    private void OpenServerInfoPanel(ServerInfo data, string online, Bitmap? iconBitmap)
    {
        AvaloniaSoundService.Click();

        IPServerTXT.Text = data.Ip;
        VersionTXT.Text = data.Version;
        PortTXT.Text = data.Port.ToString();
        OnlinePlayerTXT.Text = online;
        TitleMain1.Text = data.Name;
        DescriptionServer.Text = data.Description;

        if (iconBitmap != null)
            MainIcon3.Source = iconBitmap;

        if (data.VersionsSupported is { Count: > 1 })
        {
            VersionSelector.ItemsSource = data.VersionsSupported;
            VersionSelector.SelectedItem = data.Version;
            VersionSelector.IsVisible = true;
        }
        else
        {
            VersionSelector.IsVisible = false;
        }

        if (_boundVm != null)
            _ = _boundVm.LoadServerBackgroundAsync(data, iconBitmap);

        AvaloniaAnimationService.AnimatePageTransitionExit(ServerName, 20, 0.2);
        ServerName.IsVisible = false;
        PanelInfoServer.IsVisible = true;
        AvaloniaAnimationService.AnimatePageTransition(PanelInfoServer, 20, 0.3);
    }

    public void Loc_BtnCloseServerInfo_Click(object? sender, RoutedEventArgs e) => CloseServerInfoPanel();
    public void BackMainWindow_MouseDown(object? sender, RoutedEventArgs e) => CloseServerInfoPanel();

    private void CloseServerInfoPanel()
    {
        AvaloniaSoundService.Click();
        AvaloniaAnimationService.AnimatePageTransitionExit(PanelInfoServer, 20, 0.2);
        PanelInfoServer.IsVisible = false;
        ServerName.IsVisible = true;
        AvaloniaAnimationService.AnimatePageTransition(ServerName, 20, 0.3);
    }

    public async void PlayServer_Click(object? sender, RoutedEventArgs e)
    {
        if (_boundVm == null) return;

        AvaloniaSoundService.Click();

        string selectedVersion = (VersionSelector.IsVisible && VersionSelector.SelectedItem != null)
            ? VersionSelector.SelectedItem.ToString()!
            : VersionTXT.Text ?? "1.20.1";

        string ip = IPServerTXT.Text ?? string.Empty;
        int port = int.TryParse(PortTXT.Text, out var p) ? p : 25565;
        string name = TitleMain1.Text ?? string.Empty;

        await _boundVm.LaunchServerAsync(selectedVersion, ip, port, name);
    }

    public void DiscordLink_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.OpenExternalServerLink(_boundVm.SelectedServer?.DiscordLink, "Servers.ServerNoDiscord", "У цього сервера немає Discord каналу.");
    }

    public void SiteLink_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.OpenExternalServerLink(_boundVm.SelectedServer?.SiteLink, "Servers.ServerNoSite", "Сайт не вказано.");
    }

    public void DonateLink_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.OpenExternalServerLink(_boundVm.SelectedServer?.DonateLink, "Servers.ServerNoDonate", "Посилання на донат відсутнє.");
    }
}
