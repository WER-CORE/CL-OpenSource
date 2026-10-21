using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls
{
    public partial class LoadScreenWindow : Window
    {
        private Random _random = new Random();
        private readonly string versionLauncher = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        private LoadScreenViewModel ViewModel => (LoadScreenViewModel)DataContext!;

        public LoadScreenWindow()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is LoadScreenViewModel)
                {
                    InitializeLogic();
                }
            };
        }

        private void InitializeLogic()
        {
            SettingsManager.Load();
            if (SettingsManager.Default.IsCrashReportingEnabled)
            {
                CrashReportManager.Enable();
            }

            if (string.IsNullOrEmpty(SettingsManager.Default.LanguageCode))
            {
                string osLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
                string autoLang = osLang switch
                {
                    "uk" => "uk_UA",
                    "be" => "be_BY",
                    "pl" => "pl_PL",
                    "cs" => "cs_CZ",
                    "ru" => "uk_UA",
                    _ => "en_US"
                };
                SettingsManager.Default.LanguageCode = autoLang;
                SettingsManager.Save();
            }

            LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode);
            ViewModel.StatusText = LocalizationManager.GetString("LoadScreen.LoadingText", "Завантаження ресурсів...");

            ApplyCustomSettings();

            SettingsManager.Default.OfflineModLauncher = false;
            SettingsManager.Save();
        }

        private void ApplyCustomSettings()
        {
            try
            {
                string bgPath = SettingsManager.Default.LoadScreenBackground;
                if (!string.IsNullOrWhiteSpace(bgPath) && File.Exists(bgPath))
                {
                    using var stream = File.OpenRead(bgPath);
                    ViewModel.BackgroundImage = new Bitmap(stream);
                }
                else
                {
                    LoadDefaultBackground();
                }
            }
            catch
            {
                LoadDefaultBackground();
            }

            try
            {
                string colorHex = SettingsManager.Default.LoadScreenBarColor;
                if (!string.IsNullOrWhiteSpace(colorHex))
                {
                    if (!colorHex.StartsWith("#")) colorHex = "#" + colorHex;
                    if (Color.TryParse(colorHex, out var parsedColor))
                    {
                        ViewModel.ProgressBarBrush = new SolidColorBrush(parsedColor);
                    }
                }
            }
            catch { }
        }

        private void LoadDefaultBackground()
        {
            try
            {
                var uri = new Uri("avares://CL.Avalonia/Assets/Backgrounds/DarkThemBG.webp");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    ViewModel.BackgroundImage = new Bitmap(stream);
                }
            }
            catch { }
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            await RunStartupProcessAsync();
        }

        private async Task RunStartupProcessAsync()
        {
            await LoadLocalizedPhrases();

            try { await DiscordController.Initialize(LocalizationManager.GetString("LoadScreen.DiscordRPC", "У віконці завантаження")); } catch { }

            var animationTask = SimulateLoadingAnimationAsync();

            bool updateAvailable = false;
            try
            {
                updateAvailable = await CheckForUpdatesAsync();
            }
            catch (Exception)
            {
                SettingsManager.Default.OfflineModLauncher = true;
                SettingsManager.Save();
            }

            if (updateAvailable)
            {
                var updater = new UpdaterWindow();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = updater;
                }
                updater.Show();
                this.Close();
                return;
            }

            try
            {
                bool eulaAccepted = await CheckEulaAsync();

                if (!eulaAccepted)
                {
                    DiscordController.Deinitialize();
                    Environment.Exit(0);
                    return;
                }

                await animationTask;
                OpenMainWindow();
            }
            catch (Exception ex)
            {
                string errorDetails = $"Помилка: {ex.Message}\nСтек:\n{ex.StackTrace}";
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "crash-report.txt");
                try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); File.WriteAllText(logPath, errorDetails); } catch { }

                Environment.Exit(1);
            }
        }

        private async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                bool isPrerelease = string.Equals(SettingsManager.Default.UpdateChannel, "Preview", StringComparison.OrdinalIgnoreCase);
                var source = new global::Velopack.Sources.GithubSource("https://github.com/WER-CORE/CL-Win-Edition--Update", null, isPrerelease);
                var mgr = new global::Velopack.UpdateManager(source);
                if (mgr.IsInstalled)
                {
                    var updateInfo = await mgr.CheckForUpdatesAsync();
                    return updateInfo != null && updateInfo.TargetFullRelease != null;
                }
            }
            catch { }

            using var client = new System.Net.Http.HttpClient();
            string json = await client.GetStringAsync(Secrets.UpdateUrlCheckLoadScreen);

            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var versionElement))
            {
                string remoteVerStr = versionElement.GetString()?.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase) ?? "";
                string localVerStr = versionLauncher.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase);

                if (Version.TryParse(remoteVerStr, out Version? vRemote) && Version.TryParse(localVerStr, out Version? vLocal))
                {
                    return vRemote > vLocal;
                }
                return remoteVerStr != localVerStr;
            }
            return false;
        }

        private async Task LoadLocalizedPhrases()
        {
            try
            {
                var availableLangs = await LocalizationFetcher.GetAvailableLanguagesAsync();
                if (availableLangs?.Count > 0)
                {
                    await Task.WhenAll(availableLangs.Select(lang => LocalizationFetcher.DownloadLanguageAsync(lang)));
                }
                LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode);
                ViewModel.StatusText = LocalizationManager.GetString("LoadScreen.LoadingText", "Завантаження ресурсів...");
            }
            catch { }

            ViewModel.RandomPhrases.Clear();
            for (int i = 1; i <= 30; i++)
            {
                string phrase = LocalizationManager.GetString($"RandomPhrases.Phrase{i}", "");
                if (string.IsNullOrWhiteSpace(phrase))
                {
                    phrase = LocalizationManager.GetString($"LoadScreen.RandomPhrases.Phrase{i}", "");
                }
                if (!string.IsNullOrWhiteSpace(phrase)) ViewModel.RandomPhrases.Add(phrase);
            }

            if (ViewModel.RandomPhrases.Count == 0)
            {
                ViewModel.RandomPhrases = GetAutumnFallbackPhrases();
            }

            LoadCustomPhrases();
        }

        private void LoadCustomPhrases()
        {
            try
            {
                string dataFolder = PlatformPaths.DataFolder();
                string primaryPath = Path.Combine(dataFolder, "loading_phrases.txt");
                string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loading_phrases.txt");

                string? targetPath = null;
                if (File.Exists(primaryPath))
                {
                    targetPath = primaryPath;
                }
                else if (File.Exists(legacyPath))
                {
                    targetPath = legacyPath;
                }

                if (targetPath != null)
                {
                    var rawLines = File.ReadAllLines(targetPath, System.Text.Encoding.UTF8);
                    var validLines = new List<string>();

                    foreach (var rawLine in rawLines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("//") || line.StartsWith("#")) continue;

                        if (line.StartsWith("Текст:", StringComparison.OrdinalIgnoreCase))
                        {
                            line = line.Substring(6).Trim();
                        }
                        else if (line.StartsWith("Текст :", StringComparison.OrdinalIgnoreCase))
                        {
                            line = line.Substring(7).Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            validLines.Add(line);
                        }
                    }

                    if (validLines.Count > 0)
                    {
                        ViewModel.RandomPhrases = validLines;
                    }
                }
            }
            catch { }
        }

        private static List<string> GetAutumnFallbackPhrases()
        {
            return new List<string>
            {
                "Осінній дощ тихенько стукає у вікно, а в кімнаті пахне гарячим чаєм...",
                "Золоте листя кружляє за вікном, немов магічні частинки в грі...",
                "Homka збирає врожай гарбузів для найзатишнішої ферми у світі...",
                "Deeplay фотографує туманний світанок та золоті крони осінньої тайги...",
                "Заварюємо ароматне какао з маршмелоу та загортаємося в теплий плед...",
                "Данило запалює камін у затишному дерев'яному будиночку...",
                "WER_Clegendary тестує нові збірки під шум жовтневої зливи...",
                "кабанчік збирає солодкі ягідки в лісі та пригощає рудих лисичок...",
                "Вирізаємо сяючі ліхтарі Джека для святкового гелловінського настрою...",
                "Шурхіт пожовклого листя під лапками прирученого вовчика...",
                "Краплі дощу падають на дах, поки редстоун-механізми працюють як годинник...",
                "Шукаємо грибний біом та варимо теплий грибний супчик біля багаття...",
                "Час діставати теплі в'язані светри, зручні навушники та йти в шахту...",
                "Котик затишно муркоче на теплій печі, поки за вікном лютує вітер...",
                "Осінь на вулиці, тепло в чашці, безмежний затишок на душі..."
            };
        }

        private async Task SimulateLoadingAnimationAsync()
        {
            ViewModel.ProgressValue = 0;
            if (ViewModel.RandomPhrases.Count > 0)
                ViewModel.RandomPhraseText = ViewModel.RandomPhrases[_random.Next(ViewModel.RandomPhrases.Count)];

            for (int i = 0; i <= 100; i++)
            {
                if (this.Opacity == 0) return;

                await Task.Delay(_random.Next(25, 45));
                ViewModel.ProgressValue = i;

                if (i % 20 == 0 && i > 0 && ViewModel.RandomPhrases.Count > 0)
                {
                    string nextPhrase = ViewModel.RandomPhrases[_random.Next(ViewModel.RandomPhrases.Count)];
                    if (ViewModel.RandomPhrases.Count > 1 && nextPhrase == ViewModel.RandomPhraseText)
                    {
                        int nextIndex = (_random.Next(ViewModel.RandomPhrases.Count - 1) + 1 + ViewModel.RandomPhrases.IndexOf(ViewModel.RandomPhraseText)) % ViewModel.RandomPhrases.Count;
                        nextPhrase = ViewModel.RandomPhrases[nextIndex];
                    }
                    ViewModel.RandomPhraseText = nextPhrase;
                }
            }
        }

        private async Task<bool> CheckEulaAsync()
        {
            string currentLang = SettingsManager.Default.LanguageCode ?? "uk_UA";
            var eulaConfig = await EulaService.GetEulaAsync(currentLang);
            bool showEula = false;

            if (eulaConfig != null)
            {
                if (EulaService.IsEulaOutdated(eulaConfig.LastUpdated)) showEula = true;
            }
            else
            {
                if (SettingsManager.Default.EulaAcceptedDate == DateTime.MinValue) showEula = true;
            }

            if (showEula)
            {
                this.Opacity = 0;
                var eulaWin = new EulaWindow(eulaConfig);
                var result = await eulaWin.ShowDialog<bool>(this);
                this.Opacity = 1;
                return result;
            }

            return true;
        }

        private void OpenMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new CL_Main
                {
                    DataContext = ((App)Application.Current!).ServiceProvider!.GetRequiredService<CL_MainViewModel>()
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close();
            }
        }
    }
}
