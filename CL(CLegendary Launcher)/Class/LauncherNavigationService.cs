using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CL_CLegendary_Launcher_.Class
{
    public class LauncherNavigationService
    {
        private readonly CL_Main_ _main;
        private bool _isNavigating = false;
        private FrameworkElement _currentPage;

        public LauncherNavigationService(CL_Main_ main)
        {
            _main = main;
        }

        private async Task NavigateToPage(FrameworkElement targetPage, Border targetButton, string discordStatus, Func<Task> loadDataAction = null)
        {
            if (_isNavigating || targetPage.Visibility == Visibility.Visible) return;

            try
            {
                _isNavigating = true;
                _currentPage = targetPage;

                SoundManager.Click();

                if (!string.IsNullOrEmpty(discordStatus))
                    _ = DiscordController.UpdatePresence(discordStatus);

                AnimationService.AnimateMenuSelector(targetButton, _main.SelectPanelGrid, _main.PanelSelectNow, _main.PanelTranslateTransform);

                await _main.HideAllPages();
                AnimationService.AnimatePageTransition(targetPage);

                if (targetPage == _main.GirdPanelFooter)
                {
                    AnimationService.AnimatePageTransition(_main.SelectGirdAccount);
                }

                await Task.Delay(250);
                _isNavigating = false;

                if (loadDataAction != null)
                {
                    await loadDataAction();
                }
            }
            catch
            {
                _isNavigating = false;
            }
        }

        public async void NavigateToHome()
        {
            await NavigateToPage(_main.GirdPanelFooter, _main.PlayBtnBorder, LocalizationManager.GetString("DiscordRPC.InHome", "В головному вікні"), async () =>
            {
                if (_main.PartnerServer.Items.Count == 0 && !SettingsManager.Default.OfflineModLauncher)
                {
                    if (_currentPage == _main.GirdPanelFooter)
                        await _main._serverListService.InitializeServersAsync(false);
                }
            });
        }

        public async void NavigateToMods()
        {
            await NavigateToPage(_main.ListModsGird, _main.ModsBtnBorder, LocalizationManager.GetString("DiscordRPC.SearchingMods", "Шукає моди"), async () =>
            {
                if (_main.ModsDowloadList.Items.Count == 0)
                {
                    if (_currentPage == _main.ListModsGird)
                        await _main.UpdateModsMinecraftAsync();
                }
            });
        }

        public async void NavigateToModPacks()
        {
            await NavigateToPage(_main.ListModsBuild, _main.ModpacksBtnBorder, LocalizationManager.GetString("DiscordRPC.ViewingModpacks", "Дивиться збірки"), async () =>
            {
                if (_currentPage == _main.ListModsBuild)
                {
                    var valueList = _main._modpackService.LoadInstalledModpacks();
                    var installedPacks = valueList.Where(x => Directory.Exists(x.Path)).ToList();
                    _main.UpdateDisplayedModpacks(installedPacks);
                    await Task.CompletedTask;
                }
            });
        }

        public async void NavigateToServers()
        {
            await NavigateToPage(_main.ServerName, _main.ServersBtnBorder, LocalizationManager.GetString("DiscordRPC.ViewingServers", "Дивиться список серверів"), async () =>
            {
                if (_main.ServerList.Items.Count == 0)
                {
                    if (_currentPage == _main.ServerName)
                        await _main._serverListService.InitializeServersAsync(true, null);
                }
            });
        }

        public async void NavigateToGallery()
        {
            await NavigateToPage(_main.GalleryContainer, _main.GalleryBtnBorder, LocalizationManager.GetString("DiscordRPC.ViewingGallery", "Переглядає галерею"), () =>
            {
                if (_currentPage == _main.GalleryContainer)
                    _main.InitializeGallery();
                return Task.CompletedTask;
            });
        }
    }
}