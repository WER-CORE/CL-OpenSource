using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using CL_CLegendary_Launcher_.Avalonia;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views;
using CL_CLegendary_Launcher_.Avalonia.Views.Pages;
using CL.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class LauncherNavigationService
{
    private readonly CL_Main _main;
    private readonly IServiceProvider _sp;
    private readonly Control? _bgVisual;

    private bool _isNavigating;
    private Control? _currentPage;

    private HomePage? _homePage;
    private ServerPage? _serverPage;
    private ModsPage? _modsPage;
    private ModPacksPage? _modPacksPage;
    private SettingsPage? _settingsPage;
    private NewsPage? _newsPage;
    private GalleryPage? _galleryPage;

    public LauncherNavigationService(CL_Main main)
    {
        _main = main;
        _sp = ((App)Application.Current!).ServiceProvider!;
        _bgVisual = _main.FindControl<Control>("MainBackgroundContainer");
    }

    private HomePage GetOrCreateHomePage()
    {
        if (_homePage != null) return _homePage;

        _homePage = new HomePage
        {
            DataContext = _sp.GetRequiredService<HomePageViewModel>(),
            BackgroundVisual = _bgVisual
        };
        _homePage.SettingsRequested += (_, _) => NavigateToSettings();
        _homePage.NewsRequested += (_, _) => NavigateToNews();
        _homePage.ModPacksRequested += (_, _) => NavigateToModPacks();

        return _homePage;
    }

    public ServerPage GetServerPage()
    {
        return _serverPage ??= new ServerPage
        {
            DataContext = _sp.GetRequiredService<ServersPageViewModel>(),
            BackgroundVisual = _bgVisual
        };
    }

    private ModsPage GetOrCreateModsPage()
    {
        return _modsPage ??= new ModsPage
        {
            DataContext = _sp.GetRequiredService<ModsPageViewModel>(),
            BackgroundVisual = _bgVisual
        };
    }

    private ModPacksPage GetOrCreateModPacksPage()
    {
        return _modPacksPage ??= new ModPacksPage
        {
            DataContext = _sp.GetRequiredService<ModpacksPageViewModel>(),
            BackgroundVisual = _bgVisual
        };
    }

    private SettingsPage GetOrCreateSettingsPage()
    {
        return _settingsPage ??= new SettingsPage
        {
            DataContext = _sp.GetRequiredService<SettingsPageViewModel>()
        };
    }

    private NewsPage GetOrCreateNewsPage()
    {
        return _newsPage ??= new NewsPage
        {
            DataContext = _sp.GetRequiredService<NewsPageViewModel>(),
            BackgroundVisual = _bgVisual
        };
    }

    private GalleryPage GetOrCreateGalleryPage()
    {
        return _galleryPage ??= new GalleryPage
        {
            DataContext = _sp.GetRequiredService<GalleryPageViewModel>(),
            BackgroundVisual = _bgVisual
        };
    }

    private async Task NavigateToPage(Control targetPage, string discordStatus, Func<Task>? loadDataAction = null)
    {
        if (_isNavigating || _currentPage == targetPage) return;

        try
        {
            _isNavigating = true;

            var previousPage = _currentPage;
            if (previousPage?.DataContext is ITrimmablePage trimmableVm)
            {
                trimmableVm.TrimMemory();
            }
            else if (previousPage is ITrimmablePage trimmablePage)
            {
                trimmablePage.TrimMemory();
            }

            _currentPage = targetPage;
            _main.PageHost.Content = targetPage;

            if (!string.IsNullOrEmpty(discordStatus))
            {
                _ = DiscordController.UpdatePresence(discordStatus);
            }

            AvaloniaImageHelper.TrimCache(24);

            _ = Task.Run(async () =>
            {
                await Task.Delay(400);
                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: OperatingSystem.IsWindows());
            });

            await Task.Delay(250);
            _isNavigating = false;

            if (loadDataAction != null)
            {
                await loadDataAction();
            }
        }
        catch
        {
            _isNavigating = false;
        }
    }

    public async void NavigateToHome() =>
        await NavigateToPage(GetOrCreateHomePage(), LocalizationManager.GetString("DiscordRPC.InHome", "В головному вікні"));

    public async void NavigateToMods()
    {
        var page = GetOrCreateModsPage();
        await NavigateToPage(page, LocalizationManager.GetString("DiscordRPC.SearchingMods", "Шукає моди"), async () =>
        {
            if (page.DataContext is ModsPageViewModel vm && vm.SearchResults.Count == 0 && !vm.IsLoading)
            {
                await vm.SearchModsAsync();
            }
        });
    }

    public async void NavigateToModPacks() =>
        await NavigateToPage(GetOrCreateModPacksPage(), LocalizationManager.GetString("DiscordRPC.ViewingModpacks", "Дивиться збірки"));

    public async void NavigateToServers()
    {
        var page = GetServerPage();
        await NavigateToPage(page, LocalizationManager.GetString("DiscordRPC.ViewingServers", "Дивиться список серверів"), async () =>
        {
            if (page.DataContext is ServersPageViewModel vm)
            {
                await vm.EnsureInitializedAsync();
            }
        });
    }

    public async void NavigateToSettings() =>
        await NavigateToPage(GetOrCreateSettingsPage(), LocalizationManager.GetString("DiscordRPC.InSettings", "В налаштуваннях"));

    public async void NavigateToNews() =>
        await NavigateToPage(GetOrCreateNewsPage(), LocalizationManager.GetString("DiscordRPC.ViewingNews", "Читає новини"));

    public async void NavigateToGallery()
    {
        var page = GetOrCreateGalleryPage();
        await NavigateToPage(page, LocalizationManager.GetString("DiscordRPC.ViewingGallery", "Переглядає галерею"), async () =>
        {
            if (page.DataContext is GalleryPageViewModel vm)
            {
                await vm.EnsureInitializedAsync();
            }
        });
    }
}
