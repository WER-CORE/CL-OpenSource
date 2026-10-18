using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class NewsPageViewModel : ObservableObject, ITrimmablePage, IDisposable
{
    private readonly NewsService _newsService;
    private readonly IDialogService _dialogService;
    private bool _isLoaded;

    public void TrimMemory()
    {
        SelectedNewsImage = null;
        IsArticleOpen = false;
        foreach (var card in NewsCards)
        {
            card.Dispose();
        }
        NewsCards.Clear();
        _isLoaded = false;
    }

    public void Dispose()
    {
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }

    public ObservableCollection<NewsCardViewModel> NewsCards { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewsListVisible))]
    private bool _isArticleOpen;

    public bool IsNewsListVisible => !IsArticleOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _selectedNewsTitle = string.Empty;

    [ObservableProperty]
    private string _selectedNewsContent = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNewsImage))]
    private Bitmap? _selectedNewsImage;

    public bool HasSelectedNewsImage => SelectedNewsImage != null;

    [ObservableProperty]
    private string _searchingNewsText = string.Empty;

    [ObservableProperty]
    private string _backButtonText = string.Empty;

    [ObservableProperty]
    private string _backButtonTooltip = string.Empty;

    public NewsPageViewModel(NewsService newsService, IDialogService dialogService)
    {
        _newsService = newsService;
        _dialogService = dialogService;

        ApplyLocalization();
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public void ApplyLocalization()
    {
        SearchingNewsText = LocalizationManager.GetString("News.Searching", "Bit-CL шукає новини по CL...");
        BackButtonText = LocalizationManager.GetString("News.BackToNews", LocalizationManager.GetString("Generic.Back", "Назад до новин"));
        BackButtonTooltip = LocalizationManager.GetString("Generic.Back", "Повернутись");
    }

    public async Task InitializeAsync()
    {
        if (!_isLoaded)
        {
            _isLoaded = true;
            await LoadNewsAsync();
        }
    }

    [RelayCommand]
    public async Task LoadNewsAsync()
    {
        IsLoading = true;
        IsArticleOpen = false;

        try
        {
            var items = await _newsService.GetNewsAsync().ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                NewsCards.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        NewsCards.Add(new NewsCardViewModel(item, OnNewsCardClicked));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _dialogService.ShowMessage(
                    string.Format(LocalizationManager.GetString("Servers.NewsLoadError", "Не вдалося завантажити новини.\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Новини загубилися"),
                    MascotEmotion.Sad);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void OnNewsCardClicked(NewsItem item)
    {
        SelectedNewsTitle = item.Title ?? string.Empty;
        SelectedNewsContent = item.Description ?? string.Empty;
        SelectedNewsImage = null;
        IsArticleOpen = true;

        if (!string.IsNullOrWhiteSpace(item.IconUrl))
        {
            _ = LoadArticleImageAsync(item.IconUrl);
        }
    }

    private async Task LoadArticleImageAsync(string iconUrl)
    {
        try
        {
            var bitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(iconUrl, 800);
            if (bitmap != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => SelectedNewsImage = bitmap);
            }
        }
        catch
        {

        }
    }

    [RelayCommand]
    private void BackToList()
    {
        AvaloniaSoundService.Click();
        IsArticleOpen = false;
        SelectedNewsTitle = string.Empty;
        SelectedNewsContent = string.Empty;
        SelectedNewsImage = null;
    }

    public void Cleanup()
    {
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }
}
