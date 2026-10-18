using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CL.Core.Models;
using CL.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Windows
{
    public partial class UpdaterWindow : Window
    {
        private UpdateService _updateService;
        private string localVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        private string targetDownloadUrl = "";
        private string installPath = "";
        private string _downloadedFolderName = "win-x64";
        private UpdateInfo? _currentUpdateInfo;

        public UpdaterWindow()
        {
            InitializeComponent();
            ApplyLocalization();
            _updateService = new UpdateService();

            VersionText.Text = string.Format(LocalizationManager.GetString("Updater.CurrentVersion", "Ваша версія: {0}"), localVersion);
            installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"CL_Launcher_v{localVersion}");

            if (PathTextBox != null) PathTextBox.Text = installPath;
            Loaded += UpdaterWindow_Loaded;
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Updater.WindowTitle", "Оновлення CL Launcher");
            TxtNewVersion.Text = LocalizationManager.GetString("Updater.NewVersionAvailable", "Доступна нова версія!");
            TxtPathSelection.Text = LocalizationManager.GetString("Updater.PathSelection", "Шлях встановлення:");
            BtnUpdate.Content = LocalizationManager.GetString("Updater.BtnUpdate", "Завантажити та Встановити");
        }

        private async void UpdaterWindow_Loaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            await Task.Delay(1000);
            await CheckForUpdatesAsync();
        }

        private async void SelectPath_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions { Title = "Оберіть папку" };
            var result = await StorageProvider.OpenFolderPickerAsync(options);

            if (result != null && result.Count > 0)
            {
                installPath = result[0].Path.LocalPath;
                PathTextBox.Text = installPath;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                StatusText.Text = LocalizationManager.GetString("Updater.CheckingData", "Перевірка даних...");
                var info = await _updateService.CheckForUpdateAsync(Secrets.updateInfoUrl);

                if (info == null || string.IsNullOrEmpty(info.Version))
                {
                    OpenMainLauncher();
                    return;
                }

                _currentUpdateInfo = info;
                targetDownloadUrl = _updateService.GetCorrectDownloadUrl(info);
                _downloadedFolderName = _updateService.GetDownloadedFolderName();

                if (_updateService.IsUpdateAvailable(info.Version, localVersion))
                {
                    VersionText.Text = $"Нова версія: {info.Version} (Поточна: {localVersion})";
                    StatusText.Text = LocalizationManager.GetString("Updater.Available", "Доступне оновлення!");
                    if (BtnUpdate != null) BtnUpdate.IsVisible = true;
                    if (PathSelectionPanel != null) PathSelectionPanel.IsVisible = true;
                }
                else
                {
                    StatusText.Text = LocalizationManager.GetString("Updater.LatestVersion", "У вас остання версія");
                    await Task.Delay(2000);
                    OpenMainLauncher();
                }
            }
            catch (Exception)
            {
                OpenMainLauncher();
            }
        }

        private async void BtnUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            BtnUpdate.IsEnabled = false;
            PathSelectionPanel.IsEnabled = false;

            try
            {
                StatusText.Text = LocalizationManager.GetString("Updater.DownloadingArchive", "Завантаження архіву...");
                var progress = new Progress<(double percent, string size)>(p =>
                {
                    ProgreesBarDowload.Value = p.percent;
                    SizeText.Text = p.size;
                });
                await _updateService.DownloadUpdateAsync(targetDownloadUrl, progress);

                StatusText.Text = LocalizationManager.GetString("Updater.Extracting", "Розпакування...");
                await Task.Run(() => _updateService.ExtractUpdate(installPath));

                StatusText.Text = LocalizationManager.GetString("Updater.Migrating", "Перенесення даних...");
                string sourceBasePath = AppDomain.CurrentDomain.BaseDirectory;
                await Task.Run(() => _updateService.MigrateUserData(sourceBasePath, installPath, _downloadedFolderName));

                StatusText.Text = LocalizationManager.GetString("Updater.ReadyToLaunch", "Готово! Запуск...");
                await Task.Delay(1500);
                StartNewVersion();
            }
            catch (Exception)
            {
                if (BtnUpdate != null) BtnUpdate.IsEnabled = true;
                if (PathSelectionPanel != null) PathSelectionPanel.IsEnabled = true;
            }
        }

        private void StartNewVersion()
        {
            string expectedPath = Path.Combine(installPath, _downloadedFolderName, "CL(CLegendary Launcher).exe");
            if (!File.Exists(expectedPath)) expectedPath = Path.Combine(installPath, "CL(CLegendary Launcher).exe");

            if (File.Exists(expectedPath))
            {
                Process.Start(new ProcessStartInfo { FileName = expectedPath, WorkingDirectory = Path.GetDirectoryName(expectedPath), UseShellExecute = true });
                Environment.Exit(0);
            }
            else
            {
                this.Close();
            }
        }

        private void OpenMainLauncher()
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var loadScreen = new CL_CLegendary_Launcher_.Avalonia.Views.Controls.LoadScreenWindow();
                desktop.MainWindow = loadScreen;
                loadScreen.Show();
                this.Close();
            }
        }

        private void Header_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void CloseButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
