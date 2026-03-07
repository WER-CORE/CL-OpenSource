using CL_CLegendary_Launcher_.Class;
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
        private readonly string tempZipPath = Path.Combine(Path.GetTempPath(), "launcher_update.zip");
        private string localVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        private string targetDownloadUrl = "";
        private string installPath = "";
        private string _downloadedFolderName = "win-x64";

        public UpdaterWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            ApplyLocalization();

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

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                StatusText.Text = LocalizationManager.GetString("Updater.CheckingData", "Отримання даних...");

                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CL-Launcher-Updater");

                string json = await client.GetStringAsync(Secrets.updateInfoUrl);
                var info = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (info == null || string.IsNullOrEmpty(info.Version))
                {
                    ShowError(LocalizationManager.GetString("Updater.DataReadError", "Не вдалося прочитати дані про оновлення."));
                    return;
                }

                targetDownloadUrl = GetCorrectUrlAndSetFolder(info);

                if (IsUpdateAvailable(info.Version, localVersion))
                {
                    VersionText.Text = $"Нова версія: {info.Version} (Поточна: {localVersion})";
                    StatusText.Text = LocalizationManager.GetString("Updater.Available", "Доступне оновлення!");

                    if (PathSelectionPanel != null) PathSelectionPanel.Visibility = Visibility.Visible;
                    if (BtnUpdate != null) BtnUpdate.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Text = LocalizationManager.GetString("Updater.LatestVersion", "У вас найновіша версія.");
                    await Task.Delay(1500);
                    OpenMainLauncher();
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Updater.CheckError", "Помилка перевірки: {0}"), ex.Message));
                OpenMainLauncher();
            }
        }

        private string GetCorrectUrlAndSetFolder(UpdateInfo info)
        {
            bool is64BitOS = Environment.Is64BitOperatingSystem;
            if (!is64BitOS && !string.IsNullOrEmpty(info.UrlX86))
            {
                _downloadedFolderName = "win-x86";
                return info.UrlX86;
            }
            _downloadedFolderName = "win-x64";
            return info.UrlDefault;
        }

        private bool IsUpdateAvailable(string newVer, string currentVer)
        {
            string cleanNew = newVer?.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase) ?? "0.0.0";
            string cleanCurrent = currentVer?.Trim().Replace("v", "", StringComparison.OrdinalIgnoreCase) ?? "0.0.0";
            bool v1Success = Version.TryParse(cleanNew, out Version vRemote);
            bool v2Success = Version.TryParse(cleanCurrent, out Version vLocal);
            return v1Success && v2Success ? vRemote > vLocal : !string.Equals(cleanNew, cleanCurrent, StringComparison.OrdinalIgnoreCase);
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

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdate.IsEnabled = false;
            PathSelectionPanel.IsEnabled = false;

            try
            {
                StatusText.Text = LocalizationManager.GetString("Updater.DownloadingArchive", "Завантаження архіву...");
                await DownloadFileAsync(targetDownloadUrl);

                StatusText.Text = LocalizationManager.GetString("Updater.Extracting", "Розпакування...");
                await Task.Run(() => ExtractZipSafe(tempZipPath, installPath));

                StatusText.Text = LocalizationManager.GetString("Updater.Migrating", "Міграція налаштувань...");
                await Task.Run(() => MigrateUserData());

                StatusText.Text = LocalizationManager.GetString("Updater.ReadyToLaunch", "Готово! Запуск...");
                await Task.Delay(1500);
                StartNewVersion();
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Updater.FailedError", "Помилка оновлення: {0}"), ex.Message));
                BtnUpdate.IsEnabled = true;
                PathSelectionPanel.IsEnabled = true;
            }
        }

        private async Task DownloadFileAsync(string url)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var contentStream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (totalBytes > 0)
                {
                    double percent = (double)totalRead / totalBytes * 100;
                    Dispatcher.Invoke(() => { ProgreesBarDowload.Value = percent; SizeText.Text = $"{totalRead / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB"; });
                }
            }
        }

        private void ExtractZipSafe(string archivePath, string destination)
        {
            if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);
            ZipFile.ExtractToDirectory(archivePath, destination, true);
            try { File.Delete(archivePath); } catch { }
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
        private void MigrateUserData()
        {
            try
            {
                string sourceBasePath = AppDomain.CurrentDomain.BaseDirectory;
                string destBasePath = installPath;
                string targetDataRoot = Path.Combine(destBasePath, _downloadedFolderName);
                if (!Directory.Exists(targetDataRoot))
                {
                    targetDataRoot = destBasePath;
                }

                string sourceDataFolder = Path.Combine(sourceBasePath, "Data");
                string destDataFolder = Path.Combine(targetDataRoot, "Data");

                if (Directory.Exists(sourceDataFolder))
                {
                    Dispatcher.Invoke(() => StatusText.Text = LocalizationManager.GetString("Updater.MigratingStatus", "Міграція налаштувань та даних..."));
                    CopyDirectorySmart(sourceDataFolder, destDataFolder);
                }

                try
                {
                    string newUserSavesPath = Path.Combine(destDataFolder, "UserSaves.json");
                    if (!Directory.Exists(destDataFolder)) Directory.CreateDirectory(destDataFolder);
                    string jsonSettings = JsonSerializer.Serialize(SettingsManager.Default, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(newUserSavesPath, jsonSettings);
                }
                catch (Exception jsonEx)
                {
                    Debug.WriteLine($"Помилка міграції UserSaves.json: {jsonEx.Message}");
                }

                string[] configFiles = Directory.GetFiles(sourceBasePath, "*.json", SearchOption.TopDirectoryOnly);
                foreach (string file in configFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.EndsWith(".runtimeconfig.json") || fileName.EndsWith(".deps.json")) continue;
                    string destFile = Path.Combine(targetDataRoot, fileName);
                    try { File.Copy(file, destFile, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Загальна помилка міграції: {ex.Message}");
            }
        }

        private void CopyDirectorySmart(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                try
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    bool overwrite = file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
                    file.CopyTo(targetFilePath, overwrite);
                }
                catch (IOException) { }
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                try
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectorySmart(subDir.FullName, newDestinationDir);
                }
                catch { }
            }
        }
        private void ShowError(string message) => MascotMessageBox.Show(message, LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
        private void OpenMainLauncher() { new Windows.LoadScreen().Show(); Close(); }
    }
}