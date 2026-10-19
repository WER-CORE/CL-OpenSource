using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views;

public partial class CL_Main : Window
{
    public LauncherNavigationService NavigationService { get; }
    public TutorialTourManager TourManager => _tourManager;
    private CL_MainViewModel? _boundVm;
    private readonly AvaloniaThemeService? _themeService;
    private readonly TutorialOverlayService _tutorialService;
    private readonly TutorialTourManager _tourManager;
    private Control? _activeTabBorder;
    private bool _isProfilesLoaded;

    public CL_Main()
    {
        InitializeComponent();

        var sp = (Application.Current as App)?.ServiceProvider;
        _themeService = sp?.GetService(typeof(AvaloniaThemeService)) as AvaloniaThemeService;

        if (DataContext is not CL_MainViewModel)
        {
            DataContext = sp?.GetService(typeof(CL_MainViewModel)) as CL_MainViewModel ?? new CL_MainViewModel();
        }

        NavigationService = new LauncherNavigationService(this);
        _tutorialService = new TutorialOverlayService(
            TutorialOverlay,
            TutorialOverlayPath,
            TutorialMessageParams,
            TutorialTitleText,
            TutorialBodyText,
            TutorialStepCounter,
            TutorialTourButtonsPanel,
            TutorialBackBtn,
            TutorialNextBtn,
            TutorialFinishBtn,
            TutorialSkipBtn,
            Loc_BtnUnderstood
        );
        _tourManager = new TutorialTourManager(this, _tutorialService);

        if (_themeService != null)
        {
            _themeService.BackgroundBitmapChanged += OnBackgroundBitmapChanged;
            _themeService.InitializeSavedTheme();
            if (Bg != null)
            {
                Bg.Source = _themeService.CurrentBackgroundBitmap;
                Bg.IsVisible = _themeService.CurrentBackgroundBitmap != null;
            }
        }

        SettingsPageViewModel.ModulesConfigurationChanged += OnModulesConfigurationChanged;
        SettingsPageViewModel.UiScaleConfigurationChanged += OnUiScaleConfigurationChanged;
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
        ModpacksPageViewModel.ExportProgressChanged += OnExportProgressChanged;

        if (TopMenuStackPanel != null)
        {
            TopMenuStackPanel.SizeChanged += TopMenuStackPanel_SizeChanged;
        }

        Opened += (_, _) =>
        {
            _activeTabBorder = PlayBtnBorder;
            ApplyModulesVisibility();
            ApplyLocalization();
            ApplyCurrentUiScale();

            Dispatcher.UIThread.Post(() => MoveSlider(PlayBtnBorder, animate: false), DispatcherPriority.Loaded);
            NavigationService.NavigateToHome();
            if (!_isProfilesLoaded)
            {
                _ = RefreshProfilesUIAsync(selectSavedProfile: true);
            }

            if (!SettingsManager.Default.TutorialComplete)
            {
                TutorialGrid.IsVisible = true;
            }
        };

        Closed += (_, _) =>
        {
            if (TopMenuStackPanel != null)
                TopMenuStackPanel.SizeChanged -= TopMenuStackPanel_SizeChanged;

            if (_themeService != null)
                _themeService.BackgroundBitmapChanged -= OnBackgroundBitmapChanged;

            SettingsPageViewModel.ModulesConfigurationChanged -= OnModulesConfigurationChanged;
            SettingsPageViewModel.UiScaleConfigurationChanged -= OnUiScaleConfigurationChanged;
            SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
            ModpacksPageViewModel.ExportProgressChanged -= OnExportProgressChanged;
            DiscordController.Deinitialize();
        };
    }

    private void OnExportProgressChanged(bool isVisible, string text, double pct)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ExportProgressOverlay != null)
            {
                ExportProgressOverlay.IsVisible = isVisible;
            }
            if (ExportProgressText != null && !string.IsNullOrEmpty(text))
            {
                ExportProgressText.Text = text;
            }
            if (ExportProgressBar != null)
            {
                ExportProgressBar.Value = pct;
            }
        });
    }

    private void OnBackgroundBitmapChanged(Bitmap? newBitmap)
    {
        if (Bg != null)
        {
            Bg.Source = newBitmap;
            Bg.IsVisible = newBitmap != null;
        }
    }

    private void OnModulesConfigurationChanged()
    {
        Dispatcher.UIThread.Post(ApplyModulesVisibility);
    }

    private void OnUiScaleConfigurationChanged(double newScale)
    {
        Dispatcher.UIThread.Post(ApplyCurrentUiScale);
    }

    private void OnLanguageChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ApplyLocalization();
            Dispatcher.UIThread.Post(() => MoveSlider(_activeTabBorder ?? PlayBtnBorder, animate: false), DispatcherPriority.Loaded);
        });
    }

    public void ApplyModulesVisibility()
    {
        var s = SettingsManager.Default;

        if (ModpacksBtnBorder != null) ModpacksBtnBorder.IsVisible = s.EnableModpacks;
        if (ModsBtnBorder != null) ModsBtnBorder.IsVisible = s.EnableMods;
        if (ServersBtnBorder != null) ServersBtnBorder.IsVisible = s.EnableServers;
        if (GalleryBtnBorder != null) GalleryBtnBorder.IsVisible = s.EnableGallery;
        if (StatsTextOpen != null) StatsTextOpen.IsVisible = s.EnableMod_Statistics;

        if (_activeTabBorder is { IsVisible: false })
        {
            _activeTabBorder = PlayBtnBorder;
        }

        Dispatcher.UIThread.Post(() => MoveSlider(_activeTabBorder ?? PlayBtnBorder, animate: false), DispatcherPriority.Loaded);
    }

    public void ApplyCurrentUiScale()
    {
        if (_boundVm == null) return;
        double savedScale = SettingsManager.Default.UIScale > 0.1 ? SettingsManager.Default.UIScale : 1.0;
        bool isExpanded = WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen;
        _boundVm.UiScale = isExpanded ? savedScale : 1.0;
    }

    public void ApplyLocalization()
    {
        if (Loc_MenuBtnMain != null) Loc_MenuBtnMain.Text = LocalizationManager.GetString("Sidebar.Main", "Головна");
        if (Loc_MenuBtnModpacks != null) Loc_MenuBtnModpacks.Text = LocalizationManager.GetString("Sidebar.Modpacks", "Збірки");
        if (Loc_MenuBtnMods != null) Loc_MenuBtnMods.Text = LocalizationManager.GetString("Sidebar.Mods", "Моди");
        if (Loc_MenuBtnServers != null) Loc_MenuBtnServers.Text = LocalizationManager.GetString("Sidebar.Servers", "Сервери");
        if (Loc_MenuBtnGallery != null) Loc_MenuBtnGallery.Text = LocalizationManager.GetString("Sidebar.Gallery", "Галерея");

        if (PlayBtnBorder != null) ToolTip.SetTip(PlayBtnBorder, LocalizationManager.GetString("Sidebar.ToolTipMain", "Головна сторінка"));
        if (ModpacksBtnBorder != null) ToolTip.SetTip(ModpacksBtnBorder, LocalizationManager.GetString("Sidebar.ToolTipModpacks", "Створити або імпортувати збірку"));
        if (ModsBtnBorder != null) ToolTip.SetTip(ModsBtnBorder, LocalizationManager.GetString("Sidebar.ToolTipMods", "Завантаження модів"));
        if (ServersBtnBorder != null) ToolTip.SetTip(ServersBtnBorder, LocalizationManager.GetString("Sidebar.ToolTipServers", "Список серверів"));
        if (GalleryBtnBorder != null) ToolTip.SetTip(GalleryBtnBorder, LocalizationManager.GetString("Sidebar.ToolTipGallery", "Скріншоти"));

        if (BackIconServerList != null) ToolTip.SetTip(BackIconServerList, LocalizationManager.GetString("Generic.Back", "Повернутись"));

        if (AddProfile != null) AddProfile.Text = LocalizationManager.GetString("Accounts.AddProfile", "Додати профіль +");
        if (StatsTextOpen != null) StatsTextOpen.Text = LocalizationManager.GetString("Accounts.Stats", "Статистика");
        if (Loc_TxtOfflineAccountTitle != null) Loc_TxtOfflineAccountTitle.Text = LocalizationManager.GetString("Accounts.OfflineTitle", "Офлайн акаунт");
        if (NameNikManeger != null) NameNikManeger.Watermark = LocalizationManager.GetString("Accounts.PlaceholderNickname", "Нікнейм...");
        if (CreateAccount_Offline != null) CreateAccount_Offline.Content = LocalizationManager.GetString("Accounts.CreateBtn", "Створити");
        if (Loc_TxtLittleSkinTitle != null) Loc_TxtLittleSkinTitle.Text = LocalizationManager.GetString("Accounts.LittleSkinTitle", "LittleSkin акаунт");
        if (Login_LittleSkin != null) Login_LittleSkin.Watermark = LocalizationManager.GetString("Accounts.PlaceholderEmail", "Пошта...");
        if (PasswordLittleSkin != null) PasswordLittleSkin.Watermark = LocalizationManager.GetString("Accounts.PlaceholderPassword", "Пароль...");
        if (NoAccount_ != null) NoAccount_.Content = LocalizationManager.GetString("Accounts.NoLittleSkinAccount", "Немає акаунту?");
        if (LoginAccountLittleSkin != null) LoginAccountLittleSkin.Content = LocalizationManager.GetString("Accounts.LoginBtn", "Увійти");
        if (Loc_TxtMicrosoftTitle != null) Loc_TxtMicrosoftTitle.Text = LocalizationManager.GetString("Accounts.MicrosoftTitle", "Microsoft-онлайн акаунт");
        if (CreateAccount_Online != null) CreateAccount_Online.Content = LocalizationManager.GetString("Accounts.LoginMicrosoftBtn", "Увійти через Microsoft");

        if (Loc_TxtFundTitle != null) Loc_TxtFundTitle.Text = LocalizationManager.GetString("Fundraisers.Title", "АКТУАЛЬНІ ЗБОРИ");
        if (NoActiveFundsTXT != null) NoActiveFundsTXT.Text = LocalizationManager.GetString("Fundraisers.NoActive", "Наразі немає активних зборів.");

        if (TutorialHelloText != null) TutorialHelloText.Text = LocalizationManager.GetString("Tutorial.Hello", "Привіт, я Сіель");
        if (QuestionTXT != null) QuestionTXT.Text = LocalizationManager.GetString("Tutorial.Question", "Я підготувала короткий посібник по лаунчеру. Пройдемося разом?");
        if (NoQuestionTutorialButton != null) NoQuestionTutorialButton.Content = LocalizationManager.GetString("Tutorial.NoThanks", "Ні, дякую");
        if (YesQuestionTutorialButton != null) YesQuestionTutorialButton.Content = LocalizationManager.GetString("Tutorial.YesPlease", "Давай");
        if (TutorialTitleText != null) TutorialTitleText.Text = LocalizationManager.GetString("Tutorial.OverlayTitle", "Довідка тут");
        if (TutorialBodyText != null) TutorialBodyText.Text = LocalizationManager.GetString("Tutorial.OverlayBody", "Документація завжди під рукою. Знайти її можна у будь-який момент через меню «Інформація» > «Документація».");
        if (Loc_BtnUnderstood != null) Loc_BtnUnderstood.Content = LocalizationManager.GetString("Tutorial.UnderstoodBtn", "Зрозуміло");
        if (TutorialBackBtn != null) TutorialBackBtn.Content = LocalizationManager.GetString("TutorialTour.BtnBack", "Назад");
        if (TutorialNextBtn != null) TutorialNextBtn.Content = LocalizationManager.GetString("TutorialTour.BtnNext", "Далі");
        if (TutorialFinishBtn != null) TutorialFinishBtn.Content = LocalizationManager.GetString("TutorialTour.BtnFinish", "Завершити");
        if (TutorialSkipBtn != null) TutorialSkipBtn.Content = LocalizationManager.GetString("TutorialTour.BtnSkip", "Пропустити");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            ApplyCurrentUiScale();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _boundVm = DataContext as CL_MainViewModel;

        if (_boundVm != null)
        {
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;

            if (!string.IsNullOrEmpty(_boundVm.CurrentAccountName))
                NameNik.Text = _boundVm.CurrentAccountName;

            IconAccount.Source = _boundVm.CurrentAccountAvatar;

            _ = RefreshProfilesUIAsync(selectSavedProfile: true);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundVm == null) return;

        switch (e.PropertyName)
        {
            case nameof(CL_MainViewModel.CurrentAccountName):
                Dispatcher.UIThread.Post(() =>
                {
                    if (NameNik != null)
                        NameNik.Text = _boundVm.CurrentAccountName;
                });
                break;

            case nameof(CL_MainViewModel.CurrentAccountAvatar):
                Dispatcher.UIThread.Post(() =>
                {
                    if (IconAccount != null)
                        IconAccount.Source = _boundVm.CurrentAccountAvatar;
                });
                break;

            case nameof(CL_MainViewModel.WindowState):
                Dispatcher.UIThread.Post(() =>
                {
                    WindowState = _boundVm.WindowState;
                    ApplyCurrentUiScale();
                });
                break;
        }
    }

    #region Навігація та Верхній Слайдер

    public void SetActiveTab(Control? target, bool animate = true)
    {
        if (_activeTabBorder == target && PanelSelectNow != null && PanelSelectNow.Width > 0) return;
        _activeTabBorder = target;
        MoveSlider(target, animate);
    }

    private void TopMenuStackPanel_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => MoveSlider(_activeTabBorder ?? PlayBtnBorder, animate: false), DispatcherPriority.Loaded);
    }

    private void MoveSlider(Control? target, bool animate = true)
    {
        if (target == null || SelectPanelGrid == null || PanelSelectNow == null) return;

        var pt = target.TranslatePoint(new Point(0, 0), SelectPanelGrid);
        if (pt == null || target.Bounds.Width <= 0)
        {
            Dispatcher.UIThread.Post(() => MoveSlider(target, animate), DispatcherPriority.Loaded);
            return;
        }

        double targetX = pt.Value.X;
        double newWidth = target.Bounds.Width;

        if (PanelSelectNow.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            PanelSelectNow.RenderTransform = transform;
        }

        AvaloniaAnimationService.AnimateTabIndicator(PanelSelectNow, transform, targetX, newWidth, animate);
    }

    private void NavigateWithSlider(object? sender, Action navigateAction)
    {
        AvaloniaSoundService.Click();
        if (sender is Control target)
        {
            SetActiveTab(target, animate: true);
        }
        navigateAction();
    }

    private void PlayTXTPanelSelect_MouseDown(object? sender, PointerPressedEventArgs e) =>
        NavigateWithSlider(sender, NavigationService.NavigateToHome);

    private void modbuilds_MouseDown(object? sender, PointerPressedEventArgs e) =>
        NavigateWithSlider(sender, NavigationService.NavigateToModPacks);

    private void ModsTXTPanelSelect_MouseDown(object? sender, PointerPressedEventArgs e) =>
        NavigateWithSlider(sender, NavigationService.NavigateToMods);

    private void ServerTXTPanelSelect_MouseDown(object? sender, PointerPressedEventArgs e) =>
        NavigateWithSlider(sender, NavigationService.NavigateToServers);

    private void PhotoMinecraftTXT_MouseDown(object? sender, PointerPressedEventArgs e) =>
        NavigateWithSlider(sender, NavigationService.NavigateToGallery);

    #endregion

    #region Керування Вікном

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    private async void MinimizeWindow_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        if (_boundVm != null)
            await _boundVm.OnWindowMinimizedAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.Key == Key.F11 || e.Key == Key.F12) && _boundVm != null)
        {
            _boundVm.ToggleFullScreenCommand.Execute(null);
        }
    }

    private void MenuButton_MouseEnter(object? sender, PointerEventArgs e) { }
    private void MenuButton_MouseLeave(object? sender, PointerEventArgs e) { }
    private void BackIconServerList_MouseDown(object? sender, PointerPressedEventArgs e) { }

    #endregion

    #region Менеджер Акаунтів та Статистика

    private async Task RefreshProfilesUIAsync(bool selectSavedProfile = false)
    {
        if (_boundVm == null) return;
        _isProfilesLoaded = true;

        var profiles = await _boundVm.LoadProfilesAsync();

        if (profiles.Count > 0 && (selectSavedProfile || _boundVm.CurrentProfile == null))
        {
            int savedIndex = SettingsManager.Default.SelectIndexAccount;
            var targetProfile = (savedIndex >= 0 && savedIndex < profiles.Count)
                ? profiles[savedIndex]
                : profiles[0];

            await _boundVm.SelectProfileAsync(targetProfile);
        }
    }

    private async void IconAccount_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            AvaloniaSoundService.Click();

            if (PanelManegerAccount.IsVisible)
            {
                AvaloniaAnimationService.AnimateRotation(CheckMarkAccount, 0);
                AvaloniaAnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20);
            }
            else
            {
                AvaloniaAnimationService.AnimateRotation(CheckMarkAccount, 180);
                AvaloniaAnimationService.AnimatePageTransition(PanelManegerAccount, -20);

                var panelListStats = this.FindControl<Control>("PanelListStats");
                if (panelListStats is { IsVisible: true })
                {
                    AvaloniaAnimationService.AnimatePageTransitionExit(panelListStats, 20, 0.2);
                }

                await RefreshProfilesUIAsync(selectSavedProfile: false);
            }
        }
        catch (Exception ex)
        {
            if (_boundVm?.DialogService != null)
            {
                await _boundVm.DialogService.ShowMessageAsync(
                    string.Format(LocalizationManager.GetString("Accounts.MenuError", "Помилка меню акаунтів: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
        }
    }

    public void AddProfile_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        AvaloniaAnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20, 0.2);
        AvaloniaAnimationService.AnimateRotation(CheckMarkAccount, 0);

        AvaloniaAnimationService.AnimatePageTransition(GridFormAccountAdd);
        AvaloniaAnimationService.AnimatePageTransition(GridOfflineMode);
        AvaloniaAnimationService.AnimatePageTransition(GridSelectAccountType);
    }

    public void GirdFormAccountAdd_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        CloseAccountSelectionUI();
    }

    private void CloseAccountSelectionUI()
    {
        AvaloniaAnimationService.AnimatePageTransitionExit(GridOfflineMode, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridOnlineMode, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridFormAccountAdd, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridSelectAccountType, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridLittleSkinMode, default, 0.2);
    }

    public void MicrosoftAccount_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        SwitchAccountAddTab(GridOnlineMode);
    }

    public void OfflineAccount_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        SwitchAccountAddTab(GridOfflineMode);
    }

    public void LittleSkinAccount_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        SwitchAccountAddTab(GridLittleSkinMode);
    }

    private async void SwitchAccountAddTab(Control targetGrid)
    {
        AvaloniaAnimationService.AnimatePageTransitionExit(GridLittleSkinMode, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridOfflineMode, default, 0.2);
        AvaloniaAnimationService.AnimatePageTransitionExit(GridOnlineMode, default, 0.2);

        await Task.Delay(250);
        AvaloniaAnimationService.AnimatePageTransition(targetGrid);
    }

    public async void CreateAccount_Offline_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm == null || string.IsNullOrWhiteSpace(NameNikManeger.Text)) return;

        var newProfile = await _boundVm.AddOfflineAccountAsync(NameNikManeger.Text);
        if (newProfile != null)
        {
            NameNikManeger.Text = null;
            CloseAccountSelectionUI();
            await _boundVm.SelectProfileAsync(newProfile);
            await RefreshProfilesUIAsync();
        }
    }

    public async void LoginAccountLittleSkin_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm == null) return;

        var loginSkin = this.FindControl<TextBox>("Login_LittleSkin");
        var passwordSkin = this.FindControl<TextBox>("PasswordLittleSkin");
        if (loginSkin == null || passwordSkin == null) return;

        var newProfile = await _boundVm.AddLittleSkinAccountAsync(loginSkin.Text, passwordSkin.Text);
        if (newProfile != null)
        {
            loginSkin.Text = null;
            passwordSkin.Text = null;
            CloseAccountSelectionUI();
            await _boundVm.SelectProfileAsync(newProfile);
            await RefreshProfilesUIAsync();
        }
    }

    public async void MicrosoftLoginButton_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        if (_boundVm == null) return;

        var newProfile = await _boundVm.AddMicrosoftAccountAsync();
        if (newProfile != null)
        {
            CloseAccountSelectionUI();
            await _boundVm.SelectProfileAsync(newProfile);
            await RefreshProfilesUIAsync();
        }
    }

    private void StatsTextOpen_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();

        if (_boundVm != null)
        {
            var textStats = this.FindControl<TextBlock>("TextStatsGameMinecraft");
            if (textStats != null)
                textStats.Text = _boundVm.GetFormattedStats();
        }

        AvaloniaAnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20, 0.2);
        AvaloniaAnimationService.AnimateRotation(CheckMarkAccount, 0);

        var panelListStats = this.FindControl<Control>("PanelListStats");
        if (panelListStats != null)
            AvaloniaAnimationService.AnimatePageTransition(panelListStats, 20);
    }

    #endregion

    #region Оверлеї Зборів та Туторіалу

    public void FundraiserOverlay_MouseDown(object? sender, PointerPressedEventArgs e) =>
        FundraiserOverlay.IsVisible = false;

    public void FundraiserCard_MouseDown(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    public void CloseFundraiser_Click(object? sender, RoutedEventArgs e) =>
        FundraiserOverlay.IsVisible = false;

    public void BtnFund_Click(object? sender, RoutedEventArgs e) => OpenButtonTagUrl(sender);
    public void BtnDetails_Click(object? sender, RoutedEventArgs e) => OpenButtonTagUrl(sender);

    private static void OpenButtonTagUrl(object? sender)
    {
        if (sender is Button { Tag: string url } && !string.IsNullOrEmpty(url))
        {
            WebHelper.OpenUrl(url);
        }
    }

    public void NoQuestionTutorialButton_MouseDown(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Default.TutorialComplete = true;
        SettingsManager.Save();
        TutorialGrid.IsVisible = false;
        ShowDocsTutorial();
    }

    public void YesQuestionTutorialButton_MouseDown(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Default.TutorialComplete = true;
        SettingsManager.Default.IsDocsTutorialShown = true;
        SettingsManager.Save();
        TutorialGrid.IsVisible = false;
        _tourManager.StartTour();
    }

    public void CloseTutorial_Click(object? sender, RoutedEventArgs e)
    {
        _tutorialService.CloseTutorial(() =>
        {
            SettingsManager.Default.IsDocsTutorialShown = true;
            SettingsManager.Save();
        });
    }

    public void TutorialOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_tourManager.IsTourActive)
        {
            return;
        }
        CloseTutorial_Click(sender, e);
    }

    public void TutorialBackBtn_Click(object? sender, RoutedEventArgs e) =>
        _tourManager.PreviousStep();

    public void TutorialNextBtn_Click(object? sender, RoutedEventArgs e) =>
        _tourManager.NextStep();

    public void TutorialFinishBtn_Click(object? sender, RoutedEventArgs e) =>
        _tourManager.FinishTour();

    public void TutorialSkipBtn_Click(object? sender, RoutedEventArgs e) =>
        _tourManager.SkipTour();

    public void StartOnboardingTour() =>
        _tourManager.StartTour();

    public void ShowDocsTutorial()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (PageHost.Content is HomePage homePage)
            {
                var target = homePage.FindControl<Control>("InfoLauncherPanel") ?? homePage.FindControl<Control>("Loc_InfoMenu");
                if (target != null)
                {
                    _tutorialService.ShowTutorial(
                        target,
                        LocalizationManager.GetString("Tutorial.OverlayTitle", "Довідка тут"),
                        LocalizationManager.GetString("Tutorial.OverlayBody", "Документація завжди під рукою. Знайти її можна у будь-який момент через меню «Інформація» > «Документація»."),
                        -120
                    );
                }
            }
        }, DispatcherPriority.Loaded);
    }

    public void ShowForgeTutorial()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(400);
            if (PageHost.Content is ModPacksPage modPacksPage)
            {
                var target = modPacksPage.FindControl<Control>("DowloadModPacks") ?? modPacksPage.FindControl<Control>("CreateModPacksTXT");
                if (target != null)
                {
                    _tutorialService.ShowTutorial(
                        target,
                        LocalizationManager.GetString("Mods.WhereIsForgeTitle", "Де знайти Forge та інші ядра"),
                        LocalizationManager.GetString("Mods.WhereIsForgeDesc", "Модифіковані версії (Forge, Fabric та інші) створюються як окремі збірки. Це захищає клієнт від збоїв і конфліктів між модами.\n\nНатисни кнопку «Створити», щоб додати потрібну збірку."),
                        -120
                    );
                }
            }
        }, DispatcherPriority.Loaded);
    }

    public void CloseTutorial()
    {
        _tutorialService.CloseTutorial();
    }

    #endregion
}
