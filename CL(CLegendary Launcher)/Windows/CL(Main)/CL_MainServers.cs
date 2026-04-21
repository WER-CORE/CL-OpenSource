using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using WpfAnimatedGif;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        private string _currentDiscordUrl;
        private string _currentSiteUrl;
        private string _currentDonateUrl;

        public async Task<PartherItem> CreateServerPartherItemAsync(Dictionary<string, object> serverData)
        {
            return LauncherUIFactory.CreatePartnerServerCard(
                serverData,
                async (version, ip, port) =>
                {
                    await DowloadVanila(version, ip, port, NameNik.Text);
                    string name = serverData.ContainsKey("name") ? serverData["name"].ToString() : "Unknown";
                    await AddLastActionAsync(name, version, ip, port);
                },
                (uiItem, data) =>
                {
                    OpenServerInfoPanel(uiItem.IPServerTXT.Text,
                                        data["version"].ToString(),
                                        Convert.ToInt32(data["port"]),
                                        uiItem.OnlinePlayerTXT.Text,
                                        uiItem.TitleMain1.Text,
                                        data.ContainsKey("description") ? data["description"].ToString() : "",
                                        uiItem.MainIcon3,
                                        data);
                }
            );
        }

        public async Task<MyItemsServer> CreateServerItemAsync(Dictionary<string, object> serverData)
        {
            return LauncherUIFactory.CreateRegularServerCard(
                serverData,
                async (version, ip, port) =>
                {
                    await DowloadVanila(version, ip, port, NameNik.Text);
                    string name = serverData.ContainsKey("name") ? serverData["name"].ToString() : "Unknown";
                    await AddLastActionAsync(name, version, ip, port);
                },
                (uiItem, data) =>
                {
                    string title = uiItem.TitleMain1.Text;
                    OpenServerInfoPanel(uiItem.IPServerTXT.Text,
                                        data["version"].ToString(),
                                        Convert.ToInt32(data["port"]),
                                        uiItem.OnlinePlayerTXT.Text,
                                        title,
                                        data.ContainsKey("description") ? data["description"].ToString() : "",
                                        uiItem.MainIcon3,
                                        data);
                }
            );
        }

        private void OpenServerInfoPanel(string ip, string version, int port, string online, string title,
                                         string description, System.Windows.Controls.Image iconSource,
                                         Dictionary<string, object> data)
        {
            SoundManager.Click();

            _currentDiscordUrl = data.ContainsKey("discord") ? data["discord"]?.ToString() : null;
            _currentSiteUrl = data.ContainsKey("sitelink") ? data["sitelink"]?.ToString() : null;
            _currentDonateUrl = data.ContainsKey("donatelink") ? data["donatelink"]?.ToString() : null;

            AnimationService.AnimatePageTransition(PanelInfoServer);

            IPServerTXT.Text = ip;
            VersionTXT.Text = version;
            PortTXT.Text = port.ToString();
            OnlinePlayerTXT.Text = online;
            TitleMain1.Text = title;
            DescriptionServer.Text = description;

            var animatedSource = ImageBehavior.GetAnimatedSource(iconSource);
            if (animatedSource != null)
                ImageBehavior.SetAnimatedSource(MainIcon3, animatedSource);
            else
                MainIcon3.Source = iconSource.Source;

            this.BG.Source = null;
            this.BG.Visibility = Visibility.Hidden;

            int priority = 0;
            if (data.TryGetValue("priority", out object priorityVal))
            {
                int.TryParse(priorityVal?.ToString(), out priority);
            }

            if (data.TryGetValue("partner", out object partnerVal) && bool.TryParse(partnerVal?.ToString(), out bool isPartner) && isPartner)
            {
                if (priority == 0) priority = 10;
            }

            if (priority > 0 &&
                data.TryGetValue("bgUrl", out object bgUrlValue) &&
                Uri.TryCreate(bgUrlValue?.ToString(), UriKind.Absolute, out Uri bgUri))
            {
                try
                {
                    this.BG.Source = ImageHelper.LoadOptimizedImage(bgUri.ToString(), 200);
                    this.BG.Visibility = Visibility.Visible;
                }
                catch { }
            }
            if (data.ContainsKey("versions") && data["versions"] is JArray versionsArray)
            {
                var versionsList = versionsArray.ToObject<List<string>>();
                if (versionsList != null && versionsList.Count > 1)
                {
                    VersionSelector.ItemsSource = versionsList;
                    VersionSelector.SelectedItem = version;
                    VersionSelector.Visibility = Visibility.Visible;
                }
                else
                {
                    VersionSelector.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                VersionSelector.Visibility = Visibility.Collapsed;
            }
        }
        public void SetupPagination(List<UIElement> sortedCards)
        {
            _allServerCards = new List<UIElement>(sortedCards);
            _filteredServerCards = new List<UIElement>(_allServerCards);
            _currentPage = 1;
            DisplayCurrentPage();
        }
        private void DisplayCurrentPage()
        {
            int totalPages = (int)Math.Ceiling((double)_filteredServerCards.Count / _itemsPerPage_servers);
            if (totalPages == 0) totalPages = 1;

            var pageItems = _filteredServerCards.Skip((_currentPage_server - 1) * _itemsPerPage_servers).Take(_itemsPerPage_servers).ToList();

            ServerList.Items.Clear();
            foreach (var item in pageItems)
            {
                ServerList.Items.Add(item);
            }

            TxtPageInfo.Text = string.Format(
                LocalizationManager.GetString("Servers.PageInfo", "Сторінка {0} з {1}"),
                _currentPage_server,
                totalPages
            );
            BtnPrevPage.IsEnabled = _currentPage_server > 1;
            BtnNextPage.IsEnabled = _currentPage_server < totalPages;

            PaginationPanel.Visibility = _filteredServerCards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (_currentPage_server > 1)
            {
                _currentPage_server--;
                DisplayCurrentPage();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            int totalPages = (int)Math.Ceiling((double)_filteredServerCards.Count / _itemsPerPage_servers);
            if (_currentPage < totalPages)
            {
                _currentPage_server++;
                DisplayCurrentPage();
            }
        }
        private void BackIconServerList_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            AnimationService.AnimateBorderObject(100, 0, FonBackIconServerList, false);

            if (PanelInfoServer.Visibility == Visibility.Visible)
            {
                AnimationService.AnimatePageTransitionExit(PanelInfoServer);
                AnimationService.AnimatePageTransition(ServerName);
            }
            if (GirdTXTNews.Visibility == Visibility.Visible)
            {
                AnimationService.AnimatePageTransitionExit(GirdTXTNews);
                AnimationService.AnimatePageTransition(GirdNews);
            }
        }

        private async void PlayServer_Click(object sender, RoutedEventArgs e)
        {
            string selectedVersion = VersionSelector.Visibility == Visibility.Visible
                ? VersionSelector.SelectedItem?.ToString()
                : VersionTXT.Text;

            string ip = IPServerTXT.Text;
            int port = int.Parse(PortTXT.Text);

            await DowloadVanila(selectedVersion, ip, port, NameNik.Text);
        }

        public void ServerTXTPanelSelect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _navigationService.NavigateToServers();
            MoveMenuSelector(ServersBtnBorder);
        }
        private void SearchSystem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allServerCards == null || _allServerCards.Count == 0) return;

            string query = SearchSystemTXT.Text.ToLower().Trim();

            if (string.IsNullOrEmpty(query))
            {
                _filteredServerCards = new List<UIElement>(_allServerCards);
            }
            else
            {
                _filteredServerCards = _allServerCards.Where(card =>
                {
                    if (card is MyItemsServer ms)
                    {
                        return ms._Title.ToLower().Contains(query);
                    }

                    return false;
                }).ToList();
            }

            _currentPage = 1;
            DisplayCurrentPage();
        }
        public async Task AddLastActionAsync(Dictionary<string, string> action)
        {
            if (SettingsManager.Default.EnableMod_LatestActions) { await _lastActionService.AddLastActionAsync(action); }
        }

        private Task AddLastActionAsync(string name, string version, string ip, int port)
        {
            var action = new Dictionary<string, string>
            {
                ["type"] = "server",
                ["name"] = name ?? "",
                ["version"] = version ?? "",
                ["ip"] = ip ?? "",
                ["port"] = port.ToString()
            };
            return AddLastActionAsync(action);
        }

        private void DiscordLink_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (!string.IsNullOrEmpty(_currentDiscordUrl) && _currentDiscordUrl != "-")
                WebHelper.OpenUrl(_currentDiscordUrl);
            else
                MascotMessageBox.Show(LocalizationManager.GetString("Servers.ServerNoDiscord", "У цього сервера немає Discord каналу."), LocalizationManager.GetString("Dialogs.Oops", "Упс"), MascotEmotion.Confused);
        }

        private void SiteLink_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (!string.IsNullOrEmpty(_currentSiteUrl) && _currentSiteUrl != "-")
                WebHelper.OpenUrl(_currentSiteUrl);
            else
                MascotMessageBox.Show(LocalizationManager.GetString("Servers.ServerNoSite", "Сайт не вказано."), LocalizationManager.GetString("Dialogs.Oops", "Упс"), MascotEmotion.Confused);
        }

        private void DonateLink_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (!string.IsNullOrEmpty(_currentDonateUrl) && _currentDonateUrl != "-")
                WebHelper.OpenUrl(_currentDonateUrl);
            else
                MascotMessageBox.Show(LocalizationManager.GetString("Servers.ServerNoDonate", "Посилання на донат відсутнє."), LocalizationManager.GetString("Dialogs.Oops", "Упс"), MascotEmotion.Confused);
        }

        private void BugReport_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            WebHelper.OpenUrl("https://discord.com/channels/1195118159187939458/1195494058571866172");
        }

        private void TutorialYoutube_Click(object sender, RoutedEventArgs e)
        {
            WebHelper.OpenUrl("https://wiki.cl-launcher.app/wiki/%D0%93%D0%BE%D0%BB%D0%BE%D0%B2%D0%BD%D0%B0_%D1%81%D1%82%D0%BE%D1%80%D1%96%D0%BD%D0%BA%D0%B0");
        }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            WebHelper.OpenUrl("https://github.com/WER-CORE/CL-OpenSource");
        }

        private void Support3OSHBr_Click(object sender, RoutedEventArgs e)
        {
            AnimationService.AnimatePageTransition(FundraiserOverlay);
            LoadFundraiserAsync();
        }

        private void SupportProject_Click(object sender, RoutedEventArgs e)
        {
            string supportUrl = "https://send.monobank.ua/jar/APZdKjTvuT";

            try
            {
                Process.Start(new ProcessStartInfo(supportUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не вдалося відкрити посилання підтримки: {ex.Message}");
            }
        }

        private async void LoadFundraiserAsync()
        {
            FundLoader.Visibility = Visibility.Visible;
            FundraisersList.ItemsSource = null;
            NoActiveFundsTXT.Visibility = Visibility.Collapsed;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    string json = await client.GetStringAsync(Secrets.FundraiserURL);

                    var dataList = JsonConvert.DeserializeObject<List<FundraiserData3rd>>(json);

                    var activeFunds = dataList?.Where(d => d.isActive).ToList();

                    if (activeFunds != null && activeFunds.Count > 0)
                    {
                        foreach (var fund in activeFunds)
                        {
                            if (!string.IsNullOrEmpty(fund.imageUrl))
                            {
                                try
                                {
                                    fund.ImageBitmap = ImageHelper.LoadOptimizedImage(fund.imageUrl, 86);
                                }
                                catch { }
                            }
                        }

                        FundraisersList.ItemsSource = activeFunds;
                    }
                    else
                    {
                        NoActiveFundsTXT.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                NoActiveFundsTXT.Text = LocalizationManager.GetString("Servers.FundNetworkError", "Помилка мережі або бази зборів.");
                NoActiveFundsTXT.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                FundLoader.Visibility = Visibility.Collapsed;
            }
        }
        private void PartnerServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PartnerServer.Items.Count > 1)
            {
                Dispatcher.InvokeAsync(() => AnimateSlide(), DispatcherPriority.Loaded);
            }
        }

        private async void NextIndex()
        {
            if (PartnerServer.Items.Count < 2) return;

            int next = (PartnerServer.SelectedIndex + 1) % PartnerServer.Items.Count;
            PartnerServer.SelectedIndex = next;

            await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: false);
        }

        private void AnimateSlide()
        {
            if (PartnerServer.SelectedItem == null) return;

            int newIndex = PartnerServer.SelectedIndex;

            if (_lastIndex != -1 && _lastIndex != newIndex)
            {
                var oldItem = PartnerServer.ItemContainerGenerator.ContainerFromIndex(_lastIndex) as ListBoxItem;
                if (oldItem != null)
                {
                    AnimateItem(oldItem, 0, -300);
                }
            }

            PartnerServer.UpdateLayout();

            var newItem = PartnerServer.ItemContainerGenerator.ContainerFromIndex(newIndex) as ListBoxItem;
            if (newItem != null)
            {
                AnimateItem(newItem, 300, 0);
            }

            _lastIndex = newIndex;
        }

        private void AnimateItem(UIElement item, double from, double to)
        {
            var tt = new TranslateTransform();
            item.RenderTransform = tt;

            var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(0.5))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            tt.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void PartnerServer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _slideTimer.Stop();
        }

        private void PartnerServer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _slideTimer.Start();
        }
        private void BtnFund_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void CloseFundraiser_Click(object sender, RoutedEventArgs e) => AnimationService.AnimatePageTransitionExit(FundraiserOverlay);

        private void FundraiserOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == FundraiserOverlay) FundraiserOverlay.Visibility = Visibility.Collapsed;
        }

        private void FundraiserCard_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private async void NewsUpdateLauncher_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            await HideAllPages();

            AnimationService.AnimatePageTransition(GirdNews);
            AnimationService.AnimatePageTransition(ListNews);

            if (ListNews.Items.Count > 0) return;

            try
            {
                if (NewsLoader != null) NewsLoader.Visibility = Visibility.Visible;

                ListNews.Items.Clear();

                var newsItems = await _newsService.GetNewsAsync();

                if (newsItems != null && newsItems.Any())
                {
                    foreach (var item in newsItems)
                    {
                        var uiControl = LauncherUIFactory.CreateNewsControl(item, OnNewsItemClicked);
                        ListNews.Items.Add(uiControl);
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Servers.NewsLoadError", "Не вдалося завантажити новини.\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Новини загубилися"),
                    MascotEmotion.Sad
                );
            }
            finally
            {
                if (NewsLoader != null) NewsLoader.Visibility = Visibility.Collapsed;
            }
        }

        private void OnNewsItemClicked(NewsItem item)
        {
            SoundManager.Click();
            AnimationService.AnimateBorderObject(-120, 0, FonBackIconServerList, true);
            AnimationService.AnimatePageTransitionExit(GirdNews);
            AnimationService.AnimatePageTransition(GirdTXTNews);

            TextNews.Text = item.Description;
        }

        public void AddActionToList(Dictionary<string, string> action)
        {
            try
            {
                var item = LauncherUIFactory.CreateHistoryItem(
                    action,

                    (ver, ip, port) => DowloadVanila(ver, ip, port, NameNik.Text),

                    (ver, modVer) => DownloadVersionOptifine(ver, modVer),

                    (ver) => DowloadVanila(ver, null, null, NameNik.Text)
                );

                if (ServerMonitoring != null)
                {
                    ServerMonitoring.Items.Insert(0, item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding history item: {ex.Message}");
            }
        }

        private void TabRegularServer_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void TabModdedServer_MouseDown(object sender, MouseButtonEventArgs e) { }
    }
}