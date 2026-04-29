using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using Optifine.Installer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Path = System.IO.Path;
using Separator = System.Windows.Controls.Separator;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class CLModPackEdit : FluentWindow
    {
        public ModpackInfo CurrentModpack { get; set; }
        public string PathJsonModPack { get; set; }
        public string PathMods { get; set; }

        byte selectmodPack = 0;
        public event Action ModpackUpdated;
        private readonly HttpClient httpClient = new HttpClient();
        private bool _isDataLoaded = false;
        public CLModPackEdit()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Modpacks.ModpackEditTitle", "Налаштування збірки");
            NameWin.Text = LocalizationManager.GetString("Modpacks.ModpackEditTitle", "Налаштування збірки");

            TxtMods.Text = LocalizationManager.GetString("Modpacks.ModpackEditMods", "Моди");
            TxtResourcePacks.Text = LocalizationManager.GetString("Modpacks.ModpackEditResourcePacks", "Ресурспаки");
            TxtShaders.Text = LocalizationManager.GetString("Modpacks.ModpackEditShaders", "Шейдери");
            TxtOptions.Text = LocalizationManager.GetString("Modpacks.ModpackEditOptions", "Опції");

            SearchSystem.PlaceholderText = LocalizationManager.GetString("Modpacks.ModpackSearchPlaceholder", "Пошук файлів...");
            DownloadAddMod.Content = LocalizationManager.GetString("Modpacks.ModpackDownloadBtn", "Завантажити");
            AddFileInPack.Content = LocalizationManager.GetString("Modpacks.ModpackAddFileBtn", "Додати файл");

            TxtVersionTitle.Text = LocalizationManager.GetString("Modpacks.ModpackVersionTitle", "Версія");
            TxtMcVersion.Text = LocalizationManager.GetString("Modpacks.ModpackMcVersion", "Версія Minecraft");
            IconMcVersionLock.ToolTip = LocalizationManager.GetString("Modpacks.ModpackMcVersionTooltip", "Версію гри змінювати не можна");
            TxtLoaderVersion.Text = LocalizationManager.GetString("Modpacks.ModpackLoaderVersion", "Версія Ядра");
            BtnChangeLoader.Content = LocalizationManager.GetString("Modpacks.ModpackChangeBtn", "Змінити");

            TxtSystemTitle.Text = LocalizationManager.GetString("Modpacks.ModpackSystemTitle", "Система");
            TxtLogs.Text = LocalizationManager.GetString("Modpacks.ModpackLogs", "Консоль розробника (Logs)");
            TxtRam.Text = LocalizationManager.GetString("Modpacks.ModpackRam", "Оперативна пам’ять");

            TxtGameTitle.Text = LocalizationManager.GetString("Modpacks.ModpackGameTitle", "Гра");
            TxtWindowSize.Text = LocalizationManager.GetString("Modpacks.ModpackWindowSize", "Розмір вікна");
            TxtAutoJoin.Text = LocalizationManager.GetString("Modpacks.ModpackAutoJoin", "Авто-вхід на сервер");

            if (IPAdressServer.Text == "IP Сервера" || IPAdressServer.Text == "Server IP")
            {
                IPAdressServer.Text = LocalizationManager.GetString("Modpacks.ModpackServerIp", "IP Сервера");
            }
            if(TxtCustomizationTitle != null)
                TxtCustomizationTitle.Text = LocalizationManager.GetString("Modpacks.ModpackCustomizationTitle", "Кастомізація");
            if (BtnChangeIcon != null)
                BtnChangeIcon.Content = LocalizationManager.GetString("Modpacks.ModpackChangeIconBtn", "Змінити");
            if (TxtPackIcon != null)
                TxtPackIcon.Text = LocalizationManager.GetString("Modpacks.ModpackIcon", "Іконка збірки");

            if (TxtJavaPath != null)
                TxtJavaPath.Text = LocalizationManager.GetString("Modpacks.ModpackJavaPath", "Кастомний шлях до Java");
            if (TxtJavaPathDesc != null)
                TxtJavaPathDesc.Text = LocalizationManager.GetString("Modpacks.ModpackJavaPathDesc", "Залиште пустим для авто-пошуку");
            if (BtnBrowseJava != null)
                BtnBrowseJava.Content = LocalizationManager.GetString("Modpacks.ModpackBrowseJavaBtn", "Огляд");
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OPSlider.Maximum = GetTotalMemoryInMB();
            OPSlider.Value = CurrentModpack.OPack;
            SliderOPTXT.Text = OPSlider.Value.ToString("0") + "MB";

            WdithTXT.Text = CurrentModpack.Wdith.ToString();
            HeghitTXT.Text = CurrentModpack.Height.ToString();

            if (!string.IsNullOrEmpty(CurrentModpack.ServerIP) && CurrentModpack.ServerIP != "IP Сервера" && CurrentModpack.ServerIP != "Server IP")
                IPAdressServer.Text = CurrentModpack.ServerIP.ToString();
            else
                IPAdressServer.Text = LocalizationManager.GetString("Modpacks.ModpackServerIp", "IP Сервера");

            DebugOff_On.IsChecked = CurrentModpack.IsConsoleLogOpened;
            OnJoinServerOff_On.IsChecked = CurrentModpack.EnterInServer;
            IPAdressServer.IsEnabled = CurrentModpack.EnterInServer;

            PackMcVersionText.Text = CurrentModpack.MinecraftVersion;

            if (IsVanillaVersion())
            {
                LoaderVersionPanel.Visibility = Visibility.Collapsed;
                PackLoaderVersionText.Text = "Vanilla";
                selectmodPack = 1;
            }
            else
            {
                LoaderVersionPanel.Visibility = Visibility.Visible;
                PackLoaderVersionText.Text = $"{CurrentModpack.LoaderType} {CurrentModpack.LoaderVersion}";
            }

            if (!IsVanillaVersion())
            {
                await UpdateModsList();
            }
            List<string> detectedJavas = FindInstalledJavas();
            foreach (string java in detectedJavas)
            {
                JavaPathComboBox.Items.Add(java);
            }

            if (!string.IsNullOrEmpty(CurrentModpack.JavaPath))
            {
                if (!JavaPathComboBox.Items.Contains(CurrentModpack.JavaPath))
                {
                    JavaPathComboBox.Items.Add(CurrentModpack.JavaPath);
                }

                JavaPathComboBox.SelectedItem = CurrentModpack.JavaPath;
                JavaPathComboBox.Text = CurrentModpack.JavaPath;
            }

            if (!string.IsNullOrEmpty(CurrentModpack.UrlImage) && File.Exists(CurrentModpack.UrlImage))
            {
                PackIconImage.Source = ImageHelper.LoadOptimizedImage(CurrentModpack.UrlImage, 64);
            }
            _isDataLoaded = true;
        }
        private async void ChangeLoaderVersion_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            var btn = sender as Button;
            if (btn == null) return;

            btn.IsEnabled = false;
            object originalContent = btn.Content;
            btn.Content = LocalizationManager.GetString("Modpacks.LoaderSearch", "Пошук...");

            try
            {
                List<string> versions = await GetLoaderVersionsForEditAsync(CurrentModpack.MinecraftVersion, CurrentModpack.LoaderType);

                if (versions.Count == 0)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.LoaderNotFoundDesc", "Не знайдено версій {0} для {1}."), CurrentModpack.LoaderType, CurrentModpack.MinecraftVersion),
                        LocalizationManager.GetString("Dialogs.Oops", "Упс"),
                        MascotEmotion.Confused);
                    return;
                }

                ContextMenu menu = new ContextMenu();

                MenuItem header = new MenuItem { Header = LocalizationManager.GetString("Modpacks.LoaderSelectHeader", "Оберіть версію:"), IsEnabled = false, FontWeight = FontWeights.Bold };
                menu.Items.Add(header);
                menu.Items.Add(new Separator());

                ScrollViewer scrollViewer = new ScrollViewer
                {
                    MaxHeight = 250,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    CanContentScroll = true,
                    PanningMode = PanningMode.VerticalOnly
                };

                StackPanel stackPanel = new StackPanel();

                foreach (var ver in versions)
                {
                    MenuItem item = new MenuItem { Header = ver };

                    if (ver == CurrentModpack.LoaderVersion)
                    {
                        item.IsChecked = true;
                        item.FontWeight = FontWeights.Bold;
                    }

                    item.Click += (s, args) =>
                    {
                        ApplyNewLoaderVersion(ver);
                        menu.IsOpen = false;
                    };

                    stackPanel.Children.Add(item);
                }

                scrollViewer.Content = stackPanel;
                menu.Items.Add(scrollViewer);

                btn.ContextMenu = menu;
                menu.PlacementTarget = btn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            }
            finally
            {
                btn.IsEnabled = true;
                btn.Content = originalContent;
            }
        }
        private async Task<List<string>> GetLoaderVersionsForEditAsync(string mcVersion, string loaderType)
        {
            List<string> versions = new List<string>();

            try
            {
                if (loaderType == "Forge")
                {
                    var versionLoader = new ForgeVersionLoader(httpClient);
                    var forgeList = await versionLoader.GetForgeVersions(mcVersion);
                    foreach (var forge in forgeList)
                        versions.Add(forge.ForgeVersionName);
                }
                else if (loaderType == "Fabric")
                {
                    var fabricInstaller = new FabricInstaller(httpClient);
                    var fabricVersions = await fabricInstaller.GetLoaders(mcVersion);
                    foreach (var fabric in fabricVersions)
                        versions.Add(fabric.Version);
                }
                else if (loaderType == "Quilt")
                {
                    var quiltInstaller = new QuiltInstaller(httpClient);
                    var quiltVersions = await quiltInstaller.GetLoaders(mcVersion);
                    foreach (var quilt in quiltVersions)
                        versions.Add(quilt.Version);
                }
                else if (loaderType == "NeoForge")
                {
                    var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                    var launcher = new MinecraftLauncher(path);
                    var versionLoader = new NeoForgeInstaller(launcher);
                    var neoForgeList = await versionLoader.GetForgeVersions(mcVersion);
                    foreach (var neo in neoForgeList)
                        versions.Add(neo.VersionName);
                }
                else if (loaderType == "LiteLoader")
                {
                    var liteLoaderInstaller = new LiteLoaderInstaller(httpClient);
                    var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                    var compatibleLoaders = loaders.Where(l => l.BaseVersion == mcVersion);
                    foreach (var loader in compatibleLoaders)
                        versions.Add(loader.Version);
                }
                else if (loaderType == "Optifine")
                {
                    var loader = new OptifineInstaller(httpClient);
                    var allVersions = await loader.GetOptifineVersionsAsync();
                    var compatible = allVersions.Where(v => v.MinecraftVersion == mcVersion);
                    foreach (var v in compatible)
                        versions.Add(v.Version);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting versions: {ex.Message}");
            }

            return versions;
        }

        private void ApplyNewLoaderVersion(string newVersion)
        {
            if (newVersion == CurrentModpack.LoaderVersion) return;

            bool success = EditInstalledModpack(CurrentModpack.Name, "LoaderVersion", newVersion);

            if (success)
            {
                CurrentModpack.LoaderVersion = newVersion;

                if (PackLoaderVersionText != null)
                {
                    PackLoaderVersionText.Text = $"{CurrentModpack.LoaderType} {newVersion}";
                }

                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.LoaderChangedDesc", "Версію успішно змінено на {0}!\nЯдро завантажиться при наступному запуску."), newVersion),
                    LocalizationManager.GetString("Modpacks.LoaderChangedTitle", "Успіх"),
                    MascotEmotion.Happy);
            }
        }
        private async Task UpdateModsList()
        {
            ModsManegerList.Items.Clear();

            string currentModFolder = selectmodPack switch
            {
                0 => "mods",
                1 => "resourcepacks",
                2 => "shaderpacks",
                _ => "mods"
            };

            string modsDirectory = Path.Combine(PathMods, currentModFolder);
            if (!Directory.Exists(modsDirectory)) return;

            string[] patterns = selectmodPack switch
            {
                0 => new[] { "*.jar", "*.jar.disabled", "*.litemod", "*.litemod.disabled" },
                1 => new[] { "*.zip", "*.zip.disabled" },
                2 => new[] { "*.zip", "*.rar", "*.zip.disabled", "*.rar.disabled" },
                _ => new[] { "*.*" }
            };

            var files = patterns.SelectMany(p => Directory.GetFiles(modsDirectory, p)).ToArray();
            string search = SearchSystem.Text.ToLower();

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                if (!string.IsNullOrEmpty(search) && !fileName.ToLower().Contains(search)) continue;

                bool isEnabled = !fileName.EndsWith(".disabled");

                var item = new ItemManegerPack();
                item.Title.Text = fileName.Replace(".disabled", "");
                item.Description.Text = isEnabled
                    ? LocalizationManager.GetString("Modpacks.ModStateActive", "Активний")
                    : LocalizationManager.GetString("Modpacks.ModStateDisabled", "Вимкнено");
                item.pathmods = file;
                item.CurrentModpack = this.CurrentModpack;
                item.IsModPack = true;
                item.Off_OnMod = isEnabled;

                item.IsOnOffSwitch.Click -= item.Off_OnMods_Click;
                item.IsOnOffSwitch.IsChecked = isEnabled;
                item.IsOnOffSwitch.Click += item.Off_OnMods_Click;

                ModsManegerList.Items.Add(item);
            }
        }
        private bool EditInstalledModpack(string modpackName, string propertyName, object newValue)
        {
            try
            {
                string exeDir = AppContext.BaseDirectory;
                string jsonPath = Path.Combine(exeDir, "Data", "installed_modpacks.json");

                if (!File.Exists(jsonPath)) return false;

                string json = File.ReadAllText(jsonPath);
                var modpacks = System.Text.Json.JsonSerializer.Deserialize<List<InstalledModpack>>(json);

                var targetPack = modpacks?.FirstOrDefault(p => p.Name.Equals(modpackName, StringComparison.OrdinalIgnoreCase));
                if (targetPack == null) return false;

                var property = typeof(InstalledModpack).GetProperty(propertyName);
                if (property == null || !property.CanWrite) return false;

                object convertedValue = Convert.ChangeType(newValue, property.PropertyType);
                property.SetValue(targetPack, convertedValue);

                string updatedJson = System.Text.Json.JsonSerializer.Serialize(modpacks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, updatedJson);

                ModpackUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.ModpackSaveError", "Помилка збереження: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
                return false;
            }
        }

        private bool IsVanillaVersion()
        {
            return CurrentModpack.LoaderType == "Vanila" || CurrentModpack.LoaderType == "Vanilla";
        }

        private double GetTotalMemoryInMB()
        {
            double totalMemoryInBytes = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            return totalMemoryInBytes / (1024 * 1024);
        }

        private void ShowError(string message)
        {
            MascotMessageBox.Show(message, LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Sad);
        }
        private async void SearchSystem_TextChanged(object sender, TextChangedEventArgs e)
        {
            await UpdateModsList();
        }
        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void ExitLauncher_MouseDown(object sender, RoutedEventArgs e) => this.Close();
        private void DebugOff_On_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            bool newState = DebugOff_On.IsChecked ?? false;
            EditInstalledModpack(CurrentModpack.Name, "IsConsoleLogOpened", newState);
        }

        private void OnJoinServerOff_On_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            bool newState = OnJoinServerOff_On.IsChecked ?? false;
            IPAdressServer.IsEnabled = newState;
            EditInstalledModpack(CurrentModpack.Name, "EnterInServer", newState);
        }
        private void HeghitTXT_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HeghitTXT == null || CurrentModpack == null) return;

            if (int.TryParse(HeghitTXT.Text, out int _))
            {
                EditInstalledModpack(CurrentModpack.Name, "Height", HeghitTXT.Text);
            }
        }
        private void WdithTXT_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WdithTXT == null) return;

            if (CurrentModpack == null) return;

            if (int.TryParse(WdithTXT.Text, out int _))
            {
                EditInstalledModpack(CurrentModpack.Name, "Wdith", WdithTXT.Text);
            }
        }
        private void IPAdressServer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IPAdressServer == null || CurrentModpack == null) return;

            if (!string.IsNullOrWhiteSpace(IPAdressServer.Text))
            {
                EditInstalledModpack(CurrentModpack.Name, "ServerIP", IPAdressServer.Text);
            }
        }
        private async void ModsPack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (IsVanillaVersion()) { ShowError(LocalizationManager.GetString("Modpacks.VanillaNoMods", "Ванільна версія не підтримує моди.")); return; }
            await SwitchTab(0, 0);
        }

        private async void Resource_packPack_MouseDown(object sender, MouseButtonEventArgs e) {
            SoundManager.Click();
            await SwitchTab(1, 40);
        }
        private async void ShaderPack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (IsVanillaVersion()) { ShowError(LocalizationManager.GetString("Modpacks.VanillaNoShaders", "Ванільна версія не підтримує шейдери.")); return; }
            await SwitchTab(2, 80);
        }
        private void SettingPack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            _ = SwitchTab(3, 120);
        }

        private async Task SwitchTab(byte index, double positionY)
        {
            SoundManager.Click();
            selectmodPack = index;

            AnimationService.AnimateBorderObject(0, positionY, PanelSelectNowSiteMods, true);

            if (index == 3)
            {
                ManegerPack.Visibility = Visibility.Hidden;
                SettingPack_Mod.Visibility = Visibility.Visible;
            }
            else
            {
                ManegerPack.Visibility = Visibility.Visible;
                SettingPack_Mod.Visibility = Visibility.Hidden;
                await UpdateModsList();
            }
        }
        private void OPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SliderOPTXT.Text = OPSlider.Value.ToString("0") + "MB";
        }
        private void OPSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            EditInstalledModpack(CurrentModpack.Name, "OPack", (int)OPSlider.Value);
        }
        private void DownloadAddMod_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            DownloadEditPack downloadEditPack = new DownloadEditPack(this.CurrentModpack, selectmodPack);

            downloadEditPack.Closed += (s, args) =>
            {
                _ = UpdateModsList();
            };

            downloadEditPack.ShowDialog();
        }
        private void AddFileInPack_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = selectmodPack == 0
                    ? "Jar Files (*.jar)|*.jar"
                    : "Zip Files (*.zip)|*.zip|Rar Files (*.rar)|*.rar";
                openFileDialog.Multiselect = true;

                string currentModFolder = selectmodPack switch
                {
                    0 => "mods",
                    1 => "resourcepacks",
                    2 => "shaderpacks",
                    _ => "mods"
                };

                string modsDirectory = Path.Combine(PathMods, currentModFolder);
                Directory.CreateDirectory(modsDirectory);

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (var file in openFileDialog.FileNames)
                    {
                        string targetPath = Path.Combine(modsDirectory, Path.GetFileName(file));
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                        File.Copy(file, targetPath);
                    }
                    _ = UpdateModsList();
                }
            }
        }
        private void JavaPathComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isDataLoaded || CurrentModpack == null || JavaPathComboBox == null) return;

            EditInstalledModpack(CurrentModpack.Name, "JavaPath", JavaPathComboBox.Text);
        }

        private void JavaPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isDataLoaded || CurrentModpack == null || JavaPathComboBox.SelectedItem == null) return;

            EditInstalledModpack(CurrentModpack.Name, "JavaPath", JavaPathComboBox.SelectedItem.ToString());
        }
        private void BtnBrowseJava_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = LocalizationManager.GetString("Dialogs.JavaFilter", "Java Executable (javaw.exe)|javaw.exe|Усі файли (*.*)|*.*");
                openFileDialog.Title = LocalizationManager.GetString("Dialogs.SelectJavaTitle", "Оберіть файл javaw.exe");

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = openFileDialog.FileName;

                    if (!JavaPathComboBox.Items.Contains(selectedPath))
                    {
                        JavaPathComboBox.Items.Add(selectedPath);
                    }

                    JavaPathComboBox.SelectedItem = selectedPath;
                    JavaPathComboBox.Text = selectedPath;
                }
            }
        }
        private List<string> FindInstalledJavas()
        {
            List<string> javaPaths = new List<string>();

            try
            {
                string runtimePath = Path.Combine(SettingsManager.Default.PathLacunher, "runtime");
                if (Directory.Exists(runtimePath))
                {
                    var mojangJavas = Directory.GetFiles(runtimePath, "javaw.exe", SearchOption.AllDirectories);
                    javaPaths.AddRange(mojangJavas);
                }
            }
            catch { }

            string[] baseDirs = {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\AdoptOpenJDK",
                @"C:\Program Files\BellSoft"
            };

            foreach (var baseDir in baseDirs)
            {
                if (Directory.Exists(baseDir))
                {
                    try
                    {
                        var dirs = Directory.GetDirectories(baseDir);
                        foreach (var dir in dirs)
                        {
                            string javaw = Path.Combine(dir, "bin", "javaw.exe");
                            if (File.Exists(javaw))
                            {
                                javaPaths.Add(javaw);
                            }
                        }
                    }
                    catch { }
                }
            }
            return javaPaths.Distinct().ToList();
        }
        private void BtnChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = LocalizationManager.GetString("Dialogs.ImageFilter", "Зображення (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg");
                openFileDialog.Title = LocalizationManager.GetString("Dialogs.SelectIconTitle", "Оберіть іконку для збірки");

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        string targetIconPath = Path.Combine(CurrentModpack.Path, "icon.png");

                        if (File.Exists(targetIconPath)) File.Delete(targetIconPath);
                        File.Copy(openFileDialog.FileName, targetIconPath);

                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(targetIconPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bitmap.DecodePixelWidth = 64; 
                        bitmap.EndInit();
                        if (bitmap.CanFreeze) bitmap.Freeze();

                        PackIconImage.Source = bitmap;

                        EditInstalledModpack(CurrentModpack.Name, "UrlImage", targetIconPath);
                    }
                    catch (Exception ex)
                    {
                        MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("Dialogs.IconSaveError", "Помилка встановлення іконки: {0}"), ex.Message),
                            LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                            MascotEmotion.Sad);
                    }
                }
            }
        }
    }
}