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

    public void TrimMemory()
    {
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
    public async Task InitializeGalleryAsync()
    {
        try
        {
            string? previouslySelectedName = SelectedSource?.Name;
            string launcherPath = SettingsManager.Default.PathLacunher ?? string.Empty;
            var sources = await Task.Run(() => _screenshotService.GetScreenshotSources(launcherPath));

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
                        SelectedSource = targetSource;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] Initialize error: {ex.Message}");
        }
    }

    [RelayCommand]
    public Task RefreshAllAsync() => LoadScreenshotsAsync();

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
        IsLoading = true;

        try
        {
            string? path = SelectedSource?.FullScreenshotsPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                Screenshots.Clear();
                SelectedScreenshot = null;
                IsNoScreenshotsVisible = true;
                return;
            }

            var items = await _screenshotService.LoadScreenshotsAsync(path);

            Screenshots.Clear();
            SelectedScreenshot = null;

            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    Screenshots.Add(item);
                }
                IsNoScreenshotsVisible = false;
            }
            else
            {
                IsNoScreenshotsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GalleryPageViewModel] LoadScreenshots error: {ex.Message}");
            IsNoScreenshotsVisible = Screenshots.Count == 0;
        }
        finally
        {
            IsLoading = false;
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
