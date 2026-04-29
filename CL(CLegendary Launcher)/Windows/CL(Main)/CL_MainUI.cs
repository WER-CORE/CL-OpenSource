using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        private string currentScreenshotsPath;
        internal int serverCount;
        internal int loadedCount;

        private async void CL_CLegendary_Launcher__Loaded_1(object sender, RoutedEventArgs e)
        {
            if (!SettingsManager.Default.TutorialComplete) { AnimationService.AnimatePageTransition(TutorialGrid); }
            else { TutorialGrid.Visibility = Visibility.Collapsed; }

            if (SettingsManager.Default.width != 0 && SettingsManager.Default.height != 0)
            {
                Width.Text = SettingsManager.Default.width.ToString();
                Height.Text = SettingsManager.Default.height.ToString();
                MincraftWindowSize.Content = $"{SettingsManager.Default.width}x{SettingsManager.Default.height}";
            }
            else
            {
                SettingsManager.Default.width = 800;
                SettingsManager.Default.height = 600;
                SettingsManager.Save();
                Width.Text = "800";
                Height.Text = "600";
                MincraftWindowSize.Content = "800x600";
            }

            int savedType = SettingsManager.Default.LastSelectedType;
            string savedVer = SettingsManager.Default.LastSelectedVersion;
            string savedModVer = SettingsManager.Default.LastSelectedModVersion;

            if (savedType != 0 && !string.IsNullOrEmpty(savedVer))
            {
                VersionSelect = (byte)savedType;

                if (savedType == 5 && !string.IsNullOrEmpty(savedModVer))
                {
                    IconSelectVersion.Source = IconSelectVersion_Optifine.Source;
                    PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), savedModVer);
                }
                else if (savedType == 1)
                {
                    IconSelectVersion.Source = IconSelectVersion_Копировать.Source;
                    PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), savedVer);
                }
            }
            else
            {
                PlayTXT.Text = LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ");
            }

            LoadCustomSettings();
            this.Dispatcher.Invoke(() =>
            {
                MemoryCleaner.FlushMemoryAsync(true);
            });
        }
        private void MenuButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element)
            {
                AnimationService.AnimateScale(element, 1.1);
            }
        }
        private void MenuButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element)
            {
                AnimationService.AnimateScale(element, 1.0);
            }
        }
        private void PanelFooterScalePlus_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element)
            {
                AnimationService.AnimateScale(element, 1.03);
            }
        }
        private void PanelFooterScaleMinus_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element)
            {
                AnimationService.AnimateScale(element, 1.0);
            }
        }
        private void CL_CLegendary_Launcher__Closed(object sender, EventArgs e)
        {
            DiscordController.Deinitialize();
        }

        private void CL_CLegendary_Launcher__Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
        private void BackMainWindow_MouseDown(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            AnimationService.AnimatePageTransitionExit(PanelInfoServer);
        }
        public async Task HideAllPages()
        {
            var allPages = new List<FrameworkElement>
            {
                SelectGirdAccount, ScrollSetting, SettingPanelMinecraft, PanelManegerAccount,
                ServerName, ListModsGird, ListModsBuild, GalleryContainer,
                GirdPanelFooter, GirdNews, ListNews, GirdTXTNews
            };

            bool animationStarted = false;

            foreach (var page in allPages)
            {
                if (page.Visibility == Visibility.Visible)
                {
                    AnimationService.AnimatePageTransitionExit(page);
                    animationStarted = true;
                }
            }

            ListNews.Items?.Clear();
            TextNews.Text = null;
            ScreenshotsList.Items?.Clear();
            ModsDowloadList1.Items?.Clear();
            ModsDowloadList.Items?.Clear();

            ServerList.Items?.Clear();

            DescriptionServer.Text = null;

            if (animationStarted)
            {
                await Task.Delay(300);
            }
        }

        private void PlayTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToHome();
            MoveMenuSelector(PlayBtnBorder);
        }

        private void ModsTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToMods();
            MoveMenuSelector(ModsBtnBorder);
        }

        private void modbuilds_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToModPacks();
            MoveMenuSelector(ModpacksBtnBorder);
        }

        private void PhotoMinecraftTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToGallery();
            MoveMenuSelector(GalleryBtnBorder);
        }
        private void MoveMenuSelector(Border targetButton)
        {
            AnimationService.AnimateMenuSelector(targetButton, SelectPanelGrid, PanelSelectNow, PanelTranslateTransform);
        }
        private async void SettingPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            await HideAllPages();
            await Task.Delay(200);

            AnimationService.AnimatePageTransition(SettingPanelMinecraft, 0.3);
            AnimationService.AnimatePageTransition(ScrollSetting, 0.2);
        }

        private void FolderPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!SettingsManager.Default.EnableSubFiles && !SettingsManager.Default.EnableSubFiles_Backups)
            {
                return;
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is Grid grid && grid.ContextMenu != null)
                {
                    grid.ContextMenu.PlacementTarget = grid;
                    grid.ContextMenu.IsOpen = !grid.ContextMenu.IsOpen;
                }
            }
        }

        private string GetBaseMinecraftPath()
        {
            return LauncherFloderButton.Content.ToString();
        }

        private void OpenOrCreateFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                Process.Start(new ProcessStartInfo("explorer.exe", path));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не вдалося відкрити папку {path}. Помилка: {ex.Message}");
            }
        }

        private void OpenRootFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenOrCreateFolder(AppDomain.CurrentDomain.BaseDirectory);
        }

        private void OpenMinecraftFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenOrCreateFolder(GetBaseMinecraftPath());
        }

        private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(GetBaseMinecraftPath(), "mods");
            OpenOrCreateFolder(path);
        }

        private void OpenResourcePacksFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(GetBaseMinecraftPath(), "resourcepacks");
            OpenOrCreateFolder(path);
        }

        private void OpenShaderPacksFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(GetBaseMinecraftPath(), "shaderpacks");
            OpenOrCreateFolder(path);
        }

        private void OpenCLModpackFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(GetBaseMinecraftPath(), "CLModpack");
            OpenOrCreateFolder(path);
        }

        private void InfoPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!SettingsManager.Default.EnableSubInfo_Bug &&
        !SettingsManager.Default.EnableSubInfo_News &&
        !SettingsManager.Default.EnableSubInfo_Wiki &&
        !SettingsManager.Default.EnableSubInfo_Github &&
        !SettingsManager.Default.EnableSubInfo_Credits &&
        !SettingsManager.Default.EnableSubInfo_Support)
            {
                return;
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is Grid grid && grid.ContextMenu != null)
                {
                    grid.ContextMenu.PlacementTarget = grid;
                    grid.ContextMenu.IsOpen = !grid.ContextMenu.IsOpen;
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = SettingsManager.Default.PathLacunher;

                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", path);
                }
                else
                {
                    Directory.CreateDirectory(path);
                    Process.Start("explorer.exe", path);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.FolderOpenError", "Не вдалося відкрити папку:\n{0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            }
        }

        private void OpenGlobalBackups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var managerWindow = new WorldBackupWindow();
                managerWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.FolderOpenError", "Не вдалося відкрити список світів:\n{0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            }
        }

        private void BackMainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (ServerList.Visibility == Visibility.Visible || PanelInfoServer.Visibility == Visibility.Visible)
            {
                BackMainWindow.Visibility = Visibility.Hidden;
                SearchSystemTXT.Visibility = Visibility.Hidden;
                AnimationService.FadeOut(PanelInfoServer, 0.4);
                AnimationService.FadeOut(ServerList, 0.3);
            }
        }

        private void YesQuestionTutorialButton_MouseDown(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.TutorialComplete = true;
            SettingsManager.Default.IsDocsTutorialShown = true;
            WebHelper.OpenUrl("https://github.com/WER-CORE/CL-OpenSource");
            SettingsManager.Save();
            AnimationService.AnimatePageTransitionExit(TutorialBorder);
            AnimationService.AnimatePageTransitionExit(TutorialGrid);
        }

        private void NoQuestionTutorialButton_MouseDown(object sender, RoutedEventArgs e)
        {
            SettingsManager.Default.TutorialComplete = true;
            SettingsManager.Save();

            AnimationService.AnimatePageTransitionExit(TutorialBorder);
            AnimationService.AnimatePageTransitionExit(TutorialGrid);

            _tutorialService.ShowTutorial(InfoLauncherPanel, null, -120);
        }

        private void CloseTutorial_Click(object sender, RoutedEventArgs e)
        {
            _tutorialService.CloseTutorial(() =>
            {
                SettingsManager.Default.IsDocsTutorialShown = true;
                SettingsManager.Save();
            });
        }

        private async void LoadScreenshots()
        {
            ScreenshotsList.Items.Clear();
            NoScreenshotsText.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(currentScreenshotsPath)) return;

            var items = await _screenshotService.LoadScreenshotsAsync(currentScreenshotsPath);

            if (items.Count > 0)
            {
                NoScreenshotsText.Visibility = Visibility.Hidden;
                foreach (var item in items)
                {
                    ScreenshotsList.Items.Add(item);
                }
            }
        }

        public void InitializeGallery()
        {
            var sources = _screenshotService.GetScreenshotSources(SettingsManager.Default.PathLacunher);

            SourceSelector.ItemsSource = sources;
            SourceSelector.DisplayMemberPath = "Name";

            if (sources.Count > 0)
                SourceSelector.SelectedIndex = 0;
        }

        private void SourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();
            if (SourceSelector.SelectedItem is ScreenshotSourceItem selectedSource)
            {
                currentScreenshotsPath = selectedSource.FullScreenshotsPath;
                LoadScreenshots();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (!string.IsNullOrEmpty(currentScreenshotsPath))
            {
                if (!Directory.Exists(currentScreenshotsPath)) Directory.CreateDirectory(currentScreenshotsPath);
                System.Diagnostics.Process.Start("explorer.exe", currentScreenshotsPath);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            LoadScreenshots();
        }

        private void ScreenshotsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundManager.Click();
            if (ScreenshotsList.SelectedItem != null)
            {
                AnimationService.AnimatePageTransition(ActionPanel);
                var item = (ScreenshotItem)ScreenshotsList.SelectedItem;
                SelectedFileNameText.Text = item.FileName;
            }
            else
            {
                AnimationService.AnimatePageTransitionExit(ActionPanel);
            }
        }

        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (ScreenshotsList.SelectedItem is ScreenshotItem item)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (ScreenshotsList.SelectedItem is ScreenshotItem item)
            {
                if (_screenshotService.DeleteScreenshot(item.FilePath))
                {
                    ScreenshotsList.Items.Remove(item);

                    if (ScreenshotsList.Items.Count == 0)
                        NoScreenshotsText.Visibility = Visibility.Visible;

                    AnimationService.AnimatePageTransitionExit(ActionPanel);
                }
                else
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Dialogs.DeleteError", "Не вдалося видалити файл."),
                        LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
                }
            }
        }
    }
}