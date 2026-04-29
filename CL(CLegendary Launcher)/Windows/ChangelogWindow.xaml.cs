using CL_CLegendary_Launcher_.Class;
using System;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class ChangelogWindow : FluentWindow
    {
        private string _versionId;
        private string _versionType;

        public ChangelogWindow(string versionId, string versionType)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _versionId = versionId;
            _versionType = versionType;

            this.Loaded += ChangelogWindow_Loaded;
        }

        private async void ChangelogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Title = string.Format(LocalizationManager.GetString("ChangelogWindow.UpdateTitle", "Оновлення: {0}"), _versionId);

            WikiFallbackText.Text = LocalizationManager.GetString("ChangelogWindow.OldVersionFallback", "Ця версія занадто стара або це снапшот, якого немає в офіційному архіві Mojang.");
            OpenWikiButton.Content = LocalizationManager.GetString("ChangelogWindow.OpenWikiButton", "Читати на Minecraft Wiki");

            string currentLang = SettingsManager.Default.LanguageCode;

            string cacheFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Changelogs");
            System.IO.Directory.CreateDirectory(cacheFolder);
            string cacheFile = System.IO.Path.Combine(cacheFolder, $"{_versionId}_{currentLang}.md");

            try
            {
                if (System.IO.File.Exists(cacheFile))
                {
                    string cachedMarkdown = await System.IO.File.ReadAllTextAsync(cacheFile);
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    ChangelogMarkdownViewer.Markdown = cachedMarkdown;
                    return;
                }

                ChangelogService changelogService = new ChangelogService();
                LoadingText.Text = string.Format(LocalizationManager.GetString("ChangelogWindow.LoadingDescription", "Завантаження опису для {0}..."), _versionId);

                string englishMarkdown = await Task.Run(async () =>
                    await changelogService.GetFullChangelogMarkdownAsync(_versionId)
                );

                if (englishMarkdown == null)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    WikiFallbackPanel.Visibility = Visibility.Visible;
                    return;
                }

                string finalMarkdown;

                if (currentLang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    finalMarkdown = englishMarkdown;
                }
                else
                {
                    LoadingText.Text = LocalizationManager.GetString("ChangelogWindow.Translating", "Перекладаю текст...\n(Це може зайняти кілька секунд, заварюй чай)");

                    finalMarkdown = await Task.Run(async () =>
                        await changelogService.TranslateTextAsync(englishMarkdown, currentLang)
                    );
                }

                await System.IO.File.WriteAllTextAsync(cacheFile, finalMarkdown);

                LoadingPanel.Visibility = Visibility.Collapsed;
                ChangelogMarkdownViewer.Markdown = finalMarkdown;
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ChangelogMarkdownViewer.Markdown = $"# Йой, сталася помилка!\n\n**Деталі:** {ex.Message}";
            }
        }
        private void OpenWikiButton_Click(object sender, RoutedEventArgs e)
        {
            string query;
            string cleanId = _versionId;

            switch (_versionType)
            {
                case "old_alpha": query = $"Java Edition Alpha {cleanId.Replace("a", "")}"; break;
                case "old_beta": query = $"Java Edition Beta {cleanId.Replace("b", "")}"; break;
                case "snapshot": query = $"{cleanId} Java Edition"; break;
                case "release": default: query = $"Java Edition {cleanId}"; break;
            }

            string currentLang = SettingsManager.Default.LanguageCode;

            string baseUrl = currentLang.StartsWith("uk", StringComparison.OrdinalIgnoreCase)
                ? "https://uk.minecraft.wiki"
                : "https://minecraft.wiki";

            string searchUrl = $"{baseUrl}/w/Special:Search?search={Uri.EscapeDataString(query)}&go=Go";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = searchUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                string errorTitle = LocalizationManager.GetString("Dialogs.UrlOpenErrorTitle", "Помилка браузера");
                string errorDesc = string.Format(LocalizationManager.GetString("Dialogs.UrlOpenErrorDesc", "Не вдалося відкрити браузер.\n{0}"), ex.Message);

                MascotMessageBox.Show(errorDesc, errorTitle, MascotEmotion.Alert);
            }
        }
    }
}