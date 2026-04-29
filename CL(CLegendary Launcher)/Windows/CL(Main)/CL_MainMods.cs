using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Windows.Media.Render;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        private int _currentCategoryIndex = 0;

        private void VersionMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();
            if (Version.SelectedItem != null && VersionMods.SelectedItem != null)
            {
                DowloadMod.IsEnabled = true;
            }
            else
            {
                DowloadMod.IsEnabled = false;
            }
        }
        public async Task UpdateModsMinecraftAsync()
        {
            try
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;

                await DiscordController.UpdatePresence(LocalizationManager.GetString("DiscordRPC.SearchingMods", "Шукає моди..."));

                ModsDowloadList.Visibility = Visibility.Collapsed;
                ModsSearchLoader.Visibility = Visibility.Visible;
                ModsSkeletonPanel.Visibility = Visibility.Visible;

                if (PaginationPanel != null) PaginationPanel.Visibility = Visibility.Visible;

                ModsDowloadList.Items.Clear();
                VersionMods.Items.Clear();

                string searchText = SearchSystemModsTXT.Text.Trim();
                int offset = _currentPage * ITEMS_PER_PAGE;

                var results = await _modDownloadService.SearchModsAsync(
                    searchText,
                    SiteMods,
                    VersionType,
                    selectmodificed,
                    offset
                );

                if (token.IsCancellationRequested) return;

                if (PageNumberText != null) PageNumberText.Text = (_currentPage + 1).ToString();
                if (PrevPageBtn != null) PrevPageBtn.IsEnabled = _currentPage > 0;
                if (NextPageBtn != null) NextPageBtn.IsEnabled = results != null && results.Count >= ITEMS_PER_PAGE;

                if (results == null || !results.Any())
                {
                    ModsDowloadList.Items.Add(new TextBlock
                    {
                        Text = LocalizationManager.GetString("Mods.NotFound", "Пошук не дав результатів (або кінець списку)."),
                        Foreground = Brushes.White,
                        Margin = new Thickness(10),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontFamily = new FontFamily("Inter"),
                        FontSize = 14
                    });
                    if (NextPageBtn != null) NextPageBtn.IsEnabled = false;
                    return;
                }

                foreach (var mod in results)
                {
                    if (token.IsCancellationRequested) return;

                    var item = LauncherUIFactory.CreateModCard(
                        mod,
                        HandleModDownloadClick,
                        OpenModInBrowser
                    );

                    ModsDowloadList.Items.Add(item);
                    AnimationService.AnimatePageTransition(ModsDowloadList);

                    await Task.Delay(5, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            }
            finally
            {
                ModsSearchLoader.Visibility = Visibility.Collapsed;
                ModsSkeletonPanel.Visibility = Visibility.Collapsed;
                ModsDowloadList.Visibility = Visibility.Visible;
            }
        }

        private async void PrevPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (_currentPage > 0)
            {
                _currentPage--;
                await UpdateModsMinecraftAsync();
            }
        }

        private async void NextPageBtn_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            _currentPage++;
            await UpdateModsMinecraftAsync();
        }

        private async void SearchSystemModsTXT_TextChanged(object sender, TextChangedEventArgs e)
        {
            string defaultSearch = LocalizationManager.GetString("Generic.Search", "Пошук...");
            if (!string.IsNullOrWhiteSpace(SearchSystemModsTXT.Text) && SearchSystemModsTXT.Text != defaultSearch && SearchSystemModsTXT.Text != "Пошук")
            {
                _currentPage = 0;
                await UpdateModsMinecraftAsync();
            }
            else
            {
                ModsDowloadList.Items.Clear();
            }
        }

        private async void HandleModDownloadClick(ModSearchResult mod)
        {
            SoundManager.Click();
            if (Version != null) Version.Items.Clear();
            if (VersionMods != null) VersionMods.Items.Clear();
            if (CollectionList != null)
            {
                CollectionList.ItemsSource = null;
                CollectionList.Items.Clear();
            }

            _currentModVersions = null;

            AnimationService.FadeIn(GirdModsDowload, 0.2);
            AnimationService.AnimatePageTransition(MenuInstaller);

            try
            {
                var allReleaseVersions = await _modDownloadService.GetVersionsAsync(mod);

                if (allReleaseVersions == null || !allReleaseVersions.Any())
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Mods.ArchiveEmptyDesc", "Дивина! Я перерила усі архіви, але не знайшла жодного файлу для цього проекту."),
                        LocalizationManager.GetString("Mods.ArchiveEmptyTitle", "Пусто"),
                        MascotEmotion.Confused
                    );
                    CloseInstallerMenu();
                    return;
                }

                _currentModVersions = allReleaseVersions;

                if (ModType == "Collection")
                {
                    CollectionListBorder.Visibility = Visibility.Visible;
                    var modpacks = _modpackService.LoadInstalledModpacks();

                    if (modpacks == null || !modpacks.Any())
                    {
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("Mods.NoModpacksDesc", "У вас ще немає жодної створеної збірки! Спочатку створіть збірку, щоб я могла додати туди цей мод."),
                            LocalizationManager.GetString("Mods.NoModpacksTitle", "Немає збірок"),
                            MascotEmotion.Normal
                        );
                        CloseInstallerMenu();
                        return;
                    }

                    CollectionList.ItemsSource = modpacks;
                    CollectionList.DisplayMemberPath = "Name";
                }
                else
                {
                    CollectionListBorder.Visibility = Visibility.Hidden;
                    if (Version.Parent is Border versionBorder) versionBorder.Visibility = Visibility.Visible;

                    var gameVersions = _currentModVersions
                        .SelectMany(v => v.GameVersions)
                        .Distinct()
                        .Where(v => !string.IsNullOrEmpty(v) && char.IsDigit(v[0]))
                        .OrderByDescending(v => v)
                        .ToList();

                    if (!gameVersions.Any())
                    {
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("Mods.NoVersionsDesc", "Я знайшла файли, але жоден з них не має чіткої версії гри (можливо, мод застарів)."),
                            LocalizationManager.GetString("Mods.NoVersionsTitle", "Версії відсутні"),
                            MascotEmotion.Confused
                        );
                        CloseInstallerMenu();
                        return;
                    }

                    foreach (var gv in gameVersions) Version.Items.Add(gv);

                    if (Version.Items.Count > 0) Version.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Oops", "Збій"), MascotEmotion.Sad);
                CloseInstallerMenu();
            }
        }

        private void CloseInstallerMenu()
        {
            AnimationService.FadeOut(GirdModsDowload, 0.2);
            AnimationService.AnimatePageTransitionExit(MenuInstaller);
        }

        private async void DowloadMod_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (VersionMods.SelectedItem == null || _currentModVersions == null) return;
            SoundManager.Click();

            var selectedVersionInfo = _currentModVersions.FirstOrDefault(v => v.VersionName == VersionMods.SelectedItem.ToString());
            if (selectedVersionInfo == null) return;

            string targetPath = null;

            if (ModType == "Collection")
            {
                if (CollectionList.SelectedItem is InstalledModpack pack)
                {
                    string folderName = _modDownloadService.GetTargetFolderPath(pack, selectmodificed);

                    targetPath = Path.Combine(pack.Path, "override", folderName);
                    if (!Directory.Exists(targetPath)) targetPath = Path.Combine(pack.Path, "overrides", folderName);
                    if (!Directory.Exists(targetPath))
                    {
                        targetPath = Path.Combine(pack.Path, "override", folderName);
                        Directory.CreateDirectory(targetPath);
                    }
                }
                else
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Modpacks.SelectTargetDesc", "Оберіть збірку!"),
                        LocalizationManager.GetString("Modpacks.SelectTargetTitle", "Куди качати?"),
                        MascotEmotion.Alert);
                    return;
                }
            }
            else
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) targetPath = dialog.SelectedPath;
                    else return;
                }
            }

            DowloadMod.IsEnabled = false;

            try
            {
                await _modDownloadService.DownloadModWithDependenciesAsync(
                    selectedVersionInfo,
                    selectmodificed,
                    targetPath
                );

                if (selectmodificed == 3)
                {
                    string downloadedFile = Path.Combine(targetPath, selectedVersionInfo.FileName);
                    if (File.Exists(downloadedFile) && Path.GetExtension(downloadedFile).ToLower() == ".zip")
                    {
                        await Task.Run(() => ZipHelper.ExtractMap(downloadedFile, targetPath));
                        NotificationService.ShowNotification(
                            LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                            LocalizationManager.GetString("Mods.MapInstalled", "Мапа завантажена та розпакована!"),
                            SnackbarPresenter);
                    }
                }
                else
                {
                    NotificationService.ShowNotification(
                        LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                        string.Format(LocalizationManager.GetString("Mods.FileInstalled", "Файл встановлено у {0}!"), Path.GetFileName(targetPath)),
                        SnackbarPresenter);
                }

                CloseInstallerMenu();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("DownloadManager.ErrorTitle", "Помилка завантаження"), MascotEmotion.Sad);
            }
            finally
            {
                DowloadMod.IsEnabled = true;
            }

        }

        private void OpenModInBrowser(ModSearchResult mod)
        {
            SoundManager.Click();
            string baseUrl = mod.Site == "Modrinth" ? "https://modrinth.com" : "https://www.curseforge.com/minecraft";
            string category = mod.Site == "Modrinth"
                ? (selectmodificed == 1 ? "shader" : selectmodificed == 2 ? "resourcepack" : selectmodificed == 4 ? "datapack" : "mod")
                : (selectmodificed == 1 ? "shaders" : selectmodificed == 2 ? "texture-packs" : selectmodificed == 3 ? "worlds" : selectmodificed == 4 ? "data-packs" : "mc-mods");

            WebHelper.OpenUrl($"{baseUrl}/{category}/{mod.Slug}");
        }

        private void CollectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            VersionMods.Items.Clear();

            if (CollectionList.SelectedItem is not InstalledModpack selectedPack || _currentModVersions == null)
            {
                DowloadMod.IsEnabled = false;
                return;
            }

            string targetVer = selectedPack.MinecraftVersion;
            string targetLoader = selectedPack.LoaderType.ToLower();

            var compatibleVersions = _currentModVersions
                .Where(v => v.GameVersions.Contains(targetVer))
                .ToList();

            if (selectmodificed == 0)
            {
                compatibleVersions = compatibleVersions
                    .Where(v => v.Loaders.Any(l => l.Equals(targetLoader, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (compatibleVersions.Count > 0)
            {
                foreach (var v in compatibleVersions)
                {
                    VersionMods.Items.Add(v.VersionName);
                }
                VersionMods.SelectedIndex = 0;
                DowloadMod.IsEnabled = true;
            }
            else
            {
                string translatedError = string.Format(LocalizationManager.GetString("Modpacks.IncompatibleDesc",
                    "Ех, не вийде. Я перевірила всі файли, але не знайшла версії мода, яка б підійшла для збірки '{0}'.\n\nЗбірка вимагає Minecraft {1} ({2}), а цей мод, схоже, не оновлено під такі параметри."),
                    selectedPack.Name, targetVer, targetLoader);

                MascotMessageBox.Show(translatedError, LocalizationManager.GetString("Modpacks.IncompatibleTitle", "Несумісність"), MascotEmotion.Sad);
                DowloadMod.IsEnabled = false;
            }
        }

        private void Version_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            VersionMods.Items.Clear();
            if (Version.SelectedItem == null || _currentModVersions == null)
            {
                DowloadMod.IsEnabled = false;
                return;
            }

            string selectedGameVersion = Version.SelectedItem.ToString();
            string currentLoader = VersionType.ToLower();

            List<ModVersionInfo> filteredFileVersions;

            if (selectmodificed == 0)
            {
                filteredFileVersions = _currentModVersions
                    .Where(v => v.GameVersions.Contains(selectedGameVersion) &&
                               v.Loaders.Contains(currentLoader))
                    .OrderByDescending(v => v.VersionName)
                    .ToList();
            }
            else
            {
                filteredFileVersions = _currentModVersions
                    .Where(v => v.GameVersions.Contains(selectedGameVersion))
                    .OrderByDescending(v => v.VersionName)
                    .ToList();
            }

            foreach (var fileVersion in filteredFileVersions)
            {
                VersionMods.Items.Add(fileVersion.VersionName);
            }

            DowloadMod.IsEnabled = VersionMods.Items.Count > 0;
            if (VersionMods.Items.Count > 0)
                VersionMods.SelectedIndex = 0;
        }

        private void ModsDowloadList_Loaded(object sender, RoutedEventArgs e)
        {
            if (VirtualizingStackPanel.GetIsVirtualizing(ModsDowloadList) == false)
            {
                VirtualizingStackPanel.SetIsVirtualizing(ModsDowloadList, true);
                VirtualizingStackPanel.SetVirtualizationMode(ModsDowloadList, VirtualizationMode.Recycling);
            }
        }

        private async void OnModCategoryClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && byte.TryParse(element.Tag?.ToString(), out byte categoryId))
            {
                await ChangeModCategory(categoryId, sender);
            }
        }

        private async Task ChangeModCategory(byte categoryId, object senderButton)
        {
            SoundManager.Click();

            _currentCategoryIndex = categoryId;
            selectmodificed = categoryId;

            UpdateIndicatorPosition(animate: true);

            _currentPage = 0;
            if (ModsDowloadList != null) ModsDowloadList.Items.Clear();

            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);
            await UpdateModsMinecraftAsync();
        }

        private void CategoriesGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateIndicatorPosition(animate: false);
        }

        private void UpdateIndicatorPosition(bool animate)
        {
            if (CategoriesGrid == null || PanelSelectNowDowloadModifi == null) return;

            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= CategoriesGrid.Children.Count) return;

            var targetButton = CategoriesGrid.Children[_currentCategoryIndex] as FrameworkElement;
            if (targetButton == null) return;

            Point relativePoint = targetButton.TransformToAncestor(CategoriesGrid).Transform(new Point(0, 0));
            double newX = relativePoint.X;
            double newWidth = targetButton.ActualWidth;

            AnimationService.AnimateTabIndicator(PanelSelectNowDowloadModifi, PanelTranslateTransform6, newX, newWidth, animate);
        }
        private void DowloadModDepOff_On_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            SettingsManager.Default.ModDep = !SettingsManager.Default.ModDep;
            SettingsManager.Save();
            ModDepsToggle.IsChecked = SettingsManager.Default.ModDep;
        }

        private void ImportTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            var dowloadModPack = new DowloadModPack(_modpackService);
            dowloadModPack.Show();
        }

        private void DowloadModPacks_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            AnimationService.AnimatePageTransition(GridFormAccountAdd);
            AnimationService.AnimatePageTransition(SelectCreatePackMinecraft);
        }

        private void SearchSystemModsTXT1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string defaultSearch = LocalizationManager.GetString("Generic.Search", "Пошук...");
            if (SearchSystemModsTXT1.Text == defaultSearch || SearchSystemModsTXT1.Text == "Пошук")
            {
                SearchSystemModsTXT1.Text = "";
            }
        }

        private void SearchSystemModsTXT1_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchSystemModsTXT1.Text.Trim().ToLower();
            string defaultSearch = LocalizationManager.GetString("Generic.Search", "Пошук...").ToLower();

            if (query != defaultSearch && query != "пошук" && !string.IsNullOrEmpty(query))
            {
                var filtered = allInstalledModpacks
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name) && m.Name.ToLower().Contains(query))
                    .ToList();

                UpdateDisplayedModpacks(filtered);
            }
            else
            {
                UpdateDisplayedModpacks(allInstalledModpacks);
            }
        }

        public void UpdateDisplayedModpacks(List<InstalledModpack> modpacks)
        {
            if (ModsDowloadList1 != null)
                ModsDowloadList1.Items.Clear();

            foreach (var value in modpacks)
            {
                var item = LauncherUIFactory.CreateModpackCard(
                    value,
                    OnPlayModpackClicked,
                    OnDeleteModpackClicked,
                    OnOpenModpackFolder,
                    OnEditModpackClicked,
                    OnExportModpackClicked
                );

                ModsDowloadList1.Items.Add(item);
            }
        }

        private void OnPlayModpackClicked(InstalledModpack pack)
        {
            SoundManager.Click();
            _modpackService.PlayModPack(
                pack.MinecraftVersion,
                pack.LoaderVersion,
                pack.LoaderType,
                pack.Name,
                pack.Path,
                pack.PathJson,
                pack.TypeSite,
                pack.JavaPath
            );
        }

        private void OnDeleteModpackClicked(InstalledModpack pack)
        {
            SoundManager.Click();
            if (MascotMessageBox.Ask(
                string.Format(LocalizationManager.GetString("Dialogs.DeleteConfirm", "Видалити {0}?"), pack.Name),
                LocalizationManager.GetString("Dialogs.DeleteConfirmTitle", "Видалення"), MascotEmotion.Alert) == true)
            {
                allInstalledModpacks.Remove(pack);
                _modpackService.DeleteModpack(pack.Name);
                _modpackService.DeleteModpackFolder(pack);
                UpdateDisplayedModpacks(allInstalledModpacks);
            }
        }

        private void OnOpenModpackFolder(InstalledModpack pack)
        {
            SoundManager.Click();
            if (Directory.Exists(pack.Path))
            {
                System.Diagnostics.Process.Start("explorer.exe", pack.Path);
            }
            else
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Dialogs.FolderOpenError", "Папка збірки не знайдена!"),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Confused);
            }
        }

        private void OnEditModpackClicked(InstalledModpack pack)
        {
            SoundManager.Click();

            var editWindow = new CLModPackEdit();

            editWindow.NameWin.Text = $"{LocalizationManager.GetString("Sidebar.Settings", "Налаштування")} {pack.Name}";

            editWindow.PathJsonModPack = pack.PathJson;

            string overridePath = Path.Combine(pack.Path, "override");
            string overridesPath = Path.Combine(pack.Path, "overrides");

            if (Directory.Exists(overridesPath))
                editWindow.PathMods = overridesPath + @"\";
            else if (Directory.Exists(overridePath))
                editWindow.PathMods = overridePath + @"\";
            else
                editWindow.PathMods = pack.Path + @"\";

            var modpackInfo = new ModpackInfo
            {
                Name = pack.Name,
                Path = pack.Path,
                PathJson = pack.PathJson,
                TypeSite = pack.TypeSite,

                LoaderType = pack.LoaderType,
                MinecraftVersion = pack.MinecraftVersion,
                LoaderVersion = pack.LoaderVersion,

                UrlImage = pack.UrlImage,

                ServerIP = pack.ServerIP,
                EnterInServer = pack.EnterInServer,
                Wdith = pack.Wdith,
                Height = pack.Height,
                IsConsoleLogOpened = pack.IsConsoleLogOpened,
                OPack = pack.OPack,
                JavaPath = pack.JavaPath
            };

            editWindow.CurrentModpack = modpackInfo;

            editWindow.ModpackUpdated += () =>
            {
                var valueList = _modpackService.LoadInstalledModpacks();
                allInstalledModpacks = valueList.Where(x => Directory.Exists(x.Path)).ToList();
                UpdateDisplayedModpacks(allInstalledModpacks);
            };

            editWindow.Show();
        }

        private void OnExportModpackClicked(InstalledModpack pack)
        {
            SoundManager.Click();

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"Експорт збірки {pack.Name}",
                FileName = $"{pack.Name}_{DateTime.Now:yyyy-MM-dd}.zip",
                Filter = "Archive (*.zip)|*.zip"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    if (!Directory.Exists(pack.Path))
                    {
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("Dialogs.FolderOpenError", "Папка збірки не знайдена!"),
                            LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                            MascotEmotion.Confused);
                        return;
                    }

                    if (File.Exists(saveDialog.FileName)) File.Delete(saveDialog.FileName);

                    var manifest = new CustomModpackManifest
                    {
                        Name = pack.Name,
                        Author = Environment.UserName,
                        Version = "1.0.0",
                        Minecraft = pack.MinecraftVersion,
                        Loader = pack.LoaderType,
                        LoaderVersion = pack.LoaderVersion,
                        HasCustomIcon = File.Exists(Path.Combine(pack.Path, "icon.png")) ||
                                        File.Exists(Path.Combine(pack.Path, "overrides", "icon.png")) ||
                                        File.Exists(Path.Combine(pack.Path, "override", "icon.png")),
                        Files = new List<ModInfo>()
                    };

                    string existingJsonPath = Path.Combine(pack.Path, "modpack.json");
                    if (File.Exists(existingJsonPath))
                    {
                        try
                        {
                            string oldJson = File.ReadAllText(existingJsonPath);
                            var oldList = JsonConvert.DeserializeObject<List<ModInfo>>(oldJson);
                            if (oldList != null) manifest.Files = oldList;
                        }
                        catch
                        {
                        }
                    }

                    string newJsonContent = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                    File.WriteAllText(existingJsonPath, newJsonContent);

                    System.IO.Compression.ZipFile.CreateFromDirectory(
                        pack.Path,
                        saveDialog.FileName,
                        System.IO.Compression.CompressionLevel.Optimal,
                        true
                    );

                    NotificationService.ShowNotification(
                        LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                        LocalizationManager.GetString("Modpacks.ExportSuccess", "Збірку експортовано з новим маніфестом!"),
                        SnackbarPresenter);
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}", LocalizationManager.GetString("Dialogs.Oops", "Ой"), MascotEmotion.Sad);
                }
            }
        }
        private async void OnModProviderClicked(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (sender is FrameworkElement element && element.Tag is string provider)
            {
                double targetPos = (provider == "Modrinth") ? 0 : 45;

                AnimationService.AnimateVerticalIndicator(PanelSelectNowSiteMods, targetPos);

                SiteMods = provider;
                ModsDowloadList.Items.Clear();
                await UpdateModsMinecraftAsync();
            }
        }

        private async void OnLoaderTypeClicked(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (sender is FrameworkElement element && element.Tag is string loaderType)
            {
                double targetPos = loaderType switch
                {
                    "Fabric" => 0,
                    "Forge" => 45,
                    "Quilt" => 85,
                    "NeoForge" => 130,
                    _ => 0
                };

                AnimationService.AnimateVerticalIndicator(PanelSelectNowVersionType, targetPos);

                VersionType = loaderType;
                ModsDowloadList.Items.Clear();
                await UpdateModsMinecraftAsync();
            }
        }

        private async void ModsDowloadTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            AnimationService.AnimateVerticalIndicator(PanelSelectNowModsType, 0);

            ModType = "Collection";
            ModsDowloadList.Items.Clear();
            VersionVanilBorder.Visibility = Visibility.Hidden;
            CollectionListBorder.Visibility = Visibility.Visible;
            await UpdateModsMinecraftAsync();
        }

        private async void ModsManegerTypeTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            AnimationService.AnimateVerticalIndicator(PanelSelectNowModsType, 45);

            ModType = "Standard";
            ModsDowloadList.Items.Clear();
            VersionVanilBorder.Visibility = Visibility.Visible;
            CollectionListBorder.Visibility = Visibility.Hidden;
            await UpdateModsMinecraftAsync();
        }
        private void GirdModsDowload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            AnimationService.FadeOut(GirdModsDowload, 0.3);
            AnimationService.FadeOut(MenuInstaller, 0.3);

            if (VersionMods != null)
            {
                if (VersionMods.ItemsSource != null)
                    VersionMods.ItemsSource = null;
                else
                    VersionMods.Items.Clear();
            }
            if (Version != null)
            {
                if (Version.ItemsSource != null)
                    Version.ItemsSource = null;
                else
                    Version.Items.Clear();
            }
            if (CollectionList != null)
            {
                if (CollectionList.ItemsSource != null)
                    CollectionList.ItemsSource = null;
                else
                    CollectionList.Items.Clear();
            }
        }
        private void VanilaPackIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            SelectModPackCreate = 0;
            AnimationService.AnimateBorderObject(0, 0, SelectModPack, true);
        }

        private void ModPackIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            SelectModPackCreate = 1;
            AnimationService.AnimateBorderObject(150, 0, SelectModPack, true);
        }

        private void BorderCountionCreatePack_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            AnimationService.AnimatePageTransitionExit(SelectCreatePackMinecraft);
            AnimationService.AnimatePageTransitionExit(GridFormAccountAdd);

            if (SelectModPackCreate == 1)
            {
                var createModPackWindow = new CreateModPackWindow(_modDownloadService, _modpackService);
                createModPackWindow.Show();
            }
            else
            {
                var createVanilaPackWindow = new CreateVanilaPackWindow(_modDownloadService, _modpackService);
                createVanilaPackWindow.Show();
            }
        }
    }
}