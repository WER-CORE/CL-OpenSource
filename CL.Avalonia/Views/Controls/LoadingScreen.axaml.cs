using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System;
using CL_CLegendary_Launcher_.Avalonia.Views.Windows;
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

            SettingsManager.Default.OfflineModLauncher = false;
            SettingsManager.Save();
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
                string phrase = LocalizationManager.GetString($"LoadScreen.RandomPhrases.Phrase{i}", "");
                if (!string.IsNullOrWhiteSpace(phrase)) ViewModel.RandomPhrases.Add(phrase);
            }

            if (ViewModel.RandomPhrases.Count == 0)
            {
                ViewModel.RandomPhrases = new List<string> { "Сонце пече так, що навіть кулери ПК просять про холодний лимонад...", "Літо на вулиці, спека в грі, затишок на душі..." };
            }

            try
            {
                string phrasesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loading_phrases.txt");
                if (File.Exists(phrasesPath))
                {
                    var lines = File.ReadAllLines(phrasesPath).Where(x => !string.IsNullOrWhiteSpace(x) && !x.Trim().StartsWith("//")).ToList();
                    if (lines.Count > 0) ViewModel.RandomPhrases = lines;
                }
            }
            catch { }
        }

        private async Task SimulateLoadingAnimationAsync()
        {
            ViewModel.ProgressValue = 0;
            if (ViewModel.RandomPhrases.Count > 0)
                ViewModel.RandomPhraseText = ViewModel.RandomPhrases[_random.Next(ViewModel.RandomPhrases.Count)];

            for (int i = 0; i <= 100; i++)
            {
                if (this.Opacity == 0) return;

                await Task.Delay(_random.Next(15, 40));
                ViewModel.ProgressValue = i;

                if (i % 20 == 0 && i > 0 && ViewModel.RandomPhrases.Count > 0)
                {
                    ViewModel.RandomPhraseText = ViewModel.RandomPhrases[_random.Next(ViewModel.RandomPhrases.Count)];
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
