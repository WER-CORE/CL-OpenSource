using Avalonia.VisualTree;
using Avalonia.Input;
﻿using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CL.Core.Interfaces;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CL_CLegendary_Launcher_.Avalonia;

public partial class App : Application
{
    public IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        UIConfig.AssetScheme = "avares://CL.Avalonia";

        PlatformPaths.MigrateLegacyAppData();
        SettingsManager.Load();
        CheckAndCreateDefaultPath();

        string initialLang = string.IsNullOrWhiteSpace(SettingsManager.Default.LanguageCode) ? "uk_UA" : SettingsManager.Default.LanguageCode;
        LocalizationManager.LoadLanguage(initialLang);

        UrlOpener.ErrorHandler = (ex, target) =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AvaloniaDialogService.Show(
                    $"Не вдалося відкрити: {target}\n{ex.Message}",
                    "Помилка відкриття",
                    CL.Core.Models.MascotEmotion.Alert);
            });
        };

        ServiceProvider = ConfigureServices();

        ServiceLocator.Current.Initialize(ServiceProvider);

        RegisterGlobalSoundHandlers();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            desktop.MainWindow = new LoadScreenWindow
            {
                DataContext = ServiceProvider.GetRequiredService<LoadScreenViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterGlobalSoundHandlers()
    {
        try
        {
            Button.ClickEvent.AddClassHandler<Button>((btn, e) => AvaloniaSoundService.Click(), handledEventsToo: true);
            ToggleSwitch.IsCheckedChangedEvent.AddClassHandler<ToggleSwitch>((ts, e) => AvaloniaSoundService.Click(), handledEventsToo: true);
            CheckBox.IsCheckedChangedEvent.AddClassHandler<CheckBox>((cb, e) => AvaloniaSoundService.Click(), handledEventsToo: true);
            MenuItem.ClickEvent.AddClassHandler<MenuItem>((mi, e) => AvaloniaSoundService.Click(), handledEventsToo: true);
            ListBoxItem.PointerPressedEvent.AddClassHandler<ListBoxItem>((lbi, e) =>
            {
                if (e.GetCurrentPoint(lbi).Properties.IsLeftButtonPressed)
                {
                    AvaloniaSoundService.Click();
                }
            }, handledEventsToo: true);
            ComboBoxItem.PointerPressedEvent.AddClassHandler<ComboBoxItem>((cbi, e) =>
            {
                if (e.GetCurrentPoint(cbi).Properties.IsLeftButtonPressed)
                {
                    AvaloniaSoundService.Click();
                }
            }, handledEventsToo: true);
            InputElement.PointerPressedEvent.AddClassHandler<Control>((ctrl, e) =>
            {
                if (e.GetCurrentPoint(ctrl).Properties.IsLeftButtonPressed &&
                    ctrl is not Button &&
                    ctrl is not ListBoxItem &&
                    ctrl is not ComboBoxItem &&
                    ctrl.Cursor != null &&
                    ctrl.FindAncestorOfType<Button>() == null &&
                    ctrl.FindAncestorOfType<ListBoxItem>() == null &&
                    ctrl.FindAncestorOfType<ComboBoxItem>() == null)
                {
                    AvaloniaSoundService.Click();
                }
            }, handledEventsToo: true);
        }
        catch
        {
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IMainWindowController, AvaloniaWindowController>();
        services.AddSingleton<ITaskProgressService, AvaloniaProgressService>();
        services.AddSingleton<ISoundService, AvaloniaSoundService>();
        services.AddSingleton<AvaloniaThemeService>();
        services.AddSingleton<ScreenshotService>();

        services.AddSingleton<GameSessionManager>();
        services.AddSingleton<LastActionService>();
        services.AddSingleton<ProfileManagerService>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<GameLaunchService>();
        services.AddSingleton<ModDownloadService>();
        services.AddSingleton<ModpackService>();
        services.AddSingleton<ServerListService>();
        services.AddSingleton<NewsService>();
        services.AddSingleton(_ =>
        {
            string path = SettingsManager.Default.PathLacunher;
            if (string.IsNullOrWhiteSpace(path))
                path = PlatformPaths.DefaultLauncherPath();

            return new VersionService(path);
        });

        services.AddTransient<CL_MainViewModel>();
        services.AddTransient<LoadScreenViewModel>();

        services.AddTransient<HomePageViewModel>();
        services.AddTransient<ModpacksPageViewModel>();
        services.AddTransient<ModsPageViewModel>();
        services.AddTransient<ServersPageViewModel>();
        services.AddTransient<GalleryPageViewModel>();
        services.AddTransient<AccountsPageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<NewsPageViewModel>();

        services.AddTransient<GameLogViewModel>();
        services.AddTransient<CreateModpackViewModel>();
        services.AddTransient<DownloadModpackDialogViewModel>();
        services.AddTransient<EditModpackDialogViewModel>();
        services.AddTransient<DownloadEditPackViewModel>();
        services.AddTransient<DownloadProgressViewModel>();
        services.AddTransient<WorldBackupViewModel>();
        services.AddTransient<ChangelogDialogViewModel>();
        services.AddTransient<ColorPickerDialogViewModel>();
        services.AddTransient<CreditsDialogViewModel>();

        return services.BuildServiceProvider();
    }

    private static void CheckAndCreateDefaultPath()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Default.PathLacunher))
        {
            string defaultPath = PlatformPaths.DefaultLauncherPath();
            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }
            SettingsManager.Default.PathLacunher = defaultPath;
            SettingsManager.Save();
        }
    }
}
