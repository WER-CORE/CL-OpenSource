using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class ModsPage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<ModsPage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    private ModsPageViewModel? _boundVm;
    private ModsPageViewModel ViewModel => (DataContext as ModsPageViewModel)!;
    private int _currentCategoryIndex;

    public ModsPage()
    {
        InitializeComponent();

        if (DataContext is not ModsPageViewModel)
        {
            var sp = (Application.Current as App)?.ServiceProvider;
            DataContext = sp?.GetService(typeof(ModsPageViewModel)) as ModsPageViewModel ?? new ModsPageViewModel();
        }

        Loaded += ModsPage_Loaded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged() => Dispatcher.UIThread.Post(ApplyLocalization);

    public void ApplyLocalization()
    {
        if (Loc_SourceTitle != null) Loc_SourceTitle.Text = LocalizationManager.GetString("Mods.SourceTitle", "Джерело");
        if (Loc_LoaderTitle != null) Loc_LoaderTitle.Text = LocalizationManager.GetString("Mods.LoaderTitle", "Завантажувач");
        if (Loc_ModeTitle != null) Loc_ModeTitle.Text = LocalizationManager.GetString("Mods.ModeTitle", "Режим");
        if (Loc_ModeModpack != null) Loc_ModeModpack.Text = LocalizationManager.GetString("Mods.ModeModpack", "Збірка");
        if (Loc_ModeSeparate != null) Loc_ModeSeparate.Text = LocalizationManager.GetString("Mods.ModeSeparate", "Окремо");

        if (ModsTXT != null) ModsTXT.Text = LocalizationManager.GetString("Mods.CategoryMods", "Моди");
        if (ShardTXT != null) ShardTXT.Text = LocalizationManager.GetString("Mods.CategoryShaders", "Шейдери");
        if (ResourcePackTXT != null) ResourcePackTXT.Text = LocalizationManager.GetString("Mods.CategoryResourcePacks", "Ресурс-паки");
        if (MapsTXT != null) MapsTXT.Text = LocalizationManager.GetString("Mods.CategoryMaps", "Мапи");
        if (DataPacksTXT != null) DataPacksTXT.Text = LocalizationManager.GetString("Mods.CategoryDataPacks", "Дата-паки");

        if (SearchSystemModsTXT != null) SearchSystemModsTXT.PlaceholderText = LocalizationManager.GetString("Generic.SearchMods", "Пошук модів...");
        if (TitleModsDowload != null) TitleModsDowload.Text = LocalizationManager.GetString("Mods.ModVersionSelectionTitle", "Вибір версії моду");
        if (Loc_TxtInstallMod != null) Loc_TxtInstallMod.Text = LocalizationManager.GetString("Mods.InstallBtn", "Встановити");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
            _boundVm.ModCards.CollectionChanged -= OnModCardsChanged;
            _boundVm.OnModDownloadRequested = null;
        }

        _boundVm = DataContext as ModsPageViewModel;

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
            _boundVm.ModCards.CollectionChanged += OnModCardsChanged;
            _boundVm.OnModDownloadRequested = HandleModDownloadClick;

            CollectionList.ItemsSource = _boundVm.InstalledModpacks;
            CollectionList.DisplayMemberBinding = new Binding("Name");
            Version.ItemsSource = _boundVm.GameVersions;
            VersionMods.ItemsSource = _boundVm.ModFileVersions;
        }
    }

    private void OnModCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollModsToTop();
    }

    private void ModsPage_Loaded(object? sender, RoutedEventArgs e)
    {
        UpdateIndicatorPosition(animate: false);
        if (_boundVm != null && _boundVm.SearchResults.Count == 0 && !_boundVm.IsLoading)
        {
            _boundVm.TriggerSearch();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundVm == null) return;

        switch (e.PropertyName)
        {
            case nameof(ModsPageViewModel.IsLoading):
                ModsSearchLoader.IsVisible = _boundVm.IsLoading;
                ModsSkeletonPanel.IsVisible = _boundVm.IsLoading;
                ModsDowloadList.IsVisible = !_boundVm.IsLoading;
                if (!_boundVm.IsLoading) ScrollModsToTop();
                break;

            case nameof(ModsPageViewModel.PageNumberText):
                if (PageNumberText != null) PageNumberText.Text = _boundVm.PageNumberText;
                break;

            case nameof(ModsPageViewModel.CanGoPrevPage):
                if (PrevPageBtn != null) PrevPageBtn.IsEnabled = _boundVm.CanGoPrevPage;
                break;

            case nameof(ModsPageViewModel.CanGoNextPage):
                if (NextPageBtn != null) NextPageBtn.IsEnabled = _boundVm.CanGoNextPage;
                break;

            case nameof(ModsPageViewModel.CanDownloadSelectedMod):
                if (DowloadMod != null) DowloadMod.IsEnabled = _boundVm.CanDownloadSelectedMod;
                break;
        }
    }

    private void ScrollModsToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ModsDowloadList is { ItemCount: > 0 })
                ModsDowloadList.ScrollIntoView(0);

            var sv = ModsDowloadList?.FindDescendantOfType<ScrollViewer>();
            if (sv != null) sv.Offset = new Vector(0, 0);
        }, DispatcherPriority.Loaded);
    }

    public void SearchSystemModsTXT_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_boundVm != null)
            _boundVm.SearchText = SearchSystemModsTXT.Text ?? string.Empty;
    }

    public void PrevPageBtn_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.PrevPageCommand.Execute(null);
    }

    public void NextPageBtn_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.NextPageCommand.Execute(null);
    }

    public void ModsPaginationPanel_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_boundVm == null) return;

        if (e.Delta.Y < 0 && _boundVm.CanGoNextPage)
        {
            NextPageBtn_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Delta.Y > 0 && _boundVm.CanGoPrevPage)
        {
            PrevPageBtn_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void HandleModDownloadClick(ModSearchResult mod)
    {
        if (_boundVm == null) return;

        AvaloniaSoundService.Click();
        AvaloniaAnimationService.FadeIn(GirdModsDowload, 0.2);
        AvaloniaAnimationService.AnimatePageTransition(MenuInstaller);

        CollectionListBorder.IsVisible = _boundVm.IsCollectionMode;
        VersionVanilBorder.IsVisible = _boundVm.IsStandardMode;

        bool ready = await _boundVm.PrepareModInstallerAsync(mod);
        if (!ready)
        {
            CloseInstallerMenu();
        }
    }

    private void CloseInstallerMenu()
    {
        AvaloniaAnimationService.FadeOut(GirdModsDowload, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(MenuInstaller);
    }

    public void BtnCloseInstaller_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        CloseInstallerMenu();
    }

    public void GirdModsDowload_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        CloseInstallerMenu();
    }

    public void CollectionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null)
            _boundVm.SelectedModpack = CollectionList.SelectedItem as InstalledModpack;
    }

    public void Version_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null)
            _boundVm.SelectedGameVersion = Version.SelectedItem?.ToString();
    }

    public void VersionMods_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null)
            _boundVm.SelectedModFileVersion = VersionMods.SelectedItem?.ToString();
    }

    public async void DowloadMod_Click(object? sender, RoutedEventArgs e)
    {
        if (_boundVm == null) return;

        AvaloniaSoundService.Click();
        string? customFolder = null;

        if (_boundVm.IsStandardMode)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocalizationManager.GetString("Mods.SelectFolder", "Виберіть папку для завантаження")
            });

            if (folders is { Count: > 0 })
                customFolder = folders[0].Path.LocalPath;
            else
                return;
        }

        if (await _boundVm.DownloadSelectedModAsync(customFolder))
        {
            CloseInstallerMenu();
        }
    }

    public void OnModCategoryClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control element && byte.TryParse(element.Tag?.ToString(), out byte categoryId))
        {
            AvaloniaSoundService.Click();
            _currentCategoryIndex = categoryId;
            UpdateIndicatorPosition(animate: true);
            _boundVm?.SetCategory(categoryId);
        }
    }

    public void CategoriesGrid_SizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateIndicatorPosition(animate: false);

    private void UpdateIndicatorPosition(bool animate)
    {
        if (CategoriesGrid == null || PanelSelectNowDowloadModifi == null) return;
        if (_currentCategoryIndex < 0 || _currentCategoryIndex >= CategoriesGrid.Children.Count) return;

        if (CategoriesGrid.Children[_currentCategoryIndex] is not Control targetButton) return;

        var relativePoint = targetButton.TranslatePoint(new Point(0, 0), CategoriesGrid);
        if (relativePoint == null) return;

        double newX = relativePoint.Value.X;
        double newWidth = targetButton.Bounds.Width > 0 ? targetButton.Bounds.Width : 90;

        if (PanelSelectNowDowloadModifi.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            PanelSelectNowDowloadModifi.RenderTransform = transform;
        }

        AvaloniaAnimationService.AnimateTabIndicator(PanelSelectNowDowloadModifi, transform, newX, newWidth, animate);
    }

    public void OnModProviderClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: string provider })
        {
            AvaloniaSoundService.Click();
            AvaloniaAnimationService.AnimateVerticalIndicator(PanelSelectNowSiteMods, provider == "Modrinth" ? 0 : 45);
            _boundVm?.SetProvider(provider);
        }
    }

    public void OnLoaderTypeClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: string loaderType })
        {
            AvaloniaSoundService.Click();
            double targetPos = loaderType switch
            {
                "Fabric" => 0,
                "Forge" => 45,
                "Quilt" => 85,
                "NeoForge" => 130,
                _ => 0
            };
            AvaloniaAnimationService.AnimateVerticalIndicator(PanelSelectNowVersionType, targetPos);
            _boundVm?.SetLoader(loaderType);
        }
    }

    public void ModsDowloadTypeTXT_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        AvaloniaAnimationService.AnimateVerticalIndicator(PanelSelectNowModsType, 0);
        VersionVanilBorder.IsVisible = false;
        CollectionListBorder.IsVisible = true;
        _boundVm?.SetModTargetType("Collection");
    }

    public void ModsManegerTypeTXT_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        AvaloniaAnimationService.AnimateVerticalIndicator(PanelSelectNowModsType, 45);
        VersionVanilBorder.IsVisible = true;
        CollectionListBorder.IsVisible = false;
        _boundVm?.SetModTargetType("Standard");
    }
}
