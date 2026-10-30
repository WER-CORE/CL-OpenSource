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

        if (Loc_TxtTabPublic != null) Loc_TxtTabPublic.Text = LocalizationManager.GetString("P2P.TabPublic", "Громадські сервери");
        if (Loc_TxtTabP2P != null) Loc_TxtTabP2P.Text = LocalizationManager.GetString("P2P.TabP2P", "Гра з друзями (P2P)");
        if (Loc_TxtHostTitle != null) Loc_TxtHostTitle.Text = LocalizationManager.GetString("P2P.HostTitle", "Створити спільну гру (Хост)");
        if (Loc_TxtHostSubtitle != null) Loc_TxtHostSubtitle.Text = LocalizationManager.GetString("P2P.HostSubtitle", "Трансляція вашого LAN-світу друзям");
        if (Loc_TxtLanWorldsTitle != null) Loc_TxtLanWorldsTitle.Text = LocalizationManager.GetString("P2P.LanWorldsTitle", "Виявлені світи у вашій мережі:");
        if (Loc_BtnRefreshDiscovery != null) ToolTip.SetTip(Loc_BtnRefreshDiscovery, LocalizationManager.GetString("P2P.RefreshTooltip", "Оновити пошук світів"));
        if (Loc_TxtRefreshBtn != null) Loc_TxtRefreshBtn.Text = LocalizationManager.GetString("P2P.RefreshBtn", "Оновити");
        if (Loc_TxtManualPortInput != null) Loc_TxtManualPortInput.PlaceholderText = LocalizationManager.GetString("P2P.PortPlaceholder", "Або вкажіть порт LAN вручну (наприклад 54321)...");
        if (Loc_BtnAddManualPort != null) Loc_BtnAddManualPort.Content = LocalizationManager.GetString("P2P.AddPortBtn", "Додати");
        if (Loc_TxtLanHint != null) Loc_TxtLanHint.Text = LocalizationManager.GetString("P2P.LanHint", "Порада: увійдіть у світ Minecraft, відкрийте меню паузи і натисніть «Відкрити для LAN».");
        if (Loc_TxtApprovalTitle != null) Loc_TxtApprovalTitle.Text = LocalizationManager.GetString("P2P.ApprovalTitle", "Ручне схвалення підключень");
        if (Loc_TxtApprovalDesc != null) Loc_TxtApprovalDesc.Text = LocalizationManager.GetString("P2P.ApprovalDesc", "Запитувати дозвіл перед входом нових гравців");
        if (Loc_TxtOfflineTitle != null) Loc_TxtOfflineTitle.Text = LocalizationManager.GetString("P2P.OfflineTitle", "Дозволити гравців без ліцензії");
        if (Loc_TxtOfflineDesc != null) Loc_TxtOfflineDesc.Text = LocalizationManager.GetString("P2P.OfflineDesc", "Дозволяє друзям з офлайн-акаунтами приєднуватися до гри");
        if (Loc_TxtStartHostingBtn != null) Loc_TxtStartHostingBtn.Text = LocalizationManager.GetString("P2P.StartHostingBtn", "Почати спільну гру");
        if (Loc_TxtHostingActive != null) Loc_TxtHostingActive.Text = LocalizationManager.GetString("P2P.HostingActive", "Спільний доступ активний");
        if (Loc_TxtRoomCodeTitle != null) Loc_TxtRoomCodeTitle.Text = LocalizationManager.GetString("P2P.RoomCodeTitle", "Код кімнати (передайте друзям):");
        if (Loc_TxtCopyBtn != null) Loc_TxtCopyBtn.Text = LocalizationManager.GetString("P2P.CopyBtn", "Копіювати");
        if (Loc_TxtIpProtectedHint != null) Loc_TxtIpProtectedHint.Text = LocalizationManager.GetString("P2P.IpProtectedHint", "Ваша IP-адреса надійно прихована від сторонніх очей та стрімів.");
        if (Loc_TxtWaitingPlayers != null) Loc_TxtWaitingPlayers.Text = LocalizationManager.GetString("P2P.WaitingPlayers", "Очікування підключення гравців...");
        if (Loc_TxtStopHostingBtn != null) Loc_TxtStopHostingBtn.Text = LocalizationManager.GetString("P2P.StopHostingBtn", "Зупинити спільний доступ");
        if (Loc_TxtGuestTitle != null) Loc_TxtGuestTitle.Text = LocalizationManager.GetString("P2P.GuestTitle", "Приєднатися до гри (Гість)");
        if (Loc_TxtGuestSubtitle != null) Loc_TxtGuestSubtitle.Text = LocalizationManager.GetString("P2P.GuestSubtitle", "Підключення за унікальним кодом кімнати");
        if (Loc_TxtGuestHint != null) Loc_TxtGuestHint.Text = LocalizationManager.GetString("P2P.GuestHint", "Введіть код кімнати, наданий хостом, щоб перевірити версію та підключитися:");
        if (Loc_TxtSearchRoomBtn != null) Loc_TxtSearchRoomBtn.Text = LocalizationManager.GetString("P2P.SearchRoomBtn", "Знайти");
        if (Loc_TxtRoomFound != null) Loc_TxtRoomFound.Text = LocalizationManager.GetString("P2P.RoomFound", "Кімнату успішно знайдено!");
        if (Loc_TxtWorldLabel != null) Loc_TxtWorldLabel.Text = LocalizationManager.GetString("P2P.WorldLabel", "Світ:");
        if (Loc_TxtVersionLabel != null) Loc_TxtVersionLabel.Text = LocalizationManager.GetString("P2P.VersionLabel", "Версія:");
        if (Loc_TxtLoaderLabel != null) Loc_TxtLoaderLabel.Text = LocalizationManager.GetString("P2P.LoaderLabel", "Завантажувач:");
        if (Loc_TxtJoinAndPlayBtn != null) Loc_TxtJoinAndPlayBtn.Text = LocalizationManager.GetString("P2P.JoinAndPlayBtn", "ПРИЄДНАТИСЯ ТА ГРАТИ");
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

    public void OpenServerInfoPanel(ServerInfo data, string online = "-", Bitmap? iconBitmap = null)
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
