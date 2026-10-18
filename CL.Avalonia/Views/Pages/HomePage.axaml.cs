using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Models;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CL_CLegendary_Launcher_.Avalonia.Views;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;
using Newtonsoft.Json;
using Path = Avalonia.Controls.Shapes.Path;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class HomePage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<HomePage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? NewsRequested;
    public event EventHandler? ModPacksRequested;

    private DispatcherTimer? _slideTimer;
    private int _lastIndex = -1;

    private static readonly IBrush PressedButtonBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(255, 30, 30, 30), 0),
            new GradientStop(Color.FromArgb(255, 50, 50, 50), 1)
        }
    };

    private static readonly IBrush PressedLineBrush = new SolidColorBrush(Color.FromArgb(255, 10, 60, 10));

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is HomePageViewModel vm)
        {
            vm.PartnerCards.CollectionChanged += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (PartnerServer != null && PartnerServer.ItemCount > 0 && PartnerServer.SelectedIndex < 0)
                    {
                        PartnerServer.SelectedIndex = 0;
                        _lastIndex = 0;
                    }
                    StartSlideTimerIfReady();
                });
            };
        }
    }

    private void RestoreSavedLoaderIcon()
    {
        int savedType = SettingsManager.Default.LastSelectedType;
        if (savedType == 1 && IconSelectVersion_Copy?.Source != null)
        {
            IconSelectVersion.Source = IconSelectVersion_Copy.Source;
        }
        else if (savedType == 5 && IconSelectVersion_Copy2?.Source != null)
        {
            IconSelectVersion.Source = IconSelectVersion_Copy2.Source;
        }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        SettingsPageViewModel.ModulesConfigurationChanged += OnModulesChanged;
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;

        ApplyModulesVisibility();
        ApplyLocalization();

        if (DataContext is HomePageViewModel homeVm)
        {
            _ = homeVm.EnsureAccountSelectedAsync();
        }

        RestoreSavedLoaderIcon();
        StartSlideTimerIfReady();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SettingsPageViewModel.ModulesConfigurationChanged -= OnModulesChanged;
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
        _slideTimer?.Stop();
    }

    private void OnModulesChanged() => Dispatcher.UIThread.Post(ApplyModulesVisibility);
    private void OnLanguageChanged() => Dispatcher.UIThread.Post(ApplyLocalization);

    public void ApplyModulesVisibility()
    {
        var s = SettingsManager.Default;

        if (ChangelogHeaderPanel != null) ChangelogHeaderPanel.IsVisible = s.EnableMod_Changelog;
        if (VersionMinecraftChangeLog != null) VersionMinecraftChangeLog.IsVisible = s.EnableMod_Changelog;

        if (LatestActionsHeaderPanel != null) LatestActionsHeaderPanel.IsVisible = s.EnableMod_LatestActions;
        if (ServerMonitoring != null) ServerMonitoring.IsVisible = s.EnableMod_LatestActions;

        if (Loc_MenuOpenFolder != null) Loc_MenuOpenFolder.IsVisible = s.EnableSubFiles;
        if (Loc_MenuBackupsFolder != null) Loc_MenuBackupsFolder.IsVisible = s.EnableSubFiles_Backups;
        if (FilesMenuGrid != null) FilesMenuGrid.IsVisible = s.EnableSubFiles || s.EnableSubFiles_Backups;

        if (Loc_MenuReportBug != null) Loc_MenuReportBug.IsVisible = s.EnableSubInfo_Bug;
        if (Loc_MenuLauncherNews != null) Loc_MenuLauncherNews.IsVisible = s.EnableSubInfo_News;
        if (Loc_MenuWiki != null) Loc_MenuWiki.IsVisible = s.EnableSubInfo_Wiki;
        if (Loc_MenuGithub != null) Loc_MenuGithub.IsVisible = s.EnableSubInfo_Github;
        if (InfoSep1 != null) InfoSep1.IsVisible = s.EnableSubInfo_Github;
        if (Loc_MenuCredits != null) Loc_MenuCredits.IsVisible = s.EnableSubInfo_Credits;
        if (InfoSep2 != null) InfoSep2.IsVisible = s.EnableSubInfo_Credits;
        if (Loc_MenuSupportProject != null) Loc_MenuSupportProject.IsVisible = s.EnableSubInfo_Support;
        if (Loc_MenuSupport3OSHBr != null) Loc_MenuSupport3OSHBr.IsVisible = s.EnableSubInfo_Support;
        if (InfoSep3 != null) InfoSep3.IsVisible = s.EnableSubInfo_Support;

        bool anyInfoEnabled = s.EnableSubInfo_Bug || s.EnableSubInfo_News || s.EnableSubInfo_Wiki ||
                              s.EnableSubInfo_Github || s.EnableSubInfo_Credits || s.EnableSubInfo_Support;
        if (InfoPanel != null) InfoPanel.IsVisible = anyInfoEnabled;
    }

    public void ApplyLocalization()
    {
        if (DevLogMinecraft != null) DevLogMinecraft.Text = LocalizationManager.GetString("MainScreen.Changelog", "Що нового? (Changelog)");
        if (VersionMinecraftChangeLog != null) ToolTip.SetTip(VersionMinecraftChangeLog, LocalizationManager.GetString("MainScreen.ToolTipChangelog", "Натисніть щоб прочитати список змін"));
        if (CheckMarkVersionSelect != null) ToolTip.SetTip(CheckMarkVersionSelect, LocalizationManager.GetString("MainScreen.ToolTipVersionSelect", "Натисніть щоб дізнатися які версії доступні"));

        if (Loc_SettingsMenu != null) Loc_SettingsMenu.Text = LocalizationManager.GetString("Sidebar.Settings", "Налаштування");
        if (Loc_FilesMenu != null) Loc_FilesMenu.Text = LocalizationManager.GetString("Sidebar.Files", "Файли та Бекапи");
        if (Loc_InfoMenu != null) Loc_InfoMenu.Text = LocalizationManager.GetString("Sidebar.Info", "Інформація");

        if (Loc_PartnersTitle != null) Loc_PartnersTitle.Text = LocalizationManager.GetString("MainScreen.Partners", "Партнери");
        if (PartherHelper != null) PartherHelper.Content = LocalizationManager.GetString("MainScreen.HowToJoin", "Як потрапити?");
        if (LatestActionsTXT != null) LatestActionsTXT.Text = LocalizationManager.GetString("MainScreen.LatestActions", "Останні дії");
        if (Loc_InstallModsTxt != null) Loc_InstallModsTxt.Text = LocalizationManager.GetString("Mods.InstallModsTxt", "Встановити...");

        if (Loc_MenuOpenFolder != null) Loc_MenuOpenFolder.Header = LocalizationManager.GetString("Context.OpenFolder", "Відкрити папку...");
        if (Loc_MenuRootFolder != null) Loc_MenuRootFolder.Header = LocalizationManager.GetString("Context.RootFolder", "Коренева папка (Лаунчер)");
        if (Loc_MenuGameFolder != null) Loc_MenuGameFolder.Header = LocalizationManager.GetString("Context.GameFolder", "Папка гри (Minecraft)");
        if (Loc_MenuModsFolder != null) Loc_MenuModsFolder.Header = LocalizationManager.GetString("Context.ModsFolder", "Моди (mods)");
        if (Loc_MenuResourcePacksFolder != null) Loc_MenuResourcePacksFolder.Header = LocalizationManager.GetString("Context.ResourcePacksFolder", "Ресурспаки (resourcepacks)");
        if (Loc_MenuShadersFolder != null) Loc_MenuShadersFolder.Header = LocalizationManager.GetString("Context.ShadersFolder", "Шейдери (shaderpacks)");
        if (Loc_MenuModpacksFolder != null) Loc_MenuModpacksFolder.Header = LocalizationManager.GetString("Context.ModpacksFolder", "Збірки (CLModpack)");
        if (Loc_MenuBackupsFolder != null) Loc_MenuBackupsFolder.Header = LocalizationManager.GetString("Context.BackupsFolder", "Менеджер світів (Backups)");

        if (Loc_MenuReportBug != null) Loc_MenuReportBug.Header = LocalizationManager.GetString("Context.ReportBug", "Повідомити про баг");
        if (Loc_MenuLauncherNews != null) Loc_MenuLauncherNews.Header = LocalizationManager.GetString("Context.LauncherNews", "Новини запускача");
        if (Loc_MenuWiki != null) Loc_MenuWiki.Header = LocalizationManager.GetString("Context.Wiki", "Документація (Wiki)");
        if (Loc_MenuGithub != null) Loc_MenuGithub.Header = LocalizationManager.GetString("Context.Github", "GitHub (Код)");
        if (Loc_MenuCredits != null) Loc_MenuCredits.Header = LocalizationManager.GetString("Context.Credits", "Подяки (Автори та Перекладачі)");
        if (Loc_MenuSupportProject != null) Loc_MenuSupportProject.Header = LocalizationManager.GetString("Context.SupportProject", "Підтримати проект");
        if (Loc_MenuSupportBrigade != null) Loc_MenuSupportBrigade.Text = LocalizationManager.GetString("Context.SupportBrigade", "Підтримати 3 ОШБр");

        if (Relesed != null) Relesed.Content = LocalizationManager.GetString("Versions.Release", "Реліз");
        if (Snapshots != null) Snapshots.Content = LocalizationManager.GetString("Versions.Snapshot", "Снапшоти");
        if (Beta != null) Beta.Content = LocalizationManager.GetString("Versions.Beta", "Бета");
        if (Alpha != null) Alpha.Content = LocalizationManager.GetString("Versions.Alpha", "Альфа");

        if (SearchSystemTXT1 != null) SearchSystemTXT1.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Шукайте версію...");
        if (SearchSystemTXT2 != null) SearchSystemTXT2.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Шукайте ванільну версію...");

        (DataContext as HomePageViewModel)?.RestoreSavedVersionState();
    }

    private static T? GetService<T>() where T : class
    {
        var app = Application.Current as App;
        return app?.ServiceProvider?.GetService(typeof(T)) as T;
    }

    #region Навігація та Меню Файлів

    public void SettingPanel_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    public void FolderPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FilesMenuGrid != null)
        {
            FlyoutBase.ShowAttachedFlyout(FilesMenuGrid);
            e.Handled = true;
        }
    }

    public void InfoPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (InfoPanel != null)
        {
            FlyoutBase.ShowAttachedFlyout(InfoPanel);
            e.Handled = true;
        }
    }

    public void OpenRootFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(AppDomain.CurrentDomain.BaseDirectory);

    public void OpenMinecraftFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(GetBaseMinecraftPath());

    public void OpenModsFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(System.IO.Path.Combine(GetBaseMinecraftPath(), "mods"));

    public void OpenResourcePacksFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(System.IO.Path.Combine(GetBaseMinecraftPath(), "resourcepacks"));

    public void OpenShaderPacksFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(System.IO.Path.Combine(GetBaseMinecraftPath(), "shaderpacks"));

    public void OpenCLModpackFolder_Click(object? sender, RoutedEventArgs e) =>
        OpenOrCreateFolder(System.IO.Path.Combine(GetBaseMinecraftPath(), "CLModpack"));

    private static string GetBaseMinecraftPath()
    {
        string path = SettingsManager.Default.PathLacunher;
        return string.IsNullOrWhiteSpace(path) ? PlatformPaths.DefaultLauncherPath() : path;
    }

    private static void OpenOrCreateFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            UrlOpener.Open(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не вдалося відкрити папку {path}: {ex.Message}");
        }
    }

    #endregion

    #region Діалоги та Зовнішні Посилання

    public void VersionMinecraftChangeLog_Tapped(object? sender, TappedEventArgs e)
    {
        AvaloniaSoundService.Click();

        if (sender is ListBox { SelectedItem: VersionLogItem selectedItem })
        {
            var dialog = new ChangelogDialog(selectedItem.VersionId, selectedItem.VersionType);
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                dialog.ShowDialog(owner);
            }
        }
    }

    public async void OpenGlobalBackups_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window mainWindow) return;

        try
        {
            var managerWindow = new WorldBackupDialog();
            await managerWindow.ShowDialog(mainWindow);
        }
        catch (Exception ex)
        {
            var dialog = new MascotDialogWindow(
                string.Format(LocalizationManager.GetString("Dialogs.FolderOpenError", "Не вдалося відкрити список світів:\n{0}"), ex.Message),
                LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                MascotEmotion.Sad,
                false);

            await dialog.ShowDialog(mainWindow);
        }
    }

    public void BugReport_Click(object? sender, RoutedEventArgs e)
    {
        AvaloniaSoundService.Click();
        WebHelper.OpenUrl("https://discord.com/channels/1195118159187939458/1195494058571866172");
    }

    public void NewsUpdateLauncher_Click(object? sender, RoutedEventArgs e) =>
        NewsRequested?.Invoke(this, EventArgs.Empty);

    public void TutorialYoutube_Click(object? sender, RoutedEventArgs e) =>
        WebHelper.OpenUrl("https://cl-launcher.app/tutorial.html");

    public void GitHub_Click(object? sender, RoutedEventArgs e) =>
        WebHelper.OpenUrl("https://github.com/WER-CORE/CL-OpenSource");

    public async void OpenCredits_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window mainWindow)
        {
            var creditsWin = new CreditsDialog();
            await creditsWin.ShowDialog(mainWindow);
        }
    }

    public void SupportProject_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            UrlOpener.Open("https://send.monobank.ua/jar/APZdKjTvuT");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не вдалося відкрити посилання підтримки: {ex.Message}");
        }
    }

    public void Support3OSHBr_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is CL_Main mainWindow)
        {
            var overlay = mainWindow.FindControl<Grid>("FundraiserOverlay");
            if (overlay != null) overlay.IsVisible = true;

            _ = LoadFundraiserAsync(mainWindow);
        }
    }

    private static async Task LoadFundraiserAsync(CL_Main mainWindow)
    {
        var fundLoader = mainWindow.FindControl<AvaloniaProgressRing.ProgressRing>("FundLoader");
        var fundraisersList = mainWindow.FindControl<ItemsControl>("FundraisersList");
        var noActiveFundsTXT = mainWindow.FindControl<TextBlock>("NoActiveFundsTXT");

        if (fundLoader != null) fundLoader.IsVisible = true;
        if (fundraisersList != null) fundraisersList.ItemsSource = null;
        if (noActiveFundsTXT != null) noActiveFundsTXT.IsVisible = false;

        try
        {
            string json = await WebHelper.Client.GetStringAsync(Secrets.FundraiserURL);
            var dataList = await Task.Run(() => JsonConvert.DeserializeObject<List<FundraiserData3rd>>(json));
            var activeFunds = dataList?.Where(d => d.isActive).ToList();

            if (activeFunds is { Count: > 0 })
            {

                var imageTasks = activeFunds
                    .Where(f => !string.IsNullOrEmpty(f.imageUrl))
                    .Select(async fund =>
                    {
                        try
                        {
                            fund.ImageBitmap = await AvaloniaImageHelper.LoadOptimizedImageAsync(fund.imageUrl, 86);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to load fundraiser image: {ex.Message}");
                        }
                    });

                await Task.WhenAll(imageTasks);

                if (fundraisersList != null) fundraisersList.ItemsSource = activeFunds;
            }
            else if (noActiveFundsTXT != null)
            {
                noActiveFundsTXT.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            if (noActiveFundsTXT != null)
            {
                noActiveFundsTXT.Text = LocalizationManager.GetString("Servers.FundNetworkError", "Помилка мережі або бази зборів.");
                noActiveFundsTXT.IsVisible = true;
            }
            Debug.WriteLine(ex.Message);
        }
        finally
        {
            if (fundLoader != null) fundLoader.IsVisible = false;
        }
    }

    #endregion

    #region Запуск Гри та Селектор Версій

    public async void PlayTXT_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();

        if (DataContext is not HomePageViewModel vm) return;

        await vm.EnsureAccountSelectedAsync();

        if (vm.CurrentSession == null || string.IsNullOrWhiteSpace(vm.CurrentSession.Username))
        {
            AvaloniaDialogService.Show(
                LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Агов! Будь ласка, оберіть акаунт перед початком гри!"),
                LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Акаунт не обраний"),
                MascotEmotion.Alert);
            return;
        }

        var originalBorderBrush = BorderButton.Background;
        var originalLineBrush = Line.Background;

        BorderButton.Background = PressedButtonBrush;
        Line.Background = PressedLineBrush;

        try
        {
            if (vm.SelectedLoaderType == 0)
                vm.SelectedLoaderType = (byte)SettingsManager.Default.LastSelectedType;

            if (vm.SelectedLoaderType == 0)
            {
                AvaloniaDialogService.Show(
                    LocalizationManager.GetString("GameLaunch.NoVersionSelectedDesc", "Агов! Будь ласка, оберіть версію перед початком гри!"),
                    LocalizationManager.GetString("GameLaunch.NoVersionSelectedTitle", "Версія не обрана"),
                    MascotEmotion.Alert);
                return;
            }

            if (SelectVersionTypeGird.IsVisible)
            {
                await CloseVersionMenuAsync();
            }

            if (vm.PlayCommand.CanExecute(null))
            {
                await vm.PlayCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            await Task.Delay(150);
            BorderButton.Background = originalBorderBrush;
            Line.Background = originalLineBrush;
        }
    }

    public async void CheckMarkVersionSelect_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();

        if (SelectVersionTypeGird.IsVisible)
        {
            await CloseVersionMenuAsync();
        }
        else
        {
            OpenVersionMenu();
        }
    }

    private void OpenVersionMenu()
    {
        SelectVersionTypeGird.IsVisible = true;
        SelectVersionTypeGird.Opacity = 1;

        if (CheckMarkVersionSelect.Child is Path chevronPath)
        {
            AvaloniaAnimationService.AnimateRotation(chevronPath, 0);
        }

        AvaloniaAnimationService.AnimatePageTransition(SelectVersionTypeGird);

        if (DataContext is HomePageViewModel { IsVersionSelectorOpen: false } vm &&
            vm.ToggleVersionSelectorCommand.CanExecute(null))
        {
            vm.ToggleVersionSelectorCommand.Execute(null);
        }
    }

    private async Task CloseVersionMenuAsync()
    {
        if (!SelectVersionTypeGird.IsVisible) return;

        if (CheckMarkVersionSelect.Child is Path chevronPath)
        {
            AvaloniaAnimationService.AnimateRotation(chevronPath, 180);
        }

        if (SelectVersion.IsVisible) AvaloniaAnimationService.AnimatePageTransitionExit(SelectVersion, 20);
        AvaloniaAnimationService.AnimatePageTransitionExit(SelectVersionTypeGird, 20);
        if (SelectVersionMod.IsVisible) AvaloniaAnimationService.AnimatePageTransitionExit(SelectVersionMod, 20);

        await Task.Delay(250);

        SelectVersionTypeGird.IsVisible = false;
        SelectVersion.IsVisible = false;
        SelectVersionMod.IsVisible = false;

        if (DataContext is HomePageViewModel { IsVersionSelectorOpen: true } vm &&
            vm.ToggleVersionSelectorCommand.CanExecute(null))
        {
            vm.ToggleVersionSelectorCommand.Execute(null);
        }
    }

    public void SelectVersionVanila_PointerPressed(object? sender, PointerPressedEventArgs? e)
    {
        if (e != null) AvaloniaSoundService.Click();

        if (DataContext is HomePageViewModel vm && vm.SelectLoaderTypeCommand.CanExecute("Vanilla"))
        {
            vm.SelectLoaderTypeCommand.Execute("Vanilla");
        }

        SelectVersionMod.IsVisible = false;
        SelectVersion.IsVisible = true;

        AvaloniaAnimationService.AnimatePageTransition(SelectVersion);
        AvaloniaAnimationService.FadeIn(SelectVersionVanila, 0.2);

        if (IconSelectVersion_Copy?.Source != null)
        {
            IconSelectVersion.Source = IconSelectVersion_Copy.Source;
        }
    }

    public void SelectVersionOptifine_PointerPressed(object? sender, PointerPressedEventArgs? e)
    {
        if (e != null) AvaloniaSoundService.Click();

        if (DataContext is HomePageViewModel vm && vm.SelectLoaderTypeCommand.CanExecute("Optifine"))
        {
            vm.SelectLoaderTypeCommand.Execute("Optifine");
        }

        SelectVersion.IsVisible = false;
        SelectVersionMod.IsVisible = true;

        AvaloniaAnimationService.AnimatePageTransition(SelectVersionMod);
        AvaloniaAnimationService.FadeIn(SelectVersionOptifine, 0.2);

        if (IconSelectVersion_Copy2?.Source != null)
        {
            IconSelectVersion.Source = IconSelectVersion_Copy2.Source;
        }
    }

    public void SelectVersionRedirect_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaSoundService.Click();
        SelectVersionTypeGird.IsVisible = false;
        ModPacksRequested?.Invoke(this, EventArgs.Empty);
    }

    public async void VersionList_Tapped(object? sender, TappedEventArgs e)
    {
        AvaloniaSoundService.Click();
        await CloseVersionMenuAsync();
    }

    public async void VersionListMod_Tapped(object? sender, TappedEventArgs e)
    {
        AvaloniaSoundService.Click();
        await CloseVersionMenuAsync();
    }

    public void PanelFooterScalePlus_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control element)
            AvaloniaAnimationService.AnimateScale(element, 1.03);
    }

    public void PanelFooterScaleMinus_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control element)
            AvaloniaAnimationService.AnimateScale(element, 1.0);
    }

    #endregion

    #region Партнерські Сервери (Карусель)

    private void StartSlideTimerIfReady()
    {
        if (PartnerServer != null && PartnerServer.ItemCount > 1 && _slideTimer == null)
        {
            _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _slideTimer.Tick += (_, _) => NextPartnerSlide();
            _slideTimer.Start();
        }
        else if (_slideTimer != null && PartnerServer != null && PartnerServer.ItemCount > 1 && !_slideTimer.IsEnabled)
        {
            _slideTimer.Start();
        }
    }

    private void NextPartnerSlide()
    {
        if (PartnerServer == null || PartnerServer.ItemCount < 2) return;

        int next = (PartnerServer.SelectedIndex + 1) % PartnerServer.ItemCount;
        PartnerServer.SelectedIndex = next;
        AnimateSlide(next);
    }

    private void AnimateSlide(int newIndex)
    {
        if (PartnerServer?.SelectedItem == null) return;

        if (_lastIndex != -1 && _lastIndex != newIndex && _lastIndex < PartnerServer.ItemCount)
        {
            if (PartnerServer.ContainerFromIndex(_lastIndex) is ListBoxItem oldItem)
                AnimateItemX(oldItem, 0, -300);
        }

        if (PartnerServer.ContainerFromIndex(newIndex) is ListBoxItem newItem)
            AnimateItemX(newItem, 300, 0);

        _lastIndex = newIndex;
    }

    private static void AnimateItemX(Control item, double fromX, double toX)
    {
        item.RenderTransform = new TranslateTransform(fromX, 0);

        var anim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            FillMode = FillMode.Forward,
            Easing = new QuarticEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, toX) }
                }
            }
        };

        _ = anim.RunAsync(item);
    }

    public void PartnerServer_PointerEntered(object? sender, PointerEventArgs e)
    {
        _slideTimer?.Stop();
        PanelFooterScalePlus_PointerEntered(sender, e);
    }

    public void PartnerServer_PointerExited(object? sender, PointerEventArgs e)
    {
        _slideTimer?.Start();
        PanelFooterScaleMinus_PointerExited(sender, e);
    }

    #endregion
}
