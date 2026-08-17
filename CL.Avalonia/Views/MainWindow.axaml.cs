using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CL_CLegendary_Launcher_.Class;

namespace CL_CLegendary_Launcher_.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private readonly string _launcherPath;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _launcherPath = PlatformPaths.DefaultLauncherPath();

            SettingsManager.Load();
            LocalizationManager.LoadLanguage(SettingsManager.Default.LanguageCode ?? "uk_UA");

            UrlOpener.ErrorHandler = (ex, url) =>
                SetStatus($"Не вдалося відкрити {url}: {ex.Message}");

            this.FindControl<Button>("LoadVersionsButton")!.Click += OnLoadVersions;
            this.FindControl<Button>("OpenSiteButton")!.Click += OnOpenSite;

            ShowEnvironment();
        }

        private void ShowEnvironment()
        {
            this.FindControl<TextBlock>("PlatformText")!.Text =
                $"{PlatformPaths.OsName()} · .NET {Environment.Version} · Avalonia UI";

            this.FindControl<TextBlock>("PathText")!.Text =
                $"Каталог гри:\n{_launcherPath}";

            var javas = JavaLocator.Detect(_launcherPath);
            string javaLine = javas.Count > 0
                ? string.Join(", ", javas.Select(j => j.MajorVersion).Distinct())
                : "не знайдено";

            this.FindControl<TextBlock>("CoreStatusText")!.Text =
                $"Мова: {LocalizationManager.CurrentLanguage}\n" +
                $"Пам'ять для гри: {SettingsManager.Default.OP} МБ\n" +
                $"Java у системі: {javaLine}";

            SetStatus("Готово.");
        }

        private async void OnLoadVersions(object? sender, RoutedEventArgs e)
        {
            var button = (Button)sender!;
            var list = this.FindControl<ListBox>("VersionsList")!;

            button.IsEnabled = false;
            SetStatus("Завантаження версій з Mojang…");

            try
            {
                var service = new VersionService(_launcherPath);
                var versions = await service.GetFilteredVersionsAsync(string.Empty, true, false, false, false);

                list.ItemsSource = versions;
                SetStatus($"Отримано {versions.Count} релізів через CL.Core.");
            }
            catch (Exception ex)
            {
                SetStatus($"Помилка: {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OnOpenSite(object? sender, RoutedEventArgs e)
            => WebHelper.OpenUrl("https://discord.gg/RhyZjACs2U");

        private void SetStatus(string text)
            => this.FindControl<TextBlock>("StatusText")!.Text = text;
    }
}
