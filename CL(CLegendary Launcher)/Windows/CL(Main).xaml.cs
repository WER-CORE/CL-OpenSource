using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_
{
    public enum AccountType
    {
        Microsoft,
        LittleSkin,
        Offline
    }

    public partial class CL_Main_ : FluentWindow
    {
        byte VersionSelect = 0;
        public bool InstallVersionOnPlay = false;

        public AccountType selectAccountNow;
        public MSession session;

        public List<string> donateLink = new List<string>();
        public List<string> siteLink = new List<string>();
        public List<string> discordLink = new List<string>();
        private int _currentPage_server = 1;
        private int _itemsPerPage_servers = 5;
        private List<UIElement> _allServerCards = new List<UIElement>();
        private List<UIElement> _filteredServerCards = new List<UIElement>();

        private bool isSliderDragging = false;
        private double previousSliderValue = 2048;

        string VersionType = "Fabric";
        string SiteMods = "Modrinth";
        string ModType = "Collection";
        byte selectmodificed = 0;
        byte SelectModPackCreate = 0;

        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 10;
        private CancellationTokenSource _searchCts;
        private List<ModVersionInfo> _currentModVersions;
        private List<InstalledModpack> allInstalledModpacks = new List<InstalledModpack>();

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private TutorialOverlayService _tutorialService;
        private ScreenshotService _screenshotService;
        public ServerListService _serverListService;
        protected ProfileManagerService _profileManagerService;
        private AccountService _accountService;
        protected GameSessionManager _gameSessionManager;
        protected GameLaunchService _gameLaunchService;
        protected LastActionService _lastActionService;
        private ModDownloadService _modDownloadService;
        public ModpackService _modpackService;
        private ThemeService _themeService;
        private LauncherSettingsService _launcherSettingsService;
        private LauncherNavigationService _navigationService;
        public VersionService _versionService;
        private NewsService _newsService;

        private int _lastIndex = 0;
        private DispatcherTimer _slideTimer;
        public CL_Main_()
        {
            LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode);
            InitializeLanguagesAsync();

            InitializeComponent();
            CheckAndCreateDefaultPath();

            InitializeServices();
            InitializeModules();

            UpdateLocalization();

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            ApplicationThemeManager.Apply(this);
            _themeService.InitializeTheme();
            LoadCustomSettings();
            InitToggles();

            _serverListService.InitializeServersAsync(false, null);
            LoadProfilesAsync();

            _launcherSettingsService.Initialize();

            InitializeThemeSelection();
            _slideTimer = new DispatcherTimer();
            _slideTimer.Interval = TimeSpan.FromSeconds(10);
            _slideTimer.Tick += (s, e) => NextIndex();
            _slideTimer.Start();
        }
        public void InitializeServices()
        {
            _versionService = new VersionService(SettingsManager.Default.PathLacunher);

            _tutorialService = new TutorialOverlayService(
                this, TutorialOverlay, OverlayHoleRect, OverlayScreenRect, TutorialMessageParams, TutorialTitleText, TutorialBodyText
            );

            _themeService = new ThemeService(this);
            _newsService = new NewsService();
            _navigationService = new LauncherNavigationService(this);
            _launcherSettingsService = new LauncherSettingsService(this);
            _profileManagerService = new ProfileManagerService();
            _accountService = new AccountService(_profileManagerService);
            _gameSessionManager = new GameSessionManager();
            _serverListService = new ServerListService(this);
            _lastActionService = new LastActionService(this);
            _gameLaunchService = new GameLaunchService(this, _gameSessionManager, _lastActionService);
            _modDownloadService = new ModDownloadService();

            _modpackService = new ModpackService(this, _gameSessionManager, _gameLaunchService, _modDownloadService);
            _screenshotService = new ScreenshotService();
        }

        private void CheckAndCreateDefaultPath()
        {
            if (string.IsNullOrWhiteSpace(SettingsManager.Default.PathLacunher))
            {
                string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");
                if (!Directory.Exists(defaultPath))
                {
                    Directory.CreateDirectory(defaultPath);
                }
                SettingsManager.Default.PathLacunher = defaultPath;
                SettingsManager.Save();
            }
        }
        private async void MainTitleBar_MinimizeClicked(TitleBar sender, System.Windows.RoutedEventArgs args)
        {
            await MemoryCleaner.FlushMemoryAsync(true);
        }
    }
}