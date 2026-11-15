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
        }

        _boundVm = DataContext as ServersPageViewModel;

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
            _boundVm.ServerCards.CollectionChanged += OnServerCardsChanged;
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

}
