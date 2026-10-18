using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SettingsPageViewModel.LanguageConfigurationChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public void ApplyLocalization()
    {
        Loc_Settings_TitleMinecraft.Text = LocalizationManager.GetString("Settings.Minecraft", "Майнкрафт");
        Loc_Settings_DevConsoleTitle.Text = LocalizationManager.GetString("Settings.DevConsoleTitle", "Логи (Консоль розробника)");
        Loc_Settings_DevConsoleDesc.Text = LocalizationManager.GetString("Settings.DevConsoleDesc", "Відкриває вікно з технічною інформацією під час гри");
        Loc_Settings_FullScreenTitle.Text = LocalizationManager.GetString("Settings.FullScreenTitle", "Повноекранний режим");
        Loc_Settings_FullScreenDesc.Text = LocalizationManager.GetString("Settings.FullScreenDesc", "Запускає гру на весь монітор без рамок вікна");
        Loc_Settings_ResolutionTitle.Text = LocalizationManager.GetString("Settings.ResolutionTitle", "Роздільна здатність екрану");
        Loc_Settings_ResolutionDesc.Text = LocalizationManager.GetString("Settings.ResolutionDesc", "Розмір вікна гри.");
        Loc_Settings_RamTitle.Text = LocalizationManager.GetString("Settings.RamTitle", "Оперативна пам’ять (RAM)");
        Loc_Settings_RamDesc.Text = LocalizationManager.GetString("Settings.RamDesc", "Пам'ять для Java. Рекомендовано 4096 MB. Не ставте максимум!");
        Loc_Settings_PathTitle.Text = LocalizationManager.GetString("Settings.PathTitle", "Шлях папки для лаунчера");
        Loc_Settings_PathDesc.Text = LocalizationManager.GetString("Settings.PathDesc", "Місце зберігання світів, модів та налаштувань");
        Loc_Settings_BackupTitle.Text = LocalizationManager.GetString("Settings.BackupTitle", "Автоматичні бекапи світів");
        Loc_Settings_BackupDesc.Text = LocalizationManager.GetString("Settings.BackupDesc", "Створює копію світу перед кожним запуском гри");
        Loc_Settings_BackupCountTitle.Text = LocalizationManager.GetString("Settings.BackupCountTitle", "Кількість копій");
        Loc_Settings_BackupCountDesc.Text = LocalizationManager.GetString("Settings.BackupCountDesc", "Максимальна кількість архівів. Старі будуть видалятися");

        Loc_Settings_TitleLauncher.Text = LocalizationManager.GetString("Settings.Launcher", "Запускач");
        LanguageTitleTXT.Text = LocalizationManager.GetString("Settings.LanguageTitle", "Мова інтерфейсу (Language)");
        LanguageDescTXT.Text = LocalizationManager.GetString("Settings.LanguageDesc", "Оберіть мову для лаунчера. Оновлюється миттєво.");
        HelpTranslateTXT.Content = LocalizationManager.GetString("Settings.HelpTranslate", "Як допомогти з перекладом?");
        Loc_Settings_SoundTitle.Text = LocalizationManager.GetString("Settings.SoundTitle", "Звуки інтерфейсу");
        Loc_Settings_SoundDesc.Text = LocalizationManager.GetString("Settings.SoundDesc", "Озвучка кліків та натискань кнопок");
        Loc_Settings_ScaleTitle.Text = LocalizationManager.GetString("Settings.ScaleTitle", "Масштаб інтерфейсу");
        Loc_Settings_ScaleDesc.Text = LocalizationManager.GetString("Settings.ScaleDesc", "Збільшує або зменшує розмір всіх елементів у лаунчері");
        Loc_Settings_CrashReportTitle.Text = LocalizationManager.GetString("Settings.CrashReportTitle", "Звіти про помилки");
        Loc_Settings_CrashReportDesc.Text = LocalizationManager.GetString("Settings.CrashReportDesc", "Анонімне відправлення даних про критичні збої розробнику для їх виправлення");
        Loc_Settings_CloseLaunchTitle.Text = LocalizationManager.GetString("Settings.CloseLaunchTitle", "Закриття лаунчера під час гри");
        Loc_Settings_CloseLaunchDesc.Text = LocalizationManager.GetString("Settings.CloseLaunchDesc", "Економить ресурси ПК, закриваючи меню після старту");
        Loc_Settings_AutoDepTitle.Text = LocalizationManager.GetString("Settings.AutoDepTitle", "Автозавантаження залежностей");
        Loc_Settings_AutoDepDesc.Text = LocalizationManager.GetString("Settings.AutoDepDesc", "Автоматично додає потрібні бібліотеки (API) до модів");
        Loc_Settings_GlassTitle.Text = LocalizationManager.GetString("Settings.GlassTitle", "Ефект прозорості (Скло)");
        Loc_Settings_GlassDesc.Text = LocalizationManager.GetString("Settings.GlassDesc", "Напівпрозорий фон вікна (Mica). Вимкніть, якщо лагає");

        Loc_Settings_ModulesTitle.Text = LocalizationManager.GetString("Settings.ModulesTitle", "Модульність (Кастомізація)");
        Loc_Settings_ModulesDesc.Text = LocalizationManager.GetString("Settings.ModulesDesc", "Оберіть готовий шаблон або вимкніть непотрібні функції власноруч:");
        Loc_Settings_PresetLiteBtn.Content = LocalizationManager.GetString("Settings.PresetLiteBtn", "Мінімалізм");
        Loc_Settings_PresetBalanceBtn.Content = LocalizationManager.GetString("Settings.PresetBalanceBtn", "Баланс");
        Loc_Settings_PresetUltraBtn.Content = LocalizationManager.GetString("Settings.PresetUltraBtn", "Ультра");
        ToolTip.SetTip(Loc_Settings_PresetLiteBtn, LocalizationManager.GetString("Settings.PresetLiteTooltip", "Вимкнути все зайве"));
        ToolTip.SetTip(Loc_Settings_PresetBalanceBtn, LocalizationManager.GetString("Settings.PresetBalanceTooltip", "Оптимальний набір функцій"));
        ToolTip.SetTip(Loc_Settings_PresetUltraBtn, LocalizationManager.GetString("Settings.PresetUltraTooltip", "Увімкнути всі фічі (Комбайн)"));

        Loc_Settings_ModModpacksTitle.Text = LocalizationManager.GetString("Settings.ModModpacksTitle", "Вкладка «Збірки»");
        Loc_Settings_ModModpacksDesc.Text = LocalizationManager.GetString("Settings.ModModpacksDesc", "Відображає розділ створення та гри з модовими збірками");
        Loc_Settings_ModModsTitle.Text = LocalizationManager.GetString("Settings.ModModsTitle", "Вкладка «Моди»");
        Loc_Settings_ModModsDesc.Text = LocalizationManager.GetString("Settings.ModModsDesc", "Вбудований завантажувач модифікацій, ресурспаків та шейдерів");
        Loc_Settings_ModServersTitle.Text = LocalizationManager.GetString("Settings.ModServersTitle", "Вкладка «Сервери»");
        Loc_Settings_ModServersDesc.Text = LocalizationManager.GetString("Settings.ModServersDesc", "Загальний моніторинг серверів (Плашка партнерів не вимикається)");
        Loc_Settings_ModGalleryTitle.Text = LocalizationManager.GetString("Settings.ModGalleryTitle", "Вкладка «Галерея»");
        Loc_Settings_ModGalleryDesc.Text = LocalizationManager.GetString("Settings.ModGalleryDesc", "Перегляд та управління скріншотами з гри");
        Loc_Settings_ModAITitle.Text = LocalizationManager.GetString("Settings.ModAITitle", "ШІ-Асистент (Agent C.L.)");
        Loc_Settings_ModAIDesc.Text = LocalizationManager.GetString("Settings.ModAIDesc", "Розумний аналіз помилок у вікні логів гри");
        Loc_Settings_ModFilesTitle.Text = LocalizationManager.GetString("Settings.ModFilesTitle", "Кнопка «Файли та Бекапи»");
        Loc_Settings_ModFilesDesc.Text = LocalizationManager.GetString("Settings.ModFilesDesc", "Швидкий доступ до папок та управління архівами світів");
        Loc_Settings_SubFilesMods.Text = LocalizationManager.GetString("Settings.SubFilesMods", "↳ Показувати функцію «Відкрити папку...»");
        Loc_Settings_SubFilesBackups.Text = LocalizationManager.GetString("Settings.SubFilesBackups", "↳ Показувати функцію «Менеджер світів (Backups)»");
        Loc_Settings_ModInfoTitle.Text = LocalizationManager.GetString("Settings.ModInfoTitle", "Кнопка «Інформація»");
        Loc_Settings_ModInfoDesc.Text = LocalizationManager.GetString("Settings.ModInfoDesc", "Меню з посиланнями на новини, Wiki, GitHub та подяки");
        Loc_Settings_SubInfoBug.Text = LocalizationManager.GetString("Settings.SubInfoBug", "↳ Показувати «Повідомити про баг»");
        Loc_Settings_SubInfoNews.Text = LocalizationManager.GetString("Settings.SubInfoNews", "↳ Показувати «Новини запускача»");
        Loc_Settings_SubInfoWiki.Text = LocalizationManager.GetString("Settings.SubInfoWiki", "↳ Показувати «Документація (Wiki)»");
        Loc_Settings_SubInfoGithub.Text = LocalizationManager.GetString("Settings.SubInfoGithub", "↳ Показувати «GitHub (Код)»");
        Loc_Settings_SubInfoCredits.Text = LocalizationManager.GetString("Settings.SubInfoCredits", "↳ Показувати «Подяки (Автори та Перекладачі)»");
        Loc_Settings_SubInfoSupport.Text = LocalizationManager.GetString("Settings.SubInfoSupport", "↳ Показувати кнопки підтримки та донатів");
        Loc_Settings_ModChangelogTitle.Text = LocalizationManager.GetString("Settings.ModChangelogTitle", "Блок «Що нового? (Changelog)»");
        Loc_Settings_ModChangelogDesc.Text = LocalizationManager.GetString("Settings.ModChangelogDesc", "Відображає список змін оновлень гри на головному екрані");
        Loc_Settings_ModActionsTitle.Text = LocalizationManager.GetString("Settings.ModActionsTitle", "Блок «Останні дії»");
        Loc_Settings_ModActionsDesc.Text = LocalizationManager.GetString("Settings.ModActionsDesc", "Відображає історію запусків та дій на головному екрані");
        Loc_Settings_ModDiscordTitle.Text = LocalizationManager.GetString("Settings.ModDiscordTitle", "Discord RPC (Ігрова активність)");
        Loc_Settings_ModDiscordDesc.Text = LocalizationManager.GetString("Settings.ModDiscordDesc", "Відображає ваш статус та ігрову активність у профілі Discord");
        Loc_Settings_ModStatsTitle.Text = LocalizationManager.GetString("Settings.ModStatsTitle", "Функція Статистики");

        Loc_Settings_ThemeTitle.Text = LocalizationManager.GetString("Settings.ThemeTitle", "Тема інтерфейсу");
        Loc_Settings_ThemeDesc.Text = LocalizationManager.GetString("Settings.ThemeDesc", "Оберіть готовий стиль або створіть власний та поділіться ним");
        Loc_Settings_ThemeSection.Text = LocalizationManager.GetString("Settings.ThemeSection", "Колір секції");
        Loc_Settings_ThemeBg.Text = LocalizationManager.GetString("Settings.ThemeBg", "Колір фону");
        Loc_Settings_ThemeAccent.Text = LocalizationManager.GetString("Settings.ThemeAccent", "Додатковий колір");
        Loc_Settings_ThemeText.Text = LocalizationManager.GetString("Settings.ThemeText", "Колір тексту");
        Loc_Settings_ThemeBtn.Text = LocalizationManager.GetString("Settings.ThemeBtn", "Колір кнопки");
        Loc_Settings_ThemeImage.Text = LocalizationManager.GetString("Settings.ThemeImage", "Зображення фону");
        if (Background_imageButton != null) Background_imageButton.Content = LocalizationManager.GetString("Settings.FileBtn", "Файл...");
        if (Loc_TxtResetTheme != null) Loc_TxtResetTheme.Text = LocalizationManager.GetString("Settings.ResetBtn", "Скинути");
        if (Loc_Settings_ThemeCodeTitle != null) Loc_Settings_ThemeCodeTitle.Text = LocalizationManager.GetString("Settings.ThemeCodeTitle", "Код теми (Для головного вікна)");
        if (Loc_BtnCopyTheme != null) Loc_BtnCopyTheme.Content = LocalizationManager.GetString("Settings.CopyBtn", "Копіювати");
        if (Loc_BtnPasteTheme != null) Loc_BtnPasteTheme.Content = LocalizationManager.GetString("Settings.PasteBtn", "Вставити");

        Loc_Settings_LoadScreenTitle.Text = LocalizationManager.GetString("Settings.LoadScreenTitle", "Завантажувальний екран");
        Loc_Settings_LoadScreenDesc.Text = LocalizationManager.GetString("Settings.LoadScreenDesc", "Налаштуйте вигляд вікна запуску (Splash Screen) та поділіться своїм стилем");
        if (Loc_Settings_LoadScreenBg != null) Loc_Settings_LoadScreenBg.Text = LocalizationManager.GetString("Settings.LoadScreenBg", "Зображення фону");
        if (Loc_Settings_LoadScreenColor != null) Loc_Settings_LoadScreenColor.Text = LocalizationManager.GetString("Settings.LoadScreenColor", "Колір смужки завантаження");
        if (Loc_Settings_LoadScreenPhrases != null) Loc_Settings_LoadScreenPhrases.Text = LocalizationManager.GetString("Settings.LoadScreenPhrases", "Кастомні фрази");
        if (LoadScreenBgButton != null) LoadScreenBgButton.Content = LocalizationManager.GetString("Settings.FileBtn", "Файл...");
        if (EditPhrasesButton != null) EditPhrasesButton.Content = LocalizationManager.GetString("Settings.TextBtn", "Текст...");
        if (Loc_TxtResetLoadScreen != null) Loc_TxtResetLoadScreen.Text = LocalizationManager.GetString("Settings.ResetBtn", "Скинути");
        if (Loc_Settings_LoadScreenCodeTitle != null) Loc_Settings_LoadScreenCodeTitle.Text = LocalizationManager.GetString("Settings.LoadScreenCodeTitle", "Код теми (Завантажувального екрану)");
        if (Loc_BtnCopyLoadScreen != null) Loc_BtnCopyLoadScreen.Content = LocalizationManager.GetString("Settings.CopyBtn", "Копіювати");
        if (Loc_BtnPasteLoadScreen != null) Loc_BtnPasteLoadScreen.Content = LocalizationManager.GetString("Settings.PasteBtn", "Вставити");
    }
}
