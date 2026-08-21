using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CmlLib.Core.Auth;
using Microsoft.Identity.Client;
using SukiUI;
using SukiUI.Controls;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views
{
    public partial class MainWindow : SukiWindow
    {
        private readonly string _launcherPath;
        private GameSessionManager _gameSessionManager;
        private LastActionService _lastActionService;
        private GameLaunchService _gameLaunchService;
        private ProfileManagerService _profileManager;
        private AccountService _accountService;
        private readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
        private List<ProfileItem> _profiles = new List<ProfileItem>();
        private long _lastProgressTick;
        private List<string> _versions = new List<string>();

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _launcherPath = PlatformPaths.DefaultLauncherPath();

            InitializePlatformServices();
            _gameSessionManager = new GameSessionManager();
            _lastActionService = new LastActionService();
            _gameLaunchService = new GameLaunchService(_gameSessionManager, _lastActionService);
            _profileManager = new ProfileManagerService();
            _accountService = new AccountService(_profileManager);


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

            this.FindControl<Button>("ClearLogButton")!.Click += OnClearLog;
            this.FindControl<Button>("PickFileButton")!.Click += OnPickFile;
            this.FindControl<Button>("PickFolderButton")!.Click += OnPickFolder;

            this.FindControl<Button>("AddOfflineButton")!.Click += OnAddOffline;
            this.FindControl<Button>("AddMicrosoftButton")!.Click += OnAddMicrosoft;
            this.FindControl<Button>("AddLittleSkinButton")!.Click += OnAddLittleSkin;
            this.FindControl<Button>("SelectProfileButton")!.Click += OnSelectProfile;
            this.FindControl<Button>("DeleteProfileButton")!.Click += OnDeleteProfile;
            this.FindControl<ListBox>("ProfilesList")!.SelectionChanged += OnProfileSelected;

            ShowEnvironment();
            RefreshProfiles();
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

                // Вже встановлені версії піднімаємо вгору й позначаємо -
                // так видно, що можна запустити без повного завантаження.
                var installed = InstalledVersions();

                _versions = versions
                    .OrderByDescending(v => installed.Contains(v))
                    .ToList();

                list.ItemsSource = _versions
                    .Select(v => installed.Contains(v) ? $"{v}  ✓ встановлено" : v)
                    .ToList();

                SetStatus($"Отримано {versions.Count} релізів, з них встановлено локально: {installed.Count}.");
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

        private string? SelectedVersion()
        {
            int i = this.FindControl<ListBox>("VersionsList")!.SelectedIndex;
            return i >= 0 && i < _versions.Count ? _versions[i] : null;
        }

        private HashSet<string> InstalledVersions()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string versionsDir = System.IO.Path.Combine(_launcherPath, "versions");

            if (!System.IO.Directory.Exists(versionsDir))
                return result;

            foreach (var dir in System.IO.Directory.GetDirectories(versionsDir))
            {
                string name = System.IO.Path.GetFileName(dir);
                if (System.IO.File.Exists(System.IO.Path.Combine(dir, name + ".json")))
                    result.Add(name);
            }

            return result;
        }

        private string Nickname()
        {
            string? typed = this.FindControl<TextBox>("NicknameBox")!.Text;
            return string.IsNullOrWhiteSpace(typed) ? "Player" : typed.Trim();
        }

        private void OnOpenSite(object? sender, RoutedEventArgs e)
            => WebHelper.OpenUrl("https://discord.gg/RhyZjACs2U");

        private async void OnPickFile(object? sender, RoutedEventArgs e)
        {
            var dialogs = ServiceLocator.Current.GetService<IDialogService>();
            string path = await dialogs.OpenFileDialogAsync("Оберіть файл", "Архіви (*.zip)|*.zip");
            SetStatus(string.IsNullOrEmpty(path) ? "Вибір файлу скасовано." : $"Обрано файл: {path}");
        }

        private async void OnPickFolder(object? sender, RoutedEventArgs e)
        {
            var dialogs = ServiceLocator.Current.GetService<IDialogService>();
            string path = await dialogs.OpenFolderDialogAsync("Оберіть теку");
            SetStatus(string.IsNullOrEmpty(path) ? "Вибір теки скасовано." : $"Обрано теку: {path}");
        }

        private void RefreshProfiles()
        {
            try
            {
                var profiles = _accountService.GetProfiles() ?? new List<ProfileItem>();
                var list = this.FindControl<ListBox>("ProfilesList")!;

                list.ItemsSource = profiles
                    .Select(p => $"{p.NameAccount} [{p.TypeAccount}]")
                    .ToList();

                _profiles = profiles;
                SetStatus($"Профілів збережено: {profiles.Count}.");
            }
            catch (Exception ex)
            {
                SetStatus($"Не вдалося прочитати профілі: {ex.Message}");
            }
        }

        private void OnProfileSelected(object? sender, SelectionChangedEventArgs e)
        {
            bool has = SelectedProfile() != null;
            this.FindControl<Button>("SelectProfileButton")!.IsEnabled = has;
            this.FindControl<Button>("DeleteProfileButton")!.IsEnabled = has;
        }

        private ProfileItem? SelectedProfile()
        {
            int i = this.FindControl<ListBox>("ProfilesList")!.SelectedIndex;
            return i >= 0 && i < _profiles.Count ? _profiles[i] : null;
        }

        private async void OnAddOffline(object? sender, RoutedEventArgs e)
        {
            try
            {
                var profile = await _accountService.AddOfflineAccountAsync(Nickname());
                SetStatus($"Додано offline-акаунт {profile.NameAccount} (UUID {profile.UUID}).");
                RefreshProfiles();
            }
            catch (Exception ex)
            {
                SetStatus($"Помилка створення offline-акаунта: {ex.Message}");
            }
        }

        private async void OnAddMicrosoft(object? sender, RoutedEventArgs e)
        {
            var button = (Button)sender!;
            button.IsEnabled = false;
            SetStatus("Microsoft: запит device code...");

            try
            {
                var profile = await _accountService.AddMicrosoftAccountAsync(OnDeviceCode);

                if (profile == null)
                    SetStatus("Microsoft: вхід не завершено.");
                else
                {
                    SetStatus($"Microsoft: увійшли як {profile.NameAccount} ({profile.LicenseType}).");
                    RefreshProfiles();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Microsoft: помилка входу — {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private Task OnDeviceCode(DeviceCodeResult result)
        {
            SetStatus($"Microsoft: відкрий {result.VerificationUrl} та введи код {result.UserCode}");
            SetStatus($"Microsoft: код дійсний до {result.ExpiresOn.ToLocalTime():HH:mm:ss}.");

            WebHelper.OpenUrl(result.VerificationUrl);
            return Task.CompletedTask;
        }

        private async void OnAddLittleSkin(object? sender, RoutedEventArgs e)
        {
            string login = this.FindControl<TextBox>("LittleSkinLoginBox")!.Text ?? string.Empty;
            string password = this.FindControl<TextBox>("LittleSkinPasswordBox")!.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                SetStatus("LittleSkin: вкажи e-mail і пароль.");
                return;
            }

            var button = (Button)sender!;
            button.IsEnabled = false;
            SetStatus("LittleSkin: авторизація...");

            try
            {
                var profile = await _accountService.AddLittleSkinAccountAsync(login, password);
                SetStatus($"LittleSkin: увійшли як {profile.NameAccount}.");
                this.FindControl<TextBox>("LittleSkinPasswordBox")!.Text = string.Empty;
                RefreshProfiles();
            }
            catch (Exception ex)
            {
                SetStatus($"LittleSkin: помилка — {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async void OnSelectProfile(object? sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile();
            if (profile == null)
                return;

            var button = (Button)sender!;
            button.IsEnabled = false;
            SetStatus($"Активація профілю {profile.NameAccount}...");

            try
            {
                bool ok = await _accountService.SelectProfileAsync(profile);

                if (ok)
                {
                    var session = _accountService.CurrentSession;
                    SetStatus($"Активний профіль: {session?.Username} ({profile.TypeAccount}).");
                    this.FindControl<TextBlock>("CurrentAccountText")!.Text =
                        $"Активний профіль: {session?.Username} [{profile.TypeAccount}]\nUUID: {session?.UUID}";
                }
                else
                    SetStatus("Не вдалося активувати профіль (див. повідомлення вище).");
            }
            catch (Exception ex)
            {
                SetStatus($"Помилка активації: {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OnDeleteProfile(object? sender, RoutedEventArgs e)
        {
            var profile = SelectedProfile();
            if (profile == null)
                return;

            try
            {
                _accountService.DeleteProfile(profile);
                SetStatus($"Профіль {profile.NameAccount} видалено.");
                RefreshProfiles();
            }
            catch (Exception ex)
            {
                SetStatus($"Не вдалося видалити профіль: {ex.Message}");
            }
        }

        // Прогрес оновлює один рядок і не потрапляє в журнал:
        // під час встановлення версії подій тисячі, і кожен запис у журнал
        // перебудовував би весь текст, блокуючи UI-потік.
        public void SetProgress(string text)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => SetProgress(text), DispatcherPriority.Background);
                return;
            }

            var now = Environment.TickCount64;
            if (now - _lastProgressTick < 100 && !string.IsNullOrEmpty(text))
                return;

            _lastProgressTick = now;

            var block = this.FindControl<TextBlock>("ProgressText");
            if (block != null)
                block.Text = text;
        }

        public void SetStatus(string text)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => SetStatus(text));
                return;
            }

            _log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ")
                .AppendLine(text);

            var block = this.FindControl<SelectableTextBlock>("LogText");
            if (block != null)
                block.Text = _log.ToString();

            this.FindControl<ScrollViewer>("LogScroller")?.ScrollToEnd();
        }

        private void OnClearLog(object? sender, RoutedEventArgs e)
        {
            _log.Clear();
            var block = this.FindControl<SelectableTextBlock>("LogText");
            if (block != null)
                block.Text = string.Empty;
        }
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

        public void ShowProgressWindow() => _window.SetStatus("Початок завантаження...");

        public void CloseProgressWindow()
        {
            _window.SetProgress(string.Empty);
            _window.SetStatus("Завантаження завершено.");
        }

        public void UpdateVersionProgress(int progress, object version)
            => _window.SetProgress($"{version}: {progress}%");

        public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName)
            => _window.SetProgress($"{downloadedFiles}/{totalFiles} — {fileName}");

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

        public async Task<string> OpenFileDialogAsync(string title, string filter)
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = BuildFilters(filter)
            });

            var file = files.FirstOrDefault();
            return file?.TryGetLocalPath() ?? string.Empty;
        }

        public async Task<string> OpenFolderDialogAsync(string title)
        {
            var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            var folder = folders.FirstOrDefault();
            return folder?.TryGetLocalPath() ?? string.Empty;
        }

        // Переводить WinForms-фільтр ("Опис|*.zip;*.jar|...") у формат Avalonia.
        private static List<FilePickerFileType> BuildFilters(string filter)
        {
            var types = new List<FilePickerFileType>();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var parts = filter.Split('|');
                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    var patterns = parts[i + 1]
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0)
                        .ToList();

                    if (patterns.Count > 0)
                        types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
                }
            }

            types.Add(new FilePickerFileType("Усі файли") { Patterns = new[] { "*" } });
            return types;
        }
    }
}


