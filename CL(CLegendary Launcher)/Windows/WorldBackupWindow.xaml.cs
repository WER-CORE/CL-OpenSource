using CL_CLegendary_Launcher_.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace CL_CLegendary_Launcher_.Windows
{
    public class BackupSource
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class WorldListItem
    {
        public string Name { get; set; }
        public string FolderName { get; set; }
        public string FullPath { get; set; }
        public string IconPath { get; set; }
        public BitmapImage IconBitmap { get; set; }
        public string Version { get; set; }
        public string WorldId { get; set; }
    }

    public partial class WorldBackupWindow : FluentWindow
    {
        private WorldListItem _currentWorld;
        public string CreateBackupBtnText => LocalizationManager.GetString("Backups.CreateBtn", "СТВОРИТИ НОВИЙ БЕКАП");
        public string ZipArchiveText => " • " + LocalizationManager.GetString("Backups.ZipArchiveSuffix", "zip архів");
        public string RestoreBtnText => LocalizationManager.GetString("Backups.RestoreBtn", "Відновити");
        public string DeleteBtnText => LocalizationManager.GetString("Backups.DeleteBtn", "🗑");
        public WorldBackupWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            ApplyLocalization();

            LoadSources();
        }

        private void ApplyLocalization()
        {
            string windowTitle = LocalizationManager.GetString("Backups.ManagerTitle", "Менеджер бекапів світів");
            this.Title = windowTitle;
            TxtHeader.Text = windowTitle;

            TxtSourceLabel.Text = LocalizationManager.GetString("Backups.SourceLabel", "Джерело світів:");
            TxtWorldsList.Text = LocalizationManager.GetString("Backups.ListTitle", "Список світів");
            NoWorldsTxt.Text = LocalizationManager.GetString("Backups.NoWorldsFound", "У цій папці світів немає");

            TxtPlaceholder1.Text = LocalizationManager.GetString("Backups.PlaceholderText", "⬅ Виберіть світ зі списку зліва,");

            TxtHistoryTitle.Text = LocalizationManager.GetString("Backups.History", "Історія відновлення");
            NoBackupsTxt.Text = LocalizationManager.GetString("Backups.NoBackupsForWorld", "Для цього світу ще немає копій");
        }

        private void LoadSources()
        {
            var sources = new List<BackupSource>();
            string rootPath = SettingsManager.Default.PathLacunher;

            sources.Add(new BackupSource
            {
                Name = LocalizationManager.GetString("Backups.GlobalWorlds", "📂 Глобальні світи (Global)"),
                Path = Path.Combine(rootPath, "saves")
            });

            string versionsPath = Path.Combine(rootPath, "CLModpack");
            if (Directory.Exists(versionsPath))
            {
                var dirs = Directory.GetDirectories(versionsPath);
                foreach (var dir in dirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    string verName = dirInfo.Name;

                    var possiblePaths = new List<string>
                    {
                        Path.Combine(dir, "saves"),
                        Path.Combine(dir, "override", "saves"),
                        Path.Combine(dir, "overrides", "saves")
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            sources.Add(new BackupSource
                            {
                                Name = string.Format(LocalizationManager.GetString("Backups.ModpackWorlds", "📦 Збірка: {0}"), verName),
                                Path = path
                            });
                            break;
                        }
                    }
                }
            }
            SourceCombo.ItemsSource = sources;
            SourceCombo.SelectedIndex = 0;
        }

        private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();
            if (SourceCombo.SelectedItem is BackupSource source)
            {
                LoadWorlds(source.Path);
            }
        }

        private void LoadWorlds(string savesPath)
        {
            _currentWorld = null;
            PlaceholderPanel.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Hidden;

            var list = new List<WorldListItem>();

            if (!Directory.Exists(savesPath)) Directory.CreateDirectory(savesPath);

            var dirs = Directory.GetDirectories(savesPath);
            foreach (var dir in dirs)
            {
                string levelDatPath = Path.Combine(dir, "level.dat");
                if (File.Exists(levelDatPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    string iconPath = Path.Combine(dir, "icon.png");

                    if (!File.Exists(iconPath))
                        iconPath = "pack://application:,,,/Icon/IconCL(Common).png";

                    BitmapImage bmp = null;
                    try
                    {
                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(iconPath);
                        bmp.DecodePixelWidth = 64;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                    }
                    catch { bmp = null; }

                    string version = GetVersionFromLevelDat(levelDatPath);
                    string wId = WorldBackupService.GetWorldID(dir);

                    list.Add(new WorldListItem
                    {
                        Name = dirInfo.Name,
                        FolderName = dirInfo.Name,
                        FullPath = dir,
                        IconBitmap = bmp,
                        Version = version,
                        WorldId = wId
                    });
                }
            }

            WorldsListBox.ItemsSource = list;
            NoWorldsTxt.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetVersionFromLevelDat(string path)
        {
            try
            {
                using (FileStream fs = File.OpenRead(path))
                using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
                using (MemoryStream ms = new MemoryStream())
                {
                    gzip.CopyTo(ms);
                    byte[] data = ms.ToArray();
                    for (int i = 0; i < data.Length - 10; i++)
                    {
                        if (data[i] == 0x08 && data[i + 1] == 0x00 && data[i + 2] == 0x04 &&
                            data[i + 3] == (byte)'N' && data[i + 4] == (byte)'a' &&
                            data[i + 5] == (byte)'m' && data[i + 6] == (byte)'e')
                        {
                            int lenIndex = i + 7;
                            if (lenIndex + 1 >= data.Length) break;
                            short strLen = (short)((data[lenIndex] << 8) | data[lenIndex + 1]);

                            if (strLen > 0 && lenIndex + 2 + strLen <= data.Length)
                            {
                                string ver = Encoding.UTF8.GetString(data, lenIndex + 2, strLen);
                                if (char.IsDigit(ver[0]) || ver.Length < 30) return ver;
                            }
                        }
                    }
                }
            }
            catch { return "Unknown"; }
            return "?";
        }

        private void WorldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            if (WorldsListBox.SelectedItem is WorldListItem world)
            {
                _currentWorld = world;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                ContentPanel.Visibility = Visibility.Visible;

                SelectedName.Text = world.Name;
                SelectedVersionText.Text = string.Format(LocalizationManager.GetString("Backups.SelectedWorldVersion", "Версія: {0}"), world.Version);
                SelectedFolder.Text = string.Format(LocalizationManager.GetString("Backups.SelectedWorldFolder", "Папка: {0}"), world.FolderName);
                SelectedIcon.Source = world.IconBitmap;

                RefreshBackups();
            }
        }

        private void RefreshBackups()
        {
            if (_currentWorld == null) return;
            var backups = WorldBackupService.GetBackupsForWorld(_currentWorld.FullPath);
            BackupsList.ItemsSource = backups;
            NoBackupsTxt.Visibility = backups.Count == 0 ? Visibility.Visible : Visibility.Hidden;
        }

        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (_currentWorld == null) return;
            BtnCreate.IsEnabled = false;

            try
            {
                await WorldBackupService.CreateWorldBackupAsync(_currentWorld.FullPath);
                RefreshBackups();
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Backups.CreatedSuccess", "Бекап створено успішно!"),
                    LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Backups.CreateError", "Помилка створення: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Ой"),
                    MascotEmotion.Sad);
            }
            finally
            {
                BtnCreate.IsEnabled = true;
            }
        }

        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            var btn = sender as System.Windows.Controls.Button;
            var backup = btn?.Tag as WorldBackupInfo;
            if (backup == null) return;

            string confirmMsg = string.Format(LocalizationManager.GetString("Backups.RestoreConfirm", "Відновити світ '{0}' до стану від {1:dd.MM HH:mm}?\n\n⚠️ Поточний прогрес буде втрачено!"), _currentWorld.Name, backup.CreationTime);

            if (MascotMessageBox.Ask(confirmMsg, LocalizationManager.GetString("Backups.RestoreConfirmTitle", "Відновлення"), MascotEmotion.Alert))
            {
                this.IsEnabled = false;
                try
                {
                    string savesRoot = Directory.GetParent(_currentWorld.FullPath).FullName;
                    await WorldBackupService.RestoreWorldBackupAsync(backup.FullPath, savesRoot);

                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Backups.RestoreSuccess", "Світ відновлено!"),
                        LocalizationManager.GetString("Dialogs.Success", "Готово"),
                        MascotEmotion.Happy);

                    if (SourceCombo.SelectedItem is BackupSource source) LoadWorlds(source.Path);
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Backups.RestoreError", "Помилка: {0}"), ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Біда"),
                        MascotEmotion.Sad);
                }
                finally
                {
                    this.IsEnabled = true;
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            var btn = sender as System.Windows.Controls.Button;
            var backup = btn?.Tag as WorldBackupInfo;
            if (backup == null) return;

            if (MascotMessageBox.Ask(
                LocalizationManager.GetString("Backups.DeleteConfirm", "Видалити цей архів?"),
                LocalizationManager.GetString("Dialogs.DeleteConfirmTitle", "Видалення"),
                MascotEmotion.Normal))
            {
                try
                {
                    File.Delete(backup.FullPath);
                    RefreshBackups();
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(ex.Message, LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
                }
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}