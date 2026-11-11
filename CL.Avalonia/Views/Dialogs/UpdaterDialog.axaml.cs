using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Windows
{
    public partial class UpdaterWindow : Window
    {
        private UpdateService _updateService;
        private string localVersion = UpdateService.GetCurrentLauncherVersion();
        private string targetDownloadUrl = "";
        private string installPath = "";
        private string _downloadedFolderName = "win-x64";
        private string _targetVersionString = "";
        private readonly bool _isFlatpak = File.Exists("/.flatpak-info");
        private CL.Core.Models.UpdateInfo? _currentUpdateInfo;
        private Velopack.UpdateManager? _velopackManager;
        private Velopack.UpdateInfo? _velopackUpdateInfo;

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
            BtnUpdate.Content = _isFlatpak
                ? LocalizationManager.GetString("Updater.BtnUpdateFlatpak", "Завантажити оновлення (Flatpak)")
                : LocalizationManager.GetString("Updater.BtnUpdate", "Завантажити та Встановити");
            if (BtnRemindLater != null)
                BtnRemindLater.Content = LocalizationManager.GetString("Updater.BtnRemindLater", "Нагадати пізніше");
            if (BtnSkipVersion != null)
                BtnSkipVersion.Content = LocalizationManager.GetString("Updater.BtnSkipVersion", "Пропустити цю версію");
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

                bool isPrerelease = string.Equals(SettingsManager.Default.UpdateChannel, "Preview", StringComparison.OrdinalIgnoreCase);
                var source = new global::Velopack.Sources.GithubSource("https://github.com/WER-CORE/CL-Win-Edition--Update", null, isPrerelease);
                var mgr = new global::Velopack.UpdateManager(source);

                if (mgr.IsInstalled)
                {
                    var vInfo = await mgr.CheckForUpdatesAsync();
                    if (vInfo != null && vInfo.TargetFullRelease != null)
                    {
                        _velopackManager = mgr;
                        _velopackUpdateInfo = vInfo;
                        _targetVersionString = vInfo.TargetFullRelease.Version.ToString().Trim().TrimStart('v', 'V');
                        VersionText.Text = $"Нова версія: {vInfo.TargetFullRelease.Version} (Поточна: {mgr.CurrentVersion})";
                        StatusText.Text = LocalizationManager.GetString("Updater.Available", "Доступне оновлення!");
                        if (UpdateActionsPanel != null) UpdateActionsPanel.IsVisible = true;
                        if (PathSelectionPanel != null) PathSelectionPanel.IsVisible = false;
                        return;
                    }
                    else
                    {
                        StatusText.Text = LocalizationManager.GetString("Updater.LatestVersion", "У вас остання версія");
                        await Task.Delay(2000);
                        OpenMainLauncher();
                        return;
                    }
                }
            }
            catch { }

            try
            {
                StatusText.Text = LocalizationManager.GetString("Updater.CheckingData", "Перевірка даних...");
                bool isPreview = string.Equals(SettingsManager.Default.UpdateChannel, "Preview", StringComparison.OrdinalIgnoreCase);
                var info = await _updateService.CheckForUpdateFromGitHubAsync("WER-CORE/CL-Win-Edition--Update", isPreview)
                           ?? await _updateService.CheckForUpdateAsync(Secrets.updateInfoUrl);

                if (info == null || string.IsNullOrEmpty(info.Version))
                {
                    OpenMainLauncher();
                    return;
                }

                _currentUpdateInfo = info;
                _targetVersionString = info.Version.Trim().TrimStart('v', 'V');
                targetDownloadUrl = _updateService.GetCorrectDownloadUrl(info);
                _downloadedFolderName = _updateService.GetDownloadedFolderName();

                if (_updateService.IsUpdateAvailable(info.Version, localVersion))
                {
                    VersionText.Text = $"Нова версія: {info.Version} (Поточна: {localVersion})";
                    StatusText.Text = LocalizationManager.GetString("Updater.Available", "Доступне оновлення!");
                    if (UpdateActionsPanel != null) UpdateActionsPanel.IsVisible = true;
                    if (PathSelectionPanel != null) PathSelectionPanel.IsVisible = !_isFlatpak;
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
            if (_isFlatpak)
            {
                UrlOpener.Open("https://github.com/WER-CORE/CL-Win-Edition--Update/releases/");
                OpenMainLauncher();
                return;
            }

            if (UpdateActionsPanel != null) UpdateActionsPanel.IsEnabled = false;
            if (PathSelectionPanel != null) PathSelectionPanel.IsEnabled = false;

            try
            {
                if (_velopackManager != null && _velopackUpdateInfo != null)
                {
                    StatusText.Text = LocalizationManager.GetString("Updater.DownloadingArchive", "Завантаження оновлення...");
                    await _velopackManager.DownloadUpdatesAsync(_velopackUpdateInfo, p =>
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            ProgreesBarDowload.Value = p;
                            SizeText.Text = $"{p}%";
                        });
                    });

                    StatusText.Text = LocalizationManager.GetString("Updater.ReadyToLaunch", "Готово! Перезапуск...");
                    await Task.Delay(1500);
                    _velopackManager.ApplyUpdatesAndRestart(_velopackUpdateInfo.TargetFullRelease);
                    return;
                }

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
                if (UpdateActionsPanel != null) UpdateActionsPanel.IsEnabled = true;
                if (PathSelectionPanel != null) PathSelectionPanel.IsEnabled = true;
            }
        }

        private void BtnRemindLater_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenMainLauncher();
        }

        private void BtnSkipVersion_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_targetVersionString))
            {
                SettingsManager.Default.SkippedUpdateVersion = _targetVersionString;
                SettingsManager.Save();
            }
            OpenMainLauncher();
        }

        private void StartNewVersion()
        {
            string expectedPath = Path.Combine(installPath, _downloadedFolderName, "CL(CLegendary Launcher).exe");
            if (!File.Exists(expectedPath)) expectedPath = Path.Combine(installPath, _downloadedFolderName, "CL.Avalonia.exe");
            if (!File.Exists(expectedPath)) expectedPath = Path.Combine(installPath, "CL(CLegendary Launcher).exe");
            if (!File.Exists(expectedPath)) expectedPath = Path.Combine(installPath, "CL.Avalonia.exe");

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
            Dispatcher.UIThread.Post(() =>
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow is CL_Main && desktop.MainWindow != this)
                    {
                        this.Close();
                        return;
                    }

                    var mainWindow = new CL_Main
                    {
                        DataContext = ((App)global::Avalonia.Application.Current!).ServiceProvider!.GetRequiredService<CL_CLegendary_Launcher_.Avalonia.ViewModels.CL_MainViewModel>()
                    };
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    this.Close();
                }
            });
        }

        private void Header_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void CloseButton_Click(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            OpenMainLauncher();
        }

        private void CloseButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenMainLauncher();
        }
    }
}
