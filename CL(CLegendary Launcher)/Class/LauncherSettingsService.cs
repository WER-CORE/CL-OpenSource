using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CL_CLegendary_Launcher_.Class
{
    public class LauncherSettingsService
    {
        private readonly CL_Main_ _main;

        public LauncherSettingsService(CL_Main_ main)
        {
            _main = main;
        }

        public void Initialize()
        {
            InitLauncherPath();
            InitMemorySlider();
        }

        private void InitLauncherPath()
        {
            if (string.IsNullOrWhiteSpace(SettingsManager.Default.PathLacunher))
            {
                string basePath;

                if (OperatingSystem.IsWindows())
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");
                }
                else if (OperatingSystem.IsLinux())
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clminecraft");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CLMinecraft");
                }
                else
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clminecraft");
                }

                SettingsManager.Default.PathLacunher = basePath;
                SettingsManager.Save();
            }

            _main.LauncherFloderButton.Content = SettingsManager.Default.PathLacunher;
        }

        private void InitMemorySlider()
        {
            double totalMemoryInMB = GetTotalMemoryInMB() / 2;
            _main.OPSlider.Maximum = (int)totalMemoryInMB;
            _main.OPSlider.Value = SettingsManager.Default.OP;
            _main.SliderOPTXT.Content = $"{SettingsManager.Default.OP:0}MB";
        }

        private double GetTotalMemoryInMB()
        {
            try
            {
                double totalMemoryInBytes = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
                return totalMemoryInBytes / (1024 * 1024);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("GameLaunch.MemoryCheckErrorDesc", "Я намагалася дізнатися, скільки у тебе оперативної пам'яті, але не вийшло.\nТому я встановила 2 ГБ як безпечний варіант.\n\nПомилка: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.MemoryCheckErrorTitle", "Скільки пам'яті?"),
                    MascotEmotion.Confused);
                return 2048;
            }
        }
        public async void HandleChangePathClick()
        {
            _main.Click();

            using (var openFileDlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                openFileDlg.Description = LocalizationManager.GetString("GameLaunch.SelectFolderDesc", "Виберіть новий шлях для теки .ClMinecraft");

                if (openFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string oldPath = SettingsManager.Default.PathLacunher;
                    string selectedPath = openFileDlg.SelectedPath;
                    string newPath = Path.Combine(selectedPath, ".ClMinecraft");

                    if (string.Equals(Path.GetFullPath(oldPath).TrimEnd('\\'), Path.GetFullPath(newPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("GameLaunch.SameFolderDesc", "Ви вибрали ту саму папку!"),
                            LocalizationManager.GetString("GameLaunch.SameFolderTitle", "Увага"),
                            MascotEmotion.Confused);
                        return;
                    }

                    _main.Cursor = System.Windows.Input.Cursors.Wait;

                    try
                    {
                        await Task.Run(() =>
                        {
                            if (Directory.Exists(oldPath))
                            {
                                CopyDirectoryParallel(oldPath, newPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(newPath);
                            }

                            UpdateModpacksJsonV2(oldPath, newPath);
                        });

                        SettingsManager.Default.PathLacunher = newPath;
                        SettingsManager.Save();

                        _main.LauncherFloderButton.Content = newPath;
                        _main._versionService = new VersionService(newPath);

                        if (Directory.Exists(oldPath))
                        {
                            Task.Run(() =>
                            {
                                try { Directory.Delete(oldPath, true); } catch { }
                            });
                        }

                        MascotMessageBox.Show(
                            LocalizationManager.GetString("GameLaunch.FolderMovedDesc", "Теку успішно переміщено!"),
                            LocalizationManager.GetString("GameLaunch.FolderMovedTitle", "Успіх!"),
                            MascotEmotion.Happy);
                    }
                    catch (Exception ex)
                    {
                        try { if (Directory.Exists(newPath)) Directory.Delete(newPath, true); } catch { }

                        MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("GameLaunch.FolderMoveErrorDesc", "Ой! Я не змогла перемістити теку лаунчера.\nДеталі: {0}"), ex.Message),
                            LocalizationManager.GetString("GameLaunch.FolderMoveErrorTitle", "Помилка переміщення"),
                            MascotEmotion.Sad);
                    }
                    finally
                    {
                        _main.Cursor = System.Windows.Input.Cursors.Arrow;
                    }
                }
            }
        }
        private void CopyDirectoryParallel(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            Parallel.ForEach(dir.GetFiles(), (file) =>
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            });

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryParallel(subDir.FullName, newDestinationDir);
            }
        }
        private void UpdateModpacksJsonV2(string oldBasePath, string newBasePath)
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "installed_modpacks.json");
            if (!File.Exists(jsonPath)) return;

            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                var modpacks = JsonSerializer.Deserialize<List<InstalledModpack>>(jsonContent);

                if (modpacks != null)
                {
                    string normalizedOld = oldBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string normalizedNew = newBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    bool hasChanges = false;

                    foreach (var pack in modpacks)
                    {
                        if (!string.IsNullOrEmpty(pack.Path))
                        {
                            string newP = ReplacePathBase(pack.Path, normalizedOld, normalizedNew);
                            if (newP != pack.Path) { pack.Path = newP; hasChanges = true; }
                        }

                        if (!string.IsNullOrEmpty(pack.PathJson))
                        {
                            string newP = ReplacePathBase(pack.PathJson, normalizedOld, normalizedNew);
                            if (newP != pack.PathJson) { pack.PathJson = newP; hasChanges = true; }
                        }

                        if (!string.IsNullOrEmpty(pack.UrlImage) && !pack.UrlImage.StartsWith("http"))
                        {
                            string newP = ReplacePathBase(pack.UrlImage, normalizedOld, normalizedNew);
                            if (newP != pack.UrlImage) { pack.UrlImage = newP; hasChanges = true; }
                        }
                    }

                    if (hasChanges)
                    {
                        string newJsonContent = JsonSerializer.Serialize(modpacks, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(jsonPath, newJsonContent);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update modpacks.json: {ex.Message}");
            }
        }
        private string ReplacePathBase(string fullPath, string oldBase, string newBase)
        {
            if (fullPath.StartsWith(oldBase, StringComparison.OrdinalIgnoreCase))
            {
                return newBase + fullPath.Substring(oldBase.Length);
            }
            return fullPath;
        }
        public async void HandleResetPathClick()
        {
            _main.Click();

            string defaultPath = "";
            if (OperatingSystem.IsWindows())
            {
                defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");
            }
            else
            {
                defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ClMinecraft");
            }

            string currentPath = SettingsManager.Default.PathLacunher;

            if (string.Equals(Path.GetFullPath(currentPath).TrimEnd('\\'), Path.GetFullPath(defaultPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("GameLaunch.PathAlreadyDefaultDesc", "Шлях вже встановлено за замовчуванням!"),
                    LocalizationManager.GetString("Dialogs.Alert", "Інфо"),
                    MascotEmotion.Normal);
                return;
            }

            bool confirm = MascotMessageBox.Ask(
                string.Format(LocalizationManager.GetString("GameLaunch.PathResetConfirmDesc", "Ви впевнені, що хочете скинути шлях?\n\nВсі файли будуть переміщені з:\n{0}\n\nВ стандартну папку:\n{1}"), currentPath, defaultPath),
                LocalizationManager.GetString("GameLaunch.PathResetConfirmTitle", "Почекайте!"),
                MascotEmotion.Confused);

            if (!confirm) return;

            _main.Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(currentPath))
                    {
                        CopyDirectoryParallel(currentPath, defaultPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(defaultPath);
                    }

                    UpdateModpacksJsonV2(currentPath, defaultPath);
                });

                SettingsManager.Default.PathLacunher = defaultPath;
                SettingsManager.Save();

                _main.LauncherFloderButton.Content = defaultPath;
                _main._versionService = new VersionService(defaultPath);

                if (Directory.Exists(currentPath))
                {
                    await Task.Run(() =>
                    {
                        try { Directory.Delete(currentPath, true); } catch { }
                    });
                }

                MascotMessageBox.Show(
                    LocalizationManager.GetString("GameLaunch.PathResetSuccessDesc", "Шлях скинуто, файли успішно переміщено додому!"),
                    LocalizationManager.GetString("GameLaunch.FolderMovedTitle", "Успіх!"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(defaultPath)) Directory.Delete(defaultPath, true); } catch { }

                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("GameLaunch.PathResetErrorDesc", "Не вдалося перемістити файли назад.\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.PathResetErrorTitle", "Йой! Якась помилка з переміщенням файлів"),
                    MascotEmotion.Sad);
            }
            finally
            {
                _main.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        public void HandleResetOpClick()
        {
            _main.Click();
            _main.OPSlider.Value = 2048;
            _main.SliderOPTXT.Content = "2048MB";

            SettingsManager.Default.OP = 2048;
            SettingsManager.Save();
        }

        public void HandleResetResolutionClick()
        {
            _main.Click();
            _main.Width.Text = "800";
            _main.Height.Text = "600";
            _main.MincraftWindowSize.Content = "800x600";

            SettingsManager.Default.width = 800;
            SettingsManager.Default.height = 600;
            SettingsManager.Save();
        }
    }
}