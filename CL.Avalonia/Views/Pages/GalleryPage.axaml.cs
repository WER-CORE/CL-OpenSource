using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class GalleryPage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<GalleryPage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    private GalleryPageViewModel? _boundVm;

    public GalleryPage()
    {
        InitializeComponent();

        if (DataContext is not GalleryPageViewModel)
        {
            var sp = (Application.Current as App)?.ServiceProvider;
            DataContext = sp?.GetService(typeof(GalleryPageViewModel)) as GalleryPageViewModel ?? new GalleryPageViewModel();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _boundVm = DataContext as GalleryPageViewModel;

        if (_boundVm != null)
        {
            SourceSelector.ItemsSource = _boundVm.Sources;
            ScreenshotsList.ItemsSource = _boundVm.Screenshots;
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
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
        if (BtnOpenFolder != null) BtnOpenFolder.Content = LocalizationManager.GetString("Gallery.OpenFolderBtn", "Відкрити папку");
        if (BtnRefresh != null) ToolTip.SetTip(BtnRefresh, LocalizationManager.GetString("Sidebar.ToolTipRefresh", "Оновити"));
        if (Loc_TxtGalleryEmptyTitle != null) Loc_TxtGalleryEmptyTitle.Text = LocalizationManager.GetString("Gallery.EmptyTitle", "Тут поки що порожньо");
        if (Loc_TxtGalleryEmptyDesc != null) Loc_TxtGalleryEmptyDesc.Text = LocalizationManager.GetString("Gallery.EmptyDesc", "Зробіть скріншот у грі (F2), щоб він з'явився тут");
        if (Loc_TxtGallerySelected != null) Loc_TxtGallerySelected.Text = LocalizationManager.GetString("Gallery.Selected", "ОБРАНО");
        if (BtnOpenImage != null) BtnOpenImage.Content = LocalizationManager.GetString("Gallery.OpenImageBtn", "Відкрити");
        if (BtnDelete != null) BtnDelete.Content = LocalizationManager.GetString("Gallery.DeleteBtn", "Видалити");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundVm == null) return;

        switch (e.PropertyName)
        {
            case nameof(GalleryPageViewModel.IsLoading):
                if (LoadingProgressBar != null) LoadingProgressBar.IsVisible = _boundVm.IsLoading;
                if (_boundVm.IsLoading) ActionPanel.IsVisible = false;
                break;

            case nameof(GalleryPageViewModel.IsNoScreenshotsVisible):
                NoScreenshotsText.IsVisible = _boundVm.IsNoScreenshotsVisible;
                break;

            case nameof(GalleryPageViewModel.SelectedSource):
                SourceSelector.SelectedItem = _boundVm.SelectedSource;
                break;
        }
    }

    public void SourceSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null && SourceSelector.SelectedItem is ScreenshotSourceItem selectedSource)
        {
            _boundVm.SelectedSource = selectedSource;
        }
    }

    public void BtnOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.OpenFolder();
    }

    public void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null)
            _ = _boundVm.LoadScreenshotsAsync();
    }

    public void ScreenshotsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm == null) return;

        _boundVm.SelectedScreenshot = ScreenshotsList.SelectedItem as ScreenshotItem;

        if (_boundVm.SelectedScreenshot != null)
        {
            SelectedFileNameText.Text = _boundVm.SelectedFileName;
            AvaloniaAnimationService.AnimatePageTransition(ActionPanel);
        }
        else
        {
            AvaloniaAnimationService.AnimatePageTransitionExit(ActionPanel);
        }
    }

    public void ScreenshotsList_DoubleTapped(object? sender, TappedEventArgs e) =>
        BtnOpenImage_Click(sender, e);

    public void BtnOpenImage_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        _boundVm?.OpenImage();
    }

    public void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm != null && _boundVm.DeleteScreenshot())
        {
            AvaloniaAnimationService.AnimatePageTransitionExit(ActionPanel);
        }
    }
}
