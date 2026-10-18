using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class GalleryPageViewModel : ObservableObject
{
    private readonly ScreenshotService _screenshotService;
    private bool _isInitialized;

    public ObservableCollection<ScreenshotSourceItem> Sources { get; } = new();
    public ObservableCollection<ScreenshotItem> Screenshots { get; } = new();

    [ObservableProperty] private ScreenshotSourceItem? _selectedSource;
    [ObservableProperty] private ScreenshotItem? _selectedScreenshot;
    [ObservableProperty] private bool _isActionPanelVisible;
    [ObservableProperty] private bool _isNoScreenshotsVisible = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _selectedFileName = string.Empty;

    public GalleryPageViewModel() : this(new ScreenshotService())
    {
    }

    public GalleryPageViewModel(ScreenshotService screenshotService)
    {
        _screenshotService = screenshotService;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await InitializeGalleryAsync();
    }

    [RelayCommand]
    public async Task InitializeGalleryAsync()
    {
        try
        {
            string launcherPath = SettingsManager.Default.PathLacunher ?? string.Empty;
            var sources = await Task.Run(() => _screenshotService.GetScreenshotSources(launcherPath)).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Sources.Clear();
                foreach (var s in sources) Sources.Add(s);

                if (Sources.Count > 0)
                {
                    SelectedSource = Sources[0];
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] Initialize error: {ex.Message}");
        }
    }

    partial void OnSelectedSourceChanged(ScreenshotSourceItem? value)
    {
        _ = LoadScreenshotsAsync();
    }

    partial void OnSelectedScreenshotChanged(ScreenshotItem? value)
    {
        if (value != null)
        {
            SelectedFileName = value.FileName ?? string.Empty;
            IsActionPanelVisible = true;
        }
        else
        {
            SelectedFileName = string.Empty;
            IsActionPanelVisible = false;
        }
    }

    [RelayCommand]
    public async Task LoadScreenshotsAsync()
    {
        if (IsLoading) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            Screenshots.Clear();
            SelectedScreenshot = null;
            IsNoScreenshotsVisible = false;
        });

        try
        {
            string? path = SelectedSource?.FullScreenshotsPath;
            if (string.IsNullOrEmpty(path))
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsNoScreenshotsVisible = true);
                return;
            }

            var items = await _screenshotService.LoadScreenshotsAsync(path).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (items.Count > 0)
                {
                    foreach (var item in items) Screenshots.Add(item);
                    IsNoScreenshotsVisible = false;
                }
                else
                {
                    IsNoScreenshotsVisible = true;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] LoadScreenshots error: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => IsNoScreenshotsVisible = true);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        string? path = SelectedSource?.FullScreenshotsPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            UrlOpener.Open(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] OpenFolder error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenImage()
    {
        if (SelectedScreenshot == null || !File.Exists(SelectedScreenshot.FilePath)) return;
        UrlOpener.Open(SelectedScreenshot.FilePath);
    }

    [RelayCommand]
    private void ExecuteDeleteScreenshot() => DeleteScreenshot();

    public bool DeleteScreenshot()
    {
        if (SelectedScreenshot == null) return false;

        var target = SelectedScreenshot;
        if (_screenshotService.DeleteScreenshot(target.FilePath))
        {
            Screenshots.Remove(target);
            SelectedScreenshot = null;
            if (Screenshots.Count == 0) IsNoScreenshotsVisible = true;
            return true;
        }

        AvaloniaDialogService.Show(
            LocalizationManager.GetString("Dialogs.DeleteError", "Не вдалося видалити файл."),
            LocalizationManager.GetString("Dialogs.Error", "Помилка"),
            MascotEmotion.Sad);
        return false;
    }
}
