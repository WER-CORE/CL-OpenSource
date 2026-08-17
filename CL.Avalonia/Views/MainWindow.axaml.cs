using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
            this.FindControl<Button>("PlayButton")!.Click += OnPlay;
            this.FindControl<ListBox>("VersionsList")!.SelectionChanged += OnVersionSelected;

            ShowEnvironment();
        }

        private void ShowEnvironment()
        {
            this.FindControl<TextBlock>("PlatformText")!.Text =
                $"{PlatformPaths.OsName()} · .NET {Environment.Version} · Avalonia UI";

            this.FindControl<TextBlock>("PathText")!.Text =
                $"Каталог гри:\n{_launcherPath}";

            var javas = JavaLocator.Detect(_launcherPath);

            var javaBox = this.FindControl<ComboBox>("JavaBox")!;
            javaBox.ItemsSource = javas;
            javaBox.SelectedIndex = javas.Count > 0 ? 0 : -1;

            this.FindControl<TextBlock>("CoreStatusText")!.Text =
                $"Мова: {LocalizationManager.CurrentLanguage}\n" +
                $"Пам'ять для гри: {SettingsManager.Default.OP} МБ\n" +
                $"Java у системі: {(javas.Count > 0 ? string.Join(", ", javas.Select(j => j.MajorVersion).Distinct()) : "не знайдено")}";

            SetStatus("Готово.");
        }

        private async void OnLoadVersions(object? sender, RoutedEventArgs e)
        {
            var button = (Button)sender!;
            var list = this.FindControl<ListBox>("VersionsList")!;

            button.IsEnabled = false;
            SetStatus("Завантаження версій з Mojang...");

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

        private void OnVersionSelected(object? sender, SelectionChangedEventArgs e)
        {
            string? version = SelectedVersion();

            this.FindControl<TextBlock>("SelectedVersionText")!.Text =
                version == null ? string.Empty : $"Обрано: {version}";

            this.FindControl<Button>("PlayButton")!.IsEnabled = version != null;
        }

        private async void OnPlay(object? sender, RoutedEventArgs e)
        {
            string? version = SelectedVersion();
            if (version == null)
                return;

            var button = (Button)sender!;
            button.IsEnabled = false;

            try
            {
                var request = new LaunchRequest
                {
                    VersionName = version,
                    Nickname = Nickname(),
                    MemoryMb = SettingsManager.Default.OP,
                    JavaPath = (this.FindControl<ComboBox>("JavaBox")!.SelectedItem as JavaInstallation)?.Executable
                };

                var progress = new Progress<string>(SetStatus);
                var service = new MinecraftLaunchService(_launcherPath);

                Process process = await service.LaunchAsync(request, progress);

                SetStatus($"Гра запущена, PID {process.Id}.");
                WatchExit(process, version);
            }
            catch (Exception ex)
            {
                SetStatus($"Не вдалося запустити: {ex.Message}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void WatchExit(Process process, string version)
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.UIThread.Post(() =>
                SetStatus($"{version} завершено з кодом {process.ExitCode}."));
        }

        private string? SelectedVersion()
            => this.FindControl<ListBox>("VersionsList")!.SelectedItem as string;

        private string Nickname()
        {
            string? typed = this.FindControl<TextBox>("NicknameBox")!.Text;
            return string.IsNullOrWhiteSpace(typed) ? "Player" : typed.Trim();
        }

        private void OnOpenSite(object? sender, RoutedEventArgs e)
            => WebHelper.OpenUrl("https://discord.gg/RhyZjACs2U");

        private void SetStatus(string text)
            => this.FindControl<TextBlock>("StatusText")!.Text = text;
    }
}
