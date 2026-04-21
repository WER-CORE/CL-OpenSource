using CL_CLegendary_Launcher_.Class;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Optifine.Installer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class CreateModPackWindow : FluentWindow
    {
        private readonly ThemeService _themeService;
        private readonly ModDownloadService _modDownloadService;
        private readonly ModpackService _modpackService;

        private int _currentPage = 0;
        private const int ITEMS_PER_PAGE = 10;
        private CancellationTokenSource _searchCts;
        protected byte SelectMod = 0;
        public bool IsModPackCreated = false;
        public string LoderNow = "Forge";
        private string SiteDowload = "Modrinth";

        private List<string> iconUrl = new List<string>();

        private List<ModInfo> _tempModList = new List<ModInfo>();
        private ModSearchResult _currentModToInstall;
        private List<ModVersionInfo> _currentAvailableVersions;

        private HttpClient httpClient = new HttpClient();

        public CreateModPackWindow(ModDownloadService modDownloadService, ModpackService modpackService)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _modDownloadService = modDownloadService;
            _modpackService = modpackService;

            ApplyLocalization();
            AddVersionList();
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Modpacks.CreateModpackTitle", "Створення збірки (Forge, Fabric...)");
            TxtHeaderTitle.Text = LocalizationManager.GetString("Modpacks.CreateModpackTitle", "Створення збірки - Модової збірки (Forge,Fabric,...)");

            TxtModpackName.Text = LocalizationManager.GetString("Modpacks.ModpackNameTitle", "Назва збірки");
            NameModPackTXT.PlaceholderText = LocalizationManager.GetString("Modpacks.ModpackNamePlaceholder", "Напишіть назву...");

            TxtMcVersion.Text = LocalizationManager.GetString("Modpacks.ModpackMcVersion", "Версія Minecraft");
            TxtLoaderType.Text = LocalizationManager.GetString("Modpacks.LoaderTypeTitle", "Тип ядра");
            TxtLoaderVersion.Text = LocalizationManager.GetString("Modpacks.ModpackLoaderVersion", "Версія ядра");

            BtnMods.Content = LocalizationManager.GetString("Modpacks.ModpackEditMods", "Моди");
            BtnResourcePacks.Content = LocalizationManager.GetString("Modpacks.ModpackEditResourcePacks", "Ресурс-паки");
            BtnShaders.Content = LocalizationManager.GetString("Modpacks.ModpackEditShaders", "Шейдери");

            SearchSystemModsTXT.PlaceholderText = LocalizationManager.GetString("Modpacks.ModsSearchPlaceholder", "Пошук модів...");

            TxtSearchResults.Text = LocalizationManager.GetString("Modpacks.SearchResultsTitle", "Результати пошуку");
            TxtSearchLoading.Text = LocalizationManager.GetString("Modpacks.ModsSearchLoading", "Bit-CL шукає потрібні файли...");

            TxtSelectedMods.Text = LocalizationManager.GetString("Modpacks.SelectedModsTitle", "Обрані моди (Ваша збірка)");

            BtnCreatePack.Text = LocalizationManager.GetString("Modpacks.CreateModpackBtn", "СТВОРИТИ ЗБІРКУ");

            TitleModsDowload.Text = LocalizationManager.GetString("Modpacks.ModVersionSelectionTitle", "Вибір версії моду");
            DowloadTXT.Text = LocalizationManager.GetString("Modpacks.ModAddBtn", "Додати");
        }

        private async void AddVersionList()
        {
            VersionVanilBox?.Items.Clear();
            LoaderVersionBox?.Items.Clear();
            if (LoaderVersionBox != null) LoaderVersionBox.IsEnabled = false;

            try
            {
                var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                var launcher = new MinecraftLauncher(path);

                if (LoderNow == "Fabric")
                {
                    var fabricInstaller = new FabricInstaller(httpClient);
                    var versions = await fabricInstaller.GetSupportedVersionNames();
                    foreach (var version in versions) VersionVanilBox.Items.Add(version);
                }
                else if (LoderNow == "Quilt")
                {
                    var quiltInstaller = new QuiltInstaller(httpClient);
                    var versions = await quiltInstaller.GetSupportedVersionNames();
                    foreach (var version in versions) VersionVanilBox.Items.Add(version);
                }
                else if (LoderNow == "LiteLoader")
                {
                    var liteLoaderInstaller = new LiteLoaderInstaller(httpClient);
                    var loaders = await liteLoaderInstaller.GetAllLiteLoaders();

                    var gameVersions = loaders
                        .Select(x => x.BaseVersion)
                        .Distinct()
                        .OrderByDescending(v =>
                        {
                            if (System.Version.TryParse(v, out var ver))
                                return ver;
                            return new System.Version(0, 0, 0);
                        })
                        .ToList();

                    foreach (var version in gameVersions) VersionVanilBox.Items.Add(version);
                }
                else
                {
                    var allVersions = await launcher.GetAllVersionsAsync();
                    foreach (var ver in allVersions)
                    {
                        if (ver.Type == "release")
                        {
                            if (LoderNow == "NeoForge" && ver.Name == "1.20.1") break;
                            if (LoderNow == "Forge" && ver.Name == "1.7.10") break;

                            VersionVanilBox.Items.Add(ver.Name);
                        }
                    }
                }

                if (VersionVanilBox.Items.Count > 0) VersionVanilBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Modpacks.VersionLoadError", "Помилка завантаження версій гри: {0}"), ex.Message));
            }
        }

        public async Task UpdateLoaderVersionsList(string? targetVersionToSelect = null)
        {
            if (VersionVanilBox.SelectedItem == null) return;

            string gameVersion = VersionVanilBox.SelectedItem.ToString();

            LoaderVersionBox.Items.Clear();
            LoaderVersionBox.IsEnabled = false;

            LoaderVersionBox.Text = LocalizationManager.GetString("Generic.Loading", "Завантаження...");

            try
            {
                if (LoderNow == "Forge")
                {
                    var versionLoader = new ForgeVersionLoader(httpClient);
                    var forgeList = await versionLoader.GetForgeVersions(gameVersion);
                    foreach (var forge in forgeList)
                        LoaderVersionBox.Items.Add(forge.ForgeVersionName);
                }
                else if (LoderNow == "Fabric")
                {
                    var fabricInstaller = new FabricInstaller(httpClient);
                    var fabricVersions = await fabricInstaller.GetLoaders(gameVersion);
                    foreach (var fabric in fabricVersions)
                        LoaderVersionBox.Items.Add(fabric.Version);
                }
                else if (LoderNow == "Quilt")
                {
                    var quiltInstaller = new QuiltInstaller(httpClient);
                    var quiltVersions = await quiltInstaller.GetLoaders(gameVersion);
                    foreach (var quilt in quiltVersions)
                        LoaderVersionBox.Items.Add(quilt.Version);
                }
                else if (LoderNow == "NeoForge")
                {
                    var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                    var launcher = new MinecraftLauncher(path);
                    var versionLoader = new NeoForgeInstaller(launcher);
                    var neoForgeList = await versionLoader.GetForgeVersions(gameVersion);
                    foreach (var neo in neoForgeList)
                        LoaderVersionBox.Items.Add(neo.VersionName);
                }
                else if (LoderNow == "LiteLoader")
                {
                    var liteLoaderInstaller = new LiteLoaderInstaller(httpClient);
                    var loaders = await liteLoaderInstaller.GetAllLiteLoaders();

                    var compatibleLoaders = loaders.Where(l => l.BaseVersion == gameVersion);
                    foreach (var loader in compatibleLoaders)
                        LoaderVersionBox.Items.Add(loader.Version);
                }
                else if (LoderNow == "Optifine")
                {
                    var loader = new OptifineInstaller(httpClient);
                    var versions = await loader.GetOptifineVersionsAsync();

                    var compatible = versions.Where(v => v.MinecraftVersion == gameVersion);
                    foreach (var v in compatible)
                        LoaderVersionBox.Items.Add(v.Version);
                }

                LoaderVersionBox.Text = "";

                if (LoaderVersionBox.Items.Count > 0)
                {
                    LoaderVersionBox.IsEnabled = true;
                    bool found = false;

                    if (!string.IsNullOrEmpty(targetVersionToSelect))
                    {
                        foreach (var item in LoaderVersionBox.Items)
                        {
                            string itemStr = item.ToString();

                            if (string.Equals(itemStr, targetVersionToSelect, StringComparison.OrdinalIgnoreCase) ||
                                itemStr.Contains(targetVersionToSelect))
                            {
                                LoaderVersionBox.SelectedItem = item;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        LoaderVersionBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    LoaderVersionBox.Text = LocalizationManager.GetString("Modpacks.VersionsNotFound", "Версій не знайдено");
                }
            }
            catch (Exception ex)
            {
                LoaderVersionBox.IsEnabled = true;
                LoaderVersionBox.Text = LocalizationManager.GetString("Dialogs.Error", "Помилка мережі");
                System.Diagnostics.Debug.WriteLine($"UpdateLoaderVersionsList Error: {ex.Message}");
            }
        }

        private async void UpdateModsList()
        {
            if (VersionVanilBox.SelectedItem == null) return;

            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            ModsDowloadList.Items.Clear();
            ModsSearchLoader.Visibility = Visibility.Visible;
            ModsSkeletonPanel.Visibility = Visibility.Visible;
            ModsDowloadList.Visibility = Visibility.Collapsed;

            if (PageNumberText != null)
                PageNumberText.Text = (_currentPage + 1).ToString();

            if (PrevPageBtn != null)
                PrevPageBtn.IsEnabled = _currentPage > 0;

            if (NextPageBtn != null)
                NextPageBtn.IsEnabled = false;

            try
            {
                string searchText = SearchSystemModsTXT.Text;

                int offset = _currentPage * ITEMS_PER_PAGE;

                var results = await _modDownloadService.SearchModsAsync(
                    searchText,
                    SiteDowload,
                    LoderNow,
                    SelectMod,
                    offset
                );

                if (token.IsCancellationRequested) return;

                if (results == null || !results.Any())
                {
                    return;
                }

                if (NextPageBtn != null)
                    NextPageBtn.IsEnabled = results.Count >= ITEMS_PER_PAGE;

                foreach (var mod in results)
                {
                    var item = CreateItemFromSearchResult(mod);
                    ModsDowloadList.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching mods: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    ModsSkeletonPanel.Visibility = Visibility.Collapsed;
                    ModsSearchLoader.Visibility = Visibility.Collapsed;
                    ModsDowloadList.Visibility = Visibility.Visible;
                }
            }
        }

        private ModPackItem CreateItemFromSearchResult(ModSearchResult mod)
        {
            var item = new ModPackItem();
            item.ModPackName.Text = mod.Title;

            if (!string.IsNullOrEmpty(mod.IconUrl))
            {
                try { item.IconModPack.Source = ImageHelper.LoadOptimizedImage(mod.IconUrl, 32); } catch { }
                iconUrl.Add(mod.IconUrl);
            }
            else
            {
                item.IconModPack.Source = ImageHelper.LoadOptimizedImage("pack://application:,,,/Icon/IconCL(Common).png", 32);
            }

            item.DetailsModPackBtn.PreviewMouseDown += (s, e) =>
            {
                SoundManager.Click();
                string baseUrl = mod.Site == "Modrinth" ? "https://modrinth.com" : "https://www.curseforge.com/minecraft";
                string category = mod.Site == "Modrinth"
                    ? (SelectMod == 1 ? "shader" : SelectMod == 2 ? "resourcepack" : "mod")
                    : (SelectMod == 1 ? "shaders" : SelectMod == 2 ? "texture-packs" : "mc-mods");
                WebHelper.OpenUrl($"{baseUrl}/{category}/{mod.Slug}");
            };

            item.AddModInModPack.PreviewMouseDown += async (s, e) =>
            {
                SoundManager.Click();
                await OpenVersionSelector(mod);
            };

            return item;
        }

        private async Task OpenVersionSelector(ModSearchResult mod)
        {
            if (VersionVanilBox.SelectedItem == null)
            {
                MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.NeedMinecraftVersion", "Спочатку оберіть версію Minecraft!"), LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Alert);
                return;
            }

            _currentModToInstall = mod;

            GameVersionMinecraft.Text = mod.Title;

            MenuInstaller.Visibility = Visibility.Visible;
            VersionMods.Items.Clear();

            string selectedGameVersion = VersionVanilBox.SelectedItem.ToString();

            try
            {
                var allVersions = await _modDownloadService.GetVersionsAsync(mod);

                _currentAvailableVersions = allVersions
                    .Where(v => v.GameVersions.Contains(selectedGameVersion) &&
                                (SelectMod != 0 || v.Loaders.Any(l => l.Equals(LoderNow, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                if (_currentAvailableVersions.Any())
                {
                    foreach (var v in _currentAvailableVersions)
                    {
                        VersionMods.Items.Add(v.VersionName);
                    }
                    VersionMods.SelectedIndex = 0;
                }
                else
                {
                    MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.NoVersionsForModpack", "Не знайдено версій для вашої збірки."), LocalizationManager.GetString("Dialogs.EmptyTitle", "Пусто"), MascotEmotion.Confused);
                    MenuInstaller.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
                MenuInstaller.Visibility = Visibility.Hidden;
            }
        }

        private async void DowloadTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (VersionMods.SelectedIndex == -1 || _currentModToInstall == null || _currentAvailableVersions == null) return;

            int index = VersionMods.SelectedIndex;
            if (index >= _currentAvailableVersions.Count) return;

            var selectedVerInfo = _currentAvailableVersions[index];

            var modInfo = new ModInfo
            {
                Name = _currentModToInstall.Title,
                ProjectId = selectedVerInfo.ModId,
                FileId = selectedVerInfo.VersionId,
                Loader = LoderNow,
                Version = VersionVanilBox.SelectedItem.ToString(),
                Url = selectedVerInfo.DownloadUrl,
                LoaderType = LoderNow,
                Type = SelectMod switch { 0 => "mod", 1 => "shader", 2 => "resourcepack", _ => "mod" },
                ImageURL = _currentModToInstall.IconUrl,
                Slug = _currentModToInstall.Slug,
                FileName = selectedVerInfo.FileName
            };

            SaveModToModpackJson(modInfo);

            MenuInstaller.Visibility = Visibility.Hidden;

            if (SelectMod == 0)
            {
                try
                {
                    this.Cursor = Cursors.Wait;

                    var dependencies = await _modDownloadService.GetDependenciesModInfoAsync(selectedVerInfo, LoderNow, 0);

                    if (dependencies != null && dependencies.Count > 0)
                    {
                        int addedCount = 0;
                        foreach (var dep in dependencies)
                        {
                            if (!_tempModList.Any(m => m.ProjectId == dep.ProjectId))
                            {
                                _tempModList.Add(dep);
                                AddItemToRightList(dep);
                                addedCount++;
                            }
                        }

                        if (addedCount > 0)
                        {
                            MascotMessageBox.Show(
                                string.Format(LocalizationManager.GetString("Modpacks.ModpackDepsAdded", "Автоматично додано {0} залежних модів (наприклад, Sodium/FabricAPI)."), addedCount),
                                LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                                MascotEmotion.Happy);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка додавання залежностей: {ex.Message}");
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }
        private void CloseMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            MenuInstaller.Visibility = Visibility.Hidden;
        }
        private void SaveModToModpackJson(ModInfo mod)
        {
            try
            {
                if (_tempModList.Any(m => m.ProjectId == mod.ProjectId))
                {
                    MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.ModInListAlready", "Цей мод вже є в списку."), LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Alert);
                    return;
                }

                _tempModList.Add(mod);

                AddItemToRightList(mod);

                MascotMessageBox.Show(string.Format(LocalizationManager.GetString("Modpacks.ModAddedQueue", "Мод \"{0}\" додано до черги!"), mod.Name), LocalizationManager.GetString("Dialogs.Success", "Успіх"), MascotEmotion.Happy);

                VersionVanilBox.IsEnabled = false;
                LoaderBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Modpacks.ModAddError", "Помилка додавання: {0}"), ex.Message));
            }
        }

        private void AddItemToRightList(ModInfo mod)
        {
            AddItemModPack moditem = new AddItemModPack();
            moditem.NameMod.Text = mod.Name;
            try
            {
                moditem.IconMod.Source = ImageHelper.LoadOptimizedImage(mod.ImageURL ?? "pack://application:,,,/Icon/IconCL(Common).png", 32);
            }
            catch { }

            moditem.DeleteModFromModPack.MouseDown += (s, e) =>
            {
                AddModsInModPackList.Items.Remove(moditem);

                var itemToRemove = _tempModList.FirstOrDefault(m => m.ProjectId == mod.ProjectId);
                if (itemToRemove != null)
                {
                    _tempModList.Remove(itemToRemove);
                }

                if (_tempModList.Count == 0)
                {
                    VersionVanilBox.IsEnabled = true;
                    LoaderBox.IsEnabled = true;
                }
            };

            AddModsInModPackList.Items.Add(moditem);
        }

        private void CreateModPacksButtonTXT_MouseDown(object sender, RoutedEventArgs e)
        {
            try
            {
                SoundManager.Click();

                string modpackName = NameModPackTXT.Text.Trim();
                if (string.IsNullOrWhiteSpace(modpackName))
                {
                    ShowError(LocalizationManager.GetString("Modpacks.ModpackNameEmpty", "Введіть назву збірки!"));
                    return;
                }

                if (_tempModList.Count == 0)
                {
                    ShowError(LocalizationManager.GetString("Modpacks.ModpackEmptyError", "Збірка пуста. Додайте хоча б один мод!"));
                    return;
                }

                string loaderVersion = LoaderVersionBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(loaderVersion))
                {
                    ShowError(LocalizationManager.GetString("Modpacks.LoaderVersionNotSelected", "Дочекайтесь завантаження версії ядра (Loader Version)!"));
                    return;
                }

                string basePath = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", modpackName);
                string pathJson = Path.Combine(basePath, "modpack.json");

                Directory.CreateDirectory(basePath);

                string newJson = JsonConvert.SerializeObject(_tempModList, Formatting.Indented);
                File.WriteAllText(pathJson, newJson);

                string imageUrl = iconUrl.FirstOrDefault() ?? "pack://application:,,,/Icon/IconCL(Common).png";

                var modpack = new InstalledModpack
                {
                    Name = modpackName,
                    TypeSite = "Custom",
                    MinecraftVersion = VersionVanilBox.SelectedItem?.ToString(),
                    LoaderType = LoderNow,
                    LoaderVersion = loaderVersion,
                    Path = basePath,
                    PathJson = pathJson,
                    UrlImage = imageUrl,
                };

                _modpackService.AddModpack(modpack);

                MascotMessageBox.Show(LocalizationManager.GetString("Modpacks.ModpackCreatedSuccess", "Збірку успішно створено!"), LocalizationManager.GetString("Dialogs.Success", "Успіх"), MascotEmotion.Happy);
                IsModPackCreated = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError(string.Format(LocalizationManager.GetString("Modpacks.ModpackCreateFatalError", "Критична помилка створення: {0}"), ex.Message));
            }
        }

        private void VersionVanil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            if (VersionVanilBox.SelectedItem != null)
            {
                UpdateLoaderVersionsList();
                UpdateModsList();
            }
        }

        private void LoaderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            if (LoaderBox.SelectedItem is ComboBoxItem item)
            {
                LoderNow = item.Content.ToString();
                AddVersionList();
            }
        }

        private void SearchSystemModsTXT_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _currentPage = 0;

            UpdateModsList();
        }

        private void ModsTxt_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            _currentPage = 0;
            SelectMod = 0;
            LoadModsByType("mod");
            UpdateModsList();
        }

        private void ResourcePackTxt_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            _currentPage = 0;
            SelectMod = 2;
            LoadModsByType("resourcepack");
            UpdateModsList();
        }

        private void ShaderPackTxt_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            _currentPage = 0;
            SelectMod = 1;
            LoadModsByType("shader");
            UpdateModsList();
        }

        private void ModrinthSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "Modrinth") return;

            SiteDowload = "Modrinth";
            _currentPage = 0;

            UpdateProviderUI();
            UpdateModsList();
        }

        private void CurseForgeSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "CurseForge") return;

            SiteDowload = "CurseForge";
            _currentPage = 0;

            UpdateProviderUI();
            UpdateModsList();
        }

        private void UpdateProviderUI()
        {
            if (SiteDowload == "Modrinth")
            {
                ModrinthSite.Opacity = 1.0;
                CurseForgeSite.Opacity = 0.5;
            }
            else
            {
                ModrinthSite.Opacity = 0.5;
                CurseForgeSite.Opacity = 1.0;
            }
        }

        private void Provider_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                if (img.Name == "ModrinthSite" && SiteDowload != "Modrinth")
                {
                    img.Opacity = 0.8;
                }
                else if (img.Name == "CurseForgeSite" && SiteDowload != "CurseForge")
                {
                    img.Opacity = 0.8;
                }
            }
        }

        private void Provider_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                if (img.Name == "ModrinthSite" && SiteDowload != "Modrinth")
                {
                    img.Opacity = 0.5;
                }
                else if (img.Name == "CurseForgeSite" && SiteDowload != "CurseForge")
                {
                    img.Opacity = 0.5;
                }
            }
        }

        private void LoadModsByType(string type)
        {
            AddModsInModPackList.Items.Clear();

            var filtered = _tempModList.Where(m => m.Type == type).ToList();

            foreach (var m in filtered)
            {
                AddItemToRightList(m);
            }
        }

        private void ShowError(string msg) => MascotMessageBox.Show(msg, LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);

        private void ExitLauncher_MouseDown(object sender, RoutedEventArgs e) => this.Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void PrevPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (_currentPage > 0)
            {
                _currentPage--;
                UpdateModsList();
            }
        }

        private void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            _currentPage++;
            UpdateModsList();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (!IsModPackCreated)
            {
            }
        }
    }
}