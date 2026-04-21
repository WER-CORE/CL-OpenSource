using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CmlLib.Core;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class DownloadEditPack : FluentWindow
    {
        public ModpackInfo CurrentModpack { get; set; }
        protected byte SelectMod = 0;
        public bool IsModPackCreated = false;
        private List<string> iconUrl = new List<string>();
        public string LoderNow = "Forge";
        private string SiteDowload = "Modrinth";
        private readonly string apiKey = Secrets.CurseForgeKey;
        private static readonly HttpClient httpClient = new HttpClient();

        List<string> fileUrlDowload = new List<string>();
        List<string> versionIds = new List<string>();

        private ApiClient curseClient;
        private readonly ModDownloadService _modDownloadService;
        private int _currentPage = 0;
        private const int PageSize = 10;

        public DownloadEditPack(ModpackInfo currentModpack, byte SelectMod)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _modDownloadService = new ModDownloadService();

            CurrentModpack = currentModpack;
            this.SelectMod = SelectMod;

            ApplyLocalization();

            LoadVaribaleCurrectPack();
            UpdateModsList();
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("DownloadManager.Title", "Менеджер завантаження");

            var ModsType = SelectMod switch
            {
                0 => LocalizationManager.GetString("DownloadManager.TypeMods", "модів"),
                2 => LocalizationManager.GetString("DownloadManager.TypeShaders", "шейдерів"),
                1 => LocalizationManager.GetString("DownloadManager.TypeResourcePacks", "ресурс-паків"),
                _ => LocalizationManager.GetString("DownloadManager.TypeMods", "модів")
            };

            NameWin.Text = string.Format(LocalizationManager.GetString("DownloadManager.EditPackTitle", "Завантаження {0} у збірку {1}"), ModsType, CurrentModpack.Name);

            SearchSystem.PlaceholderText = LocalizationManager.GetString("Modpacks.ModSearchPlaceholder", "Пошук...");
            ModrinthSite.ToolTip = LocalizationManager.GetString("Modpacks.ModrinthTooltip", "Шукати на Modrinth");
            CurseForgeSite.ToolTip = LocalizationManager.GetString("Modpacks.CurseForgeTooltip", "Шукати на CurseForge");

            TxtQueue.Text = LocalizationManager.GetString("DownloadManager.QueueTitle", "До черги:");
            CreateModPacksButton.Content = LocalizationManager.GetString("DownloadManager.DownloadAndAddBtn", "Завантажити та Додати");

            TxtInstallMod.Text = LocalizationManager.GetString("DownloadManager.InstallModTitle", "Встановлення мода");
            TitleModsDowload.Text = LocalizationManager.GetString("DownloadManager.SelectFileVersionTitle", "Оберіть версію файлу:");
            DowloadTXT.Content = LocalizationManager.GetString("Mods.InstallBtn", "Встановити");
        }
        private void LoadVaribaleCurrectPack()
        {
            if (CurrentModpack != null)
            {
                LoadLoaderVersion(CurrentModpack.Name);
                VersionVanil.Items.Add(CurrentModpack.MinecraftVersion);
                VersionVanil.SelectedItem = CurrentModpack.MinecraftVersion;

                VersionVanil.IsEnabled = false; ListModsList.IsEnabled = false;

                LoaderButton.Content = CurrentModpack.LoaderType;
                LoderNow = CurrentModpack.LoaderType;
            }
        }
        void LoadLoaderVersion(string modpackName)
        {
            string path = @$"{AppContext.BaseDirectory}Data\installed_modpacks.json";

            if (!File.Exists(path))
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.ConfigFileNotFoundDesc", "Ой! Я не знайшла файл конфігурації installed_modpacks.json."),
                    LocalizationManager.GetString("Modpacks.ConfigFileNotFoundTitle", "Файл не знайдено"),
                    MascotEmotion.Confused);
                return;
            }

            string json = File.ReadAllText(path);
            JArray modpacks = JArray.Parse(json);

            var found = modpacks.FirstOrDefault(x => (string)x["Name"] == modpackName);

            if (found != null)
            {
                string loaderVersion = (string)found["LoaderVersion"];
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    ListModsList.Items.Add(loaderVersion);
                    ListModsList.SelectedItem = loaderVersion;
                }
                else
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Modpacks.LoaderVersionMissingDesc", "У налаштуваннях цієї збірки не вказано версію завантажувача."),
                        LocalizationManager.GetString("Modpacks.LoaderVersionMissingTitle", "Дані відсутні"),
                        MascotEmotion.Confused);
                }
            }
            else
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.ModpackMissingDesc", "Хм... Я перевірила список, але цю збірку не знайшла."),
                    LocalizationManager.GetString("Modpacks.ModpackMissingTitle", "Збірка зникла"),
                    MascotEmotion.Confused);
            }
        }

        private void AddModpackToInstalled(InstalledModpack modpack)
        {
            string jsonPath = Path.Combine(ModpackPaths.DataDirectory, "modpack_temp_add.json");

            List<InstalledModpack> modpacks = new();
            if (File.Exists(jsonPath))
            {
                var existingJson = File.ReadAllText(jsonPath);
                modpacks = JsonSerializer.Deserialize<List<InstalledModpack>>(existingJson) ?? new();
            }

            if (!modpacks.Any(m => m.Name == modpack.Name))
                modpacks.Add(modpack);

            string newJson = JsonSerializer.Serialize(modpacks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, newJson);
        }

        private async void UpdateModsList()
        {
            if (VersionVanil.SelectedItem == null || (ListModsList.SelectedItem == null && CurrentModpack.LoaderType != "Vanila"))
                return;

            if (ModsDowloadList.Items != null)
                ModsDowloadList.Items.Clear();

            TxtPageNumber.Text = (_currentPage + 1).ToString();
            BtnPrevPage.IsEnabled = _currentPage > 0;

            try
            {
                if (SiteDowload == "Modrinth")
                {
                    int offset = _currentPage * PageSize;

                    var urls = new[] {
    $"https://api.modrinth.com/v2/search?query={SearchSystem.Text}&facets=[[%22categories:{LoderNow.ToLower()}%22],[%22project_type:mod%22]]&limit={PageSize}&offset={offset}",
    $"https://api.modrinth.com/v2/search?query={SearchSystem.Text}&facets=[[%22project_type:resourcepack%22]]&limit={PageSize}&offset={offset}",
    $"https://api.modrinth.com/v2/search?query={SearchSystem.Text}&facets=[[%22project_type:shader%22]]&limit={PageSize}&offset={offset}",
                    };

                    var response = await httpClient.GetStringAsync(urls[(int)SelectMod]);
                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

                    if (result["hits"].Count < PageSize) BtnNextPage.IsEnabled = false;
                    else BtnNextPage.IsEnabled = true;

                    foreach (var mod in result["hits"])
                    {
                        string loaderType = SelectMod switch
                        {
                            0 => "mod",
                            1 => "shader",
                            2 => "resourcepack",
                            _ => "mod"
                        };
                        var item = CreateItemFromModrinth(mod, loaderType);
                        ModsDowloadList.Items.Add(item);
                    }
                }
                else if (SiteDowload == "CurseForge")
                {
                    int classId = SelectMod switch
                    {
                        0 => 6,
                        2 => 6552,
                        1 => 12,
                        _ => 6
                    };

                    dynamic modLoaderType = null;
                    if (CurrentModpack.LoaderType != "Vanila")
                    {
                        modLoaderType = LoderNow switch
                        {
                            "Forge" => ModLoaderType.Forge,
                            "Fabric" => ModLoaderType.Fabric,
                            "Quilt" => ModLoaderType.Quilt,
                            "NeoForge" => ModLoaderType.NeoForge,
                            "LiteLoader" => ModLoaderType.LiteLoader,
                            "Optifine" => ModLoaderType.Any,
                            _ => null
                        };
                    }

                    curseClient ??= new ApiClient(apiKey);

                    int index = _currentPage * PageSize;

                    var searchResponse = await curseClient.SearchModsAsync(
                        gameId: 432,
                        classId: classId,
                        modLoaderType: modLoaderType,
                        gameVersion: VersionVanil.SelectedItem?.ToString(),
                        pageSize: PageSize,
                        searchFilter: SearchSystem.Text,
                        index: index
                    );

                    if (searchResponse.Data.Count < PageSize) BtnNextPage.IsEnabled = false;
                    else BtnNextPage.IsEnabled = true;

                    foreach (var mod in searchResponse.Data)
                    {
                        var item = CreateItemJarFromCurseForge(mod, SelectMod);
                        ModsDowloadList.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.ErrorDesc", "Ех, не вдалося завантажити список модів.\n{0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка списку"),
                    MascotEmotion.Sad);
            }
        }

        private ModPackItem CreateItemJarFromCurseForge(Mod mod, int type)
        {
            var item = new ModPackItem();
            item.ModPackName.Text = mod.Name;
            string icon = mod.Logo?.Url ?? "";
            iconUrl.Add(icon);

            item.IconModPack.Source = !string.IsNullOrEmpty(icon)
                ? ImageHelper.LoadOptimizedImage(icon, 32)
                : ImageHelper.LoadOptimizedImage("pack://application:,,,/Icon/IconCL(Common).png", 32);

            item.DetailsModPackBtn.PreviewMouseDown += (s, e) => { 
                SoundManager.Click(); 
                WebHelper.OpenUrl(mod.Links.WebsiteUrl);
            };

            item.downloda_url = "";
            item.game_version = VersionVanil.SelectedItem?.ToString();
            item.loaders = LoderNow;
            item.ProjectId = mod.Id.ToString();
            item.Slug = mod.Slug;
            item.Name = mod.Name;
            item.Type = type;

            item.AddModInModPack.Visibility = Visibility.Visible;
            item.AddModInModPack.MouseDown += async (s, e) =>
            {
                SoundManager.Click();
                AnimationService.FadeIn(MenuInstaller, 0.3);
                var info = new ModInfo
                {
                    Name = item.Name,
                    ProjectId = item.ProjectId,
                    Type = type == 0 ? "mod" : type == 1 ? "shader" : "resourcepack",
                    ImageURL = mod.Logo.Url,
                    Slug = item.Slug,
                };
                await GetLatestCompatibleModVersions_CurseForge(new List<ModInfo> { info }, VersionVanil.SelectedItem?.ToString());
            };

            return item;
        }

        private ModPackItem CreateItemFromModrinth(dynamic mod, string loaderType)
        {
            var item = new ModPackItem();
            item.ModPackName.Text = mod["title"];
            var icon = mod["icon_url"]?.ToString();
            iconUrl.Add(icon);

            if (!string.IsNullOrEmpty(icon) && Uri.IsWellFormedUriString(icon, UriKind.Absolute))
                item.IconModPack.Source = ImageHelper.LoadOptimizedImage(icon, 32);
            else
                item.IconModPack.Source = ImageHelper.LoadOptimizedImage("pack://application:,,,/Icon/IconCL(Common).png", 32);

            item.DetailsModPackBtn.PreviewMouseDown += (s, e) => { SoundManager.Click(); WebHelper.OpenUrl($"https://modrinth.com/{loaderType}/{mod.Slug}"); };

            item.ProjectId = mod["project_id"];
            item.Slug = mod["slug"];
            item.Name = mod["title"];
            item.AddModInModPack.Visibility = Visibility.Visible;

            item.AddModInModPack.MouseDown += (s, e) =>
            {
                SoundManager.Click();
                AnimationService.FadeIn(MenuInstaller, 0.3);
                GetCompatibleVersions(new List<ModInfo>
                {
                    new ModInfo
                    {
                        Name = mod["title"].ToString(),
                        ProjectId = mod["project_id"].ToString(),
                        Loader = LoderNow,
                        LoaderType = loaderType,
                        Version = VersionVanil.SelectedItem.ToString(),
                        Url = $"https://api.modrinth.com/v2/project/{mod["project_id"]}/version",
                        Type = loaderType,
                        ImageURL = icon,
                        Slug = item.Slug,
                    }
                }).ContinueWith(task => { }, TaskScheduler.FromCurrentSynchronizationContext());
            };
            return item;
        }

        private async Task<List<ModInfo>> GetCompatibleVersions(List<ModInfo> mods)
        {
            List<ModInfo> updatedMods = new();
            VersionMods.Items.Clear();
            fileUrlDowload?.Clear();
            versionIds?.Clear();

            if (VersionVanil.SelectedItem == null)
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.ForgotMinecraftVersionDesc", "Секундочку! Ти забув обрати версію Minecraft."),
                    LocalizationManager.GetString("Modpacks.ForgotMinecraftVersionTitle", "Версія?"),
                    MascotEmotion.Alert);
                return updatedMods;
            }

            string selectedGameVersion = VersionVanil.SelectedItem.ToString();
            AnimationService.FadeIn(MenuInstaller, 0.3);

            using HttpClient httpClient = new();

            foreach (var mod in mods)
            {
                try
                {
                    string url = $"https://api.modrinth.com/v2/project/{mod.ProjectId}/version";
                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var versionsArray = JArray.Parse(responseBody);

                    if (versionsArray == null || versionsArray.Count == 0)
                    {
                        MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("Modpacks.NoVersionsFoundForMod", "Дивина, я не знайшла версій для \"{0}\"."), mod.Name),
                            LocalizationManager.GetString("Dialogs.EmptyTitle", "Пусто"),
                            MascotEmotion.Confused);
                        continue;
                    }

                    foreach (var version in versionsArray)
                    {
                        var gameVersions = version["game_versions"].ToObject<List<string>>();
                        var loaders = version["loaders"].ToObject<List<string>>();

                        bool isCompatible = mod.LoaderType == "mod"
                            ? gameVersions.Contains(selectedGameVersion) && loaders.Any(l => l.Equals(mod.Loader, StringComparison.OrdinalIgnoreCase))
                            : gameVersions.Contains(selectedGameVersion);

                        if (isCompatible)
                        {
                            VersionMods.Items.Add(version["version_number"]?.ToString());

                            versionIds.Add(version["id"]?.ToString());

                            var files = version["files"] as JArray;
                            string fileUrl = files?[0]?["url"]?.ToString();
                            fileUrlDowload.Add(fileUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ErrorProcessingMod", "Помилка при обробці \"{0}\":\n{1}"), mod.Name, ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Збій"),
                        MascotEmotion.Sad);
                }
            }
            return updatedMods;
        }

        private async Task<List<ModInfo>> GetLatestCompatibleModVersions_CurseForge(List<ModInfo> mods, string gameVersion)
        {
            List<ModInfo> updatedMods = new();
            VersionMods.Items.Clear();
            fileUrlDowload?.Clear();
            versionIds?.Clear();

            string selectedGameVersion = VersionVanil.SelectedItem.ToString();
            AnimationService.FadeIn(MenuInstaller, 0.3);
            curseClient ??= new ApiClient(apiKey);

            foreach (var mod in mods)
            {
                try
                {
                    if (!int.TryParse(mod.ProjectId, out int modId)) continue;

                    var modLoaderType = LoderNow switch
                    {
                        "Fabric" => ModLoaderType.Fabric,
                        "Quilt" => ModLoaderType.Quilt,
                        "NeoForge" => ModLoaderType.NeoForge,
                        _ => ModLoaderType.Forge
                    };

                    var response = await curseClient.GetModFilesAsync(modId, null, modLoaderType);
                    if (response?.Data == null) continue;

                    var compatibleFiles = response.Data.Where(file =>
                        file.GameVersions.Any(v => v.Equals(selectedGameVersion, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    if (compatibleFiles.Count == 0)
                    {
                        MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("Modpacks.NoCompatibleVersionsCurseForge", "Немає сумісних версій для \"{0}\"."), mod.Name),
                            LocalizationManager.GetString("Modpacks.IncompatibleTitle", "Несумісність"),
                            MascotEmotion.Sad);
                        continue;
                    }

                    foreach (var file in compatibleFiles)
                    {
                        VersionMods.Items.Add($"{file.DisplayName}");

                        versionIds.Add(file.Id.ToString());

                        string url = file.DownloadUrl;
                        if (string.IsNullOrEmpty(url))
                        {
                            string typePath = mod.Type switch { "shader" => "shaders", "resourcepack" => "texture-packs", _ => "mc-mods" };
                            url = $"https://www.curseforge.com/minecraft/{typePath}/{mod.Slug}/download/{file.Id}";
                        }
                        fileUrlDowload.Add(url);
                    }
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.CurseForgeErrorDesc", "Помилка CurseForge: {0}"), ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Збій"),
                        MascotEmotion.Sad);
                }
            }
            return updatedMods;
        }

        private void SearchSystem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchSystem.Text != LocalizationManager.GetString("Modpacks.ModSearchPlaceholder", "Пошук..."))
            {
                _currentPage = 0;
                UpdateModsList();
            }
        }

        private void ModrinthSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "Modrinth") return;

            ModrinthSite.Opacity = 1.0;
            CurseForgeSite.Opacity = 0.5;

            if (ModsDowloadList.Items != null) ModsDowloadList.Items.Clear();
            SiteDowload = "Modrinth";
            _currentPage = 0;

            UpdateModsList();
        }

        private void CurseForgeSite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            if (SiteDowload == "CurseForge") return;

            ModrinthSite.Opacity = 0.5;
            CurseForgeSite.Opacity = 1.0;

            if (ModsDowloadList.Items != null) ModsDowloadList.Items.Clear();
            SiteDowload = "CurseForge";
            _currentPage = 0;

            UpdateModsList();
        }

        private async void DowloadTXT_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (VersionMods.SelectedItem == null)
            {
                MenuInstaller.Visibility = Visibility.Hidden;
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.SelectVersionAlertDesc", "Тицьни на потрібну версію зі списку!"),
                    LocalizationManager.GetString("Modpacks.SelectVersionAlertTitle", "Обери версію"),
                    MascotEmotion.Alert);
                return;
            }

            int selectedIndex = VersionMods.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= fileUrlDowload.Count) return;

            var selectedModItem = ModsDowloadList.SelectedItem as ModPackItem;

            string currentFileId = (selectedIndex < versionIds.Count) ? versionIds[selectedIndex] : null;

            var modInfo = new ModInfo
            {
                Name = $"{selectedModItem?.ModPackName?.Text?.ToString() ?? "Mod"}:{VersionMods.SelectedItem?.ToString() ?? "?"}",
                ProjectId = selectedModItem?.ProjectId,
                FileId = currentFileId,
                Loader = ListModsList.SelectedItem?.ToString(),
                Version = VersionVanil.SelectedItem?.ToString(),
                Url = fileUrlDowload[selectedIndex],
                LoaderType = LoderNow,
                Type = SelectMod switch { 0 => "mod", 2 => "shader", 1 => "resourcepack", _ => "mod" },
                ImageURL = selectedModItem?.IconModPack?.Source?.ToString(),
                Slug = selectedModItem?.Slug
            };

            SaveModToModpackJson(modInfo, silent: false);

            MenuInstaller.Visibility = Visibility.Hidden;

            if (SelectMod == 0 && !string.IsNullOrEmpty(currentFileId))
            {
                try
                {
                    this.Cursor = Cursors.Wait;

                    var verInfo = new ModVersionInfo
                    {
                        ModId = modInfo.ProjectId,
                        VersionId = modInfo.FileId,
                        Site = SiteDowload,
                        GameVersions = new List<string> { modInfo.Version }
                    };

                    var dependencies = await _modDownloadService.GetDependenciesModInfoAsync(verInfo, LoderNow, 0);

                    if (dependencies != null && dependencies.Count > 0)
                    {
                        int addedCount = 0;
                        foreach (var dep in dependencies)
                        {
                            if (dep.ProjectId != modInfo.ProjectId)
                            {
                                SaveModToModpackJson(dep, silent: true);
                                addedCount++;
                            }
                        }

                        if (addedCount > 0)
                        {
                            MascotMessageBox.Show(
                                string.Format(LocalizationManager.GetString("Modpacks.ModpackDepsAdded", "Автоматично додано {0} залежних модів (API/Lib)."), addedCount),
                                LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                                MascotEmotion.Happy);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Помилка залежностей: {ex.Message}");
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }

        private void GirdModsDowload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            AnimationService.FadeOut(MenuInstaller, 0.3);
            if (fileUrlDowload.Count != 0) fileUrlDowload.Clear();
        }

        private void CreateModPacksButtonTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            this.Close();
        }

        private void SaveModToModpackJson(ModInfo mod, bool silent = false)
        {
            string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name);
            Directory.CreateDirectory(modpackFolder);
            string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

            List<ModInfo> modList = new();
            if (File.Exists(jsonPath))
            {
                try { modList = JsonSerializer.Deserialize<List<ModInfo>>(File.ReadAllText(jsonPath)) ?? new List<ModInfo>(); }
                catch { modList = new List<ModInfo>(); }
            }

            if (!modList.Any(m => m.Name == mod.Name || (m.ProjectId == mod.ProjectId && !string.IsNullOrEmpty(m.ProjectId))))
            {
                modList.Add(mod);

                AddItemModPack moditem = new AddItemModPack();
                moditem.NameMod.Text = mod.Name;
                try { moditem.IconMod.Source = ImageHelper.LoadOptimizedImage(mod.ImageURL ?? "pack://application:,,,/Icon/IconCL(Common).png", 32); } catch { }

                moditem.DeleteModFromModPack.MouseDown += (s, e) =>
                {
                    AddModsInModPackList.Items.Remove(moditem);
                    if (File.Exists(jsonPath))
                    {
                        var list = JsonSerializer.Deserialize<List<ModInfo>>(File.ReadAllText(jsonPath)) ?? new List<ModInfo>();
                        list = list.Where(m => m.Name != moditem.NameMod.Text).ToList();
                        File.WriteAllText(jsonPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                    }
                };

                AddModsInModPackList.Items.Add(moditem);

                string newJson = JsonSerializer.Serialize(modList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, newJson);

                if (!silent)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ModAddedSuccessDesc", "Ура! Мод \"{0}\" успішно додано!"), mod.Name),
                        LocalizationManager.GetString("Dialogs.Success", "Готово!"),
                        MascotEmotion.Happy);
                }
            }
            else
            {
                if (!silent)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ModAlreadyInListDesc", "Мод \"{0}\" вже є в списку."), mod.Name),
                        LocalizationManager.GetString("Modpacks.ModAlreadyInListTitle", "Вже є"),
                        MascotEmotion.Alert);
                }
            }

            VersionVanil.IsEnabled = false;
            ListModsList.IsEnabled = false;
        }
        private async void CreateModPacksButton_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            var progress = new DowloadProgress();
            progress.Show();

            string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", CurrentModpack.Name);
            string jsonPath = Path.Combine(modpackFolder, "modpack_temp_add.json");

            if (!File.Exists(jsonPath))
            {
                progress.Close();
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.NoModsToDownloadDesc", "Немає модів для завантаження."),
                    LocalizationManager.GetString("Dialogs.EmptyTitle", "Пусто"),
                    MascotEmotion.Alert);
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(jsonPath);
                var mods = JsonConvert.DeserializeObject<List<ModInfo>>(json) ?? new List<ModInfo>();

                int total = mods.Count;
                int completed = 0;

                foreach (var mod in mods)
                {
                    string subFolder = mod.Type switch
                    {
                        "mod" => "mods",
                        "shader" => "shaderpacks",
                        "resourcepack" => "resourcepacks",
                        _ => "mods"
                    };

                    string basePath = CurrentModpack.Path;
                    string targetDir = Path.Combine(basePath, subFolder);

                    bool hasOverrides = Directory.Exists(Path.Combine(basePath, "overrides"));
                    bool hasOverride = Directory.Exists(Path.Combine(basePath, "override"));
                    
                    if (hasOverrides)
                    {
                        targetDir = Path.Combine(basePath, "overrides", subFolder);
                    }
                    else if (hasOverride)
                    {
                        targetDir = Path.Combine(basePath, "override", subFolder);
                    }
                    else if (Directory.Exists(Path.Combine(basePath, ".minecraft")))
                    {
                        targetDir = Path.Combine(basePath, ".minecraft", subFolder);
                    }

                    Directory.CreateDirectory(targetDir); string fileName = Path.GetFileName(mod.Url);
                    string filePath = Path.Combine(targetDir, fileName);

                    progress.DowloadProgressBarFileTask(total, completed, fileName);

                    if (!File.Exists(filePath))
                    {
                        bool success = await DownloadFileWithProgress(mod.Url, filePath, progress);
                        if (!success) await HandleManualDownloadPrompt(mod.Url, filePath, fileName);
                    }
                    completed++;
                }

                File.Delete(jsonPath);
                progress.Close();
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.AllModsDownloadedDesc", "Всі моди успішно завантажені!"),
                    LocalizationManager.GetString("Dialogs.Success", "Готово!"),
                    MascotEmotion.Happy);
                this.Close();
            }
            catch (Exception ex)
            {
                progress.Close();
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.ErrorDesc", "Помилка: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Збій"),
                    MascotEmotion.Sad);
            }
        }
        private async Task<bool> DownloadFileWithProgress(string url, string savePath, DowloadProgress progress)
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
                byte[] buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;
                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    totalRead += bytesRead;
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    if (totalBytes > 0)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes);
                        progress.Dispatcher.Invoke(() => progress.DowloadProgressBarFile(percent));
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private async Task HandleManualDownloadPrompt(string url, string fullPath, string filename, string errorMessage = "")
        {
            bool result = MascotMessageBox.Ask(
                string.Format(LocalizationManager.GetString("DownloadManager.ManualDownloadAskDesc", "Не вдалося скачати {0}. Відкрити браузер?"), filename),
                LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                MascotEmotion.Sad);

            if (result)
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
        }

        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }
        private void ExitLauncher_MouseDown(object sender, RoutedEventArgs e) => this.Close();
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (_currentPage > 0)
            {
                _currentPage--;
                UpdateModsList();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            _currentPage++;
            UpdateModsList();
        }
    }
}