using CL.Core.Services;
using CL_CLegendary_Launcher_.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_
{
    public partial class UpdaterWindow : FluentWindow
    {
        private CL.Core.Services.UpdateService _updateService;
        private string localVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        private string targetDownloadUrl = "";
        private string installPath = "";
        private string _downloadedFolderName = "win-x64";
        private CL.Core.Models.UpdateInfo _currentUpdateInfo;

        public UpdaterWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            ApplyLocalization();
            _updateService = new CL.Core.Services.UpdateService();

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

        private async void UpdaterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1000);
            await CheckForUpdatesAsync();
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "Folder Selection." };
            if (dialog.ShowDialog() == true)
            {
                installPath = Path.GetDirectoryName(dialog.FileName);
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
                    ShowError(LocalizationManager.GetString("Updater.DataReadError", "Не вдалося прочитати дані оновлення."));
                    return;
                }

                _currentUpdateInfo = info;
                targetDownloadUrl = _updateService.GetCorrectDownloadUrl(info);
                _downloadedFolderName = _updateService.GetDownloadedFolderName();

                if (_updateService.IsUpdateAvailable(info.Version, localVersion))
                {
                    VersionText.Text = $"Нова версія: {info.Version} (Поточна: {localVersion})";
                    StatusText.Text = LocalizationManager.GetString("Updater.Available", "Доступне оновлення!");
                    if (BtnUpdate != null) BtnUpdate.Visibility = Visibility.Visible;
                    if (PathSelectionPanel != null) PathSelectionPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Text = LocalizationManager.GetString("Updater.LatestVersion", "У вас остання версія");
                    await Task.Delay(2000);
                    OpenMainLauncher();
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Updater.CheckError", "Помилка перевірки: {0}"), ex.Message));
                OpenMainLauncher();
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
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
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Updater.FailedError", "Помилка оновлення: {0}"), ex.Message));
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
                Application.Current.Shutdown();
            }
            else
            {
                ShowError(string.Format(LocalizationManager.GetString("Updater.FileNotFoundError", "Не знайдено файл запуску!"), expectedPath));
            }
        }

        private void ShowError(string message) => MascotMessageBox.Show(message, LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
        private void OpenMainLauncher() { new Windows.LoadScreen().Show(); Close(); }
    }
}