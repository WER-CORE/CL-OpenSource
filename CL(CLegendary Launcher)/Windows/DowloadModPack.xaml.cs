using CL_CLegendary_Launcher_.Class;
using CmlLib.Core;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using JsonSerializer = System.Text.Json.JsonSerializer;
using MenuItem = System.Windows.Controls.MenuItem;

namespace CL_CLegendary_Launcher_.Windows
{
    public class ModpackFileVersion
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public string GameVersion { get; set; }
        public string LoaderType { get; set; }
    }

    public partial class DowloadModPack : Window
    {
        private List<string> iconUrl = new List<string>();
        private string LoderNow = "Forge";
        private string SiteDowload = "Modrinth"; 
        private static ApiClient _cfApiClientInstance;
        private static readonly HttpClient httpClient = new HttpClient();
        private ModpackService _modpackService;

        private const string DefaultIconPath = "pack://application:,,,/Icon/IconCL(Common).png";
        private const int ListIconSize = 100;

        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 10;

        public DowloadModPack(ModpackService modpackService)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _modpackService = modpackService;

            ApplyLocalization();

            ModsDowloadList.SelectionChanged += ModsDowloadList_SelectionChanged;

            UpdatePaginationButtons();
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Modpacks.DownloadModpackWindowTitle", "CL - Завантаження та імпорт мод-паків");
            TxtHeaderTitle.Text = LocalizationManager.GetString("Modpacks.DownloadModpackHeader", "CL - Завантаження/Імпорт збірок");

            TxtImport.Text = LocalizationManager.GetString("Modpacks.ImportModpackBtn", "Імпорт");
            ImportFileModPacks.ToolTip = LocalizationManager.GetString("Modpacks.ImportModpackTooltip", "Встановити модпак з .zip або .mrpack файлу");

            SearchSystemModsTXT.PlaceholderText = LocalizationManager.GetString("Modpacks.SearchModpacksPlaceholder", "Пошук збірок...");
            SearchSystemModsTXT.ToolTip = LocalizationManager.GetString("Modpacks.SearchModpacksTooltip", "Введіть назву для пошуку");

            ModrinthSite.ToolTip = LocalizationManager.GetString("Modpacks.ModrinthTooltip", "Шукати на Modrinth");
            CurseForgeSite.ToolTip = LocalizationManager.GetString("Modpacks.CurseForgeTooltip", "Шукати на CurseForge");

            TxtMcVersion.Text = LocalizationManager.GetString("Modpacks.McVersionTitle", "Версія MC:");
            SelectLoaderBorder.ToolTip = LocalizationManager.GetString("Modpacks.SelectLoaderTooltip", "Тип завантажувача");

            PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackSelectFileDefault", "Спочатку оберіть збірку");
            PackVersionSelector.ToolTip = LocalizationManager.GetString("Modpacks.ModpackSelectFileTooltip", "Оберіть конкретну версію файлу для завантаження");

            DowloaadModPacksButtonTXT.Text = LocalizationManager.GetString("Modpacks.DownloadPacksBtn", "Завантажити");
            DowloaadModPacksButton.ToolTip = LocalizationManager.GetString("Modpacks.DownloadPacksTooltip", "Почати встановлення обраної версії");
        }

        private void ExitLauncher_MouseDown(object sender, RoutedEventArgs e) => this.Close();
        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); }
        private void MainWin_Loaded(object sender, RoutedEventArgs e) { }

        private async void PrevPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (_currentPage > 0)
            {
                _currentPage--;
                await RefreshList();
            }
        }

        private async void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            _currentPage++;
            await RefreshList();
        }

        private void UpdatePaginationButtons()
        {
            if (PrevPageBtn != null) PrevPageBtn.IsEnabled = _currentPage > 0;
            if (PageNumberText != null) PageNumberText.Text = $"{_currentPage + 1}";
        }

        private void ModrinthSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "Modrinth") return;

            ModrinthSite.Opacity = 1.0;
            CurseForgeSite.Opacity = 0.5;

            if (ModsDowloadList.Items != null) ModsDowloadList.Items.Clear();
            SiteDowload = "Modrinth";
            ResetPaginationAndLoad();
        }

        private void CurseForgeSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "CurseForge") return;

            ModrinthSite.Opacity = 0.5;
            CurseForgeSite.Opacity = 1.0;

            if (ModsDowloadList.Items != null) ModsDowloadList.Items.Clear();
            SiteDowload = "CurseForge";
            ResetPaginationAndLoad();
        }

        private void SearchSystemModsTXT_TextChanged(object sender, TextChangedEventArgs e)
        {
            ResetPaginationAndLoad();
        }

        private void VersionVanil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();
            ResetPaginationAndLoad();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            string content = (sender as MenuItem).Header.ToString();
            LoderNow = content;
            SelectLoader.Content = content;

            AddVersionList();
            _currentPage = 0;
        }

        private async void ResetPaginationAndLoad()
        {
            _currentPage = 0;
            ClearVersionSelector();
            await RefreshList();
        }

        private async Task RefreshList()
        {
            UpdatePaginationButtons();

            if (VersionVanil.SelectedItem == null) return;

            if (SiteDowload == "Modrinth") await LoadModrinthModsAsync(SearchSystemModsTXT.Text);
            else await LoadCurseForgeModpacksAsync(SearchSystemModsTXT.Text);
        }

        private void ClearVersionSelector()
        {
            PackVersionSelector.ItemsSource = null;
            PackVersionSelector.Items.Clear();
            DowloaadModPacksButton.IsEnabled = false;
            PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackSelectFileDefault", "Спочатку оберіть збірку");
        }
        private async Task<ApiClient> GetCfClientAsync()
        {
            if (_cfApiClientInstance != null) return _cfApiClientInstance;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("", "");
                var response = await client.GetAsync($"{Secrets.CurseForgeKey}");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    string key = data["key"]?.ToString();
                    _cfApiClientInstance = new ApiClient(key);
                    return _cfApiClientInstance;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не вдалося отримати ключ CurseForge: {ex.Message}");
            }
            return null;
        }
        private async Task LoadModrinthModsAsync(string searchText)
        {
            ModsSearchLoader.Visibility = Visibility.Visible;
            ModsSkeletonPanel.Visibility = Visibility.Visible;
            ModsDowloadList.Visibility = Visibility.Collapsed;
            ModsDowloadList.Items.Clear();
            iconUrl.Clear();

            try
            {
                string version = VersionVanil.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(version)) return;

                int offset = _currentPage * ITEMS_PER_PAGE;
                string url = $"https://api.modrinth.com/v2/search?query={searchText}&facets=[[\"categories:{LoderNow}\"],[\"project_type:modpack\"],[\"versions:{version}\"]]&limit={ITEMS_PER_PAGE}&offset={offset}&sort=downloads";

                var response = await httpClient.GetStringAsync(url);
                dynamic result = JsonConvert.DeserializeObject(response);

                foreach (var mod in result["hits"])
                {
                    var item = CreateItemJarFromModrinth(mod);
                    await AddItemWithAnimation(item);
                }

                int count = ((JArray)result["hits"]).Count;
                if (NextPageBtn != null) NextPageBtn.IsEnabled = count >= ITEMS_PER_PAGE;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"Помилка Modrinth: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Збій"), MascotEmotion.Sad);
            }
            finally
            {
                ModsSkeletonPanel.Visibility = Visibility.Collapsed;
                ModsSearchLoader.Visibility = Visibility.Collapsed;
                ModsDowloadList.Visibility = Visibility.Visible;
            }
        }

        private async Task LoadCurseForgeModpacksAsync(string searchText)
        {
            ModsSearchLoader.Visibility = Visibility.Visible;
            ModsSkeletonPanel.Visibility = Visibility.Visible;
            ModsDowloadList.Visibility = Visibility.Collapsed;
            ModsDowloadList.Items.Clear();
            iconUrl.Clear();

            try
            {
                var cfApi = await GetCfClientAsync();
                if (cfApi == null) return;

                var modLoaderType = LoderNow switch
                {
                    "Forge" => ModLoaderType.Forge,
                    "Fabric" => ModLoaderType.Fabric,
                    "Quilt" => ModLoaderType.Quilt,
                    "NeoForge" => ModLoaderType.NeoForge,
                    _ => ModLoaderType.Any
                };

                var gameVersion = VersionVanil?.SelectedItem?.ToString();
                int index = _currentPage * ITEMS_PER_PAGE;

                var searchResponse = await cfApi.SearchModsAsync(
                    gameId: 432,
                    searchFilter: searchText,
                    classId: 4471,
                    pageSize: ITEMS_PER_PAGE,
                    index: index,
                    sortField: ModsSearchSortField.Popularity,
                    modLoaderType: modLoaderType,
                    gameVersion: gameVersion
                );

                foreach (var mod in searchResponse.Data)
                {
                    var item = CreateItemJarFromCurseForge(mod);
                    await AddItemWithAnimation(item);
                }

                if (NextPageBtn != null) NextPageBtn.IsEnabled = searchResponse.Data.Count >= ITEMS_PER_PAGE;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"Помилка CurseForge: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Збій"), MascotEmotion.Sad);
            }
            finally
            {
                ModsSkeletonPanel.Visibility = Visibility.Collapsed;
                ModsSearchLoader.Visibility = Visibility.Collapsed;
                ModsDowloadList.Visibility = Visibility.Visible;
            }
        }

        private ModPackItem CreateItemJarFromModrinth(dynamic mod)
        {
            var item = new ModPackItem();
            item.ModPackName.Text = mod["title"];
            var icon = mod["icon_url"]?.ToString();
            iconUrl.Add(icon);

            if (!string.IsNullOrEmpty(icon)) item.IconModPack.Source = ImageHelper.LoadOptimizedImage(icon, ListIconSize);
            else item.IconModPack.Source = ImageHelper.LoadOptimizedImage(DefaultIconPath, ListIconSize);

            item.DetailsModPackBtn.PreviewMouseDown += (s, e) =>
            {
                WebHelper.OpenUrl($"https://modrinth.com/modpack/{mod["slug"]}"); SoundManager.Click();
            };

            item.ProjectId = mod["project_id"];
            item.Name = mod["title"];
            item.AddModInModPack.Visibility = Visibility.Collapsed;
            return item;
        }

        private ModPackItem CreateItemJarFromCurseForge(CurseForge.APIClient.Models.Mods.Mod mod)
        {
            var item = new ModPackItem();
            item.ModPackName.Text = mod.Name;
            var icon = mod.Logo?.Url ?? "";
            iconUrl.Add(icon);

            if (!string.IsNullOrEmpty(icon)) item.IconModPack.Source = ImageHelper.LoadOptimizedImage(icon, ListIconSize);
            else item.IconModPack.Source = ImageHelper.LoadOptimizedImage(DefaultIconPath, ListIconSize);

            item.DetailsModPackBtn.PreviewMouseDown += (s, e) =>
            {
                WebHelper.OpenUrl(mod.Links?.WebsiteUrl ?? $"https://www.curseforge.com/minecraft/modpacks/{mod.Slug}");
                SoundManager.Click();
            };

            item.ProjectId = mod.Id.ToString();
            item.Name = mod.Name;
            item.AddModInModPack.Visibility = Visibility.Collapsed;
            return item;
        }

        private async Task AddItemWithAnimation(UIElement item)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                ModsDowloadList.Items.Add(item);
                await Task.Delay(20);
            });
        }

        private async void ModsDowloadList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearVersionSelector();

            if (ModsDowloadList.SelectedItem is ModPackItem selectedItem)
            {
                PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackFetchingVersions", "Шукаю версії...");
                await LoadVersionsForSelectedPack(selectedItem);
            }
        }

        private async Task LoadVersionsForSelectedPack(ModPackItem item)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    PackVersionSelector.ItemsSource = null;
                    DowloaadModPacksButton.IsEnabled = false;
                });

                string targetMcVersion = VersionVanil.SelectedItem?.ToString();
                string targetLoader = LoderNow;

                if (string.IsNullOrEmpty(targetMcVersion))
                {
                    Dispatcher.Invoke(() => PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackSelectMcVersionFirst", "Оберіть версію MC зліва"));
                    return;
                }

                List<ModpackFileVersion> availableFiles = new List<ModpackFileVersion>();

                if (SiteDowload == "Modrinth")
                {
                    string url = $"https://api.modrinth.com/v2/project/{item.ProjectId}/version";
                    var response = await httpClient.GetStringAsync(url);
                    var versions = JsonConvert.DeserializeObject<List<dynamic>>(response);

                    foreach (var ver in versions)
                    {
                        var gameVersions = ver["game_versions"].ToObject<List<string>>();
                        var loaders = ver["loaders"].ToObject<List<string>>();

                        if (gameVersions.Contains(targetMcVersion) &&
                           (loaders.Contains(targetLoader.ToLower()) || targetLoader == "Optifine"))
                        {
                            var files = ver["files"];
                            foreach (var file in files)
                            {
                                if (file["primary"].ToObject<bool>() || file["filename"].ToString().EndsWith(".mrpack"))
                                {
                                    availableFiles.Add(new ModpackFileVersion
                                    {
                                        Name = $"{ver["name"]}",
                                        DownloadUrl = file["url"].ToString(),
                                        FileName = file["filename"].ToString(),
                                        GameVersion = targetMcVersion,
                                        LoaderType = targetLoader
                                    });
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (SiteDowload == "CurseForge")
                {
                    if (int.TryParse(item.ProjectId, out int modId))
                    {
                        var cfApi = await GetCfClientAsync();
                        if (cfApi != null)
                        {
                            var filesResponse = await cfApi.GetModFilesAsync(modId, pageSize: 50);

                            if (filesResponse.Data != null)
                            {
                                foreach (var file in filesResponse.Data)
                                {
                                    bool isCorrectVersion = file.GameVersions.Any(v => v == targetMcVersion);
                                    bool isCorrectLoader = file.GameVersions.Any(v => v.Equals(targetLoader, StringComparison.OrdinalIgnoreCase));
                                    if (targetLoader == "Optifine" || targetLoader == "LiteLoader") isCorrectLoader = true;

                                    if (isCorrectVersion && isCorrectLoader)
                                    {
                                        availableFiles.Add(new ModpackFileVersion
                                        {
                                            Name = file.DisplayName,
                                            DownloadUrl = file.DownloadUrl,
                                            FileName = file.FileName,
                                            GameVersion = targetMcVersion,
                                            LoaderType = targetLoader
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                Dispatcher.Invoke(() =>
                {
                    PackVersionSelector.ItemsSource = availableFiles;

                    if (availableFiles.Count > 0)
                    {
                        PackVersionSelector.SelectedIndex = 0;
                        PackVersionSelector.Text = "";
                        DowloaadModPacksButton.IsEnabled = true;
                    }
                    else
                    {
                        PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackNoFilesForVersion", "Немає файлів для цієї версії");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження версій: {ex.Message}");
                Dispatcher.Invoke(() => PackVersionSelector.Text = LocalizationManager.GetString("Modpacks.ModpackApiError", "Помилка API"));
            }
        }

        private async void DowloaadModPacksButtonTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ModsDowloadList.SelectedItem is not ModPackItem selectedModpack) return;
            SoundManager.Click();

            if (PackVersionSelector.SelectedItem is not ModpackFileVersion selectedFile)
            {
                MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.SelectVersionBeforeModpack", "Будь ласка, оберіть версію файлу зі списку."), LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Alert);
                return;
            }

            string tempFolder = Path.Combine(SettingsManager.Default.PathLacunher, $@"CLModpack\{selectedModpack.Name}_TempDownload");
            Directory.CreateDirectory(tempFolder);

            string zipFileName = selectedFile.FileName ?? $"{selectedModpack.Name}.zip";
            string zipFilePath = Path.Combine(tempFolder, zipFileName);

            try
            {
                DowloaadModPacksButton.IsEnabled = false;
                DowloaadModPacksButtonTXT.Text = LocalizationManager.GetString("Modpacks.ModpackCielDownloading", "Сіель завантажує...");

                string realDownloadUrl = selectedFile.DownloadUrl;

                if (string.IsNullOrEmpty(realDownloadUrl))
                {
                    MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.ModpackLinkEmpty", "Посилання на файл пусте."), LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
                    return;
                }

                await DownloadModpackAsync(realDownloadUrl, zipFilePath);

                string extractPath = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", selectedModpack.Name);
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                Directory.CreateDirectory(extractPath);

                ZipFile.ExtractToDirectory(zipFilePath, extractPath);

                var rootDir = new DirectoryInfo(extractPath);
                var subDirs = rootDir.GetDirectories();
                var files = rootDir.GetFiles();

                if (files.Length == 0 && subDirs.Length == 1)
                {
                    var nestedDir = subDirs[0];
                    foreach (var file in nestedDir.GetFiles())
                    {
                        string destFile = Path.Combine(extractPath, file.Name);
                        file.MoveTo(destFile);
                    }
                    foreach (var dir in nestedDir.GetDirectories())
                    {
                        string destDir = Path.Combine(extractPath, dir.Name);
                        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                        dir.MoveTo(destDir);
                    }
                    nestedDir.Delete();
                }

                string loaderName = selectedFile.LoaderType;
                string loaderVersion = "Auto";
                string vanillaVersion = selectedFile.GameVersion;

                string pathJson = SiteDowload == "Modrinth"
                    ? Path.Combine(extractPath, "modrinth.index.json")
                    : Path.Combine(extractPath, "manifest.json");

                if (File.Exists(pathJson))
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(pathJson);
                        var jsonObj = JsonConvert.DeserializeObject<JObject>(jsonContent);

                        if (SiteDowload == "Modrinth")
                        {
                            var deps = jsonObj["dependencies"] as JObject;
                            if (deps != null)
                            {
                                foreach (var property in deps.Properties())
                                {
                                    if (property.Name.Contains(loaderName.ToLower()))
                                    {
                                        loaderVersion = property.Value?.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                        else if (SiteDowload == "CurseForge")
                        {
                            var loadersArr = jsonObj["minecraft"]?["modLoaders"];
                            if (loadersArr != null)
                            {
                                var loaderInfo = loadersArr.FirstOrDefault(l => l["id"].ToString().StartsWith(loaderName.ToLower()));
                                if (loaderInfo != null)
                                {
                                    string fullId = loaderInfo["id"].ToString();
                                    if (fullId.Contains("-")) loaderVersion = fullId.Split('-')[1];
                                }
                            }
                        }
                    }
                    catch { }
                }

                string remoteIconUrl = "";
                if (ModsDowloadList.SelectedIndex >= 0 && ModsDowloadList.SelectedIndex < iconUrl.Count)
                    remoteIconUrl = iconUrl[ModsDowloadList.SelectedIndex];

                string localIconPath = await DownloadAndSaveIconAsync(remoteIconUrl, extractPath);
                string finalIconPath = !string.IsNullOrEmpty(localIconPath) ? localIconPath : DefaultIconPath;

                SaveModpackToJson(new InstalledModpack
                {
                    Name = selectedModpack.Name,
                    TypeSite = SiteDowload,
                    MinecraftVersion = vanillaVersion,
                    LoaderType = loaderName,
                    LoaderVersion = loaderVersion,
                    Path = extractPath,
                    UrlImage = finalIconPath,
                    PathJson = pathJson
                });

                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.ModpackDownloadSuccess", "Ура! Мод-пак '{0}' успішно завантажено!"), selectedFile.Name),
                    LocalizationManager.GetString("Dialogs.Success", "Готово!"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.ModpackDownloadFail", "Ех, сталася помилка.\n{0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Критичний збій"),
                    MascotEmotion.Sad);
            }
            finally
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                DowloaadModPacksButton.IsEnabled = true;
                DowloaadModPacksButtonTXT.Text = LocalizationManager.GetString("Modpacks.DownloadPacksBtn", "Завантажити");
            }
        }

        private async void ImportFileModPacksTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Modpack Files (*.zip;*.rar;*.mrpack)|*.zip;*.rar;*.mrpack",
                Title = LocalizationManager.GetString("Modpacks.ModpackSelectFileDialogTitle", "Оберіть файл мод-паку")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ModsSearchLoader.Visibility = Visibility.Visible;
                    ModsDowloadList.Visibility = Visibility.Collapsed;
                    ImportFileModPacks.Opacity = 0.5;
                    ImportFileModPacks.IsEnabled = false;

                    var importedPack = await _modpackService.ImportModpackFromFileAsync(dialog.FileName);

                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ModpackImportSuccess", "Збірка '{0}' успішно імпортована!"), importedPack.Name),
                        LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                        MascotEmotion.Happy);
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ModpackImportFail", "Не вдалося імпортувати файл.\n{0}"), ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Помилка імпорту"),
                        MascotEmotion.Sad);
                }
                finally
                {
                    ModsSearchLoader.Visibility = Visibility.Collapsed;
                    ModsDowloadList.Visibility = Visibility.Visible;
                    ImportFileModPacks.IsEnabled = true;
                    ImportFileModPacks.Opacity = 1.0;
                }
            }
        }

        private async Task AddVersionList()
        {
            VersionVanil.Items.Clear();
            ModsDowloadList.Items.Clear();
            string searchText = SearchSystemModsTXT.Text.ToLower().Trim();
            if (searchText == LocalizationManager.GetString("Modpacks.SearchModpacksPlaceholder", "Пошук збірок...").ToLower()) searchText = "";

            Regex regex = new Regex(string.IsNullOrEmpty(searchText) ? ".*" : Regex.Escape(searchText).Replace(@"\*", ".*"), RegexOptions.IgnoreCase);

            try
            {
                IEnumerable<string> versions = null;
                var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                var launcher = new MinecraftLauncher(path);
                var allVersions = await launcher.GetAllVersionsAsync();

                if (LoderNow == "Forge")
                {
                    var minVer = new Version(1, 7, 10);
                    versions = allVersions.Where(v => v.Type == "release").Where(v => ParseGameVersion(v.Name) >= minVer).Select(v => v.Name);
                }
                else if (LoderNow == "Fabric")
                {
                    var fabricInstaller = new CmlLib.Core.ModLoaders.FabricMC.FabricInstaller(httpClient);
                    versions = await fabricInstaller.GetSupportedVersionNames();
                }
                else if (LoderNow == "Quilt")
                {
                    var quiltInstaller = new CmlLib.Core.ModLoaders.QuiltMC.QuiltInstaller(httpClient);
                    versions = await quiltInstaller.GetSupportedVersionNames();
                }
                else if (LoderNow == "NeoForge")
                {
                    var minVer = new Version(1, 20, 1);
                    versions = allVersions.Where(v => v.Type == "release").Where(v => ParseGameVersion(v.Name) >= minVer).Select(v => v.Name);
                }
                else
                {
                    var minVer = new Version(1, 7, 2);
                    versions = allVersions.Where(v => v.Type == "release").Where(v => ParseGameVersion(v.Name) >= minVer).Select(v => v.Name);
                }

                if (versions != null)
                {
                    foreach (var version in versions)
                    {
                        if (regex.IsMatch(version)) VersionVanil.Items.Add(version);
                    }
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.VersionLoadError", "Помилка завантаження версій гри: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка версій"),
                    MascotEmotion.Sad);
            }

            if (VersionVanil.SelectedValue != null) await RefreshList();
        }

        private System.Version ParseGameVersion(string versionStr)
        {
            try
            {
                var cleanStr = Regex.Match(versionStr, @"^[0-9\.]+").Value;
                var parts = cleanStr.Split('.');
                if (parts.Length == 2) cleanStr += ".0";
                if (System.Version.TryParse(cleanStr, out var version)) return version;
            }
            catch { }
            return new System.Version(0, 0, 0);
        }

        private async Task<string> DownloadAndSaveIconAsync(string imageUrl, string destinationFolder)
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;
            try
            {
                string extension = Path.GetExtension(imageUrl);
                if (string.IsNullOrEmpty(extension) || extension.Length > 5) extension = ".png";
                string localFileName = $"icon{extension}";
                string localFilePath = Path.Combine(destinationFolder, localFileName);
                using (var response = await httpClient.GetAsync(imageUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var imageBytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(localFilePath, imageBytes);
                        return localFilePath;
                    }
                }
            }
            catch { }
            return null;
        }

        private void SaveModpackToJson(InstalledModpack modpack)
        {
            Directory.CreateDirectory(ModpackPaths.DataDirectory);
            string jsonPath = ModpackPaths.InstalledModpacksJson;
            List<InstalledModpack> modpacks = new();
            if (File.Exists(jsonPath))
            {
                string existingJson = File.ReadAllText(jsonPath);
                modpacks = JsonSerializer.Deserialize<List<InstalledModpack>>(existingJson) ?? new List<InstalledModpack>();
            }
            if (!modpacks.Any(m => m.Name == modpack.Name)) modpacks.Add(modpack);
            string newJson = JsonSerializer.Serialize(modpacks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, newJson);
        }

        private async Task DownloadModpackAsync(string url, string savePath)
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (totalBytes.HasValue)
                        {
                            double progress = (double)totalRead / totalBytes.Value * 100;
                            DowloaadModPacksButtonTXT.Text = string.Format(LocalizationManager.GetString("Modpacks.ModpackProgress", "Завантажено: {0:F2}%"), progress);
                        }
                    }
                }
            }
        }
    }
}