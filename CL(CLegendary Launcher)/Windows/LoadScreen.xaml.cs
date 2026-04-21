using CL_CLegendary_Launcher_.Class;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class LoadScreen : Window
    {
        private List<string> RandomPhrases = new List<string>();
        private Random _random = new Random();
        private readonly string versionLauncher = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        public LoadScreen()
        {
            SettingsManager.Load();
            if (SettingsManager.Default.IsCrashReportingEnabled)
            {
                CrashReportManager.Enable();
            }
            if (string.IsNullOrEmpty(SettingsManager.Default.LanguageCode))
            {
                string osLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();

                string autoLang = "en_US";

                switch (osLang)
                {
                    case "uk":
                        autoLang = "uk_UA";
                        break;
                    case "be":
                        autoLang = "be_BY";
                        break;
                    case "pl":
                        autoLang = "pl_PL";
                        break;
                    case "cs":
                        autoLang = "cs_CZ";
                        break;
                    case "ru":
                        autoLang = "uk_UA";
                        break;
                }

                SettingsManager.Default.LanguageCode = autoLang;
                SettingsManager.Save();
            }

            string savedLang = SettingsManager.Default.LanguageCode;
            LocalizationManager.LoadLanguage(savedLang);

            InitializeComponent();

            LoadingText.Text = LocalizationManager.GetString("LoadScreen.LoadingText", "Завантаження ресурсів...");

            LoadLocalizedPhrases();

            LoadCustomPhrases();

            ApplyCustomSettings();

            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.Mica);
            VersionLauncherTXT.Text = versionLauncher + "-Beta";

            SettingsManager.Default.OfflineModLauncher = false;
            SettingsManager.Save();

            Loaded += LoadScreen_Loaded;
        }

        private void LoadLocalizedPhrases()
        {
            RandomPhrases.Clear();

            for (int i = 1; i <= 30; i++)
            {
                string phrase = LocalizationManager.GetString($"LoadScreen.RandomPhrases.Phrase{i}", "");
                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    RandomPhrases.Add(phrase);
                }
            }

            if (RandomPhrases.Count == 0)
            {
                RandomPhrases = new List<string>
                {
                    "Перша зелена травичка пробивається крізь землю...",
                    "Заварюємо свіжий фруктовий чай...",
                    "Homka саджає перші весняні квіти...",
                    "Deeplay фотографує цвітіння сакур на камеру 🌸...",
                    "Теплий весняний дощик стукає по вікнах...",
                    "Час ховати зимові куртки далеко в шафу...",
                    "Пахне свіжоскошеною травою та бузком...",
                    "Дерева вкриваються ніжним білим цвітом...",
                    "Данило готує мангал для перших шашликів...",
                    "WER_Clegendary шукає ідеальне місце для пікніка...",
                    "Природа нарешті прокидається за вікном...",
                    "Мружимося від яскравого весняного сонечка...",
                    "Пелюстки вишень кружляють у теплому повітрі...",
                    "Час виходити на довгі вечірні прогулянки...",
                    "Свіже весняне повітря надихає на пригоди...",
                    "Готуємось до теплих травневих вихідних...",
                    "Всі чекають на потепління (або вже садять картоплю)...",
                    "Тепло на вулиці, сонячно на душі..."
                };
            }
        }

        private void LoadCustomPhrases()
        {
            try
            {
                string phrasesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "loading_phrases.txt");
                if (File.Exists(phrasesPath))
                {
                    var lines = File.ReadAllLines(phrasesPath)
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .Where(x => !x.Trim().StartsWith("//"))
                                    .Where(x => !x.Trim().StartsWith("#"))
                                    .ToList();

                    if (lines.Count > 0)
                    {
                        RandomPhrases = lines;
                    }
                }
            }
            catch { }
        }

        private void ApplyCustomSettings()
        {
            string bgPath = SettingsManager.Default.LoadScreenBackground;
            if (!string.IsNullOrEmpty(bgPath) && File.Exists(bgPath))
            {
                try
                {
                    BG.Source = ImageHelper.LoadOptimizedImage(bgPath, 300);
                }
                catch { }
            }

            string colorHex = SettingsManager.Default.LoadScreenBarColor;
            if (!string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex);
                    LoadingProgressBar.Foreground = brush;
                }
                catch { }
            }
        }

        private async void LoadScreen_Loaded(object sender, RoutedEventArgs e)
        {
            await RunStartupProcessAsync();
        }

        private async Task RunStartupProcessAsync()
        {
            await DiscordController.Initialize(LocalizationManager.GetString("LoadScreen.DiscordRPC", "У віконці завантаження"));

            var animationTask = SimulateLoadingAnimationAsync();

            bool updateAvailable = false;

            try
            {
                updateAvailable = await CheckForUpdatesAsync();
            }
            catch (Exception)
            {
                var result = MascotMessageBox.Ask(
                    LocalizationManager.GetString("LoadScreen.Errors.UpdateOfflineAsk", "Ех, не вийшло перевірити оновлення. Спробувати офлайн?"),
                    LocalizationManager.GetString("LoadScreen.Errors.UpdateFailTitle", "Помилка оновлення"),
                    MascotEmotion.Sad
                );

                if (result == true)
                {
                    SettingsManager.Default.OfflineModLauncher = true;
                    SettingsManager.Save();
                    updateAvailable = false;
                }
                else
                {
                    this.Close();
                    return;
                }
            }

            if (updateAvailable)
            {
                UpdaterWindow updater = new UpdaterWindow();
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
                    Application.Current.Shutdown();
                    return;
                }

                await animationTask;
                OpenMainWindow();
            }
            catch (Exception ex)
            {
                string errorDetails = $"Помилка: {ex.Message}\n\n" +
                                      $"Де сталося: {ex.TargetSite}\n\n" +
                                      $"Стек (для розробника):\n{ex.StackTrace}";

                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "crash-report.txt");
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.WriteAllText(logPath, errorDetails);
                }
                catch { }

                string crashFormat = LocalizationManager.GetString("LoadScreen.Errors.CriticalCrashMessage", "Ой! Критична помилка...\nФайл {0}\nКоротко: {1}");
                string translatedCrashMsg = string.Format(crashFormat, logPath, ex.Message);

                MascotMessageBox.Show(
                    translatedCrashMsg,
                    LocalizationManager.GetString("LoadScreen.Errors.CriticalCrashTitle", "Критичний збій"),
                    MascotEmotion.Dead);
                this.Close();
            }
        }

        private async Task<bool> CheckForUpdatesAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CL-Launcher");
                client.Timeout = TimeSpan.FromSeconds(5);

                string json = await client.GetStringAsync(Secrets.UpdateUrlCheckLoadScreen);

                var info = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (info != null && !string.IsNullOrEmpty(info.Version))
                {
                    string remoteVerStr = info.Version.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase);
                    string localVerStr = versionLauncher.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase);

                    if (Version.TryParse(remoteVerStr, out Version vRemote) &&
                        Version.TryParse(localVerStr, out Version vLocal))
                    {
                        return vRemote > vLocal;
                    }

                    return remoteVerStr != localVerStr;
                }
            }
            return false;
        }

        private async Task SimulateLoadingAnimationAsync()
        {
            LoadingProgressBar.Value = 0;
            if (RandomPhrases.Count > 0)
                RandomPhraseText.Text = RandomPhrases[_random.Next(RandomPhrases.Count)];

            for (int i = 0; i <= 100; i++)
            {
                if (!this.IsVisible) return;

                await Task.Delay(_random.Next(30, 70));

                DoubleAnimation progressAnimation = new DoubleAnimation
                {
                    From = LoadingProgressBar.Value,
                    To = i,
                    Duration = TimeSpan.FromMilliseconds(100)
                };
                LoadingProgressBar.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);

                if (i % 20 == 0 && i > 0 && RandomPhrases.Count > 0)
                {
                    string randomPhrase = RandomPhrases[_random.Next(RandomPhrases.Count)];
                    RandomPhraseText.Text = randomPhrase;
                }
            }
        }

        private void OpenMainWindow()
        {
            DiscordController.Deinitialize();
            var mainWindow = new CL_Main_();
            mainWindow.Show();
            this.Close();
        }

        private async Task<bool> CheckEulaAsync()
        {
            var eulaConfig = await EulaService.GetEulaAsync();
            bool showEula = false;

            if (eulaConfig != null)
            {
                if (EulaService.IsEulaOutdated(eulaConfig.LastUpdated))
                {
                    showEula = true;
                }
            }
            else
            {
                if (SettingsManager.Default.EulaAcceptedDate == DateTime.MinValue)
                {
                    showEula = true;
                }
            }

            if (showEula)
            {
                this.Visibility = Visibility.Hidden;
                EulaWindow eulaWin = new EulaWindow(eulaConfig);
                bool? result = eulaWin.ShowDialog();
                this.Visibility = Visibility.Visible;
                return result == true;
            }

            return true;
        }
    }

    public class UpdateInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("url")]
        public string UrlDefault { get; set; } = "";

        [JsonPropertyName("url_x86")]
        public string UrlX86 { get; set; } = "";
    }
}