using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CL.Core.Interfaces;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Platform;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;

namespace CL_CLegendary_Launcher_.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                LoadSettings();

                var services = BuildServices();
                RegisterCoreServices(services);

                var dialogs = services.GetRequiredService<IDialogService>();
                UrlOpener.ErrorHandler = (ex, url) => dialogs.ShowMessage(
                    string.Format(LocalizationManager.GetString("Dialogs.UrlOpenError", "Не вдалося відкрити {0}:\n{1}"), url, ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"));

                var viewModel = services.GetRequiredService<MainWindowViewModel>();
                viewModel.Settings.ApplyTheme();

                desktop.MainWindow = new MainWindow { DataContext = viewModel };
                _ = viewModel.InitializeAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }

        // Мова має бути завантажена до створення вікна: LocExtension читає рядки під час розбору XAML.
        private static void LoadSettings()
        {
            PlatformPaths.MigrateLegacyAppData();
            SettingsManager.Load();

            if (string.IsNullOrEmpty(SettingsManager.Default.PathLacunher))
            {
                SettingsManager.Default.PathLacunher = PlatformPaths.DefaultLauncherPath();
                SettingsManager.Save();
            }

            LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode ?? "uk_UA");
        }

        private static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
            services.AddSingleton<ISukiToastManager, SukiToastManager>();

            services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
            services.AddSingleton<IDialogService, AvaloniaDialogService>();
            services.AddSingleton<IMainWindowController, AvaloniaWindowController>();
            services.AddSingleton<ISoundService, NullSoundService>();
            services.AddSingleton<DownloadProgressViewModel>();
            services.AddSingleton<ITaskProgressService>(sp => sp.GetRequiredService<DownloadProgressViewModel>());

            services.AddSingleton<GameSessionManager>();
            services.AddSingleton<LastActionService>();
            services.AddSingleton<GameLaunchService>();
            services.AddSingleton<ProfileManagerService>();
            services.AddSingleton<AccountService>();

            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<AccountsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            return services.BuildServiceProvider();
        }

        // Сервіси ядра поки що беруть платформні реалізації через ServiceLocator, а не через конструктор.
        private static void RegisterCoreServices(IServiceProvider services)
        {
            var locator = ServiceLocator.Current;
            locator.Register<IDispatcherService, IDispatcherService>(services.GetRequiredService<IDispatcherService>());
            locator.Register<IDialogService, IDialogService>(services.GetRequiredService<IDialogService>());
            locator.Register<IMainWindowController, IMainWindowController>(services.GetRequiredService<IMainWindowController>());
            locator.Register<ISoundService, ISoundService>(services.GetRequiredService<ISoundService>());
            locator.Register<ITaskProgressService, ITaskProgressService>(services.GetRequiredService<ITaskProgressService>());
        }
    }
}
