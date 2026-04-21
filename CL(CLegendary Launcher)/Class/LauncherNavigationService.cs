using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;

namespace CL_CLegendary_Launcher_.Class
{
    public class LauncherNavigationService
    {
        private readonly CL_Main_ _main;
        private bool _isNavigating = false;

        public LauncherNavigationService(CL_Main_ main)
        {
            _main = main;
        }
        private async Task NavigateToPage(FrameworkElement targetPage, double indicatorPosition, string discordStatus, Func<Task> loadDataAction = null)
        {
            if (_isNavigating || targetPage.Visibility == Visibility.Visible) return;

            try
            {
                _isNavigating = true;

                SoundManager.Click();

                if (!string.IsNullOrEmpty(discordStatus))
                    await DiscordController.UpdatePresence(discordStatus);

                AnimationService.AnimateBorder(indicatorPosition, 0, _main.PanelSelectNow);

                await _main.HideAllPages();
                AnimationService.AnimatePageTransition(targetPage);

                if (targetPage == _main.GirdPanelFooter)
                {
                    AnimationService.AnimatePageTransition(_main.SelectGirdAccount);
                }

                if (loadDataAction != null)
                {
                    await loadDataAction();
                }
            }
            finally
            {
                await Task.Delay(200);
                _isNavigating = false;
            }
        }

        public async void NavigateToHome()
        {
            await NavigateToPage(_main.GirdPanelFooter, 0, LocalizationManager.GetString("DiscordRPC.InHome", "В головному вікні"), async () =>
            {
                if (_main.PartnerServer.Items.Count == 0 && !SettingsManager.Default.OfflineModLauncher)
                {
                    await _main._serverListService.InitializeServersAsync(false);
                }
            });
        }

        public async void NavigateToMods()
        {
            await NavigateToPage(_main.ListModsGird, 190, LocalizationManager.GetString("DiscordRPC.SearchingMods", "Шукає моди"), async () =>
            {
                if (_main.ModsDowloadList.Items.Count == 0)
                {
                    await _main.UpdateModsMinecraftAsync();
                }
            });
        }

        public async void NavigateToModPacks()
        {
            await NavigateToPage(_main.ListModsBuild, 95, LocalizationManager.GetString("DiscordRPC.ViewingModpacks", "Дивиться збірки"), async () =>
            {
                var valueList = _main._modpackService.LoadInstalledModpacks();
                var installedPacks = valueList.Where(x => Directory.Exists(x.Path)).ToList();
                _main.UpdateDisplayedModpacks(installedPacks);

                await Task.CompletedTask;
            });
        }

        public async void NavigateToServers()
        {
            await NavigateToPage(_main.ServerName, 285, LocalizationManager.GetString("DiscordRPC.ViewingServers", "Дивиться список серверів"), async () =>
            {
                if (_main.ServerList.Items.Count == 0)
                {
                    await _main._serverListService.InitializeServersAsync(true, null);
                }
            });
        }

        public async void NavigateToGallery()
        {
            await NavigateToPage(_main.GalleryContainer, 385, LocalizationManager.GetString("DiscordRPC.ViewingGallery", "Переглядає галерею"), () =>
            {
                _main.InitializeGallery();
                return Task.CompletedTask;
            });
        }

    }
}