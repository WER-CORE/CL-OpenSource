using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Models;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class NewsCardViewModel : ObservableObject
{
    private readonly Action<NewsItem>? _onClick;

    public NewsItem? NewsData { get; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private Bitmap? _image;
    [ObservableProperty] private bool _isLoadingImage;

    public NewsCardViewModel()
    {
    }

    public NewsCardViewModel(NewsItem data, Action<NewsItem>? onClick = null)
    {
        NewsData = data;
        _onClick = onClick;
        Title = data.Title ?? string.Empty;
        Description = data.Description ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(data.IconUrl))
        {
            _ = LoadImageAsync(data.IconUrl);
        }
    }

    private async Task LoadImageAsync(string iconUrl)
    {
        IsLoadingImage = true;
        try
        {
            var bitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(iconUrl, 250);
            if (bitmap != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => Image = bitmap);
            }
        }
        catch
        {

        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoadingImage = false);
        }
    }

    [RelayCommand]
    private void OpenNews()
    {
        AvaloniaSoundService.Click();
        if (NewsData != null)
        {
            _onClick?.Invoke(NewsData);
        }
    }
}
