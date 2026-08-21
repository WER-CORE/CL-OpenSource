using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CmlLib.Core.Auth;
using SukiUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private readonly string _launcherPath;
        private GameSessionManager _gameSessionManager;
        private LastActionService _lastActionService;
        private GameLaunchService _gameLaunchService;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _launcherPath = PlatformPaths.DefaultLauncherPath();

            InitializePlatformServices();
            _gameSessionManager = new GameSessionManager();
            _lastActionService = new LastActionService();
            _gameLaunchService = new GameLaunchService(_gameSessionManager, _lastActionService);


            SettingsManager.Load();
            if (string.IsNullOrEmpty(SettingsManager.Default.PathLacunher))
            {
                SettingsManager.Default.PathLacunher = _launcherPath;
                SettingsManager.Save();
            }
            LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode ?? "uk_UA");

            UrlOpener.ErrorHandler = (ex, url) =>
                SetStatus($"Не вдалося відкрити {url}: {ex.Message}");

            this.FindControl<Button>("LoadVersionsButton")!.Click += OnLoadVersions;
            this.FindControl<Button>("OpenSiteButton")!.Click += OnOpenSite;
            this.FindControl<Button>("PlayButton")!.Click += OnPlay;
            this.FindControl<ListBox>("VersionsList")!.SelectionChanged += OnVersionSelected;

            ShowEnvironment();
        }

        
        private void InitializePlatformServices()
        {
            ServiceLocator.Current.Register<IDispatcherService, AvaloniaDispatcherService>(new AvaloniaDispatcherService());
            ServiceLocator.Current.Register<IMainWindowController, AvaloniaWindowController>(new AvaloniaWindowController(this));
            ServiceLocator.Current.Register<ITaskProgressService, AvaloniaTaskProgressService>(new AvaloniaTaskProgressService(this));
            ServiceLocator.Current.Register<ISoundService, DummySoundService>(new DummySoundService());
            ServiceLocator.Current.Register<IDialogService, AvaloniaDialogService>(new AvaloniaDialogService(this));
        }

        private void ShowEnvironment()
        {
            this.FindControl<TextBlock>("PlatformText")!.Text =
                $"{PlatformPaths.OsName()} · .NET {Environment.Version} · Avalonia UI";

            this.FindControl<TextBlock>("PathText")!.Text =
                $"Каталог гри:\n{_launcherPath}";

            var javas = JavaLocator.Detect(_launcherPath);

            var javaBox = this.FindControl<ComboBox>("JavaBox")!;
            javaBox.ItemsSource = javas;
            javaBox.SelectedIndex = javas.Count > 0 ? 0 : -1;

            this.FindControl<TextBlock>("CoreStatusText")!.Text =
                $"Мова: {LocalizationManager.CurrentLanguage}\n" +
                $"Пам'ять для гри: {SettingsManager.Default.OP} МБ\n" +
                $"Java у системі: {(javas.Count > 0 ? string.Join(", ", javas.Select(j => j.MajorVersion).Distinct()) : "не знайдено")}";

            SetStatus("Готово.");
        }

        private async void OnLoadVersions(object? sender, RoutedEventArgs e)
        {
            var button = (Button)sender!;
            var list = this.FindControl<ListBox>("VersionsList")!;

            button.IsEnabled = false;
            SetStatus("Завантаження версій з Mojang...");

            try
            {
                var service = new VersionService(_launcherPath);
                var versions = await service.GetFilteredVersionsAsync(string.Empty, true, false, false, false);

                list.ItemsSource = versions;
                SetStatus($"Отримано {versions.Count} релізів через CL.Core.");
            }
            catch (Exception ex)
            {
                SetStatus($"Помилка: {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OnVersionSelected(object? sender, SelectionChangedEventArgs e)
        {
            string? version = SelectedVersion();

            this.FindControl<TextBlock>("SelectedVersionText")!.Text =
                version == null ? string.Empty : $"Обрано: {version}";

            this.FindControl<Button>("PlayButton")!.IsEnabled = version != null;
        }

        private async void OnPlay(object? sender, RoutedEventArgs e)
        {
            string? version = SelectedVersion();
            if (version == null)
                return;

            var button = (Button)sender!;
            button.IsEnabled = false;

            try
            {
                var config = new LaunchConfiguration
                {
                    IsOffline = true,
                    MinimumRamMb = 1024,
                    MaximumRamMb = SettingsManager.Default.OP,
                    ScreenWidth = 854,
                    ScreenHeight = 480,
                    Session = MSession.CreateOfflineSession(Nickname()),
                    AccountType = AccountType.Offline
                };

                await _gameLaunchService.LaunchGameAsync(LoaderType.Vanilla, version, "", config);
                SetStatus($"Гру успішно завершено.");
            }
            catch (Exception ex)
            {
                SetStatus($"Не вдалося запустити: {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void WatchExit(Process process, string version)
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.UIThread.Post(() =>
                SetStatus($"{version} завершено з кодом {process.ExitCode}."));
        }

        private string? SelectedVersion()
            => this.FindControl<ListBox>("VersionsList")!.SelectedItem as string;

        private string Nickname()
        {
            string? typed = this.FindControl<TextBox>("NicknameBox")!.Text;
            return string.IsNullOrWhiteSpace(typed) ? "Player" : typed.Trim();
        }

        private void OnOpenSite(object? sender, RoutedEventArgs e)
            => WebHelper.OpenUrl("https://discord.gg/RhyZjACs2U");

        public void SetStatus(string text)
            => this.FindControl<TextBlock>("StatusText")!.Text = text;
    }

    public class AvaloniaDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => Dispatcher.UIThread.Post(action);
        public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
        public Task<T> InvokeAsync<T>(Func<T> action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
        public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    }

    public class AvaloniaWindowController : IMainWindowController
    {
        private readonly MainWindow _window;
        public AvaloniaWindowController(MainWindow window) => _window = window;

        public void SetPlayButtonText(string text) { }
        public void SetInstallVersionOnPlay(bool value) { }
        public void Minimize() { }
        public void Restore() { }
        public void Close() { }
        public void ShowGameLog(Process process) { }
        public void ShowGameLogFromFile(string logFilePath) { }
        public void ShowSnackbar(string title, string message, int durationSeconds = 3) 
            => _window.SetStatus($"[Снекбар]: {title} - {message}");
        public void UpdateServerMonitoring() { }
    }

    public class AvaloniaTaskProgressService : ITaskProgressService
    {
        private readonly MainWindow _window;
        public CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();
        public string Title { get; set; } = "";
        public bool IsLoaded => true;

        public AvaloniaTaskProgressService(MainWindow window) => _window = window;

        public void ShowProgressWindow() => _window.SetStatus("Відкрито вікно прогресу...");
        public void CloseProgressWindow() => _window.SetStatus("Вікно прогресу закрито.");
        
        public void UpdateVersionProgress(int progress, object version) 
            => _window.SetStatus($"[Версія]: {progress}%");
            
        public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName) 
            => _window.SetStatus($"[Файли]: {downloadedFiles}/{totalFiles} - {fileName}");
            
        public void UpdateFileProgress(int progress) { }
        public void AddActiveDownload(ConcurrentDownloadItem item) { }
        public void RemoveActiveDownload(ConcurrentDownloadItem item) { }
    }

    public class DummySoundService : ISoundService
    {
        public void PlaySound(string resourceName) { }
        public void StopSound() { }
    }

    public class AvaloniaDialogService : IDialogService
    {
        private readonly MainWindow _window;
        public AvaloniaDialogService(MainWindow window) => _window = window;

        public void ShowMessage(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
            => _window.SetStatus($"[Повідомлення]: {message}");

        public Task ShowMessageAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            _window.SetStatus($"[Повідомлення]: {message}");
            return Task.CompletedTask;
        }

        public bool AskQuestion(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            _window.SetStatus($"[Запитання]: {message} (Автоматично ТАК)");
            return true;
        }

        public Task<bool> AskQuestionAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            _window.SetStatus($"[Запитання]: {message} (Автоматично ТАК)");
            return Task.FromResult(true);
        }

        public Task<string> OpenFileDialogAsync(string title, string filter)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<string> OpenFolderDialogAsync(string title)
        {
            return Task.FromResult(string.Empty);
        }
    }
}


