using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;

public partial class DownloadEditPackItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Bitmap? _iconBitmap;
    [ObservableProperty] private string _projectId = string.Empty;
    [ObservableProperty] private string _slug = string.Empty;
    [ObservableProperty] private int _type;
    [ObservableProperty] private string _loader = string.Empty;
    [ObservableProperty] private string _gameVersion = string.Empty;
    [ObservableProperty] private string _iconUrl = string.Empty;

    private readonly string _detailsUrl;
    private readonly Action<DownloadEditPackItemViewModel> _onAddClicked;

    public DownloadEditPackItemViewModel(string detailsUrl, Action<DownloadEditPackItemViewModel> onAddClicked)
    {
        _detailsUrl = detailsUrl;
        _onAddClicked = onAddClicked;
    }

    partial void OnIconUrlChanged(string value)
    {
        _ = LoadIconAsync(value);
    }

    private async Task LoadIconAsync(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            IconBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(url, 32);
        }
        IconBitmap ??= await AvaloniaImageHelper.LoadOptimizedImageAsync("avares://CL.Avalonia/Assets/IconCL(Common).webp", 32);
    }

    [RelayCommand]
    private void OpenDetails()
    {
        AvaloniaSoundService.Click();
        if (!string.IsNullOrEmpty(_detailsUrl))
        {
            WebHelper.OpenUrl(_detailsUrl);
        }
    }

    [RelayCommand]
    private void AddToQueue()
    {
        AvaloniaSoundService.Click();
        _onAddClicked?.Invoke(this);
    }
}

public partial class DownloadEditPackQueueItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private Bitmap? _iconBitmap;
    [ObservableProperty] private ModInfo _modInfo = new();

    private readonly Action<DownloadEditPackQueueItemViewModel> _onDeleteClicked;

    public DownloadEditPackQueueItemViewModel(Action<DownloadEditPackQueueItemViewModel> onDeleteClicked)
    {
        _onDeleteClicked = onDeleteClicked;
    }

    [RelayCommand]
    private void Delete()
    {
        AvaloniaSoundService.Click();
        _onDeleteClicked?.Invoke(this);
    }
}
