using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using MCQuery;
using Microsoft.Win32;
using MojangAPI;
using MojangAPI.Model;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Optifine.Installer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WpfAnimatedGif;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using File = System.IO.File;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace CL_CLegendary_Launcher_
{
    public enum AccountType
    {
        Microsoft,
        LittleSkin,
        Offline
    }
    //public class LanguageItem
    //{
    //    public string Name { get; set; } 
    //    public string Code { get; set; } 
    //}
    public class ProfileItem
    {
        public string NameAccount { get; set; }
        public string UUID { get; set; }
        public string AccessToken { get; set; }
        public string ImageUrl { get; set; }
        public int Index { get; set; }
        public AccountType TypeAccount { get; set; }
    }
    public class ModFile
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("primary")]
        public bool Primary { get; set; }
    }
    public class ModVersion
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version_number")]
        public string VersionNumber { get; set; }

        [JsonProperty("game_versions")]
        public List<string> GameVersions { get; set; } = new();

        [JsonProperty("loaders")]
        public List<string> Loaders { get; set; } = new();

        [JsonProperty("files")]
        public List<ModFile> Files { get; set; } = new();
    }
    public class NewsItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
    }
    public class ModpackInfo
    {
    public string Name { get; set; }
    public string TypeSite { get; set; }
    public string MinecraftVersion { get; set; }
    public string LoaderVersion { get; set; }
    public string LoaderType { get; set; }
    public string Path { get; set; }
    public string PathJson { get; set; }
    public string UrlImage { get; set; }
        public bool IsConsoleLogOpened { get; set; } = false;
        public int OPack { get; set; } = 4096;
        public int Wdith { get; set; } = 600;
        public int Height { get; set; } = 800;
        public bool EnterInServer { get; set; } = false;
        public string ServerIP { get; set; } = "Назва сервера";
    }
    public class VersionLogItem
    {
        public string VersionId { get; set; }
        public string IconPath { get; set; } 
        public string VersionType { get; set; }
        public override string ToString()
        {
            return VersionId;
        }
    }
    public partial class CL_Main_ : FluentWindow
    {
        //Вибор версії і чи встановлюємо і граємо
        byte VersionSelect = 0;
        public bool InstallVersionOnPlay = false;
        // Ліцензія та акаунти
        public AccountType selectAccountNow;
        bool MicosoftAccount = false;
        JELoginHandler loginHandler;
        public MSession session;
        // Список серверів
        public List<string> donateLink = new List<string>();
        public List<string> siteLink = new List<string>();
        public List<string> discordLink = new List<string>();
        private bool isSliderDragging = false;
        // Список модів
        string VersionType = "Fabric";
        string SiteMods = "Modrinth";
        string ModType = "Collection";
        byte selectmodificed = 0;
        // Сервери 
        public int loadedCount = 0;
        public int serverCount = 0;
        // ОП
        private double previousSliderValue = 2048;
        // Завантаження
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        // Логіка роботи запускача
        private readonly ScreenshotService _screenshotService;
        public readonly ServerListService _serverListService;
        protected readonly ProfileManagerService _profileManagerService;
        protected readonly GameSessionManager _gameSessionManager;
        protected readonly GameLaunchService _gameLaunchService;
        protected readonly LastActionService _lastActionService;
        private readonly ModDownloadService _modDownloadService;
        private List<ModVersionInfo> _currentModVersions;
        public readonly ModpackService _modpackService;
        private List<InstalledModpack> allInstalledModpacks = new List<InstalledModpack>();
        private readonly ThemeService _themeService;
        private readonly DragDropService _dragDropService;
        private readonly LauncherSettingsService _launcherSettingsService;
        private readonly LauncherNavigationService _navigationService;

        public CL_Main_()
        {
            loginHandler = JELoginHandlerBuilder.BuildDefault();
            InitializeComponent();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            //string langCode = Settings1.Default.LanguageCode;
            //if (string.IsNullOrEmpty(langCode)) langCode = "uk_UA";

            //Lang.Instance.LoadAsync(langCode).Wait();

            ApplicationThemeManager.Apply(this);
            _themeService = new ThemeService(this);

            _navigationService = new LauncherNavigationService(this);
            _launcherSettingsService = new LauncherSettingsService(this);
            _profileManagerService = new ProfileManagerService();
            _gameSessionManager = new GameSessionManager();
            _serverListService = new ServerListService(this);
            _lastActionService = new LastActionService(this);
            _gameLaunchService = new GameLaunchService(this, _gameSessionManager, _lastActionService);
            _modDownloadService = new ModDownloadService(this);
            _modpackService = new ModpackService(this, _gameSessionManager, _gameLaunchService);
            _screenshotService = new ScreenshotService();
            _dragDropService = new DragDropService(this);

            _serverListService.InitializeServersAsync(false, null);
            _lastActionService.LoadLastActionsFromJsonAsync();

            LoadChangeLogMinecraft();
            InitToggles();

            _launcherSettingsService.Initialize();
            _dragDropService.Initialize();
            _themeService.InitializeTheme();

            LoadProfilesAsync();
            DiscordController.Initialize("В головному вікні");
            _slideTimer = new DispatcherTimer();
            _slideTimer.Interval = TimeSpan.FromSeconds(10);
            _slideTimer.Tick += (s, e) => NextIndex();
            _slideTimer.Start();
        }
        private void LoadCustomSettings()
        {
            string savedColor = Settings1.Default.LoadScreenBarColor;

            if (string.IsNullOrEmpty(savedColor)) savedColor = "#00BEFF";

            try
            {
                LoadScreenColorButton.Content = _themeService.CreateColorButtonContent(savedColor);
            }
            catch
            {
                LoadScreenColorButton.Content = "#Помилка";
            }
        }
        private string FormatNumber(long num)
        {
            if (num >= 1_000_000)
                return (num / 1_000_000D).ToString("0.#") + "m";

            if (num >= 1_000)
                return (num / 1_000D).ToString("0.#") + "k";

            return num.ToString();
        }

        public string FormatNumber(string numStr)
        {
            if (string.IsNullOrEmpty(numStr)) return "0";

            string cleanStr = numStr
                .Replace(" ", "")  
                .Replace(",", "")   
                .Replace(".", "")
                .Trim();

            if (long.TryParse(cleanStr, out long result))
            {
                return FormatNumber(result);
            }

            return numStr;
        }
        public async Task HideAllPages()
        {
            var allPages = new List<FrameworkElement>
            {
        SelectGirdAccount, ScrollSetting, SettingPanelMinecraft, PanelManegerAccount,
        ServerName, ListModsGird, ListModsBuild, GalleryContainer,
        GirdPanelFooter, GirdNews, ListNews, GirdTXTNews
            };

            bool animationStarted = false;

            foreach (var page in allPages)
            {
                if (page.Visibility == Visibility.Visible)
                {
                    AnimationService.AnimatePageTransitionExit(page);
                    animationStarted = true;
                }
            }

            LoadingMessage.Visibility = Visibility.Hidden;

            ListNews.Items?.Clear();
            TextNews.Text = null;
            ScreenshotsList.Items?.Clear();
            ServerList.Items?.Clear();
            DescriptionServer.Text = null;
            ModsDowloadList1.Items?.Clear();

            if (animationStarted)
            {
                await Task.Delay(300);
            }
        }
        public async void LoadProfilesAsync() 
        {
            List<ProfileItem> profiles;
            try
            {
                profiles = await Task.Run(() => _profileManagerService.LoadProfiles());
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой! Я намагався завантажити твої профілі, але файл пошкоджений.\n{ex.Message}",
                    "Помилка профілів",
                    MascotEmotion.Alert
                );
                _profileManagerService.SaveProfiles(new List<ProfileItem>());
                profiles = new List<ProfileItem>();
            }

            ListAccount.Items.Clear();

            if (profiles.Count == 0)
            {
                Settings1.Default.SelectIndexAccount = -1;
                Settings1.Default.Save();
                return;
            }

            for (int i = 0; i < profiles.Count; i++) 
            {
                var profile = profiles[i];
                byte currentIndex = (byte)i;

                var item = CreateProfileItem(profile, currentIndex);

                item.DeleteAccount.MouseDown += (s, e) =>
                {
                    if (MascotMessageBox.Ask("Справді видалити профіль?", "Видалення", MascotEmotion.Alert) == true)
                    {
                        DeleteProfile(profiles, profile, item);
                        NotificationService.ShowNotification("Успіх", "Профіль стерто.", SnackbarPresenter, 3);
                    }
                };

                item.ClickSelectAccount.MouseDown += async (s, e) =>
                {
                    await SelectProfile(profile, currentIndex);
                };

                ListAccount.Items.Add(item);
            }

            int savedIndex = Settings1.Default.SelectIndexAccount;

            if (savedIndex >= 0 && savedIndex < profiles.Count)
            {
                var savedProfile = profiles[savedIndex];
                await SelectProfile(savedProfile, (byte)savedIndex);
            }
            else
            {
                Settings1.Default.SelectIndexAccount = -1;
                Settings1.Default.Save();
            }
        }
        private ItemManegerProfile CreateProfileItem(ProfileItem profile, int index)
        {
            BitmapImage image = null;

            if (!string.IsNullOrEmpty(profile.ImageUrl))
            {
                try
                {
                    Uri uri;

                    if (profile.ImageUrl.StartsWith("http"))
                    {
                        uri = new Uri(profile.ImageUrl, UriKind.Absolute);
                    }
                    else
                    {
                        uri = new Uri(profile.ImageUrl, UriKind.RelativeOrAbsolute);
                    }

                    image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = uri;
                    image.CacheOption = BitmapCacheOption.OnLoad; 
                    image.EndInit();
                    image.Freeze(); 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження картинки: {ex.Message}");
                    image = null;
                }
            }

            if (image == null)
            {
                image = new BitmapImage(new Uri("pack://application:,,,/Assets/big-steve-face-2002298922 2.png"));
            }

            return new ItemManegerProfile
            {
                NameAccount2 = profile.NameAccount,
                UUID = profile.UUID,
                AccessToken = profile.AccessToken,
                ImageUrl = profile.ImageUrl,
                index = index,
                TypeAccount = (ItemManegerProfile.AccountType)profile.TypeAccount,
                IconAccountType = { Source = image },
                NameAccount = { Text = profile.NameAccount }
            };
        }
        private void DeleteProfile(List<ProfileItem> profiles, ProfileItem profile, ItemManegerProfile item)
        {
            bool isDeletingCurrentAccount = (Settings1.Default.SelectIndexAccount == item.index);

            profiles.Remove(profile);
            _profileManagerService.SaveProfiles(profiles);

            ListAccount.Items.Remove(item);

            if (Settings1.Default.SelectIndexAccount > item.index)
            {
                Settings1.Default.SelectIndexAccount--;
                Settings1.Default.Save();
            }

            if (isDeletingCurrentAccount)
            {
                MicosoftAccount = false;
                selectAccountNow = AccountType.Offline;

                Settings1.Default.SelectIndexAccount = -1;
                Settings1.Default.MicrosoftAccount = false;
                Settings1.Default.Save();

                NameNik.Text = "Відсутній акаунт";
                IconAccount.Source = null;

                if (item.TypeAccount == ItemManegerProfile.AccountType.Microsoft) { loginHandler.Signout(); loginHandler.SignoutWithBrowser(); }
            }

        }

        private async Task SelectProfile(ProfileItem profile, byte index)
        {
            try
            {
                session = await _profileManagerService.CreateSessionForProfileAsync(profile, loginHandler);

                MicosoftAccount = (profile.TypeAccount == AccountType.Microsoft);
                selectAccountNow = profile.TypeAccount;
                Settings1.Default.SelectIndexAccount = index;
                Settings1.Default.Save();

                NameNik.Text = profile.NameAccount;
                IconAccount.Source = new BitmapImage(new Uri(profile.ImageUrl));
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"Ой, халепа! Не пускає в акаунт. Може, пароль не той, або сервери взяли вихідний? Ось деталі: {ex.Message}",default,MascotEmotion.Alert);
                Settings1.Default.SelectIndexAccount = -1;
                Settings1.Default.Save();
                NameNik.Text = "Помилка входу";
                IconAccount.Source = null;
            }
        }


        private async void CreateAccount_Offline_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            string uuidValue;
            try
            {
                Mojang mojang = new Mojang(new HttpClient());
                PlayerUUID uuid = await mojang.GetUUID($"{NameNikManeger.Text}");
                uuidValue = uuid.UUID;
            }
            catch (Exception)
            {
                uuidValue = Guid.NewGuid().ToString();
            }

            ProfileItem profileItem = new ProfileItem
            {
                NameAccount = NameNikManeger.Text,
                UUID = uuidValue,
                AccessToken = "-",
                ImageUrl = $"pack://application:,,,/Assets/big-steve-face-2002298922 2.png".ToString(),
                TypeAccount = AccountType.Offline
            };

            if (_profileManagerService.SaveProfile(profileItem))
            {
                NameNikManeger.Text = null;
                AnimationService.FadeOut(GirdOfflineMode, 0.2); AnimationService.FadeOut(GirdOnlineMode, 0.2); AnimationService.FadeOut(GirdFormAccountAdd, 0.2); AnimationService.FadeOut(GirdSelectAccountType, 0.2);
                LoadProfilesAsync();
            }
        }
        private async void MicrosoftLoginButton_Click(object sender, MouseButtonEventArgs e)
        {
            Click();
            try
            { 
                var session = await loginHandler.AuthenticateInteractively();
              
                var mojangApi = new Mojang();
                bool ownsGame = await mojangApi.CheckGameOwnership(session.AccessToken);
                if (!ownsGame)
                {
                    MascotMessageBox.Show(
                        $"Привіт, {session.Username}!\n" +
                        "На жаль, я не знайшла купленої ліцензії Minecraft Java Edition на цьому акаунті.",
                        "Ліцензія відсутня",
                        MascotEmotion.Sad);
                    return;
                }
                ProfileItem profileItem = new ProfileItem
                {
                    NameAccount = session.Username,
                    UUID = session.UUID,
                    ImageUrl = $"https://mc-heads.net/avatar/{session.UUID}",
                    AccessToken = session.AccessToken,
                    TypeAccount = AccountType.Microsoft
                };

                if (_profileManagerService.SaveProfile(profileItem))
                {
                    Settings1.Default.MicrosoftAccount = true;
                    Settings1.Default.Save();
                    LoadProfilesAsync();

                    MascotMessageBox.Show(
                        $"Ура! Ліцензію перевірено!\n" +
                        $"Ласкаво просимо, {session.Username}!",
                        "Вхід успішний",
                        MascotEmotion.Happy);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой, щось пішло не так.\nДеталі: {ex.Message}",
                    "Помилка входу",
                    MascotEmotion.Sad);

                Settings1.Default.MicrosoftAccount = false;
                Settings1.Default.Save();
            }
            finally
            {
                CloseAccountSelectionUI();
            }
        }
        private void CloseAccountSelectionUI()
        {
            AnimationService.FadeOut(GirdOfflineMode, 0.2);
            AnimationService.FadeOut(GirdOnlineMode, 0.2);
            AnimationService.FadeOut(GirdFormAccountAdd, 0.2);
            AnimationService.FadeOut(GirdSelectAccountType, 0.2);
        }
        private async void LoginAccountLittleSkin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                session = await _profileManagerService.LoginLittleSkinAsync(Login_LittleSkin.Text, PasswordLittleSkin.Text);

                ProfileItem profileItem = new ProfileItem
                {
                    NameAccount = session.Username,
                    UUID = session.UUID,
                    AccessToken = session.AccessToken,
                    ImageUrl = $"pack://application:,,,/Assets/LittleSkinAccount.png",
                    TypeAccount = AccountType.LittleSkin
                };

                if (_profileManagerService.SaveProfile(profileItem))
                {
                    Login_LittleSkin.Text = null; PasswordLittleSkin.Text = null;
                    AnimationService.FadeOut(GirdLittleSkinMode, 0.2); AnimationService.FadeOut(GirdFormAccountAdd, 0.2); AnimationService.FadeOut(GirdSelectAccountType, 0.2);
                    LoadProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ех, біда! Не вдалося підключитися до LittleSkin.\n" +
                    $"Ти впевнений, що ввів правильний логін та пароль?\n\n" +
                    $"Ось що пішло не так: {ex.Message}",
                    "Помилка LittleSkin",
                    MascotEmotion.Sad
                );
            }
        }
        private async void LoadChangeLogMinecraft()
        {
            if (Settings1.Default.OfflineModLauncher) { return; }

            try
            {
                string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"CL-Legendary-Launcher/{Assembly.GetExecutingAssembly().GetName().Version}");

                    string json = await httpClient.GetStringAsync(url);

                    JObject manifest = JObject.Parse(json);
                    JArray versions = (JArray)manifest["versions"];

                    VersionMinecraftChangeLog.Items.Clear();

                    string iconBase = "pack://application:,,,/Assets/";

                    foreach (var version in versions)
                    {
                        string id = version["id"]?.ToString();
                        string type = version["type"]?.ToString();

                        string iconPath = iconBase + "VanilaVersion.png";

                        switch (type)
                        {
                            case "snapshot":
                                iconPath = iconBase + "DirtIconSnapshot.png";
                                break;
                            case "old_beta":
                                iconPath = iconBase + "StoneIconBeta.png";
                                break;
                            case "old_alpha":
                                iconPath = iconBase + "CobblestoneIconAlpha.png";
                                break;
                            case "release":
                            default:
                                iconPath = iconBase + "VanilaVersion.png";
                                break;
                        }

                        VersionMinecraftChangeLog.Items.Add(new VersionLogItem
                        {
                            VersionId = id,
                            IconPath = iconPath,
                            VersionType = type
                        });

                        if (id == "a1.0.4") break;
                    }

                    if (VersionMinecraftChangeLog.Items.Count > 0)
                        VersionMinecraftChangeLog.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Вибач, але я не змогла дістати свіжий випуск новин (Changelog).\n" +
                    $"Можливо, інтернет зник або сервери Mojang втомилися відповідати.\n\n" +
                    $"Ось що трапилося: {ex.Message}",
                    "Немає новин",
                    MascotEmotion.Sad
                );
            }
        }
        private void LauncherFloderButton_Click(object sender, RoutedEventArgs e)
        {
            _launcherSettingsService.HandleChangePathClick();
        }

        private void ResetPathMinecraftLauncher_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _launcherSettingsService.HandleResetPathClick();
        }

        private void ResetOP_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _launcherSettingsService.HandleResetOpClick();
        }

        private void ResetResolution_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _launcherSettingsService.HandleResetResolutionClick();
        }
        private void InitToggles()
        {
            DebugToggle.IsChecked  = Settings1.Default.EnableLog;
            CloseLauncherToggle.IsChecked = Settings1.Default.CloseLaucnher;
            ModDepsToggle.IsChecked = Settings1.Default.ModDep;
            GlassEffectToggle.IsChecked = Settings1.Default.DisableGlassEffect;
            FullScreenToggle.IsChecked = Settings1.Default.FullScreen;
        }
        async void AddVersion()
        {
            try
            {
                VersionList.Items?.Clear();

                string searchText = SearchSystemTXT1.Text.ToLower().Trim();

                var path = new MinecraftPath(Settings1.Default.PathLacunher);
                var launcher = new MinecraftLauncher(path);

                var versions = await launcher.GetAllVersionsAsync();

                string pattern = string.IsNullOrEmpty(searchText) ? ".*" : searchText.Replace("*", ".*");
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

                var addedVersions = new HashSet<string>();

                var suggestionList = new List<string>();

                foreach (var ver in versions)
                {
                    bool isMatch = false;

                    if (ver.Type == "local" && regex.IsMatch(ver.Name) && VersionSelect == 2)
                    {
                        isMatch = true;
                        VersionListVanila.Items.Add(ver.Name);
                    }
                    else if (ver.Type == "release" && regex.IsMatch(ver.Name) && Relesed.IsChecked == true && VersionSelect != 2)
                    {
                        isMatch = true;
                        if (VersionSelect == 2) { VersionListVanila.Items.Add(ver.Name); }
                    }
                    else if (ver.Type == "snapshot" && regex.IsMatch(ver.Name) && Snapshots.IsChecked == true && VersionSelect != 2)
                    {
                        isMatch = true;
                    }
                    else if (ver.Type == "old_beta" && regex.IsMatch(ver.Name) && Beta.IsChecked == true && VersionSelect != 2)
                    {
                        isMatch = true;
                    }
                    else if (ver.Type == "old_alpha" && regex.IsMatch(ver.Name) && Alpha.IsChecked == true && VersionSelect != 2)
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        if (!addedVersions.Contains(ver.Name))
                        {
                            VersionList.Items.Add(ver.Name);

                            suggestionList.Add(ver.Name);

                            addedVersions.Add(ver.Name);
                        }
                    }
                }
                if (!string.IsNullOrEmpty(searchText))
                {
                    SearchSystemTXT1.ItemsSource = suggestionList;
                }
                else
                {
                    SearchSystemTXT1.ItemsSource = null;
                }

                if (VersionList.Items.Count == 0)
                {
                    NotificationService.ShowNotification(
                            "Упс...",
                            "Ця версія десь добряче сховалася! Сіел не може її знайти. Перевірте, чи правильно ви написали.",
                            SnackbarPresenter, 3);
                }
            }
            catch 
            {
                return;
            }
        }
        public async Task AddVersionOptifine()
        {
            try
            {
                VersionListVanila.Items.Clear();
                VersionListMod.Items.Clear();

                string searchText = SearchSystemTXT2.Text.ToLower().Trim();

                var path = new MinecraftPath(Settings1.Default.PathLacunher);
                var launcher = new MinecraftLauncher(path);

                var versions = await launcher.GetAllVersionsAsync();

                Regex regex = new Regex(string.IsNullOrEmpty(searchText) ? ".*" : Regex.Escape(searchText).Replace(@"\*", ".*"), RegexOptions.IgnoreCase);

                bool foundAny = false;

                var suggestionList = new List<string>();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var ver in versions)
                    {
                        if (ver.Name == "1.6.4") continue;

                        if (ver.Type == "release" && regex.IsMatch(ver.Name))
                        {
                            VersionListVanila.Items.Add(ver.Name);

                            suggestionList.Add(ver.Name);

                            foundAny = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        SearchSystemTXT2.ItemsSource = suggestionList;
                    }
                    else
                    {
                        SearchSystemTXT2.ItemsSource = null; 
                    }
                });

                if (!foundAny)
                {
                    NotificationService.ShowNotification(
                        "Тут пусто...",
                        "Сіел не знайшла жодної версії OptiFine за цим запитом. Можливо, спробуємо іншу назву?",
                        SnackbarPresenter, 3
                    );
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой! Я не змогла завантажити список версій OptiFine.\n" +
                    $"Можливо, їхній сайт тимчасово недоступний або щось блокує з'єднання.\n\n" +
                    $"Технічна деталь: {ex.Message}",
                    "Помилка OptiFine",
                    MascotEmotion.Sad
                );
            }
        }
        private async void AddVersionModeVersion()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            try
            {
                Application.Current.Dispatcher.Invoke(() => VersionListMod.Items.Clear());

                var selectedVersion = VersionListVanila.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedVersion)) return;

                if (VersionSelect == 5)
                {
                    try
                    {
                        var optifineInstaller = new OptifineInstaller(new HttpClient());
                        var versions = await optifineInstaller.GetOptifineVersionsAsync();

                        if (token.IsCancellationRequested) return;

                        var filtered = versions
                            .Where(v => v.MinecraftVersion == selectedVersion)
                            .ToList();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            VersionListMod.Items.Clear();

                            foreach (var v in filtered)
                            {
                                string name = $"{v.Version}";
                                VersionListMod.Items.Add(name);
                            }

                            if (VersionListMod.Items.Count == 0)
                            {
                                VersionListMod.Items.Add("Немає доступних версій OptiFine");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            VersionListMod.Items.Clear();
                            VersionListMod.Items.Add($"Помилка: {ex.Message}");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой! Я не змогла отримати список версій для цього модифікатора (OptiFine/Forge тощо).\n" +
                    $"Схоже, сервер розробників не відповідає, і я не знаю, яку версію качати.\n\n" +
                    $"Технічна деталь: {ex.Message}",
                    "Помилка версій",
                    MascotEmotion.Sad
                );
            }
        }
        private async void DownloadVersionOptifine(string effectiveVersion = null, string effectiveVersionMod = null)
        {
            string mcVersion = !string.IsNullOrEmpty(effectiveVersion)
                ? effectiveVersion
                : VersionListVanila.SelectedItem?.ToString();

            string optifineVersion = !string.IsNullOrEmpty(effectiveVersionMod)
                ? effectiveVersionMod
                : VersionListMod.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(mcVersion) || string.IsNullOrEmpty(optifineVersion))
            {
                MascotMessageBox.Show(
                    "Секундочку! А що саме ми запускаємо?\n" +
                    "Ти забув обрати версію Minecraft та OptiFine у списках.\n\n" +
                    "Тицьни на потрібні версії, і погнали!",
                    "Не обрано версію",
                    MascotEmotion.Alert
                );
                return;
            }

            Settings1.Default.LastSelectedVersion = mcVersion;
            Settings1.Default.LastSelectedModVersion = optifineVersion;
            Settings1.Default.LastSelectedType = 5; 
            Settings1.Default.Save();

            await _gameLaunchService.LaunchGameAsync(LoaderType.Optifine, mcVersion, optifineVersion);
        }
        async Task DowloadVanila(string version, string server, int? serverport, string username)
        {
            string effectiveVersion = version ?? VersionRelesedVanilLast.Content.ToString();

            Settings1.Default.LastSelectedModVersion = null;
            Settings1.Default.LastSelectedVersion = effectiveVersion;
            Settings1.Default.LastSelectedType = 1; 
            Settings1.Default.Save(); 

            await _gameLaunchService.LaunchGameAsync(LoaderType.Vanilla, effectiveVersion, null, server, serverport);
        }
        async Task PlayLocal(string version, string server, int? serverport, string username)
        {
            string effectiveVersion = version ?? CustomVersion_Modificetion.Content.ToString();

            Settings1.Default.LastSelectedModVersion = null;
            Settings1.Default.LastSelectedVersion = effectiveVersion;
            Settings1.Default.LastSelectedType = 2; 
            Settings1.Default.Save();

            await _gameLaunchService.LaunchGameAsync(LoaderType.Custom_Local, effectiveVersion, null, server, serverport);
        }
        private void CL_CLegendary_Launcher__Loaded_1(object sender, RoutedEventArgs e)
        {
            if (!Settings1.Default.TutorialComplete) { AnimationService.AnimatePageTransition(TutorialGrid); }
            else { TutorialGrid.Visibility = Visibility.Collapsed;  }

            if (Settings1.Default.width != 0 && Settings1.Default.height != 0)
            {
                Width.Text = Settings1.Default.width.ToString();
                Height.Text = Settings1.Default.height.ToString();
                MincraftWindowSize.Content = $"{Settings1.Default.width}x{Settings1.Default.height}";
            }
            else
            {
                Settings1.Default.width = 800;
                Settings1.Default.height = 600;
                Settings1.Default.Save();
                Width.Text = "800";
                Height.Text = "600";
                MincraftWindowSize.Content = "800x600";
            }
            int savedType = Settings1.Default.LastSelectedType;
            string savedVer = Settings1.Default.LastSelectedVersion;
            string savedModVer = Settings1.Default.LastSelectedModVersion;

            if (savedType != 0 && !string.IsNullOrEmpty(savedVer))
            {
                VersionSelect = (byte)savedType;

                if (savedType == 5 && !string.IsNullOrEmpty(savedModVer))
                {
                    IconSelectVersion.Source = IconSelectVersion_Optifine.Source;
                    PlayTXT.Content = $"ГРАТИ В ({savedModVer})";
                }
                else if (savedType == 2)
                {
                    IconSelectVersion.Source = IconSelectVersion_Custom.Source;
                    PlayTXT.Content = $"ГРАТИ В ({savedVer})";
                }
                else if (savedType == 1)
                {
                    IconSelectVersion.Source = IconSelectVersion_Копировать.Source;
                    PlayTXT.Content = $"ГРАТИ В ({savedVer})";
                }
            }
            else
            {
                PlayTXT.Content = "ОБЕРІТЬ ВЕРСІЮ";
            }

            LoadCustomSettings();
            this.Dispatcher.Invoke(() =>
            {
                MemoryCleaner.FlushMemory();
            });
            //LoadLanguagesToComboBox();
        }
        public void Click()
        {
            Task.Run(() =>
            {
                var Click = new NAudio.Vorbis.VorbisWaveReader(Resource2.click);
                using (var waveOut = new NAudio.Wave.WaveOutEvent())
                {
                    waveOut.Volume = 0.1f;
                    waveOut.Init(Click);
                    waveOut.Play();
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            });
        }
        public async Task<PartherItem> CreateServerPartherItemAsync(Dictionary<string, object> serverData)
        {
            var serverName = serverData.ContainsKey("name") ? serverData["name"].ToString() : "Unknown Server";
            int port = serverData.ContainsKey("port") ? Convert.ToInt32(serverData["port"]) : 25565;
            string descriptionServer = serverData.ContainsKey("description") ? serverData["description"].ToString() : "Опис відсутній.";
            string ip = serverData.ContainsKey("ip") ? serverData["ip"].ToString() : "";

            int priority = 0;
            if (serverData.TryGetValue("priority", out object priorityVal))
                int.TryParse(priorityVal?.ToString(), out priority);
            else if (serverData.TryGetValue("partner", out object partnerVal) && bool.Parse(partnerVal.ToString()))
                priority = 10; 

            bool isNeon = serverData.ContainsKey("neonEffect") && bool.Parse(serverData["neonEffect"].ToString());
            string neonColorHex = serverData.ContainsKey("borderColor") ? serverData["borderColor"].ToString() : "#FFFFFF";             
            string textColorHex = serverData.ContainsKey("textColor") ? serverData["textColor"].ToString() : null;

            var item = new PartherItem
            {
                _Title = serverName,
                _description = $"Тип: {serverData["type"]}\nВерсія: {serverData["version"]}",
                IPServerTXT = { Content = ip },
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            item.PlayServerTXT1.MouseDown += async (s, e) =>
            {
                DowloadVanila(serverData["version"].ToString(), ip, port, NameNik.Text);
                await AddLastActionAsync(serverName, serverData["version"].ToString(), ip, port);
            };

            item.OpenInfoServerTXT.MouseDown += (s, e) =>
            {
                AnimationService.AnimateBorderObject(-120, 0, FonBackIconServerList, true);
                AnimationService.FadeIn(PanelInfoServer, 0.3);
                AnimationService.FadeOut(ServerName, 0.2);

                IPServerTXT.Content = item.IPServerTXT.Content;
                VersionTXT.Content = $@"{serverData["version"]}";
                PortTXT.Content = $"{port}";
                OnlinePlayerTXT.Content = item.OnlinePlayerTXT.Content;
                TitleMain1.Content = item.TitleMain1.Content;
                DescriptionServer.Text = descriptionServer;

                if (priority > 100)
                {
                    var source = ImageBehavior.GetAnimatedSource(item.MainIcon3);
                    if (source != null) ImageBehavior.SetAnimatedSource(MainIcon3, source);
                    else MainIcon3.Source = item.MainIcon3.Source;
                }
                else
                {
                    MainIcon3.Source = item.MainIcon3.Source;
                }

                BG.Source = null;
                BG.Visibility = Visibility.Hidden;
                if (priority > 0 && serverData.TryGetValue("bgUrl", out object bgUrlValue) &&
                    Uri.TryCreate(bgUrlValue?.ToString(), UriKind.Absolute, out Uri bgUri))
                {
                    try
                    {
                        var bgBmp = new BitmapImage(bgUri);
                        BG.Source = bgBmp;
                        BG.Visibility = Visibility.Visible;
                    }
                    catch { BG.Visibility = Visibility.Hidden; }
                }
            };

            _ = Task.Run(() =>
            {
                try
                {
                    var serverStatus = new MCServer(ip, port);
                    var status = serverStatus.Status(500);
                    Dispatcher.Invoke(() => item.OnlinePlayerTXT.Content = $"{status.Players.Online}/{status.Players.Max}");
                }
                catch
                {
                    Dispatcher.Invoke(() => item.OnlinePlayerTXT.Content = "-");
                }
            });

            if (serverData.TryGetValue("logoUrl", out object logoValue) &&
                Uri.TryCreate(logoValue?.ToString(), UriKind.Absolute, out Uri logoUri))
            {
                try
                {
                    var imageSource = new BitmapImage();
                    imageSource.BeginInit();
                    imageSource.UriSource = logoUri;
                    imageSource.CacheOption = BitmapCacheOption.OnLoad;
                    imageSource.EndInit();

                    if (priority > 100)
                    {
                        ImageBehavior.SetAnimatedSource(item.MainIcon3, imageSource);
                    }
                    else
                    {
                        item.MainIcon3.Source = imageSource;
                    }
                }
                catch { }
            }

            if (priority > 0)
            {
                if (serverData.TryGetValue("bgUrl", out object bgUrlValue) &&
                    Uri.TryCreate(bgUrlValue?.ToString(), UriKind.Absolute, out Uri bgUri))
                {
                    try
                    {
                        var bgImage = new BitmapImage(bgUri);
                        item.BGParther.Background = new ImageBrush(bgImage) { Stretch = Stretch.UniformToFill };
                    }
                    catch { item.Background = new LinearGradientBrush(Colors.Gold, Colors.Orange, 45); }
                }
                else
                {
                    item.Background = new LinearGradientBrush(Colors.Gold, Colors.Orange, 45);
                }
            }
            else
            {
                item.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            }

            item.BorderThickness = new Thickness(0);
            item.BorderBrush = Brushes.Transparent;
            item.Margin = new Thickness(0);
            try
            {
                if (priority >= 100 && isNeon && !string.IsNullOrEmpty(neonColorHex))
                {
                    try
                    {
                        if (!neonColorHex.StartsWith("#")) neonColorHex = "#" + neonColorHex;
                        var glowColor = (Color)ColorConverter.ConvertFromString(neonColorHex);

                        item.ClipToBounds = true;

                        item.BGParther.Effect = null;

                        if (item.BGParther is Border border)
                        {
                            border.BorderThickness = new Thickness(2);
                            border.BorderBrush = new SolidColorBrush(glowColor);

                        }
                    }
                    catch { }
                }
                if (priority >= 20 && !string.IsNullOrEmpty(textColorHex))
                {
                    if (!textColorHex.StartsWith("#")) textColorHex = "#" + textColorHex;
                    var textColor = (Color)ColorConverter.ConvertFromString(textColorHex);
                    item.TitleMain1.Foreground = new SolidColorBrush(textColor);
                }
            }
            catch { }

            return item;
        }
        public async Task<MyItemsServer> CreateServerItemAsync(Dictionary<string, object> serverData)
        {
            var serverName = serverData.ContainsKey("name") ? serverData["name"].ToString() : "Unknown Server";
            int port = serverData.ContainsKey("port") ? Convert.ToInt32(serverData["port"]) : 25565;
            string descriptionServer = serverData.ContainsKey("description") ? serverData["description"].ToString() : "Опис відсутній.";

            bool partner = false;
            if (serverData.TryGetValue("partner", out object partnerValue))
                bool.TryParse(partnerValue?.ToString(), out partner);

            int priority = 0;
            if (serverData.TryGetValue("priority", out object priorityVal))
                int.TryParse(priorityVal?.ToString(), out priority);
            else if (partner)
                priority = 10;

            string borderColorHex = serverData.ContainsKey("borderColor") ? serverData["borderColor"].ToString() : "#FFFFFF";
            string textColorHex = serverData.ContainsKey("textColor") ? serverData["textColor"].ToString() : null;

            bool isNeon = false;
            if (serverData.TryGetValue("neonEffect", out object neonValue))
                bool.TryParse(neonValue?.ToString(), out isNeon);

            var item = new MyItemsServer
            {
                _Title = serverName,
                Description_ = $"Тип: {serverData["type"]}\nВерсія: {serverData["version"]}",
                IPServerTXT = { Content = serverData["ip"] },
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            item.PlayServerTXT1.MouseDown += async (s, e) =>
            {
                DowloadVanila($@"{serverData["version"]}", $@"{serverData["ip"]}", port, NameNik.Text);
                await AddLastActionAsync(serverName, $@"{serverData["version"]}", $@"{serverData["ip"]}", port);
            };

            item.OpenInfoServerTXT.MouseDown += (s, e) =>
            {
                AnimationService.AnimateBorderObject(-120, 0, FonBackIconServerList, true);
                AnimationService.AnimatePageTransition(PanelInfoServer);
                AnimationService.AnimatePageTransitionExit(ServerName);

                IPServerTXT.Content = item.IPServerTXT.Content;
                VersionTXT.Content = $@"{serverData["version"]}";
                PortTXT.Content = $"{port}";
                OnlinePlayerTXT.Content = item.OnlinePlayerTXT.Content;
                TitleMain1.Content = item.TitleMain1.Text;
                DescriptionServer.Text = descriptionServer;

                if (priority >= 100)
                {
                    var source = ImageBehavior.GetAnimatedSource(item.MainIcon3);
                    if (source != null) ImageBehavior.SetAnimatedSource(MainIcon3, source);
                    else MainIcon3.Source = item.MainIcon3.Source;
                }
                else
                {
                    MainIcon3.Source = item.MainIcon3.Source;
                }

                this.BG.Source = null;
                this.BG.Visibility = Visibility.Hidden;

                if (priority > 0 && serverData.TryGetValue("bgUrl", out object bgUrlValue) &&
                    Uri.TryCreate(bgUrlValue?.ToString(), UriKind.Absolute, out Uri bgUri))
                {
                    try
                    {
                        var bitmapImage = new BitmapImage(bgUri);
                        this.BG.Source = bitmapImage;
                        this.BG.Visibility = Visibility.Visible;
                    }
                    catch { this.BG.Visibility = Visibility.Hidden; }
                }
            };

            _ = Task.Run(() =>
            {
                try
                {
                    var serverStatus = new MCServer(serverData["ip"].ToString(), port);
                    var status = serverStatus.Status(500);
                    Dispatcher.Invoke(() => item.OnlinePlayerTXT.Content = $"{status.Players.Online}/{status.Players.Max}");
                }
                catch
                {
                    Dispatcher.Invoke(() => item.OnlinePlayerTXT.Content = "-");
                }
            });

            if (serverData.TryGetValue("logoUrl", out object logoValue) &&
                Uri.TryCreate(logoValue?.ToString(), UriKind.Absolute, out Uri logoUri))
            {
                try
                {
                    var imageSource = new BitmapImage();
                    imageSource.BeginInit();
                    imageSource.UriSource = logoUri;
                    imageSource.CacheOption = BitmapCacheOption.OnLoad;
                    imageSource.EndInit();

                    if (priority > 100)
                        ImageBehavior.SetAnimatedSource(item.MainIcon3, imageSource);
                    else
                        item.MainIcon3.Source = imageSource;
                }
                catch { }
            }

            Uri itemBgUri = null;
            bool hasItemBg = serverData.TryGetValue("bgUrl", out object itemBgVal) &&
                             Uri.TryCreate(itemBgVal?.ToString(), UriKind.Absolute, out itemBgUri);

            if (priority > 0)
            {
                if (hasItemBg && itemBgUri != null)
                {
                    try
                    {
                        var bgImage = new BitmapImage(itemBgUri);
                        item.BGParther.Background = new ImageBrush(bgImage) { Stretch = Stretch.UniformToFill };
                    }
                    catch { item.Background = new LinearGradientBrush(Colors.Gold, Colors.Orange, 45); }
                }
                else
                {
                    item.Background = new LinearGradientBrush(Colors.Gold, Colors.Orange, 45);
                }
            }
            else
            {
                item.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            }

            item.BorderThickness = new Thickness(0);
            item.BorderBrush = Brushes.Transparent;
            item.Effect = null;
            item.Margin = new Thickness(5);
            item.TitleMain1.ClearValue(Label.ForegroundProperty);

            if (priority > 0)
            {
                try
                {
                    if (priority >= 100 && !string.IsNullOrEmpty(borderColorHex))
                    {
                        try
                        {
                            if (!borderColorHex.StartsWith("#")) borderColorHex = "#" + borderColorHex;
                            var color = (Color)ColorConverter.ConvertFromString(borderColorHex);

                            if (isNeon)
                            {
                                item.ClipToBounds = false;

                                if (item.BGParther != null)
                                {
                                    item.BGParther.BorderBrush = new SolidColorBrush(color);
                                    item.BGParther.BorderThickness = new Thickness(1.5); 

                                    item.BGParther.Effect = new System.Windows.Media.Effects.DropShadowEffect
                                    {
                                        Color = color,
                                        Direction = 0,
                                        ShadowDepth = 0,
                                        BlurRadius = 15, 
                                        Opacity = 0.6
                                    };
                                }
                            }
                        }
                        catch { }
                    }

                    if (priority >= 20 && !string.IsNullOrEmpty(textColorHex))
                    {
                        if (!textColorHex.StartsWith("#")) textColorHex = "#" + textColorHex;
                        var textColor = (Color)ColorConverter.ConvertFromString(textColorHex);
                        item.TitleMain1.Foreground = new SolidColorBrush(textColor);
                    }
                }
                catch
                {
                    item.Effect = null;
                    item.Margin = new Thickness(5);
                }
            }

            return item;
        }
        public async Task AddLastActionAsync(Dictionary<string, string> action)
        {
            await _lastActionService.AddLastActionAsync(action);
        }

        private Task AddLastActionAsync(string name, string version, string ip, int port)
        {
            var action = new Dictionary<string, string>
            {
                ["type"] = "server",
                ["name"] = name ?? "",
                ["version"] = version ?? "",
                ["ip"] = ip ?? "",
                ["port"] = port.ToString()
            };

            return AddLastActionAsync(action); 
        }
        public void AddActionToList(Dictionary<string, string> action)
        {
            try
            {
                string type = action.ContainsKey("type") ? action["type"] : "other";
                string name = action.ContainsKey("name") ? action["name"] : "Unknown";
                string version = action.ContainsKey("version") ? action["version"] : "";
                string ip = action.ContainsKey("ip") ? action["ip"] : "";
                string port = action.ContainsKey("port") ? action["port"] : "";
                string loader = action.ContainsKey("loader") ? action["loader"] : "vanilla";
                string loaderVersion = action.ContainsKey("loaderVersion") ? action["loaderVersion"] : "";

                string displayText;

                if (type == "server")
                {
                    displayText = $"{name} : {version} ({loader}{(string.IsNullOrEmpty(loaderVersion) ? "" : " " + loaderVersion)}) : Server";
                }
                else if (type == "version")
                {
                    displayText = $"{name} : {version} ({loader}{(string.IsNullOrEmpty(loaderVersion) ? "" : " " + loaderVersion)}) : Version";
                }
                else
                {
                    displayText = $"{name} : {type}";
                }

                var textBlock = new TextBlock
                {
                    Text = displayText,
                    Margin = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap
                };

                textBlock.MouseLeftButtonDown += (s, e) =>
                {
                    if (type == "server")
                    {
                        int.TryParse(port, out int parsedPort);
                        DowloadVanila(version, ip, parsedPort, NameNik.Text);
                    }
                    else if (type == "version")
                    {
                        if (loader == "optifine") { DownloadVersionOptifine(version, loaderVersion); }
                        if (loader == "vanilla") { DowloadVanila(version, null, null, NameNik.Text); }
                    }
                };

                ServerMonitoring.Items.Add(textBlock);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой! У мене стався якийсь збій у пам'яті.\n" +
                    $"Я не можу відобразити список твоїх останніх дій (історію).\n\n" +
                    $"Ось чому це сталося: {ex.Message}",
                    "Збій історії",
                    MascotEmotion.Confused
                );
            }
        }
        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 10;
        private CancellationTokenSource _searchCts;

        public async Task UpdateModsMinecraftAsync()
        {
            try
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;

                await DiscordController.UpdatePresence("Дивиться моди");

                ModsDowloadList.Visibility = Visibility.Visible;
                if (PaginationPanel != null) PaginationPanel.Visibility = Visibility.Visible;

                ModsDowloadList.Items?.Clear();
                VersionMods.Items?.Clear();

                string searchText = SearchSystemModsTXT.Text.Trim();
                int offset = _currentPage * ITEMS_PER_PAGE;

                var results = await _modDownloadService.SearchModsAsync(
                    searchText,
                    SiteMods,
                    VersionType,
                    selectmodificed,
                    offset
                );

                if (token.IsCancellationRequested) return;

                if (PageNumberText != null)
                    PageNumberText.Text = (_currentPage + 1).ToString();

                if (PrevPageBtn != null)
                    PrevPageBtn.IsEnabled = _currentPage > 0;

                if (NextPageBtn != null)
                {
                    NextPageBtn.IsEnabled = results != null && results.Count >= ITEMS_PER_PAGE;
                }

                if (results == null || !results.Any())
                {
                    ModsDowloadList.Items.Add(new TextBlock
                    {
                        Text = "Пошук не дав результатів (або кінець списку).",
                        Foreground = Brushes.White,
                        Margin = new Thickness(10)
                    });
                    if (NextPageBtn != null) NextPageBtn.IsEnabled = false;
                    return;
                }

                foreach (var mod in results)
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        var item = CreateItemJarFromSearchResult(mod);
                        ModsDowloadList.Items.Add(item);
                        AnimationService.AnimatePageTransition(item);
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"Помилка створення картки: {innerEx.Message}");
                    }

                    await Task.Delay(30, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"Помилка пошуку: {ex.Message}", "Помилка", MascotEmotion.Sad);
            }
        }
        private async void PrevPageBtn_Click(object sender, RoutedEventArgs e)
        {
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            if (_currentPage > 0)
            {
                _currentPage--;
                await UpdateModsMinecraftAsync();
            }
        }

        private async void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            _currentPage++;
            await UpdateModsMinecraftAsync();
        }

        private async void SearchSystemModsTXT_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchSystemModsTXT.Text) && SearchSystemModsTXT.Text != "Пошук")
            {
                await UpdateModsMinecraftAsync();
            }
            else
            {
                ModsDowloadList.Items.Clear();
            }
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);
        }
        private ItemJar CreateItemJarFromSearchResult(ModSearchResult mod)
        {
            var bitmap = new BitmapImage();
            if (!string.IsNullOrEmpty(mod.IconUrl))
            {
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(mod.IconUrl);
                bitmap.DecodePixelWidth = 64;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }

            var item = new ItemJar
            {
                ModTitle = mod.Title,
                ModDescription = mod.Description,
                ModImage = bitmap,
                Author = mod.Author,
                DownloadCount = FormatNumber(mod.Downloads),   
                LastUpdateDate = mod.UpdatedDate,
                CreateDate = mod.CreatedDate,
                UrlMods = mod.Slug,
                TypeSite = mod.Site,
                ProjectId = mod.ModId,

                ModId = (mod.Site == "CurseForge" && int.TryParse(mod.ModId, out int id)) ? id : 0,
                FileId = mod.CF_FileId
            };

            item.DowloadTXT.MouseDown += (s, e) => HandleModDownloadClick(mod);

            item.UserControl.MouseDoubleClick += (s, e) =>
            {
                string baseUrl = mod.Site == "Modrinth" ? "https://modrinth.com" : "https://www.curseforge.com/minecraft";

                string category = mod.Site == "Modrinth"
                    ? (selectmodificed == 1 ? "shader" :
                       selectmodificed == 2 ? "resourcepack" :
                       selectmodificed == 4 ? "datapack" :
                       "mod") 
                    : (selectmodificed == 1 ? "shaders" :
                       selectmodificed == 2 ? "texture-packs" :
                       selectmodificed == 3 ? "worlds" :       
                       selectmodificed == 4 ? "data-packs"
                       : "mc-mods");

                string url = $"{baseUrl}/{category}/{mod.Slug}";
                WebHelper.OpenUrl(url);
            };

            return item;
        }
        private async void HandleModDownloadClick(ModSearchResult mod)
        {
            if (Version != null) Version.Items.Clear();
            if (VersionMods != null) VersionMods.Items.Clear();

            if (CollectionList != null)
            {
                CollectionList.ItemsSource = null;
                CollectionList.Items.Clear();
            }

            _currentModVersions = null;
            AnimationService.FadeIn(GirdModsDowload, 0.2);
            AnimationService.AnimatePageTransition(MenuInstaller);

            try
            {
                var allReleaseVersions = await _modDownloadService.GetVersionsAsync(mod);

                if (allReleaseVersions == null || !allReleaseVersions.Any())
                {
                    MascotMessageBox.Show(
                        "Дивина! Я перерила усі архіви, але не знайшла жодного файлу для цього проекту.\n" +
                        "Можливо, автор видалив версії або проект ще не готовий.",
                        "Пусто",
                        MascotEmotion.Confused
                    );
                    CloseInstallerMenu();
                    return;
                }

                _currentModVersions = allReleaseVersions;

                if (ModType == "Collection")
                {
                    CollectionListBorder.Visibility = Visibility.Visible;

                    var modpacks = _modpackService.LoadInstalledModpacks();
                    CollectionList.ItemsSource = modpacks;
                    CollectionList.DisplayMemberPath = "Name";
                }
                else
                {

                    CollectionListBorder.Visibility = Visibility.Hidden;
                    if (Version.Parent is Border versionBorder) versionBorder.Visibility = Visibility.Visible;

                    if (VersionMods.Parent is Border loaderBorder2)
                    {
                        loaderBorder2.Visibility = (selectmodificed == 0) ? Visibility.Visible : Visibility.Collapsed;
                    }

                    var gameVersions = _currentModVersions
                        .SelectMany(v => v.GameVersions)
                        .Distinct()
                        .Where(v => char.IsDigit(v[0]))
                        .OrderByDescending(v => ParseGameVersion(v))
                        .ToList();

                    if (gameVersions.Count == 0 && (selectmodificed == 3 || selectmodificed == 4))
                    {
                        foreach (var v in _currentModVersions)
                        {
                            if (!Version.Items.Contains(v.VersionName))
                                Version.Items.Add(v.VersionName);
                        }
                    }
                    else
                    {
                        foreach (var gv in gameVersions) Version.Items.Add(gv);
                    }

                    if (Version.Items.Count > 0) Version.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ой-йой! Я намагалася підготувати встановлення, але спіткнулася об помилку.\n" +
                    $"Спробуй ще раз пізніше.\n\n" +
                    $"Ось що трапилося: {ex.Message}",
                    "Збій підготовки",
                    MascotEmotion.Sad
                );
                CloseInstallerMenu();
            }
        }
        private void CloseInstallerMenu()
        {
            AnimationService.FadeOut(GirdModsDowload, 0.2);
            AnimationService.AnimatePageTransitionExit(MenuInstaller);
        }
        private System.Version ParseGameVersion(string versionStr)
        {
            try
            {
                var cleanStr = System.Text.RegularExpressions.Regex.Match(versionStr, @"^[0-9\.]+").Value;

                var parts = cleanStr.Split('.');

                if (parts.Length == 2)
                {
                    cleanStr = $"{parts[0]}.{parts[1]}.0";
                }
                else if (parts.Length == 1 && !string.IsNullOrEmpty(cleanStr))
                {
                    cleanStr = $"{parts[0]}.0.0";
                }

                if (System.Version.TryParse(cleanStr, out var version))
                {
                    return version;
                }
            }
            catch { }

            return new System.Version(0, 0, 0);
        }
        private async void DowloadMod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (VersionMods.SelectedItem == null || _currentModVersions == null) return;

            var selectedVersionInfo = _currentModVersions.FirstOrDefault(v => v.VersionName == VersionMods.SelectedItem.ToString());
            if (selectedVersionInfo == null) return;

            string targetPath = null;

            if (ModType == "Collection")
            {
                if (CollectionList.SelectedItem is InstalledModpack pack)
                {
                    string folderName = selectmodificed switch
                    {
                        1 => "shaderpacks",
                        2 => "resourcepacks",
                        3 => "saves",     
                        4 => "datapacks",   
                        _ => "mods"
                    };

                    targetPath = Path.Combine(pack.Path, "override", folderName);
                    if (!Directory.Exists(targetPath)) targetPath = Path.Combine(pack.Path, "overrides", folderName);
                    if (!Directory.Exists(targetPath)) targetPath = Path.Combine(pack.Path, "override", folderName);

                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    MascotMessageBox.Show("Оберіть збірку!", "Куди качати?", MascotEmotion.Alert);
                    return;
                }
            }
            else
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) targetPath = dialog.SelectedPath;
                    else return;
                }
            }

            ProgressDowloadFile.Value = 0;
            ProgressDowloadFile.Visibility = Visibility.Visible;
            DowloadMod.IsEnabled = false;

            try
            {
                var progress = new Progress<int>(p => ProgressDowloadFile.Value = p);

                await _modDownloadService.DownloadModWithDependenciesAsync(
                    selectedVersionInfo,
                    selectmodificed,
                    CancellationToken.None,
                    progress,
                    targetPath
                );

                if (selectmodificed == 3) 
                {
                    string downloadedFile = Path.Combine(targetPath, selectedVersionInfo.FileName);

                    if (File.Exists(downloadedFile) && Path.GetExtension(downloadedFile).ToLower() == ".zip")
                    {
                        await Task.Run(() => ExtractMap(downloadedFile, targetPath));

                        NotificationService.ShowNotification("Успіх!", "Мапа завантажена та розпакована!", SnackbarPresenter);
                    }
                    else
                    {
                        NotificationService.ShowNotification("Увага!", "Файл завантажено, але це не .zip, тому я його не чіпала.", SnackbarPresenter);
                    }
                }
                else
                {
                    NotificationService.ShowNotification("Успіх!", $"Файл встановлено у {targetPath}!", SnackbarPresenter);
                }

                CloseInstallerMenu();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"Помилка: {ex.Message}", "Помилка завантаження", MascotEmotion.Sad);
            }
            finally
            {
                ProgressDowloadFile.Visibility = Visibility.Hidden;
                DowloadMod.IsEnabled = true;
            }
        }
        private void ExtractMap(string zipPath, string extractToFolder)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    var rootFolders = archive.Entries
                        .Where(e => !string.IsNullOrEmpty(e.Name)) 
                        .Select(e => e.FullName.Split('/')[0])
                        .Distinct()
                        .ToList();

                    bool hasSingleRootFolder = rootFolders.Count == 1;

                    if (hasSingleRootFolder)
                    {
                        archive.ExtractToDirectory(extractToFolder, true);
                    }
                    else
                    {
                        string folderName = Path.GetFileNameWithoutExtension(zipPath);
                        string newMapFolder = Path.Combine(extractToFolder, folderName);

                        Directory.CreateDirectory(newMapFolder);
                        archive.ExtractToDirectory(newMapFolder, true);
                    }
                }

                File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MascotMessageBox.Show(
                        $"Халепа! Я намагався, але цей файл ніяк не хоче розпаковуватися.\nМожливо, він пошкоджений?\n\nПомилка: {ex.Message}",
                        "Невдача з мапою",
                        MascotEmotion.Sad)
                );
            }
        }
        private void CollectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VersionMods.Items.Clear();

            if (CollectionList.SelectedItem is not InstalledModpack selectedPack || _currentModVersions == null)
            {
                DowloadMod.IsEnabled = false;
                return;
            }

            string targetVer = selectedPack.MinecraftVersion;
            string targetLoader = selectedPack.LoaderType.ToLower(); 

            var compatibleVersions = _currentModVersions
                .Where(v => v.GameVersions.Contains(targetVer))
                .ToList();

            if (selectmodificed == 0)
            {
                compatibleVersions = compatibleVersions
                    .Where(v => v.Loaders.Any(l => l.Equals(targetLoader, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (compatibleVersions.Count > 0)
            {
                foreach (var v in compatibleVersions)
                {
                    VersionMods.Items.Add(v.VersionName);
                }
                VersionMods.SelectedIndex = 0;
                DowloadMod.IsEnabled = true;
            }
            else
            {
                MascotMessageBox.Show(
                    $"Ех, не вийде. Я перевірила всі файли, але не знайшла версії мода, яка б підійшла для збірки '{selectedPack.Name}'.\n\n" +
                    $"Збірка вимагає Minecraft {targetVer} ({targetLoader}), а цей мод, схоже, не оновлено під такі параметри.",
                    "Несумісність",
                    MascotEmotion.Sad
                ); DowloadMod.IsEnabled = false;
            }
        }
        private void Version_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VersionMods.Items.Clear();
            if (Version.SelectedItem == null || _currentModVersions == null)
            {
                DowloadMod.IsEnabled = false;
                return;
            }

            string selectedGameVersion = Version.SelectedItem.ToString();
            string currentLoader = VersionType.ToLower();

            List<ModVersionInfo> filteredFileVersions;

            if (selectmodificed == 0) 
            {
                filteredFileVersions = _currentModVersions
                   .Where(v => v.GameVersions.Contains(selectedGameVersion) &&
                               v.Loaders.Contains(currentLoader))
                   .OrderByDescending(v => v.VersionName)
                   .ToList();
            }
            else
            {
                filteredFileVersions = _currentModVersions
                   .Where(v => v.GameVersions.Contains(selectedGameVersion))
                   .OrderByDescending(v => v.VersionName)
                   .ToList();
            }

            foreach (var fileVersion in filteredFileVersions)
            {
                VersionMods.Items.Add(fileVersion.VersionName);
            }

            DowloadMod.IsEnabled = VersionMods.Items.Count > 0;
            if (VersionMods.Items.Count > 0)
                VersionMods.SelectedIndex = 0;
        }
        private void IconAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadProfilesAsync();

                if (PanelManegerAccount.Visibility == Visibility.Visible)
                {
                    IconRotateTransform.Angle = 0;
                    AnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20);
                    ListAccount.Items?.Clear();
                }
                else
                {
                    IconRotateTransform.Angle = 180;
                    AnimationService.AnimatePageTransition(PanelManegerAccount, -20);
                }

                if (PanelListStats.Visibility == Visibility.Visible)
                {
                    AnimationService.FadeOut(PanelListStats, 0.3);
                }

                Click();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                        $"Ой! Я намагалася відкрити меню акаунтів, але щось пішло не так.\n" +
                        $"Можливо, файл профілів пошкоджений або сталася помилка інтерфейсу.\n\n" +
                        $"Технічні деталі: {ex.Message}",
                        "Помилка меню",
                        MascotEmotion.Sad
                    );
            }
        }
        private void BackMainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (ServerList.Visibility == Visibility.Visible || PanelInfoServer.Visibility == Visibility.Visible)
            {
                BackMainWindow.Visibility = Visibility.Hidden;
                SearchSystemTXT.Visibility = Visibility.Hidden;
                AnimationService.FadeOut(PanelInfoServer, 0.4);
                AnimationService.FadeOut(ServerList, 0.3);
            }
        }
        private void Relesed_MouseDown_1(object sender, RoutedEventArgs e)
        {
            if (VersionSelect == 1)
                AddVersion();
        }
        private void Alpha_Click(object sender, RoutedEventArgs e)
        {
            if (VersionSelect == 1)
                AddVersion();
        }
        private void Beta_Click(object sender, RoutedEventArgs e)
        {
            if (VersionSelect == 1)
                AddVersion();
        }
        private void Snapshots_Click(object sender, RoutedEventArgs e)
        {
            if (VersionSelect == 1)
                AddVersion();
        }
        private async void SearchSystem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ServerList.Items != null) ServerList.Items.Clear();
            string searchQuery = SearchSystemTXT.Text;

            await _serverListService.InitializeServersAsync(true, searchQuery);
        }
        private void SearchSystem1_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!this.IsLoaded) return;
            AddVersion();
        }

        private void SearchSystemTXT1_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (!this.IsLoaded) return;
            string selectedVersion = args.SelectedItem.ToString();

            sender.Text = selectedVersion;

            foreach (var item in VersionList.Items)
            {
                if (item.ToString() == selectedVersion)
                {
                    VersionList.ScrollIntoView(item);
                    break;
                }
            }
        }
        private void SearchSystemTXT2_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (!this.IsLoaded) return;
            string selectedVersion = args.SelectedItem.ToString();

            sender.Text = selectedVersion;

            foreach (var item in VersionListVanila.Items)
            {
                if (item.ToString() == selectedVersion)
                {
                    VersionListVanila.ScrollIntoView(item);

                    break;
                }
            }
        }
        public void ShowGameLog(Process process)
        {
            GameLog gameLog = new GameLog();
            gameLog.Show();

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += gameLog.MinecraftProcess_OutputDataReceived;
            process.ErrorDataReceived += gameLog.MinecraftProcess_ErrorDataReceived;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        private void PlayTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToHome();
        }
        private void ModsTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToMods();
        }

        private void modbuilds_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToModPacks();
        }

        public void ServerTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToServers();
        }
        public void PhotoMinecraftTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToGallery();
        }
        private async void CheckMarkVersionSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (!InstallVersionOnPlay)
            {
                if (SelectVersionTypeGird.Visibility == Visibility.Visible)
                {
                    IconVersionRotateTransform.Angle = 180;
                    AnimationService.AnimatePageTransitionExit(SelectVersion, 20);
                    AnimationService.AnimatePageTransitionExit(SelectVersionTypeGird, 20);
                    AnimationService.AnimatePageTransitionExit(SelectVersionMod, 20);

                    int savedType = Settings1.Default.LastSelectedType;
                    string savedVer = Settings1.Default.LastSelectedVersion;
                    string savedModVer = Settings1.Default.LastSelectedModVersion;

                    if (savedType != 0 && !string.IsNullOrEmpty(savedVer))
                    {
                        VersionSelect = (byte)savedType;

                        if (savedType == 5 && !string.IsNullOrEmpty(savedModVer))
                        {
                            PlayTXT.Content = $"ГРАТИ В ({savedModVer})";
                        }
                        else 
                        {
                            PlayTXT.Content = $"ГРАТИ В ({savedVer})";
                        }
                    }
                    else
                    {
                        PlayTXT.Content = "ОБЕРІТЬ ВЕРСІЮ"; 
                        VersionSelect = 0;
                    }
                }
                else
                {
                    IconVersionRotateTransform.Angle = 0;
                    AnimationService.AnimatePageTransition(SelectVersionTypeGird);

                    var path = new MinecraftPath(Settings1.Default.PathLacunher);
                    var launcher = new MinecraftLauncher(path);
                    var versions = await launcher.GetAllVersionsAsync();

                    VersionRelesedVanilLast.Content = versions.LatestReleaseName;
                    VersionRelesedVanilLast1.Content = versions.LatestReleaseName;
                    VersionRelesedVanilLast5.Content = versions.LatestReleaseName;
                }
            }
        }
        private void SelectVersionVanila_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionVanila.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            else
            {
                SelectVersionVanila.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }
        private void SelectVersionVanila_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionVanila.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            else
            {
                SelectVersionVanila.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
        }
        private void SelectVersionLocal_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionCustom.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            else
            {
                SelectVersionCustom.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }
        private void SelectVersionLocal_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionCustom.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            else
            {
                SelectVersionCustom.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
        }
        private void SelectVersionVanila_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            VersionList.Items.Clear();
            AddVersion();
            AnimationService.AnimatePageTransition(SelectVersion); AnimationService.FadeIn(SelectVersionVanila, 0.2); AnimationService.FadeOut(SelectVersionMod, 0.2);
            VersionSelect = 1;
            IconSelectVersion.Source = IconSelectVersion_Копировать.Source;
        }
        private void SelectVersionLocal_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            VersionList.Items.Clear();
            AddVersion();
            AnimationService.AnimatePageTransition(SelectVersion); AnimationService.FadeIn(SelectVersionVanila, 0.2); AnimationService.FadeOut(SelectVersionMod, 0.2);
            VersionSelect = 2;
            IconSelectVersion.Source = IconSelectVersion_Custom.Source;
        }
        private void SelectVersionOptifine_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlayTXT.Content = "ГРАТИ";
            VersionListMod.Items.Clear();
            Click();
            AddVersionOptifine();
            AnimationService.AnimatePageTransition(SelectVersionMod); AnimationService.FadeIn(SelectVersionOptifine, 0.2); AnimationService.FadeOut(SelectVersion, 0.2);
            VersionSelect = 5; 
            IconSelectVersion.Source = IconSelectVersion_Optifine.Source;
        }
        private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Click();
            if (VersionList.SelectedItem != null)
            {
                string selectedVer = VersionList.SelectedItem.ToString();

                VersionRelesedVanilLast.Content = selectedVer;
                PlayTXT.Content = $"ГРАТИ В ({selectedVer})";

                Settings1.Default.LastSelectedVersion = selectedVer;
                Settings1.Default.LastSelectedType = VersionSelect; 
                Settings1.Default.Save();
            }
        }
        private async void PlayTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (NameNik.Text == "Відсутній акаунт")
            {
                MascotMessageBox.Show("Агов! Будь ласка, оберіть акаунт перед початком гри!", "Акаунт не обраний", MascotEmotion.Alert);
                return;
            }

            if (BorderButton != null)
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 30, 30, 30), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 50, 50, 50), 1));
                BorderButton.Background = brush;
                Line.Background = new SolidColorBrush(Color.FromArgb(255, 10, 60, 10));
            }

            if (!InstallVersionOnPlay)
            {
                if (VersionSelect == 0)
                {
                    VersionSelect = (byte)Settings1.Default.LastSelectedType;
                }

                if (VersionSelect == 0)
                {
                    MascotMessageBox.Show("Агов! Будь ласка, оберіть версію перед початком гри!", "Версія не обрана", MascotEmotion.Alert);
                    await Task.Delay(200);
                    RestoreButtonColor();
                    return;
                }

                string ver = Settings1.Default.LastSelectedVersion;
                string modVer = Settings1.Default.LastSelectedModVersion;

                switch (VersionSelect)
                {
                    case 1: 
                        DowloadVanila(ver, null, null, NameNik.Text);
                        break;
                    case 2:
                        PlayLocal(ver, null, null, NameNik.Text);
                        break;
                    case 5:
                        DownloadVersionOptifine(ver, modVer);
                        break;
                }

                AnimationService.FadeOut(SelectVersion, 0.3);
                AnimationService.FadeOut(SelectVersionMod, 0.3);
                AnimationService.FadeOut(SelectVersionTypeGird, 0.3);

            }

            await Task.Delay(200);
            RestoreButtonColor();
        }
        private void RestoreButtonColor()
        {
            if (BorderButton != null)
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 23, 218, 31), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 24, 166, 30), 1));
                BorderButton.Background = brush;
                Line.Background = new SolidColorBrush(Color.FromArgb(255, 22, 149, 27));
            }
        }
        private async void SettingPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await HideAllPages();

            AnimationService.AnimatePageTransition(SettingPanelMinecraft, 0.3); 
            AnimationService.AnimatePageTransition(ScrollSetting, 0.2);
        }

        private void FolderPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            string path = $@"{Settings1.Default.PathLacunher}";

            if (File.Exists(path) == false)
                Process.Start("explorer.exe", path);
        }
        private void InfoPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();

            if(GirdInfo.Visibility == Visibility.Hidden)
            {
                GirdInfo.Visibility = Visibility.Visible;
                AnimationService.AnimatePageTransition(GirdInfo);
            }
            else
            {
                AnimationService.AnimatePageTransitionExit(GirdInfo);
            }
        }
        private void BackIconServerList_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorderObject(100, 0, FonBackIconServerList, false);
            if (PanelInfoServer.Visibility == Visibility.Visible) { AnimationService.AnimatePageTransitionExit(PanelInfoServer); AnimationService.AnimatePageTransition(ServerName); }
            if (GirdTXTNews.Visibility == Visibility.Visible) { AnimationService.AnimatePageTransitionExit(GirdTXTNews); AnimationService.AnimatePageTransition(GirdNews); }
        }

        private void PlayServer_Click(object sender, RoutedEventArgs e)
        {
            Click();
            DowloadVanila(VersionTXT.Content.ToString(), IPServerTXT.Content.ToString(), Convert.ToInt32(PortTXT.Content), NameNik.Text);
            AddLastActionAsync(TitleMain1.Content.ToString(), VersionTXT.Content.ToString(), IPServerTXT.Content.ToString(), Convert.ToInt32(PortTXT.Content));
        }
        private void DiscordLink_Click(object sender, RoutedEventArgs e)
        {
            WebHelper.OpenUrl(discordLink[ServerList.SelectedIndex]);
        }

        private void SiteLink_Click(object sender, RoutedEventArgs e)
        {
            WebHelper.OpenUrl(siteLink[ServerList.SelectedIndex]);
        }

        private void DonateLink_Click(object sender, RoutedEventArgs e)
        {
            WebHelper.OpenUrl(donateLink[ServerList.SelectedIndex]);
        }

        private void OPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isSliderDragging) return;

            SliderOPTXT.Content = OPSlider.Value.ToString("0") + "MB";

            double direction = OPSlider.Value - previousSliderValue;

            var track = OPSlider.Template.FindName("PART_Track", OPSlider) as Track;
            var thumb = track?.Thumb;
            if (thumb != null && thumb.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = scale.ScaleY = Math.Max(0.5, Math.Min(2, scale.ScaleX + (direction > 0 ? 0.009 : -0.009)));
            }

            previousSliderValue = OPSlider.Value;
        }

        private void OPSlider_PreviewMouseDown_1(object sender, MouseButtonEventArgs e)
        {
            Click();
            isSliderDragging = true; 
        }

        private void OPSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            isSliderDragging = false;

            Settings1.Default.OP = (int)OPSlider.Value;
            Settings1.Default.Save();
        }
        private void MincraftWindowSize_Click(object sender, RoutedEventArgs e)
        {
            Click();

            if (EditWditXHeghit.Visibility == Visibility.Visible)
            {
                EditWditXHeghit.Visibility = Visibility.Collapsed;
            }
            else
            {
                EditWditXHeghit.Visibility = Visibility.Visible;
            }
        }
        private void DisableGlassEffectToggle_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();

            Settings1.Default.DisableGlassEffect = !Settings1.Default.DisableGlassEffect;
            Settings1.Default.Save();

            _themeService.ToggleGlassEffect(Settings1.Default.DisableGlassEffect);

            GlassEffectToggle.IsChecked = Settings1.Default.DisableGlassEffect;
        }
        private void DisablePotatoRegimeToggle_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();

            Settings1.Default.IsPotatoMode = !Settings1.Default.IsPotatoMode;
            Settings1.Default.Save();

            _themeService.ApplyPotatoMode();

            PotatoModeToggle.IsChecked = Settings1.Default.IsPotatoMode;
        }
        private void ScreenSizeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ScreenSizeListBox.SelectedItem as ListBoxItem;
            if (selectedItem == null)
            {
                MascotMessageBox.Show(
                            "Дивина! Я бачила натискання, але не зрозуміла, який саме варіант ти обрав.\n" +
                            "Спробуй натиснути на рядок ще раз!",
                            "Пустий вибір",
                            MascotEmotion.Confused
                        ); return;
            }

            var resolution = selectedItem.Content.ToString().Split('x');
            if (resolution.Length != 2 || !int.TryParse(resolution[0], out int width) || !int.TryParse(resolution[1], out int height))
            {
                MascotMessageBox.Show(
                            "Ой! Я намагалася розібрати цей розмір екрана, але цифри записані якось дивно.\n" +
                            "Формат має бути 'ШиринаxВисота' (наприклад, 1920x1080), а тут щось інше.",
                            "Невірний формат",
                            MascotEmotion.Sad
                        ); return;
            }

            Width.Text = resolution[0];
            Height.Text = resolution[1];
            MincraftWindowSize.Content = $"{width}x{height}";

            Settings1.Default.width = width;
            Settings1.Default.height = height;
            Settings1.Default.Save();
        }
        private void Width_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleTextBoxInput(Width, "width");
        }

        private void Height_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleTextBoxInput(Height, "height");
        }
        private void Width_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            HandleTextBoxInput(Width, "width");
        }

        private void Height_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            HandleTextBoxInput(Height, "height");
        }

        private void HandleTextBoxInput(System.Windows.Controls.TextBox textBox, string dimensionKey)
        {
            int caretIndex = textBox.CaretIndex;

            string validText = Regex.Replace(textBox.Text, @"[^\d]", "");

            if (string.IsNullOrEmpty(validText))
            {
                textBox.Text = dimensionKey == "width" ? "800" : "600"; 
            }
            else
            {
                textBox.Text = validText;

                if (int.TryParse(validText, out int result))
                {
                    result = Math.Max(800, Math.Min(3840, result)); 
                    if (dimensionKey == "width")
                    {
                        Settings1.Default.width = result;
                    }
                    else
                    {
                        Settings1.Default.height = result;
                    }

                    Settings1.Default.Save();
                }
            }

            UpdateMinecraftWindowSize();
            textBox.CaretIndex = caretIndex;
        }

        private void UpdateMinecraftWindowSize()
        {
            MincraftWindowSize.Content = $"{Settings1.Default.width}x{Settings1.Default.height}";
        }

        private async void ModrinthSiteModsTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 0, PanelSelectNowSiteMods);
            SiteMods = "Modrinth";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }

        private async void CurseForgeSiteModsTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 45, PanelSelectNowSiteMods);
            SiteMods = "CurseForge";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }

        private async void FabricVesionTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 0, PanelSelectNowVersionType);
            VersionType = "Fabric";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }

        private async void ForgeVesionTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 40, PanelSelectNowVersionType);
            VersionType = "Forge";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }
        private async void ModsDowloadTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 0, PanelSelectNowModsType);
            ModType = "Collection";
            ModsDowloadList.Items.Clear();
            VersionVanilBorder.Visibility = Visibility.Hidden;
            CollectionListBorder.Visibility = Visibility.Visible;
            await UpdateModsMinecraftAsync();
        }
        private async void ModsManegerTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 45, PanelSelectNowModsType);
            ModType = "Standard";
            ModsDowloadList.Items.Clear();
            VersionVanilBorder.Visibility = Visibility.Visible;
            CollectionListBorder.Visibility = Visibility.Hidden;
            await UpdateModsMinecraftAsync();
        }
        public BitmapImage ConvertBitmapToBitmapImage(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }
        private void DebugOff_On_Click(object sender, RoutedEventArgs e)
        {
            Click();
            Settings1.Default.EnableLog = !Settings1.Default.EnableLog;
            Settings1.Default.Save();
            DebugToggle.IsChecked = Settings1.Default.EnableLog;
        }
        private void CloseLaucnherPlayMinecraft_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();
            Settings1.Default.CloseLaucnher = !Settings1.Default.CloseLaucnher;
            Settings1.Default.Save();
            CloseLauncherToggle.IsChecked = Settings1.Default.CloseLaucnher;
        }
        private void GirdModsDowloadExit_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.FadeOut(GirdModsDowload, 0.3);
        }
        private void GirdModsDowload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.FadeOut(GirdModsDowload, 0.3);
            AnimationService.FadeOut(MenuInstaller, 0.3);

            VersionMods.Items?.Clear(); Version.Items?.Clear(); CollectionList.Items?.Clear();
        }
        private void VersionMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Version.SelectedItem != null && VersionMods.SelectedItem != null)
            {
                DowloadMod.IsEnabled = true;
            }
            else
            {
                DowloadMod.IsEnabled = false;
            }
        }
        private void BugReport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.FadeIn(BugReport, 0.3);
            WebHelper.OpenUrl(@$"https://discord.com/channels/1195118159187939458/1195494058571866172");
            AnimationService.AnimatePageTransitionExit(GirdInfo);
        }

        private async void NewsUpdateLauncher_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await HideAllPages();

            AnimationService.FadeIn(NewsUpdateLauncher, 0.2); AnimationService.AnimatePageTransition(GirdNews); AnimationService.AnimatePageTransition(ListNews);
            try
            {
                HttpClient client = new HttpClient();
                string json = await client.GetStringAsync("https://raw.githubusercontent.com/WER-CORE/CL-OpenSource/refs/heads/main/NewsListTest.json"); // або вставте своє посилання

                var newsItems = JsonConvert.DeserializeObject<List<NewsItem>>(json);

                ListNews.Items.Clear();

                foreach (var item in newsItems)
                {
                    ItemNews itemNews = new ItemNews();
                    itemNews.TitleUpdate.Content = item.Title;
                    itemNews.description = item.Description;
                    itemNews.ImageNews.Source = new BitmapImage(new Uri(item.IconUrl, UriKind.Absolute));
                    itemNews.MouseDown += (s, e) =>
                    {
                        Click();
                        AnimationService.AnimateBorderObject(-120, 0, FonBackIconServerList, true);
                        AnimationService.AnimatePageTransitionExit(GirdNews);
                        AnimationService.AnimatePageTransition(GirdTXTNews);
                        TextNews.Text = item.Description;
                    };

                    ListNews.Items.Add(itemNews);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Ех, халепа! Я намагалася дізнатися про останні події, але не змогла завантажити новини.\n" +
                    $"Можливо, зник інтернет або сервери новин зараз недоступні.\n\n" +
                    $"Ось що завадило: {ex.Message}",
                    "Новини загубилися",
                    MascotEmotion.Sad
                );
            }
        }
        private void AddProfile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();

            AnimationService.AnimatePageTransition(GirdFormAccountAdd); AnimationService.AnimatePageTransition(GirdOfflineMode); AnimationService.AnimatePageTransition(GirdSelectAccountType);

            string directoryPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            string profilesManegerPath = System.IO.Path.Combine(directoryPath, "ProfilesManeger.json");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (File.Exists(profilesManegerPath) == false)
            {
                using (FileStream fs = File.Create(profilesManegerPath))
                {
                    fs.Close();
                }
            }
        }

        private void GirdFormAccountAdd_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (GirdSelectAccountType.Visibility == Visibility.Visible) { AnimationService.AnimatePageTransitionExit(GirdFormAccountAdd); AnimationService.AnimatePageTransitionExit(GirdOfflineMode); AnimationService.AnimatePageTransitionExit(GirdOnlineMode); AnimationService.AnimatePageTransitionExit(GirdSelectAccountType); AnimationService.AnimatePageTransitionExit(GirdLittleSkinMode); }
            if (SelectCreatePackMinecraft.Visibility == Visibility.Visible) { AnimationService.AnimatePageTransitionExit(SelectCreatePackMinecraft); AnimationService.AnimatePageTransitionExit(GirdFormAccountAdd); }
        }

        private void MicrosoftAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimatePageTransitionExit(GirdLittleSkinMode, default, 0.2);
            AnimationService.AnimatePageTransitionExit(GirdOfflineMode, default, 0.2);
            AnimationService.AnimatePageTransition(GirdOnlineMode);
        }

        private void OfflineAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimatePageTransitionExit(GirdLittleSkinMode, default, 0.2);
            AnimationService.AnimatePageTransitionExit(GirdOnlineMode, default, 0.2);
            AnimationService.AnimatePageTransition(GirdOfflineMode);
        }
        private void LittleSkinAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimatePageTransitionExit(GirdOfflineMode,default, 0.2);
            AnimationService.AnimatePageTransitionExit(GirdOnlineMode,default,0.2);
            AnimationService.AnimatePageTransition(GirdLittleSkinMode);
        }
        private void SelectVersionOptifine_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionOptifine.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            else
            {
                SelectVersionOptifine.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }
        private void SelectVersionOptifine_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ThemeService.currentTheme == "Dark") SelectVersionOptifine.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            else
            {
                SelectVersionOptifine.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
        }
        private void ThemNight_Light_Click(object sender, MouseButtonEventArgs e)
        {
            _themeService.HandleThemeToggleClick();
        }

        private void Background_imageButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleBackgroundImageClick();
        }

        private void Section_colourButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleColorChange(Section_colourButton, "Section_colour", "MainBackgroundBrushServer");
        }

        private void Background_colourButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleColorChange(Background_colourButton, "Background_colour", "MainBackgroundBrush");
        }

        private void Additional_colourButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleColorChange(Additional_colourButton, "Additional_colour", "MainBackgroundProgressBar");
        }

        private void Text_colourButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleColorChange(Text_colourButton, "Text_colour", "MainForegroundBrush");
        }

        private void Button_colourButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleColorChange(Button_colourButton, "Button_colour", "MainBackgroundButton");
        }

        private void SaveandAcceptCustomThem_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleSaveCustomThemeClick();
        }
        private void ResetCustomSetting_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _themeService.HandleResetCustomThemeClick();
        }
        private async void QuitVersionSelectMod_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 75, PanelSelectNowVersionType);
            VersionType = "Quilt";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }
        private async void NeoForgeVersionSelectMod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimateBorder(0, 110, PanelSelectNowVersionType);
            VersionType = "NeoForge";
            ModsDowloadList.Items.Clear();
            await UpdateModsMinecraftAsync();
        }
        private void SearchSystemTXT2_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!this.IsLoaded) return;
            if (VersionSelect == 5)
                AddVersionOptifine();
        }
        private void VersionListVanila_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionListVanila.SelectedItem != null)
            {
                string selectedVer = VersionListVanila.SelectedItem.ToString();

                if (VersionSelect == 2)
                {
                    VersionRelesedVanilLast1.Content = selectedVer;

                    Settings1.Default.LastSelectedVersion = selectedVer;
                    Settings1.Default.LastSelectedType = 2;
                    Settings1.Default.Save();

                    PlayTXT.Content = $"ГРАТИ В ({selectedVer})";
                }

                if (VersionSelect == 5)
                {
                    VersionRelesedVanilLast5.Content = selectedVer;
                }

                AddVersionModeVersion();
            }
        }

        private void VersionListMod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionListVanila.SelectedItem != null && VersionSelect == 5)
            {
                if (VersionListMod.SelectedItem != null)
                {
                    string mcVer = VersionListVanila.SelectedItem.ToString();
                    string modVer = VersionListMod.SelectedItem.ToString();

                    PlayTXT.Content = $"ГРАТИ В ({modVer})";

                    Settings1.Default.LastSelectedVersion = mcVer;    
                    Settings1.Default.LastSelectedModVersion = modVer;  
                    Settings1.Default.LastSelectedType = 5;             
                    Settings1.Default.Save();
                }
            }
        }
        private void ModsDowloadList_Loaded(object sender, RoutedEventArgs e)
        {
            if (VirtualizingStackPanel.GetIsVirtualizing(ModsDowloadList) == false)
            {
                VirtualizingStackPanel.SetIsVirtualizing(ModsDowloadList, true);
                VirtualizingStackPanel.SetVirtualizationMode(ModsDowloadList, VirtualizationMode.Recycling);
            }
        }
        private async void DataPacksTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            AnimationService.AnimateBorder(610, 0, PanelSelectNowDowloadModifi);
            selectmodificed = 4; 
            await UpdateModsMinecraftAsync();
        }
        private async void MapsTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            AnimationService.AnimateBorder(450, 0, PanelSelectNowDowloadModifi);
            selectmodificed = 3;
            await UpdateModsMinecraftAsync();
        }
        private async void ResourcePackTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            AnimationService.AnimateBorder(300, 0, PanelSelectNowDowloadModifi);
            selectmodificed = 2;
            await UpdateModsMinecraftAsync();
        }

        private async void ShardTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            AnimationService.AnimateBorder(150, 0, PanelSelectNowDowloadModifi);
            selectmodificed = 1;
            await UpdateModsMinecraftAsync();
        }

        private async void ModsTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);

            AnimationService.AnimateBorder(0, 0, PanelSelectNowDowloadModifi);
            selectmodificed = 0;
            await UpdateModsMinecraftAsync();
        }
        private async void TutorialYoutube_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.FadeIn(TutorialYoutube, 0.3);
            WebHelper.OpenUrl("https://wer-developers-organization.gitbook.io/cl-minecraft-launcher/");
            AnimationService.AnimatePageTransitionExit(GirdInfo);
        }

        private void DowloadModDepOff_On_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();
            Settings1.Default.ModDep = !Settings1.Default.ModDep;
            Settings1.Default.Save();

            ModDepsToggle.IsChecked = Settings1.Default.ModDep;
        }
        private void ImportTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            DowloadModPack dowloadModPack = new DowloadModPack(_modpackService);
            dowloadModPack.Show();
        }

        private void CreateModPacksTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            CreateModPackWindow createModPackWindow = new CreateModPackWindow(_modDownloadService,_modpackService);
            createModPackWindow.Show();
        }

        private void DowloadModPacks_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            AnimationService.AnimatePageTransition(GirdFormAccountAdd); AnimationService.AnimatePageTransition(SelectCreatePackMinecraft);
        }

        private void SearchSystemModsTXT1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SearchSystemModsTXT1.Text == "Пошук")
            {
                SearchSystemModsTXT1.Text = "";
            }
        }
        private void SearchSystemModsTXT1_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchSystemModsTXT1.Text.Trim().ToLower();

            if (query != "Пошук")
            {

                var filtered = allInstalledModpacks
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && m.Name.ToLower().Contains(query))
                .ToList();

                UpdateDisplayedModpacks(filtered);
            }
        }
        public void UpdateDisplayedModpacks(List<InstalledModpack> modpacks)
        {
            if (ModsDowloadList1 != null)
                ModsDowloadList1.Items.Clear();

            foreach (var value in modpacks)
            {
                var item = new ItemModPack();
                item.NameModPack.Content = value.Name;
                item.DescriptionModPack.Content = $"{value.LoaderType} {value.MinecraftVersion} : {value.LoaderVersion}";
                item.IconModPack.Source = new BitmapImage(new Uri(value.UrlImage));

                item.BorderPlay.MouseDown += (s, e) => _modpackService.PlayModPack(
                    value.MinecraftVersion,
                    value.LoaderVersion,
                    value.LoaderType,
                    value.Name,
                    value.Path,
                    value.PathJson,
                    value.TypeSite);

                item.DeleteTXT.MouseDown += (s, e) =>
                {
                    Click();
                    allInstalledModpacks.Remove(value);
                    UpdateDisplayedModpacks(allInstalledModpacks); 

                    _modpackService.DeleteModpack(value.Name);
                    _modpackService.DeleteModpackFolder(value);
                };

                item.FloderPack.MouseDown += (s, e) =>
                {
                    Click();
                    if (Directory.Exists(value.Path))
                    {
                        Process.Start("explorer.exe", value.Path);
                    }
                    else
                    {
                        MascotMessageBox.Show(
                            "Дивина! Я намагалася відкрити папку цієї збірки, але не знайшла її на диску.\n" +
                            "Можливо, її хтось випадково видалив або перемістив в інше місце?",
                            "Папка зникла",
                            MascotEmotion.Confused
                        );
                    }
                };

                item.EditModPackTXT.MouseDown += (s, e) =>
                {
                    Click();
                    CLModPackEdit editWindow = new CLModPackEdit();
                    editWindow.NameWin.Text = $"Налаштування збірки {value.Name}";
                    editWindow.PathJsonModPack = value.PathJson;

                    string overridePath = Path.Combine(value.Path, "override");
                    string overridesPath = Path.Combine(value.Path, "overrides");

                    if (Directory.Exists(overridesPath))
                        editWindow.PathMods = overridesPath + @"\";
                    else if (Directory.Exists(overridePath))
                        editWindow.PathMods = overridePath + @"\";
                    else
                        editWindow.PathMods = value.Path + @"\";

                    editWindow.TypeModPack = value.TypeSite;
                    editWindow.VersionType = value.LoaderType;
                    editWindow.version = value.MinecraftVersion;

                    editWindow.ModpackUpdated += () =>
                    {
                        var valueList = _modpackService.LoadInstalledModpacks();
                        allInstalledModpacks = valueList.Where(x => Directory.Exists(x.Path)).ToList();
                        UpdateDisplayedModpacks(allInstalledModpacks);
                    };

                    var installed = value as InstalledModpack;

                    var modpack = new ModpackInfo
                    {
                        Name = installed.Name,
                        Path = installed.Path,
                        PathJson = installed.PathJson,
                        TypeSite = installed.TypeSite,
                        LoaderType = installed.LoaderType,
                        MinecraftVersion = installed.MinecraftVersion,
                        LoaderVersion = installed.LoaderVersion, 
                        UrlImage = installed.UrlImage,
                        ServerIP = installed.ServerIP,
                        EnterInServer = installed.EnterInServer,
                        Wdith = installed.Wdith,
                        Height = installed.Height,
                        IsConsoleLogOpened = installed.IsConsoleLogOpened,
                        OPack = installed.OPack,
                    };

                    editWindow.CurrentModpack = modpack;
                    editWindow.Show();
                };

                ModsDowloadList1.Items.Add(item);
            }
        }
        private void CL_CLegendary_Launcher__Closed(object sender, EventArgs e)
        {
            DiscordController.Deinitialize();
        }
        private void CL_CLegendary_Launcher__Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
        public bool isMouseClickSelection = false;
        public void VersionMinecraftSelectLog()
        {
            if (VersionMinecraftChangeLog == null || !(VersionMinecraftChangeLog.SelectedItem is VersionLogItem selectedItem))
                return;

            string query;
            string cleanId = selectedItem.VersionId;

            switch (selectedItem.VersionType)
            {
                case "old_alpha":
                    query = $"Java Edition Alpha {cleanId.Replace("a", "")}";
                    break;

                case "old_beta":
                    query = $"Java Edition Beta {cleanId.Replace("b", "")}";
                    break;

                case "snapshot":
                    query = $"{cleanId} Java Edition";
                    break;

                case "release":
                default:
                    query = $"Java Edition {cleanId}";
                    break;
            }
            string searchUrl = $"https://uk.minecraft.wiki/w/Special:Search?search={Uri.EscapeDataString(query)}&go=Go";

            try
            {
                WebHelper.OpenUrl(searchUrl);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"Не вдалося відкрити браузер.\n{ex.Message}",
                    "Помилка",
                    MascotEmotion.Sad
                );
            }
        }
        private void VersionMinecraftChangeLog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isMouseClickSelection) return;
            
            VersionMinecraftSelectLog();
            isMouseClickSelection = false;
        }
        private void VersionMinecraftChangeLog_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            isMouseClickSelection = true;
        }
        private void FullScreenOff_On_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();
            Settings1.Default.FullScreen = !Settings1.Default.FullScreen;
            Settings1.Default.Save();

            FullScreenToggle.IsChecked = Settings1.Default.FullScreen;
        }
        private int _lastIndex = 0;
        private DispatcherTimer _slideTimer;

        private void PartnerServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PartnerServer.Items.Count > 1)
            {
                Dispatcher.InvokeAsync(() => AnimateSlide(), DispatcherPriority.Loaded);
            }
        }

        private async void NextIndex()
        {
            if (PartnerServer.Items.Count < 2) return;

            int next = (PartnerServer.SelectedIndex + 1) % PartnerServer.Items.Count;
            PartnerServer.SelectedIndex = next;

            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);
        }

        private void AnimateSlide()
        {
            if (PartnerServer.SelectedItem == null) return;

            int newIndex = PartnerServer.SelectedIndex;

            if (_lastIndex != -1 && _lastIndex != newIndex)
            {
                var oldItem = PartnerServer.ItemContainerGenerator.ContainerFromIndex(_lastIndex) as ListBoxItem;
                if (oldItem != null)
                {
                    AnimateItem(oldItem, 0, -300);
                }
            }

            PartnerServer.UpdateLayout();

            var newItem = PartnerServer.ItemContainerGenerator.ContainerFromIndex(newIndex) as ListBoxItem;
            if (newItem != null)
            {
                AnimateItem(newItem, 300, 0);
            }

            _lastIndex = newIndex;
        }

        private void AnimateItem(UIElement item, double from, double to)
        {
            var tt = new TranslateTransform();
            item.RenderTransform = tt;

            var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(0.5))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            tt.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void PartnerServer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _slideTimer.Stop();
        }

        private void PartnerServer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _slideTimer.Start();
        }

        private void ExitLauncher_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimationService.FadeIn(BordrExitTXT, 0.5);
        }

        private void ExitLauncher_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimationService.FadeOut(BordrExitTXT, 0.5);
        }

        private void ExitLauncher_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Environment.Exit(0);
        }
        private void HiddeWin_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimationService.FadeIn(BordrHiddeTXT, 0.5);
        }
        private void MaxResWin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
                MaxResTXT.Text = "❐";
            }
            else
            {
                this.WindowState = WindowState.Normal;
                MaxResTXT.Text = "▢"; 
            }
        }

        private void MaxResWin_MouseEnter(object sender, MouseEventArgs e)
        {
            BordrMaxResTXT.Visibility = Visibility.Visible;
        }

        private void MaxResWin_MouseLeave(object sender, MouseEventArgs e)
        {
            BordrMaxResTXT.Visibility = Visibility.Hidden;
        }
        private void HiddeWin_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimationService.FadeOut(BordrHiddeTXT, 0.5);
        }
        private async void HiddeWin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);
        }
        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void StatsTextOpen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();

            TextStatsGameMinecraft.Content = _gameSessionManager.GetFormattedStats();
            AnimationService.AnimatePageTransitionExit(PanelManegerAccount,-20,0.2);
            AnimationService.AnimatePageTransition(PanelListStats,20);
        }
        byte SelectModPackCreate = 0;
        private void VanilaPackIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SelectModPackCreate = 0;
            AnimationService.AnimateBorderObject(0, 0, SelectModPack, true);
        }

        private void ModPackIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SelectModPackCreate = 1;
            AnimationService.AnimateBorderObject(150, 0, SelectModPack, true);
        }

        private void BorderCountionCreatePack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            AnimationService.AnimatePageTransitionExit(SelectCreatePackMinecraft); AnimationService.AnimatePageTransitionExit(GirdFormAccountAdd);
            if (SelectModPackCreate == 1)
            {
                CreateModPackWindow createModPackWindow = new CreateModPackWindow(_modDownloadService,_modpackService);
                createModPackWindow.Show();
            }
            else
            {
                CreateVanilaPackWindow createVanilaPackWindow = new CreateVanilaPackWindow();
                createVanilaPackWindow.Show();
            }
        }

        private void YesQuestionTutorialButton_MouseDown(object sender, RoutedEventArgs e)
        {
            Settings1.Default.TutorialComplete = true;
            Settings1.Default.IsDocsTutorialShown = true;
            WebHelper.OpenUrl(@"https://wer-developers-organization.gitbook.io/cl-minecraft-launcher");
            Settings1.Default.Save();
            AnimationService.AnimatePageTransitionExit(TutorialBorder);
            AnimationService.AnimatePageTransitionExit(TutorialGrid);
        }

        private void NoQuestionTutorialButton_MouseDown(object sender, RoutedEventArgs e)
        {
            Settings1.Default.TutorialComplete = true;
            Settings1.Default.Save();
            AnimationService.AnimatePageTransitionExit(TutorialBorder);
            AnimationService.AnimatePageTransitionExit(TutorialGrid);
            ShowDocsTutorial(InfoLauncherPanel,null,-120);
        }
        private string currentScreenshotsPath;
        private async void LoadScreenshots()
        {
            ScreenshotsList.Items.Clear();
            NoScreenshotsText.Visibility = Visibility.Visible; 

            if (string.IsNullOrEmpty(currentScreenshotsPath)) return;

            var items = await _screenshotService.LoadScreenshotsAsync(currentScreenshotsPath);

            if (items.Count > 0)
            {
                NoScreenshotsText.Visibility = Visibility.Hidden;
                foreach (var item in items)
                {
                    ScreenshotsList.Items.Add(item);
                }
            }
        }

        public void InitializeGallery()
        {
            var sources = _screenshotService.GetScreenshotSources(Settings1.Default.PathLacunher);

            SourceSelector.ItemsSource = sources;
            SourceSelector.DisplayMemberPath = "Name";

            if (sources.Count > 0)
                SourceSelector.SelectedIndex = 0;
        }

        private void SourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceSelector.SelectedItem is ScreenshotSourceItem selectedSource)
            {
                currentScreenshotsPath = selectedSource.FullScreenshotsPath;
                LoadScreenshots();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentScreenshotsPath))
            {
                if (!Directory.Exists(currentScreenshotsPath)) Directory.CreateDirectory(currentScreenshotsPath);
                System.Diagnostics.Process.Start("explorer.exe", currentScreenshotsPath);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadScreenshots();
        }

        private void ScreenshotsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScreenshotsList.SelectedItem != null)
            {
                AnimationService.AnimatePageTransition(ActionPanel);
                var item = (ScreenshotItem)ScreenshotsList.SelectedItem;
                SelectedFileNameText.Text = item.FileName;
            }
            else
            {
                AnimationService.AnimatePageTransitionExit(ActionPanel);
            }
        }

        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            if (ScreenshotsList.SelectedItem is ScreenshotItem item)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ScreenshotsList.SelectedItem is ScreenshotItem item)
            {
                if (_screenshotService.DeleteScreenshot(item.FilePath))
                {
                    ScreenshotsList.Items.Remove(item);

                    if (ScreenshotsList.Items.Count == 0)
                        NoScreenshotsText.Visibility = Visibility.Visible;

                    AnimationService.AnimatePageTransitionExit(ActionPanel);
                }
                else
                {
                    MessageBox.Show("Не вдалося видалити файл.");
                }
            }
        }
        private void ShowDocsTutorial(FrameworkElement targetButton, double? customHeight = null, double verticalOffset = 0)
        {
            if (customHeight.HasValue)
            {
                TutorialMessageParams.Height = customHeight.Value;
            }
            else
            {
                TutorialMessageParams.Height = double.NaN; 
            }

            Point relativePoint = targetButton.TransformToAncestor(this)
                                              .Transform(new Point(0, 0));

            double padding = 5;
            OverlayHoleRect.Rect = new Rect(
                relativePoint.X - padding,
                relativePoint.Y - padding,
                targetButton.ActualWidth + (padding * 2),
                targetButton.ActualHeight + (padding * 2)
            );

            OverlayScreenRect.Rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);

            double finalY = relativePoint.Y + verticalOffset;

            double finalX = relativePoint.X - 310;

            if (relativePoint.X < 320)
            {
                finalX = relativePoint.X + targetButton.ActualWidth + 20;
            }

            TutorialMessageParams.Margin = new Thickness(finalX, finalY, 0, 0);

            TutorialOverlay.Visibility = Visibility.Visible;
            TutorialOverlay.Opacity = 0;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            TutorialOverlay.BeginAnimation(Grid.OpacityProperty, fadeIn);
        }
        private void CloseTutorial_Click(object sender, RoutedEventArgs e)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
            fadeOut.Completed += (s, a) =>
            {
                TutorialOverlay.Visibility = Visibility.Collapsed;

                Settings1.Default.IsDocsTutorialShown = true;
                Settings1.Default.Save();
            };

            TutorialOverlay.BeginAnimation(Grid.OpacityProperty, fadeOut);
        }
        private void LoadScreenBgButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleLoadScreenBackgroundClick();
        }

        private void LoadScreenColorButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleLoadScreenColorClick(LoadScreenColorButton);
        }

        private void EditPhrasesButton_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleEditPhrasesClick();
        }

        private void ResetLoadScreen_Click(object sender, RoutedEventArgs e)
        {
            _themeService.HandleResetLoadScreenClick(LoadScreenColorButton);
        }
        private void CopyLoadScreen_Click(object sender, RoutedEventArgs e)
        {
            string code = _themeService.ExportLoadScreen();
            LoadScreenCodeBox.Text = code;
            Clipboard.SetText(code);
            NotificationService.ShowNotification("Код LoadScreen скопійовано!", "Експорт", SnackbarPresenter, default, default, ControlAppearance.Info);
        }

        private void CopyTheme_Click(object sender, RoutedEventArgs e)
        {
            string code = _themeService.ExportMainTheme();
            ThemeCodeBox.Text = code;
            Clipboard.SetText(code);
            NotificationService.ShowNotification("Код теми скопійовано!", "Експорт",SnackbarPresenter,default,default, ControlAppearance.Info);
        }

        private void PasteTheme_Click(object sender, RoutedEventArgs e)
        {
            string code = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : ThemeCodeBox.Text.Trim();

            if (!string.IsNullOrEmpty(code))
            {
                _themeService.ImportMainTheme(code);
            }
        }
        private void PasteLoadScreen_Click(object sender, RoutedEventArgs e)
        {
            string code = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : LoadScreenCodeBox.Text.Trim();

            if (!string.IsNullOrEmpty(code))
            {
                _themeService.ImportLoadScreen(code);
            }
        }
        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.OnClosed(e);
        }
        private void TabRegularServer_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void TabModdedServer_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
