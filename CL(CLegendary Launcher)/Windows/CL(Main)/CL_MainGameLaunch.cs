using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        public bool isMouseClickSelection = false;

        async void AddVersion()
        {
            try
            {
                string search = SearchSystemTXT1.Text.ToLower().Trim();
                VersionList.Items.Clear();

                bool isOffline = SettingsManager.Default.OfflineModLauncher || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

                if (isOffline)
                {
                    var localVersions = GetLocalVersions(search);

                    var vanillaLocals = localVersions.Where(v =>
                        !v.ToLower().Contains("forge") &&
                        !v.ToLower().Contains("fabric") &&
                        !v.ToLower().Contains("quilt") &&
                        !v.ToLower().Contains("optifine")).ToList();

                    if (vanillaLocals.Count > 0)
                    {
                        vanillaLocals.ForEach(v => VersionList.Items.Add(v));
                    }
                    else
                    {
                        VersionList.Items.Add(LocalizationManager.GetString("GameLaunch.OfflineNoVersions", "В офлайні немає завантажених версій"));
                    }
                    SearchSystemTXT1.ItemsSource = string.IsNullOrEmpty(search) ? null : vanillaLocals;
                }
                else
                {
                    var list = await _versionService.GetFilteredVersionsAsync(
                        search, Relesed.IsChecked == true, Snapshots.IsChecked == true,
                        Beta.IsChecked == true, Alpha.IsChecked == true);

                    list.ForEach(v => VersionList.Items.Add(v));
                    SearchSystemTXT1.ItemsSource = string.IsNullOrEmpty(search) ? null : list;
                }
            }
            catch (Exception) { }
        }

        public async Task AddVersionOptifine()
        {
            try
            {
                VersionListVanila.Items.Clear();
                string searchText = SearchSystemTXT2.Text.ToLower().Trim();

                bool isOffline = SettingsManager.Default.OfflineModLauncher || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

                if (isOffline)
                {
                    var localVersions = GetLocalVersions(searchText);

                    var optifineLocals = localVersions.Where(v => v.ToLower().Contains("optifine")).ToList();

                    if (optifineLocals.Count > 0)
                    {
                        foreach (var v in optifineLocals) VersionListVanila.Items.Add(v);
                    }
                    else
                    {
                        VersionListVanila.Items.Add(LocalizationManager.GetString("GameLaunch.OptifineNoVersions", "Завантажених Optifine немає"));
                    }
                    SearchSystemTXT2.ItemsSource = string.IsNullOrEmpty(searchText) ? null : optifineLocals;
                }
                else
                {
                    var versions = await _versionService.GetFilteredVersionsAsync(searchText, true, false, false, false);
                    var filteredVersions = versions.Where(v => v != "1.6.4").ToArray();

                    foreach (var v in filteredVersions) VersionListVanila.Items.Add(v);
                    SearchSystemTXT2.ItemsSource = string.IsNullOrEmpty(searchText) ? null : filteredVersions;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    $"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}",
                    LocalizationManager.GetString("GameLaunch.OptifineErrorTitle", "Помилка Optifine"),
                    MascotEmotion.Sad);
            }
        }

        private List<string> GetLocalVersions(string keyword = null)
        {
            var localVersions = new List<string>();
            var path = new CmlLib.Core.MinecraftPath(SettingsManager.Default.PathLacunher);

            if (System.IO.Directory.Exists(path.Versions))
            {
                var dirs = System.IO.Directory.GetDirectories(path.Versions);
                foreach (var dir in dirs)
                {
                    string verName = System.IO.Path.GetFileName(dir);

                    if (string.IsNullOrEmpty(keyword) || verName.ToLower().Contains(keyword.ToLower()))
                    {
                        localVersions.Add(verName);
                    }
                }
            }
            return localVersions;
        }

        private async void AddVersionModeVersion()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            var selectedVersion = VersionListVanila.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedVersion)) return;

            try
            {
                VersionListMod.Items.Clear();

                bool isOffline = SettingsManager.Default.OfflineModLauncher || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

                if (isOffline || selectedVersion.ToLower().Contains("optifine"))
                {
                    VersionListMod.Items.Add(LocalizationManager.GetString("GameLaunch.LocalBuiltIn", "Вбудовано (Локально)"));
                    VersionListMod.SelectedIndex = 0;
                    return;
                }

                var modVersions = await _versionService.GetLoaderVersionsAsync(VersionSelect, selectedVersion);

                if (token.IsCancellationRequested) return;

                if (modVersions.Count > 0)
                    foreach (var v in modVersions) VersionListMod.Items.Add(v);
                else
                    VersionListMod.Items.Add(LocalizationManager.GetString("GameLaunch.NoAvailableVersions", "Немає доступних версій:("));
            }
            catch (Exception ex)
            {
                VersionListMod.Items.Add($"{LocalizationManager.GetString("Dialogs.Error", "Помилка")}: {ex.Message}");
            }
        }

        private async void DownloadVersionOptifine(string effectiveVersion = null, string effectiveVersionMod = null)
        {
            string mcVersion = !string.IsNullOrEmpty(effectiveVersion)
                ? effectiveVersion
                : VersionListVanila.SelectedItem?.ToString();

            string optifineVersion = !string.IsNullOrEmpty(effectiveVersionMod)
                ? effectiveVersionMod
                : VersionListMod.SelectedItem?.ToString();

            bool isOfflinePrepacked = mcVersion != null && mcVersion.ToLower().Contains("optifine");

            if (string.IsNullOrEmpty(mcVersion) || (!isOfflinePrepacked && string.IsNullOrEmpty(optifineVersion)))
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("GameLaunch.OptifineNotSelectedDesc", "Секундочку! А що саме ми запускаємо?\nТи забув обрати версію Minecraft та OptiFine у списках.\n\nТицьни на потрібні версії, і погнали!"),
                    LocalizationManager.GetString("GameLaunch.NoVersionSelectedTitle", "Версія не обрана"),
                    MascotEmotion.Alert
                );
                return;
            }

            SettingsManager.Default.LastSelectedVersion = mcVersion;
            SettingsManager.Default.LastSelectedModVersion = optifineVersion ?? "";
            SettingsManager.Default.LastSelectedType = 5;
            SettingsManager.Save();

            await _gameLaunchService.LaunchGameAsync(LoaderType.Optifine, mcVersion, optifineVersion);
        }

        async Task DowloadVanila(string version, string server, int? serverport, string username)
        {
            string effectiveVersion = version ?? VersionRelesedVanilLast.Text.ToString();

            SettingsManager.Default.LastSelectedModVersion = null;
            SettingsManager.Default.LastSelectedVersion = effectiveVersion;
            SettingsManager.Default.LastSelectedType = 1;
            SettingsManager.Save();

            await _gameLaunchService.LaunchGameAsync(LoaderType.Vanilla, effectiveVersion, null, server, serverport);
        }

        private async void PlayTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (NameNik.Text == LocalizationManager.GetString("Accounts.NoAccount", "Відсутній акаунт") || NameNik.Text == "Відсутній акаунт" || NameNik.Text == "No account")
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Агов! Будь ласка, оберіть акаунт перед початком гри!"),
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Акаунт не обраний"),
                    MascotEmotion.Alert);
                return;
            }

            if (BorderButton != null)
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 30, 30, 30), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 50, 50, 50), 1));
                BorderButton.Background = brush;
                Line.Background = new SolidColorBrush(Color.FromArgb(255, 10, 60, 10));
            }

            if (!InstallVersionOnPlay)
            {
                if (VersionSelect == 0)
                {
                    VersionSelect = (byte)SettingsManager.Default.LastSelectedType;
                }

                if (VersionSelect == 0)
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("GameLaunch.NoVersionSelectedDesc", "Агов! Будь ласка, оберіть версію перед початком гри!"),
                        LocalizationManager.GetString("GameLaunch.NoVersionSelectedTitle", "Версія не обрана"),
                        MascotEmotion.Alert);
                    await Task.Delay(200);
                    RestoreButtonColor();
                    return;
                }

                string ver = SettingsManager.Default.LastSelectedVersion;
                string modVer = SettingsManager.Default.LastSelectedModVersion;

                switch (VersionSelect)
                {
                    case 1:
                        await DowloadVanila(ver, null, null, NameNik.Text);
                        break;

                    case 5:
                        DownloadVersionOptifine(ver, modVer);
                        break;
                }

                AnimationService.FadeOut(SelectVersion, 0.3);
                AnimationService.FadeOut(SelectVersionMod, 0.3);
                AnimationService.FadeOut(SelectVersionTypeGird, 0.3);
            }

            await Task.Delay(200);
            RestoreButtonColor();
        }

        private void RestoreButtonColor()
        {
            if (BorderButton != null)
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 23, 218, 31), 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 24, 166, 30), 1));
                BorderButton.Background = brush;
                Line.Background = new SolidColorBrush(Color.FromArgb(255, 22, 149, 27));
            }
        }

        public void ShowGameLog(Process process)
        {
            GameLog gameLog = new GameLog();
            gameLog.Show();

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += gameLog.MinecraftProcess_OutputDataReceived;
            process.ErrorDataReceived += gameLog.MinecraftProcess_ErrorDataReceived;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private async void LoadChangeLogMinecraft()
        {
            if (SettingsManager.Default.OfflineModLauncher) { return; }

            try
            {
                var logs = await _versionService.GetChangeLogAsync();

                VersionMinecraftChangeLog.Items.Clear();

                foreach (var item in logs)
                {
                    VersionMinecraftChangeLog.Items.Add(item);
                }

                if (VersionMinecraftChangeLog.Items.Count > 0)
                    VersionMinecraftChangeLog.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("MainScreen.ChangelogErrorDesc", "Вибач, але я не змогла дістати свіжий випуск новин (Changelog).\nМожливо, інтернет зник або сервери Mojang втомилися відповідати.\n\nОсь що трапилося: {0}"), ex.Message),
                    LocalizationManager.GetString("MainScreen.ChangelogErrorTitle", "Немає новин"),
                    MascotEmotion.Sad
                );
            }
        }

        public void VersionMinecraftSelectLog()
        {
            if (VersionMinecraftChangeLog == null || !(VersionMinecraftChangeLog.SelectedItem is VersionLogItem selectedItem))
                return;

            string query;
            string cleanId = selectedItem.VersionId;

            switch (selectedItem.VersionType)
            {
                case "old_alpha": query = $"Java Edition Alpha {cleanId.Replace("a", "")}"; break;
                case "old_beta": query = $"Java Edition Beta {cleanId.Replace("b", "")}"; break;
                case "snapshot": query = $"{cleanId} Java Edition"; break;
                case "release": default: query = $"Java Edition {cleanId}"; break;
            }
            string searchUrl = $"https://uk.minecraft.wiki/w/Special:Search?search={Uri.EscapeDataString(query)}&go=Go";

            try
            {
                WebHelper.OpenUrl(searchUrl);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.UrlOpenErrorDesc", "Не вдалося відкрити браузер.\n{0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.UrlOpenErrorTitle", "Помилка браузера"),
                    MascotEmotion.Sad);
            }
        }

        private void VersionMinecraftChangeLog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isMouseClickSelection) return;
            VersionMinecraftSelectLog();
            isMouseClickSelection = false;
        }

        private void VersionMinecraftChangeLog_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            isMouseClickSelection = true;
        }

        private void SearchSystem1_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!this.IsLoaded) return;
            AddVersion();
        }

        private void SearchSystemTXT1_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (!this.IsLoaded) return;
            string selectedVersion = args.SelectedItem.ToString();
            sender.Text = selectedVersion;

            foreach (var item in VersionList.Items)
            {
                if (item.ToString() == selectedVersion)
                {
                    VersionList.ScrollIntoView(item);
                    break;
                }
            }
        }

        private void SearchSystemTXT2_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!this.IsLoaded) return;
            if (VersionSelect == 5) AddVersionOptifine();
        }

        private void SearchSystemTXT2_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (!this.IsLoaded) return;
            string selectedVersion = args.SelectedItem.ToString();
            sender.Text = selectedVersion;

            foreach (var item in VersionListVanila.Items)
            {
                if (item.ToString() == selectedVersion)
                {
                    VersionListVanila.ScrollIntoView(item);
                    break;
                }
            }
        }

        private async void CheckMarkVersionSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            if (!InstallVersionOnPlay)
            {
                if (SelectVersionTypeGird.Visibility == Visibility.Visible)
                {
                    IconVersionRotateTransform.Angle = 180;
                    AnimationService.AnimatePageTransitionExit(SelectVersion, 20);
                    AnimationService.AnimatePageTransitionExit(SelectVersionTypeGird, 20);
                    AnimationService.AnimatePageTransitionExit(SelectVersionMod, 20);

                    int savedType = SettingsManager.Default.LastSelectedType;
                    string savedVer = SettingsManager.Default.LastSelectedVersion;
                    string savedModVer = SettingsManager.Default.LastSelectedModVersion;

                    if (savedType != 0 && !string.IsNullOrEmpty(savedVer))
                    {
                        VersionSelect = (byte)savedType;
                        if (savedType == 5 && !string.IsNullOrEmpty(savedModVer))
                            PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), savedModVer);
                        else
                            PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), savedVer);
                    }
                    else
                    {
                        PlayTXT.Text = LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ");
                        VersionSelect = 0;
                    }
                }
                else
                {
                    IconVersionRotateTransform.Angle = 0;
                    AnimationService.AnimatePageTransition(SelectVersionTypeGird);

                    var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                    var launcher = new MinecraftLauncher(path);
                    var versions = await launcher.GetAllVersionsAsync();

                    VersionRelesedVanilLast.Text = versions.LatestReleaseName;
                    VersionRelesedVanilLast5.Text = versions.LatestReleaseName;
                }
            }
        }

        private void SelectVersionVanila_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            VersionList.Items.Clear();
            AddVersion();
            AnimationService.AnimatePageTransition(SelectVersion); AnimationService.FadeIn(SelectVersionVanila, 0.2); AnimationService.FadeOut(SelectVersionMod, 0.2);
            VersionSelect = 1;
            IconSelectVersion.Source = IconSelectVersion_Копировать.Source;
        }

        private void SelectVersionOptifine_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlayTXT.Text = LocalizationManager.GetString("GameLaunch.PlayBtnPlay", "ГРАТИ");
            VersionListMod.Items.Clear();
            Click();
            AddVersionOptifine();
            AnimationService.AnimatePageTransition(SelectVersionMod); AnimationService.FadeIn(SelectVersionOptifine, 0.2); AnimationService.FadeOut(SelectVersion, 0.2);
            VersionSelect = 5;
            IconSelectVersion.Source = IconSelectVersion_Optifine.Source;
        }

        private async void SelectVersionRedirect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Click();
            SelectVersionTypeGird.Visibility = Visibility.Hidden;

            _navigationService.NavigateToModPacks();

            await Task.Delay(1000);

            if (CreateModPacksTXT.IsVisible && CreateModPacksTXT.IsLoaded)
            {
                _tutorialService.ShowTutorial(
                    CreateModPacksTXT,
                    LocalizationManager.GetString("Mods.WhereIsForgeTitle", "Де Forge? Та інші лоудери."),
                    LocalizationManager.GetString("Mods.WhereIsForgeDesc", "Ми використовуємо професійну систему!\n\nМодифіковані версії (Forge, Fabric, ...) створюються як окремі 'Збірки'. Це захищає ваші моди від конфліктів і крашів.\n\nНатисніть кнопку 'Створити', щоб встановити Forge або Fabric та інші."),
                    null,
                    -120
                );
            }
        }

        private void VersionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Click();
            if (VersionList.SelectedItem != null)
            {
                string selectedVer = VersionList.SelectedItem.ToString();
                VersionRelesedVanilLast.Text = selectedVer;
                PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), selectedVer);

                SettingsManager.Default.LastSelectedVersion = selectedVer;
                SettingsManager.Default.LastSelectedType = VersionSelect;
                SettingsManager.Save();
            }
        }

        private void VersionListVanila_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionListVanila.SelectedItem != null)
            {
                string selectedVer = VersionListVanila.SelectedItem.ToString();

                if (VersionSelect == 2)
                {
                    SettingsManager.Default.LastSelectedVersion = selectedVer;
                    SettingsManager.Default.LastSelectedType = 2;
                    SettingsManager.Save();
                    PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), selectedVer);
                }

                if (VersionSelect == 5)
                {
                    VersionRelesedVanilLast5.Text = selectedVer;
                }

                AddVersionModeVersion();
            }
        }

        private void VersionListMod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionListVanila.SelectedItem != null && VersionSelect == 5)
            {
                if (VersionListMod.SelectedItem != null)
                {
                    string mcVer = VersionListVanila.SelectedItem.ToString();
                    string modVer = VersionListMod.SelectedItem.ToString();

                    if (mcVer.ToLower().Contains("optifine"))
                        PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), mcVer);
                    else
                        PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), modVer);

                    SettingsManager.Default.LastSelectedVersion = mcVer;
                    SettingsManager.Default.LastSelectedModVersion = modVer;
                    SettingsManager.Default.LastSelectedType = 5;
                    SettingsManager.Save();
                }
            }
        }

        private void SelectVersionVanila_MouseEnter(object sender, MouseEventArgs e) => ToggleBorderColor(SelectVersionVanila, true);
        private void SelectVersionVanila_MouseLeave(object sender, MouseEventArgs e) => ToggleBorderColor(SelectVersionVanila, false);
        private void SelectVersionOptifine_MouseEnter(object sender, MouseEventArgs e) => ToggleBorderColor(SelectVersionOptifine, true);
        private void SelectVersionOptifine_MouseLeave(object sender, MouseEventArgs e) => ToggleBorderColor(SelectVersionOptifine, false);

        private void ToggleBorderColor(Border border, bool isEnter)
        {
            if (ThemeService.currentTheme == "Dark")
                border.BorderBrush = new SolidColorBrush(isEnter ? Color.FromRgb(255, 255, 255) : Color.FromRgb(0, 0, 0));
            else
                border.BorderBrush = new SolidColorBrush(isEnter ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255));
        }

        private void Relesed_MouseDown_1(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Alpha_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Infdev_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Indev_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Classic_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Pre_classic_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Beta_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
        private void Snapshots_Click(object sender, RoutedEventArgs e) { if (VersionSelect == 1) AddVersion(); }
    }
}