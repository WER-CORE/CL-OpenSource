using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        private void LoadCustomSettings()
        {
            string savedColor = SettingsManager.Default.LoadScreenBarColor;
            if (string.IsNullOrEmpty(savedColor)) savedColor = "#00BEFF";

            try
            {
                LoadScreenColorButton.Content = _themeService.CreateColorButtonContent(savedColor);
            }
            catch
            {
                LoadScreenColorButton.Content = $"#{LocalizationManager.GetString("Dialogs.Error", "Помилка")}";
            }
        }

        private void InitToggles()
        {
            MemToggle.IsChecked = SettingsManager.Default.EnableMemeSound;
            DebugToggle.IsChecked = SettingsManager.Default.EnableLog;
            CloseLauncherToggle.IsChecked = SettingsManager.Default.CloseLaucnher;
            ModDepsToggle.IsChecked = SettingsManager.Default.ModDep;
            GlassEffectToggle.IsChecked = !SettingsManager.Default.DisableGlassEffect;
            FullScreenToggle.IsChecked = SettingsManager.Default.FullScreen;
            AutoBackupToggle.IsChecked = SettingsManager.Default.EnableAutoBackup;
            BackupCountText.Text = SettingsManager.Default.MaxAutoBackups.ToString();
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
        private async void InitializeLanguagesAsync()
        {
            var languages = await LocalizationFetcher.GetAvailableLanguagesAsync();

            LanguageComboBox.ItemsSource = languages;

            string currentLangCode = SettingsManager.Default.LanguageCode ?? "uk_UA";

            foreach (LanguageItem item in LanguageComboBox.Items)
            {
                if (item.Code == currentLangCode)
                {
                    LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
                    LanguageComboBox.SelectedItem = item;
                    LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                    break;
                }
            }
        }
        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DateTime today = DateTime.Now;
            bool isAprilFoolsWeek = today.Month == 4 && today.Day >= 1 && today.Day <= 7;

            if (!IsLoaded || LanguageComboBox.SelectedItem is not LanguageItem selectedLang) return;

            if (LocalizationManager.CurrentLanguage != selectedLang.Code)
            {
                LanguageComboBox.IsEnabled = false;

                bool success = await LocalizationFetcher.DownloadLanguageAsync(selectedLang);
                if (selectedLang.Code == "uk_UA" && isAprilFoolsWeek)
                {
                    _1AprilGird.Visibility = Visibility.Visible;
                }
                else { _1AprilGird.Visibility = Visibility.Collapsed; }

                if (success)
                {
                    LocalizationManager.LoadLanguage(selectedLang.Code);

                    SettingsManager.Default.LanguageCode = selectedLang.Code;
                    SettingsManager.Save();

                    UpdateLocalization();
                }
                else
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Dialogs.Error", "Не вдалося завантажити мовний пакет. Перевірте підключення до мережі."),
                        LocalizationManager.GetString("Dialogs.Oops", "Упс"),
                        MascotEmotion.Sad);
                }

                LanguageComboBox.IsEnabled = true;
            }
        }
        private void UpdateLocalization()
        {
            if (PlayBtnBorder != null) PlayBtnBorder.ToolTip = LocalizationManager.GetString("Sidebar.ToolTipMain", "Головна сторінка");
            if (ModpacksBtnBorder != null) ModpacksBtnBorder.ToolTip = LocalizationManager.GetString("Sidebar.ToolTipModpacks", "Створити або імпортувати збірку");
            if (ModsBtnBorder != null) ModsBtnBorder.ToolTip = LocalizationManager.GetString("Sidebar.ToolTipMods", "Завантаження модів");
            if (ServersBtnBorder != null) ServersBtnBorder.ToolTip = LocalizationManager.GetString("Sidebar.ToolTipServers", "Список серверів");
            if (GalleryBtnBorder != null) GalleryBtnBorder.ToolTip = LocalizationManager.GetString("Sidebar.ToolTipGallery", "Скріншоти");

            if (Loc_MenuBtnMain != null) Loc_MenuBtnMain.Text = LocalizationManager.GetString("Sidebar.Main", "Головна");
            if (Loc_MenuBtnModpacks != null) Loc_MenuBtnModpacks.Text = LocalizationManager.GetString("Sidebar.Modpacks", "Збірки");
            if (Loc_MenuBtnMods != null) Loc_MenuBtnMods.Text = LocalizationManager.GetString("Sidebar.Mods", "Моди");
            if (Loc_MenuBtnServers != null) Loc_MenuBtnServers.Text = LocalizationManager.GetString("Sidebar.Servers", "Сервери");
            if (Loc_MenuBtnGallery != null) Loc_MenuBtnGallery.Text = LocalizationManager.GetString("Sidebar.Gallery", "Галерея");

            if (Loc_SettingsMenu != null) Loc_SettingsMenu.Text = LocalizationManager.GetString("Sidebar.Settings", "Налаштування");
            if (Loc_FilesMenu != null) Loc_FilesMenu.Text = LocalizationManager.GetString("Sidebar.Files", "Файли та Бекапи");
            if (Loc_InfoMenu != null) Loc_InfoMenu.Text = LocalizationManager.GetString("Sidebar.Info", "Інформація");

            if (PlayTXT != null) PlayTXT.Text = LocalizationManager.GetString("MainScreen.PlayButton", "ОБЕРІТЬ ВЕРСІЮ");
            if (DevLogMinecraft != null) DevLogMinecraft.Text = LocalizationManager.GetString("MainScreen.Changelog", "Що нового? (Changelog)");
            if (LatestActionsTXT != null) LatestActionsTXT.Text = LocalizationManager.GetString("MainScreen.LatestActions", "Останні дії");
            if (Loc_PartnersTitle != null) Loc_PartnersTitle.Text = LocalizationManager.GetString("MainScreen.Partners", "Партнери");
            if (PartherHelper != null) PartherHelper.Content = LocalizationManager.GetString("MainScreen.HowToJoin", "Як потрапити?");

            if (VersionMinecraftChangeLog != null)
            {
                VersionMinecraftChangeLog.Tag = LocalizationManager.GetString("MainScreen.ChangesIn", "Зміни в ");
                VersionMinecraftChangeLog.ToolTip = LocalizationManager.GetString("MainScreen.ToolTipChangelog", "Натисніть, щоб прочитати список змін цієї версії");
            }
            if (CheckMarkVersionSelect != null) CheckMarkVersionSelect.ToolTip = LocalizationManager.GetString("MainScreen.ToolTipVersionSelect", "НАТИСНІТЬ ЩОБ ДІЗНАТИСЯ ЯКІ Є ВЕРСІЇ НА ВИБІР");

            if (SearchSystemTXT1 != null) SearchSystemTXT1.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Пошук...");
            if (SearchSystemTXT2 != null) SearchSystemTXT2.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Пошук...");
            if (SearchSystemModsTXT != null) SearchSystemModsTXT.PlaceholderText = LocalizationManager.GetString("Generic.SearchMods", "Пошук модифікацій...");
            if (SearchSystemTXT != null) SearchSystemTXT.PlaceholderText = LocalizationManager.GetString("Generic.SearchServers", "Пошук сервера...");
            if (SearchSystemModsTXT1 != null) SearchSystemModsTXT1.PlaceholderText = LocalizationManager.GetString("Generic.Search", "Пошук...");
            if (TitleManegerCollection1 != null) TitleManegerCollection1.Text = LocalizationManager.GetString("Generic.Continue", "Продовжити");

            if (Loc_Settings_TitleMinecraft != null) Loc_Settings_TitleMinecraft.Text = LocalizationManager.GetString("Settings.Minecraft", "Майнкрафт");
            if (Loc_Settings_DevConsoleTitle != null) Loc_Settings_DevConsoleTitle.Text = LocalizationManager.GetString("Settings.DevConsoleTitle", "Логи (Консоль розробника)");
            if (Loc_Settings_DevConsoleDesc != null) Loc_Settings_DevConsoleDesc.Text = LocalizationManager.GetString("Settings.DevConsoleDesc", "Відкриває вікно з технічною інформацією під час гри");
            if (Loc_Settings_FullScreenTitle != null) Loc_Settings_FullScreenTitle.Text = LocalizationManager.GetString("Settings.FullScreenTitle", "Повноекранний режим");
            if (Loc_Settings_FullScreenDesc != null) Loc_Settings_FullScreenDesc.Text = LocalizationManager.GetString("Settings.FullScreenDesc", "Запускає гру на весь монітор без рамок вікна");
            if (Loc_Settings_ResolutionTitle != null) Loc_Settings_ResolutionTitle.Text = LocalizationManager.GetString("Settings.ResolutionTitle", "Роздільна здатність екрану");
            if (Loc_Settings_ResolutionDesc != null) Loc_Settings_ResolutionDesc.Text = LocalizationManager.GetString("Settings.ResolutionDesc", "Розмір вікна гри.");
            if (Loc_Settings_RamTitle != null) Loc_Settings_RamTitle.Text = LocalizationManager.GetString("Settings.RamTitle", "Оперативна пам’ять (RAM)");
            if (Loc_Settings_RamDesc != null) Loc_Settings_RamDesc.Text = LocalizationManager.GetString("Settings.RamDesc", "Пам'ять для Java. Рекомендовано 4096 MB.");
            if (Loc_Settings_PathTitle != null) Loc_Settings_PathTitle.Text = LocalizationManager.GetString("Settings.PathTitle", "Шлях папки для лаунчера");
            if (Loc_Settings_PathDesc != null) Loc_Settings_PathDesc.Text = LocalizationManager.GetString("Settings.PathDesc", "Місце зберігання світів, модів та налаштувань");
            if (Loc_Settings_BackupTitle != null) Loc_Settings_BackupTitle.Text = LocalizationManager.GetString("Settings.BackupTitle", "Автоматичні бекапи світів");
            if (Loc_Settings_BackupDesc != null) Loc_Settings_BackupDesc.Text = LocalizationManager.GetString("Settings.BackupDesc", "Створює копію світу перед кожним запуском гри");
            if (Loc_Settings_BackupCountTitle != null) Loc_Settings_BackupCountTitle.Text = LocalizationManager.GetString("Settings.BackupCountTitle", "Кількість копій");
            if (Loc_Settings_BackupCountDesc != null) Loc_Settings_BackupCountDesc.Text = LocalizationManager.GetString("Settings.BackupCountDesc", "Максимальна кількість архівів. Старі будуть видалятися");

            if (Loc_Settings_TitleLauncher != null) Loc_Settings_TitleLauncher.Text = LocalizationManager.GetString("Settings.Launcher", "Запускач");
            if (HelpTranslateTXT != null) { HelpTranslateTXT.Content = LocalizationManager.GetString("Settings.HelpTranslate", "Як допомогти з перекладом?"); }
            if (LanguageTitleTXT != null) LanguageTitleTXT.Text = LocalizationManager.GetString("Settings.LanguageTitle", "Мова інтерфейсу (Language)");
            if (LanguageDescTXT != null) LanguageDescTXT.Text = LocalizationManager.GetString("Settings.LanguageDesc", "Оберіть мову для лаунчера.");
            if (Loc_Settings_CloseLaunchTitle != null) Loc_Settings_CloseLaunchTitle.Text = LocalizationManager.GetString("Settings.CloseLaunchTitle", "Закриття лаунчера під час гри");
            if (Loc_Settings_CloseLaunchDesc != null) Loc_Settings_CloseLaunchDesc.Text = LocalizationManager.GetString("Settings.CloseLaunchDesc", "Економить ресурси ПК");
            if (Loc_Settings_AutoDepTitle != null) Loc_Settings_AutoDepTitle.Text = LocalizationManager.GetString("Settings.AutoDepTitle", "Автозавантаження залежностей");
            if (Loc_Settings_AutoDepDesc != null) Loc_Settings_AutoDepDesc.Text = LocalizationManager.GetString("Settings.AutoDepDesc", "Автоматично додає потрібні бібліотеки");
            if (Loc_Settings_GlassTitle != null) Loc_Settings_GlassTitle.Text = LocalizationManager.GetString("Settings.GlassTitle", "Ефект прозорості (Скло)");
            if (Loc_Settings_GlassDesc != null) Loc_Settings_GlassDesc.Text = LocalizationManager.GetString("Settings.GlassDesc", "Напівпрозорий фон вікна (Mica).");

            if (Loc_Settings_ThemeTitle != null) Loc_Settings_ThemeTitle.Text = LocalizationManager.GetString("Settings.ThemeTitle", "Тема інтерфейсу");
            if (Loc_Settings_ThemeDesc != null) Loc_Settings_ThemeDesc.Text = LocalizationManager.GetString("Settings.ThemeDesc", "Оберіть готовий стиль");
            if (Loc_Settings_ThemeSection != null) Loc_Settings_ThemeSection.Text = LocalizationManager.GetString("Settings.ThemeSection", "Колір секції");
            if (Loc_Settings_ThemeBg != null) Loc_Settings_ThemeBg.Text = LocalizationManager.GetString("Settings.ThemeBg", "Колір фону");
            if (Loc_Settings_ThemeAccent != null) Loc_Settings_ThemeAccent.Text = LocalizationManager.GetString("Settings.ThemeAccent", "Додатковий колір");
            if (Loc_Settings_ThemeText != null) Loc_Settings_ThemeText.Text = LocalizationManager.GetString("Settings.ThemeText", "Колір тексту");
            if (Loc_Settings_ThemeBtn != null) Loc_Settings_ThemeBtn.Text = LocalizationManager.GetString("Settings.ThemeBtn", "Колір кнопки");
            if (Loc_Settings_ThemeImage != null) Loc_Settings_ThemeImage.Text = LocalizationManager.GetString("Settings.ThemeImage", "Зображення фону");
            if (Loc_Settings_ThemeCodeTitle != null) Loc_Settings_ThemeCodeTitle.Text = LocalizationManager.GetString("Settings.ThemeCodeTitle", "Код теми (Для головного вікна)");

            if (Loc_Settings_LoadScreenTitle != null) Loc_Settings_LoadScreenTitle.Text = LocalizationManager.GetString("Settings.LoadScreenTitle", "Завантажувальний екран");
            if (Loc_Settings_LoadScreenDesc != null) Loc_Settings_LoadScreenDesc.Text = LocalizationManager.GetString("Settings.LoadScreenDesc", "Налаштуйте вигляд вікна запуску");
            if (Loc_Settings_LoadScreenBg != null) Loc_Settings_LoadScreenBg.Text = LocalizationManager.GetString("Settings.LoadScreenBg", "Зображення фону");
            if (Loc_Settings_LoadScreenColor != null) Loc_Settings_LoadScreenColor.Text = LocalizationManager.GetString("Settings.LoadScreenColor", "Колір смужки завантаження");
            if (Loc_Settings_LoadScreenPhrases != null) Loc_Settings_LoadScreenPhrases.Text = LocalizationManager.GetString("Settings.LoadScreenPhrases", "Кастомні фрази");
            if (Loc_Settings_LoadScreenCodeTitle != null) Loc_Settings_LoadScreenCodeTitle.Text = LocalizationManager.GetString("Settings.LoadScreenCodeTitle", "Код теми (Завантажувального екрану)");

            if (Loc_BtnResetTheme != null) Loc_BtnResetTheme.Content = LocalizationManager.GetString("Settings.ResetBtn", "Скинути");
            if (Loc_BtnCopyTheme != null) Loc_BtnCopyTheme.Content = LocalizationManager.GetString("Settings.CopyBtn", "Копіювати");
            if (Loc_BtnPasteTheme != null) Loc_BtnPasteTheme.Content = LocalizationManager.GetString("Settings.PasteBtn", "Вставити");
            if (Loc_BtnResetLoadScreen != null) Loc_BtnResetLoadScreen.Content = LocalizationManager.GetString("Settings.ResetBtn", "Скинути");
            if (Loc_BtnCopyLoadScreen != null) Loc_BtnCopyLoadScreen.Content = LocalizationManager.GetString("Settings.CopyBtn", "Копіювати");
            if (Loc_BtnPasteLoadScreen != null) Loc_BtnPasteLoadScreen.Content = LocalizationManager.GetString("Settings.PasteBtn", "Вставити");
            if (Loc_ThemeDark != null) Loc_ThemeDark.Content = LocalizationManager.GetString("Settings.ThemeDark", "Темна");
            if (Loc_ThemeLight != null) Loc_ThemeLight.Content = LocalizationManager.GetString("Settings.ThemeLight", "Світла");
            if (Loc_ThemeCustom != null) Loc_ThemeCustom.Content = LocalizationManager.GetString("Settings.ThemeCustom", "Кастомна");
            if (Background_imageButton != null) Background_imageButton.Content = LocalizationManager.GetString("Settings.FileBtn", "Файл...");
            if (LoadScreenBgButton != null) LoadScreenBgButton.Content = LocalizationManager.GetString("Settings.FileBtn", "Файл...");
            if (EditPhrasesButton != null) EditPhrasesButton.Content = LocalizationManager.GetString("Settings.TextBtn", "Текст...");

            if (Loc_MenuOpenFolder != null) Loc_MenuOpenFolder.Header = LocalizationManager.GetString("Context.OpenFolder", "Відкрити папку...");
            if (Loc_MenuRootFolder != null) Loc_MenuRootFolder.Header = LocalizationManager.GetString("Context.RootFolder", "Коренева папка");
            if (Loc_MenuGameFolder != null) Loc_MenuGameFolder.Header = LocalizationManager.GetString("Context.GameFolder", "Папка гри");
            if (Loc_MenuModsFolder != null) Loc_MenuModsFolder.Header = LocalizationManager.GetString("Context.ModsFolder", "Моди");
            if (Loc_MenuResourcePacksFolder != null) Loc_MenuResourcePacksFolder.Header = LocalizationManager.GetString("Context.ResourcePacksFolder", "Ресурспаки");
            if (Loc_MenuShadersFolder != null) Loc_MenuShadersFolder.Header = LocalizationManager.GetString("Context.ShadersFolder", "Шейдери");
            if (Loc_MenuModpacksFolder != null) Loc_MenuModpacksFolder.Header = LocalizationManager.GetString("Context.ModpacksFolder", "Збірки");
            if (Loc_MenuBackupsFolder != null) Loc_MenuBackupsFolder.Header = LocalizationManager.GetString("Context.BackupsFolder", "Менеджер світів");
            if (Loc_MenuReportBug != null) Loc_MenuReportBug.Header = LocalizationManager.GetString("Context.ReportBug", "Повідомити про баг");
            if (Loc_MenuLauncherNews != null) Loc_MenuLauncherNews.Header = LocalizationManager.GetString("Context.LauncherNews", "Новини запускача");
            if (Loc_MenuWiki != null) Loc_MenuWiki.Header = LocalizationManager.GetString("Context.Wiki", "Документація (Wiki)");
            if (Loc_MenuGithub != null) Loc_MenuGithub.Header = LocalizationManager.GetString("Context.Github", "GitHub (Код)");
            if (Loc_MenuSupportProject != null) Loc_MenuSupportProject.Header = LocalizationManager.GetString("Context.SupportProject", "Підтримати проект");
            if (Loc_MenuSupportBrigade != null) Loc_MenuSupportBrigade.Text = LocalizationManager.GetString("Context.SupportBrigade", "Підтримати 3 ОШБр");

            if (Loc_SourceTitle != null) Loc_SourceTitle.Text = LocalizationManager.GetString("Mods.SourceTitle", "Джерело");
            if (Loc_LoaderTitle != null) Loc_LoaderTitle.Text = LocalizationManager.GetString("Mods.LoaderTitle", "Завантажувач");
            if (Loc_ModeTitle != null) Loc_ModeTitle.Text = LocalizationManager.GetString("Mods.ModeTitle", "Режим");
            if (Loc_ModeModpack != null) Loc_ModeModpack.Text = LocalizationManager.GetString("Mods.ModeModpack", "У збірку");
            if (Loc_ModeSeparate != null) Loc_ModeSeparate.Text = LocalizationManager.GetString("Mods.ModeSeparate", "Окремо");
            if (ModsTXT != null) ModsTXT.Text = LocalizationManager.GetString("Mods.CategoryMods", "Моди");
            if (ShardTXT != null) ShardTXT.Text = LocalizationManager.GetString("Mods.CategoryShaders", "Шейдери");
            if (ResourcePackTXT != null) ResourcePackTXT.Text = LocalizationManager.GetString("Mods.CategoryResourcePacks", "Ресурспаки");
            if (MapsTXT != null) MapsTXT.Text = LocalizationManager.GetString("Mods.CategoryMaps", "Мапи");
            if (DataPacksTXT != null) DataPacksTXT.Text = LocalizationManager.GetString("Mods.CategoryDataPacks", "Датапаки");
            if (Loc_TxtSearchingMods != null) Loc_TxtSearchingMods.Text = LocalizationManager.GetString("Mods.SearchingMods", "Bit-CL шукає...");
            if (Loc_InstallModsTxt != null) Loc_InstallModsTxt.Text = LocalizationManager.GetString("Mods.InstallModsTxt", "Встановити...");
            if (DowloadMod != null) DowloadMod.Content = LocalizationManager.GetString("Mods.InstallBtn", "Встановити");

            if (ImportModPacks != null) ImportModPacks.ToolTip = LocalizationManager.GetString("Modpacks.ToolTipImport", "Натисніть, щоб імпортувати мод-пак збірки");
            if (DowloadModPacks != null) DowloadModPacks.ToolTip = LocalizationManager.GetString("Modpacks.ToolTipCreate", "Натисніть, щоб створити власну мод-пак збірку");
            if (Loc_TxtModpackFilterTitle != null) Loc_TxtModpackFilterTitle.Text = LocalizationManager.GetString("Modpacks.FilterTitle", "Фільтр Збірок");
            if (ImportTXT != null) ImportTXT.Text = LocalizationManager.GetString("Modpacks.ImportBtn", "Імпортувати");
            if (Loc_RunCreateNew != null) Loc_RunCreateNew.Text = LocalizationManager.GetString("Modpacks.CreateNewBtn", "Створити нову");
            if (VanilaPackTxt != null) VanilaPackTxt.Text = LocalizationManager.GetString("Modpacks.VanillaPack", "Ванільна збірка");
            if (ModPackTxt != null) ModPackTxt.Text = LocalizationManager.GetString("Modpacks.ModdedPack", "Модова збірка");
            if (TitleManegerCollection != null) TitleManegerCollection.Text = LocalizationManager.GetString("Modpacks.CreateTitle", "Створення збірки");
            if (TitleModsDowload != null) TitleModsDowload.Text = LocalizationManager.GetString("Modpacks.SelectVersionTitle", "Вибір версії моду");

            if (Loc_RadioNormalServers != null) Loc_RadioNormalServers.Content = LocalizationManager.GetString("Servers.NormalServers", "Звичайні");
            if (Loc_RadioModdedServers != null) Loc_RadioModdedServers.Content = LocalizationManager.GetString("Servers.ModdedServers", "Модові");
            if (Loc_TxtSearchingServers != null) Loc_TxtSearchingServers.Text = LocalizationManager.GetString("Servers.Searching", "Шукає сервери...");
            if (OwnerButton != null) OwnerButton.Content = LocalizationManager.GetString("Servers.AddServer", "Додати свій сервер");
            if (PlayServer != null) PlayServer.Content = LocalizationManager.GetString("Servers.PlayOnServer", "ГРАТИ НА СЕРВЕРІ");
            if (DescriptionServer != null) DescriptionServer.Text = LocalizationManager.GetString("Servers.Description", "Опис...");

            if (NameNik != null && (NameNik.Text == "Відсутній акаунт" || NameNik.Text == "No account" || NameNik.Text == LocalizationManager.GetString("Accounts.NoAccount", "Відсутній акаунт")))
            {
                NameNik.Text = LocalizationManager.GetString("Accounts.NoAccount", "Відсутній акаунт");
            }

            if (NameNikManeger != null) NameNikManeger.PlaceholderText = LocalizationManager.GetString("Accounts.PlaceholderNickname", "Нік...");
            if (Login_LittleSkin != null) Login_LittleSkin.PlaceholderText = LocalizationManager.GetString("Accounts.PlaceholderEmail", "Пошта...");
            if (PasswordLittleSkin != null) PasswordLittleSkin.PlaceholderText = LocalizationManager.GetString("Accounts.PlaceholderPassword", "Пароль...");
            if (AddProfile != null) AddProfile.Text = LocalizationManager.GetString("Accounts.AddProfile", "Додати профіль +");
            if (StatsTextOpen != null) StatsTextOpen.Text = LocalizationManager.GetString("Accounts.Stats", "Статистика");
            if (Loc_TxtOfflineAccountTitle != null) Loc_TxtOfflineAccountTitle.Text = LocalizationManager.GetString("Accounts.OfflineTitle", "Офлайн акаунт");
            if (Loc_TxtLittleSkinTitle != null) Loc_TxtLittleSkinTitle.Text = LocalizationManager.GetString("Accounts.LittleSkinTitle", "LittleSkin акаунт");
            if (Loc_TxtMicrosoftTitle != null) Loc_TxtMicrosoftTitle.Text = LocalizationManager.GetString("Accounts.MicrosoftTitle", "Microsoft-онлайн акаунт");
            if (CreateAccount_Offline != null) CreateAccount_Offline.Content = LocalizationManager.GetString("Accounts.CreateBtn", "Створити");
            if (LoginAccountLittleSkin != null) LoginAccountLittleSkin.Content = LocalizationManager.GetString("Accounts.LoginBtn", "Увійти");
            if (CreateAccount_Online != null) CreateAccount_Online.Content = LocalizationManager.GetString("Accounts.LoginMicrosoftBtn", "Увійти через Microsoft");
            if (NoAccount_ != null) NoAccount_.Content = LocalizationManager.GetString("Accounts.NoLittleSkinAccount", "Немає акаунту?");

            if (BtnOpenFolder != null) BtnOpenFolder.Content = LocalizationManager.GetString("Gallery.OpenFolderBtn", "Відкрити папку");
            if (Loc_TxtGalleryEmptyTitle != null) Loc_TxtGalleryEmptyTitle.Text = LocalizationManager.GetString("Gallery.EmptyTitle", "Тут поки що пусто");
            if (Loc_TxtGalleryEmptyDesc != null) Loc_TxtGalleryEmptyDesc.Text = LocalizationManager.GetString("Gallery.EmptyDesc", "Зробіть скріншот...");
            if (Loc_TxtGallerySelected != null) Loc_TxtGallerySelected.Text = LocalizationManager.GetString("Gallery.Selected", "ВИБРАНО");
            if (BtnOpenImage != null) BtnOpenImage.Content = LocalizationManager.GetString("Gallery.OpenImageBtn", "Відкрити");
            if (BtnDelete != null) BtnDelete.Content = LocalizationManager.GetString("Gallery.DeleteBtn", "Видалити");
            if (BtnRefresh != null) BtnRefresh.ToolTip = LocalizationManager.GetString("Gallery.BtnRefreshTooltip", "Оновити список");
            
            if (Loc_MenuCredits != null) Loc_MenuCredits.Header = LocalizationManager.GetString("Context.Credits", "Подяки (Автори та Перекладачі)");
            
            if (Loc_TxtFundTitle != null) Loc_TxtFundTitle.Text = LocalizationManager.GetString("Fundraisers.Title", "АКТУАЛЬНІ ЗБОРИ");
            if (NoActiveFundsTXT != null) NoActiveFundsTXT.Text = LocalizationManager.GetString("Fundraisers.NoActive", "Наразі немає активних зборів.");

            if (TutorialHelloText != null) TutorialHelloText.Text = LocalizationManager.GetString("Tutorial.Hello", "Привіт, я Сіель!");
            if (QuestionTXT != null) QuestionTXT.Text = LocalizationManager.GetString("Tutorial.Question", "Я підготувала документацію...");
            if (NoQuestionTutorialButton != null) NoQuestionTutorialButton.Content = LocalizationManager.GetString("Tutorial.NoThanks", "Ні, дякую");
            if (YesQuestionTutorialButton != null) YesQuestionTutorialButton.Content = LocalizationManager.GetString("Tutorial.YesPlease", "Так, давай!");
            if (TutorialTitleText != null) TutorialTitleText.Text = LocalizationManager.GetString("Tutorial.OverlayTitle", "Довідка тут!");
            if (TutorialBodyText != null) TutorialBodyText.Text = LocalizationManager.GetString("Tutorial.OverlayBody", "Якщо що, я завжди поряд...");
            if (Loc_BtnUnderstood != null) Loc_BtnUnderstood.Content = LocalizationManager.GetString("Tutorial.UnderstoodBtn", "Зрозуміло, далі");

            if (Loc_TxtSearchingNews != null) Loc_TxtSearchingNews.Text = LocalizationManager.GetString("News.Searching", "Bit-CL шукає новини...");

            if (Relesed != null) Relesed.Content = LocalizationManager.GetString("Versions.Release", "Реліз");
            if (Snapshots != null) Snapshots.Content = LocalizationManager.GetString("Versions.Snapshot", "Снапшоти");
            if (Beta != null) Beta.Content = LocalizationManager.GetString("Versions.Beta", "Бета");
            if (Alpha != null) Alpha.Content = LocalizationManager.GetString("Versions.Alpha", "Альфа");
            if (SettingTXT1_Копировать != null) SettingTXT1_Копировать.Text = LocalizationManager.GetString("Versions.Vanilla", "Ваніла");
        }
        private void OpenCredits_Click(object sender, RoutedEventArgs e)
        {
            CreditsWindow creditsWin = new CreditsWindow();

            creditsWin.Owner = this;
            creditsWin.ShowDialog();

            this.Opacity = 1.0;
        }
        private void ScreenSizeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ScreenSizeListBox.SelectedItem as ListBoxItem;
            if (selectedItem == null)
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Dialogs.EmptySelectionDesc", "Дивина! Я бачила натискання, але не зрозуміла, який саме варіант ти обрав.\nСпробуй натиснути на рядок ще раз!"),
                    LocalizationManager.GetString("Dialogs.EmptySelectionTitle", "Пустий вибір"),
                    MascotEmotion.Confused
                );
                return;
            }

            var resolution = selectedItem.Content.ToString().Split('x');
            if (resolution.Length != 2 || !int.TryParse(resolution[0], out int width) || !int.TryParse(resolution[1], out int height))
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Dialogs.InvalidResolutionDesc", "Ой! Я намагалася розібрати цей розмір екрана, але цифри записані якось дивно.\nФормат має бути 'ШиринаxВисота' (наприклад, 1920x1080), а тут щось інше."),
                    LocalizationManager.GetString("Dialogs.InvalidResolutionTitle", "Невірний формат"),
                    MascotEmotion.Sad
                );
                return;
            }

            Width.Text = resolution[0];
            Height.Text = resolution[1];
            MincraftWindowSize.Content = $"{width}x{height}";

            SettingsManager.Default.width = width;
            SettingsManager.Default.height = height;
            SettingsManager.Save();
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
                        SettingsManager.Default.width = result;
                    else
                        SettingsManager.Default.height = result;

                    SettingsManager.Save();
                }
            }

            UpdateMinecraftWindowSize();
            textBox.CaretIndex = caretIndex;
        }

        private void UpdateMinecraftWindowSize()
        {
            MincraftWindowSize.Content = $"{SettingsManager.Default.width}x{SettingsManager.Default.height}";
        }

        private void Width_PreviewKeyDown(object sender, KeyEventArgs e) => HandleTextBoxInput(Width, "width");
        private void Height_PreviewKeyDown(object sender, KeyEventArgs e) => HandleTextBoxInput(Height, "height");
        private void Width_PreviewKeyUp(object sender, KeyEventArgs e) => HandleTextBoxInput(Width, "width");
        private void Height_PreviewKeyUp(object sender, KeyEventArgs e) => HandleTextBoxInput(Height, "height");

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.WindowState = WindowState.Maximized;
            }
        }

        private void FullScreenOff_On_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();
            SettingsManager.Default.FullScreen = !SettingsManager.Default.FullScreen;
            SettingsManager.Save();
            FullScreenToggle.IsChecked = SettingsManager.Default.FullScreen;
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
            SettingsManager.Default.OP = (int)OPSlider.Value;
            SettingsManager.Save();
        }
        private void MemToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SettingsManager.Default.EnableMemeSound = !SettingsManager.Default.EnableMemeSound;
            SettingsManager.Save();
            MemToggle.IsChecked = SettingsManager.Default.EnableMemeSound;
            Click();
        }
        private void DebugOff_On_Click(object sender, RoutedEventArgs e)
        {
            Click();
            SettingsManager.Default.EnableLog = !SettingsManager.Default.EnableLog;
            SettingsManager.Save();
            DebugToggle.IsChecked = SettingsManager.Default.EnableLog;
        }

        private void CloseLaucnherPlayMinecraft_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();
            SettingsManager.Default.CloseLaucnher = !SettingsManager.Default.CloseLaucnher;
            SettingsManager.Save();
            CloseLauncherToggle.IsChecked = SettingsManager.Default.CloseLaucnher;
        }

        private void DisableGlassEffectToggle_MouseDown(object sender, RoutedEventArgs e)
        {
            Click();

            SettingsManager.Default.DisableGlassEffect = !SettingsManager.Default.DisableGlassEffect;
            SettingsManager.Save();

            _themeService.ToggleGlassEffect(SettingsManager.Default.DisableGlassEffect);
            GlassEffectToggle.IsChecked = !SettingsManager.Default.DisableGlassEffect;
        }

        private void InitializeThemeSelection()
        {
            string currentSettingsTheme = SettingsManager.Default.Them;

            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag.ToString() == currentSettingsTheme)
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedTheme = selectedItem.Tag.ToString();

                Click();

                _themeService.ApplyTheme(selectedTheme);

                if (selectedTheme == "Custom")
                {
                    _themeService.LoadBackgroundImage();
                }

                SettingsManager.Default.Them = selectedTheme;
                SettingsManager.Save();
            }
        }

        private void Background_imageButton_Click(object sender, RoutedEventArgs e) => _themeService.HandleBackgroundImageClick();

        private void OnThemeColorButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagData)
            {
                var parts = tagData.Split('|');
                if (parts.Length == 2)
                {
                    _themeService.HandleColorChange(btn, parts[0], parts[1]);
                }
            }
        }

        private void SaveandAcceptCustomThem_Click(object sender, RoutedEventArgs e) => _themeService.HandleSaveCustomThemeClick();
        private void ResetCustomSetting_Click(object sender, RoutedEventArgs e) => _themeService.HandleResetCustomThemeClick();
        private void LoadScreenBgButton_Click(object sender, RoutedEventArgs e) => _themeService.HandleLoadScreenBackgroundClick();
        private void LoadScreenColorButton_Click(object sender, RoutedEventArgs e) => _themeService.HandleLoadScreenColorClick(LoadScreenColorButton);
        private void EditPhrasesButton_Click(object sender, RoutedEventArgs e) => _themeService.HandleEditPhrasesClick();
        private void ResetLoadScreen_Click(object sender, RoutedEventArgs e) => _themeService.HandleResetLoadScreenClick(LoadScreenColorButton);

        private void CopyLoadScreen_Click(object sender, RoutedEventArgs e)
        {
            string code = _themeService.ExportLoadScreen();
            LoadScreenCodeBox.Text = code;
            Clipboard.SetText(code);
            MascotMessageBox.Show(LocalizationManager.GetString("ThemesCustomization.CopyScreenSuccess", "Код LoadScreen скопійовано!"), "Експорт",  MascotEmotion.Happy);
        }

        private void CopyTheme_Click(object sender, RoutedEventArgs e)
        {
            string code = _themeService.ExportMainTheme();
            ThemeCodeBox.Text = code;
            Clipboard.SetText(code);
            MascotMessageBox.Show(LocalizationManager.GetString("ThemesCustomization.CopyThemeSuccess", "Код теми скопійовано!"), "Експорт", MascotEmotion.Happy);
        }

        private void PasteTheme_Click(object sender, RoutedEventArgs e)
        {
            string code = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : ThemeCodeBox.Text.Trim();
            if (!string.IsNullOrEmpty(code)) _themeService.ImportMainTheme(code);
        }

        private void PasteLoadScreen_Click(object sender, RoutedEventArgs e)
        {
            string code = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : LoadScreenCodeBox.Text.Trim();
            if (!string.IsNullOrEmpty(code)) _themeService.ImportLoadScreen(code);
        }

        private void AutoBackupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.ToggleSwitch toggle)
            {
                SettingsManager.Default.EnableAutoBackup = toggle.IsChecked ?? false;
                SettingsManager.Save();
            }
        }

        private void BackupCountMinus_Click(object sender, RoutedEventArgs e)
        {
            byte current = byte.Parse(BackupCountText.Text);
            if (current > 1)
            {
                current--;
                UpdateBackupCount(current);
            }
        }

        private void BackupCountPlus_Click(object sender, RoutedEventArgs e)
        {
            byte current = byte.Parse(BackupCountText.Text);
            if (current < 20)
            {
                current++;
                UpdateBackupCount(current);
            }
        }

        private void UpdateBackupCount(byte count)
        {
            BackupCountText.Text = count.ToString();
            SettingsManager.Default.MaxAutoBackups = count;
            SettingsManager.Save();
        }
    }
}