using System.Threading;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class GalleryPageViewModel : ObservableObject, ITrimmablePage
{
    private readonly ScreenshotService _screenshotService;
    private bool _isInitialized;
    private CancellationTokenSource? _loadCts;

    public void TrimMemory()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        foreach (var item in Screenshots)
        {
            item.Dispose();
        }
        Screenshots.Clear();
        SelectedScreenshot = null;
        _isInitialized = false;
        IsNoScreenshotsVisible = true;
    }

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
    public Task InitializeGalleryAsync() => InitializeGalleryInternalAsync(forceReloadScreenshots: false);

    [RelayCommand]
    public Task RefreshAllAsync() => InitializeGalleryInternalAsync(forceReloadScreenshots: true);

    private async Task InitializeGalleryInternalAsync(bool forceReloadScreenshots)
    {
        try
        {
            string? previouslySelectedName = SelectedSource?.Name;
            string launcherPath = SettingsManager.Default.PathLacunher ?? string.Empty;
            var sources = await Task.Run(() => _screenshotService.GetScreenshotSources(launcherPath)).ConfigureAwait(false);

            bool sourceChanged = false;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!Sources.SequenceEqual(sources))
                {
                    Sources.Clear();
                    foreach (var s in sources) Sources.Add(s);

                    var match = !string.IsNullOrEmpty(previouslySelectedName)
                        ? Sources.FirstOrDefault(s => s.Name == previouslySelectedName)
                        : null;

                    var targetSource = match ?? Sources.FirstOrDefault();
                    if (!Equals(SelectedSource, targetSource))
                    {
                        sourceChanged = true;
                        SelectedSource = targetSource;
                    }
                }
            });

            if (forceReloadScreenshots && !sourceChanged)
            {
                await LoadScreenshotsAsync();
            }
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
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            SelectedScreenshot = null;
        });

        try
        {
            string? path = SelectedSource?.FullScreenshotsPath;
            if (string.IsNullOrEmpty(path))
            {
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var item in Screenshots) item.Dispose();
                        Screenshots.Clear();
                        IsNoScreenshotsVisible = true;
                    });
                }
                return;
            }

            var items = await _screenshotService.LoadScreenshotsAsync(path, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                foreach (var item in items) item.Dispose();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                {
                    foreach (var item in items) item.Dispose();
                    return;
                }

                foreach (var oldItem in Screenshots)
                {
                    oldItem.Dispose();
                }
                Screenshots.Clear();

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
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] LoadScreenshots error: {ex.Message}");
            if (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsNoScreenshotsVisible = true);
            }
        }
        finally
        {
            if (_loadCts == cts)
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
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
            target.Dispose();
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
