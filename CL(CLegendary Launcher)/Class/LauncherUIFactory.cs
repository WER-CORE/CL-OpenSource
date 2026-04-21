using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using MCQuery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace CL_CLegendary_Launcher_.Class
{
    public static class LauncherUIFactory
    {
        private static readonly BitmapImage _defaultAvatar;
        static LauncherUIFactory()
        {
            try
            {
                _defaultAvatar = ImageHelper.LoadOptimizedImage("pack://application:,,,/Assets/big-steve-face-2002298922 2.png", 32);
            }
            catch
            {
                _defaultAvatar = null;
            }
        }
        public static TextBlock CreateHistoryItem(
        Dictionary<string, string> action,
        Action<string, string, int> onPlayServer,
        Action<string, string> onPlayOptifine,
        Action<string> onPlayVanilla
        )
        {
            string type = action.ContainsKey("type") ? action["type"] : "other";
            string name = action.ContainsKey("name") ? action["name"] : LocalizationManager.GetString("Servers.UnknownServer", "Unknown");
            string version = action.ContainsKey("version") ? action["version"] : "";
            string ip = action.ContainsKey("ip") ? action["ip"] : "";
            string portStr = action.ContainsKey("port") ? action["port"] : "25565";
            string loader = action.ContainsKey("loader") ? action["loader"] : "vanilla";
            string loaderVersion = action.ContainsKey("loaderVersion") ? action["loaderVersion"] : "";

            string displayText;
            if (type == "server")
            {
                string serverStr = LocalizationManager.GetString("MainScreen.HistoryServer", "Сервер");
                displayText = $"{name} : {version} ({loader}{(string.IsNullOrEmpty(loaderVersion) ? "" : " " + loaderVersion)}) : {serverStr}";
            }
            else if (type == "version")
            {
                string versionStr = LocalizationManager.GetString("MainScreen.HistoryVersion", "Версія");
                displayText = $"{name} : {version} ({loader}{(string.IsNullOrEmpty(loaderVersion) ? "" : " " + loaderVersion)}) : {versionStr}";
            }
            else
            {
                displayText = $"{name} : {type}";
            }

            var textBlock = new TextBlock
            {
                Text = displayText,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            textBlock.MouseLeftButtonDown += (s, e) =>
            {
                if (type == "server")
                {
                    int.TryParse(portStr, out int parsedPort);
                    onPlayServer(version, ip, parsedPort);
                }
                else if (type == "version")
                {
                    if (loader == "optifine")
                    {
                        onPlayOptifine(version, loaderVersion);
                    }
                    else if (loader == "vanilla")
                    {
                        onPlayVanilla(version);
                    }
                }
            };

            return textBlock;
        }
        public static PartherItem CreatePartnerServerCard(
          Dictionary<string, object> serverData,
          Action<string, string, int> onPlayClick,
          Action<PartherItem, Dictionary<string, object>> onInfoClick)
        {
            var serverName = serverData.ContainsKey("name") ? serverData["name"].ToString() : LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер");
            int port = serverData.ContainsKey("port") ? Convert.ToInt32(serverData["port"]) : 25565;
            string ip = serverData.ContainsKey("ip") ? serverData["ip"].ToString() : "";
            string version = serverData.ContainsKey("version") ? serverData["version"].ToString() : "";
            string type = serverData.ContainsKey("type") ? serverData["type"].ToString() : "";

            int priority = 0;
            if (serverData.TryGetValue("priority", out object priorityVal)) int.TryParse(priorityVal?.ToString(), out priority);
            else if (serverData.TryGetValue("partner", out object partnerVal) && bool.Parse(partnerVal.ToString())) priority = 10;

            bool isNeon = serverData.ContainsKey("neonEffect") && bool.Parse(serverData["neonEffect"].ToString());
            string neonColorHex = serverData.ContainsKey("borderColor") ? serverData["borderColor"].ToString() : "#FFFFFF";
            string textColorHex = serverData.ContainsKey("textColor") ? serverData["textColor"].ToString() : null;

            var item = new PartherItem
            {
                _Title = serverName,
                _description = string.Format(LocalizationManager.GetString("Servers.TypeVersionFormat", "Тип: {0}\nВерсія: {1}"), type, version),
                IPServerTXT = { Text = ip },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            item.PlayServerTXT1.MouseDown += (s, e) => onPlayClick(version, ip, port);
            item.OpenInfoServerTXT.MouseDown += (s, e) => onInfoClick(item, serverData);

            Task.Run(() =>
            {
                try
                {
                    var serverStatus = new MCServer(ip, port);
                    var status = serverStatus.Status(500);
                    Application.Current.Dispatcher.Invoke(() => item.OnlinePlayerTXT.Text = $"{status.Players.Online}/{status.Players.Max}");
                }
                catch
                {
                    Application.Current.Dispatcher.Invoke(() => item.OnlinePlayerTXT.Text = "-");
                }
            });

            if (serverData.TryGetValue("logoUrl", out object logoValue) &&
                Uri.TryCreate(logoValue?.ToString(), UriKind.Absolute, out Uri logoUri))
            {
                int decodeSize = (priority > 100) ? 0 : 64;

                var imageSource = ImageHelper.LoadOptimizedImage(logoUri.ToString(), decodeSize);

                if (imageSource != null)
                {
                    if (priority > 100)
                    {
                        ImageBehavior.SetAnimatedSource(item.MainIcon3, imageSource);
                    }
                    else
                    {
                        item.MainIcon3.Source = imageSource;
                    }
                }
            }
            ApplyServerStyles(item, serverData, priority, isNeon, neonColorHex, textColorHex);
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            item.RenderTransform = new ScaleTransform(1.0, 1.0);
            item.MouseEnter += (s, e) => AnimationService.AnimateScale(item, 1.02, 0.15);
            item.MouseLeave += (s, e) => AnimationService.AnimateScale(item, 1.0, 0.15);

            return item;
        }

        private static void IPServerTXT_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            throw new NotImplementedException();
        }

        public static MyItemsServer CreateRegularServerCard(
          Dictionary<string, object> serverData,
          Action<string, string, int> onPlayClick,
          Action<MyItemsServer, Dictionary<string, object>> onInfoClick
        )
        {
            var serverName = serverData.ContainsKey("name") ? serverData["name"].ToString() : LocalizationManager.GetString("Servers.UnknownServer", "Невідомий сервер");
            int port = serverData.ContainsKey("port") ? Convert.ToInt32(serverData["port"]) : 25565;
            string ip = serverData.ContainsKey("ip") ? serverData["ip"].ToString() : "";
            string version = serverData.ContainsKey("version") ? serverData["version"].ToString() : "";
            string type = serverData.ContainsKey("type") ? serverData["type"].ToString() : "";

            bool partner = false;
            if (serverData.TryGetValue("partner", out object partnerValue)) bool.TryParse(partnerValue?.ToString(), out partner);
            int priority = 0;
            if (serverData.TryGetValue("priority", out object priorityVal)) int.TryParse(priorityVal?.ToString(), out priority);
            else if (partner) priority = 10;

            string borderColorHex = serverData.ContainsKey("borderColor") ? serverData["borderColor"].ToString() : "#FFFFFF";
            string textColorHex = serverData.ContainsKey("textColor") ? serverData["textColor"].ToString() : null;
            bool isNeon = serverData.TryGetValue("neonEffect", out object neonValue) && bool.Parse(neonValue?.ToString());

            var item = new MyItemsServer
            {
                _Title = serverName,
                Description_ = string.Format(LocalizationManager.GetString("Servers.TypeVersionFormat", "Тип: {0}\nВерсія: {1}"), type, version),
                IPServerTXT = { Text = ip },
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            item.PlayServerTXT1.MouseDown += (s, e) => onPlayClick(version, ip, port);
            item.OpenInfoServerTXT.MouseDown += (s, e) => onInfoClick(item, serverData);

            Task.Run(() =>
            {
                try
                {
                    var serverStatus = new MCServer(ip, port);
                    var status = serverStatus.Status(500);
                    Application.Current.Dispatcher.Invoke(() => item.OnlinePlayerTXT.Text = $"{status.Players.Online}/{status.Players.Max}");
                }
                catch
                {
                    Application.Current.Dispatcher.Invoke(() => item.OnlinePlayerTXT.Text = "-");
                }
            });

            if (serverData.TryGetValue("logoUrl", out object logoValue) &&
                Uri.TryCreate(logoValue?.ToString(), UriKind.Absolute, out Uri logoUri))
            {
                string urlStr = logoUri.ToString();
                bool isGif = urlStr.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

                int decodeSize = isGif ? 0 : 128;

                var imageSource = ImageHelper.LoadOptimizedImage(urlStr, decodeSize);

                if (imageSource != null)
                {
                    if (isGif)
                    {
                        ImageBehavior.SetAnimatedSource(item.MainIcon3, imageSource);
                    }
                    else
                    {
                        ImageBehavior.SetAnimatedSource(item.MainIcon3, null);
                        item.MainIcon3.Source = imageSource;
                    }
                }
            }

            ApplyServerStyles(item, serverData, priority, isNeon, borderColorHex, textColorHex);
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            item.RenderTransform = new ScaleTransform(1.0, 1.0);
            item.MouseEnter += (s, e) => AnimationService.AnimateScale(item, 1.02, 0.15);
            item.MouseLeave += (s, e) => AnimationService.AnimateScale(item, 1.0, 0.15);

            return item;
        }

        private static void ApplyServerStyles(dynamic item, Dictionary<string, object> serverData, int priority, bool isNeon, string borderColorHex, string textColorHex)
        {
            bool imageLoaded = false;

            if (priority > 0 && serverData.TryGetValue("bgUrl", out object bgUrlValue))
            {
                string url = bgUrlValue?.ToString();

                var bgImage = ImageHelper.LoadOptimizedImage(url, 250);

                if (bgImage != null)
                {
                    item.BGParther.Background = new ImageBrush(bgImage) { Stretch = Stretch.UniformToFill };
                    imageLoaded = true;
                }
            }

            if (!imageLoaded)
            {
                if (priority > 0)
                {
                    item.Background = new LinearGradientBrush(Colors.Gold, Colors.Orange, 45);
                }
                else
                {
                    item.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                }
            }

            item.BorderThickness = new Thickness(0);
            item.BorderBrush = Brushes.Transparent;
            item.Margin = new Thickness(priority > 0 ? 0 : 5);

            try
            {
                if (priority >= 100 && isNeon && !string.IsNullOrEmpty(borderColorHex))
                {
                    if (!borderColorHex.StartsWith("#")) borderColorHex = "#" + borderColorHex;

                    try
                    {
                        var glowColor = (Color)ColorConverter.ConvertFromString(borderColorHex);

                        if (item is PartherItem) item.ClipToBounds = true;
                        else item.ClipToBounds = false;

                        if (item.BGParther is Border border)
                        {
                            border.BorderThickness = new Thickness(2);
                            border.BorderBrush = new SolidColorBrush(glowColor);

                            if (item is MyItemsServer)
                            {
                                border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                                {
                                    Color = glowColor,
                                    Direction = 0,
                                    ShadowDepth = 0,
                                    BlurRadius = 15,
                                    Opacity = 0.6
                                };
                            }
                            else
                            {
                                border.Effect = null;
                            }
                        }
                    }
                    catch { }
                }

                if (priority >= 20 && !string.IsNullOrEmpty(textColorHex))
                {
                    if (!textColorHex.StartsWith("#")) textColorHex = "#" + textColorHex;
                    try
                    {
                        var textColor = (Color)ColorConverter.ConvertFromString(textColorHex);
                        item.TitleMain1.Foreground = new SolidColorBrush(textColor);
                    }
                    catch { }
                }
            }
            catch { }
        }
        public static ItemJar CreateModCard(ModSearchResult mod, Action<ModSearchResult> onDownloadClick, Action<ModSearchResult> onOpenUrlClick)
        {
            var item = new ItemJar
            {
                ModTitle = mod.Title,
                ModDescription = mod.Description,
                Author = mod.Author,
                DownloadCount = FormatNumber(mod.Downloads),
                LastUpdateDate = mod.UpdatedDate,

                UrlMods = mod.Slug,
                TypeSite = mod.Site,
                ProjectId = mod.ModId,
                ModId = (mod.Site == "CurseForge" && int.TryParse(mod.ModId, out int id)) ? id : 0,
                FileId = mod.CF_FileId
            };

            if (!string.IsNullOrEmpty(mod.IconUrl))
            {
                try
                {
                    item.ModImage = ImageHelper.LoadOptimizedImage(mod.IconUrl, 42);
                }
                catch { }
            }

            item.DowloadTXT.PreviewMouseDown += (s, e) => onDownloadClick(mod);

            item.DetailsMod.PreviewMouseDown += (s, e) => onOpenUrlClick(mod);
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            item.RenderTransform = new ScaleTransform(1.0, 1.0);
            item.MouseEnter += (s, e) => AnimationService.AnimateScale(item, 1.02, 0.15);
            item.MouseLeave += (s, e) => AnimationService.AnimateScale(item, 1.0, 0.15);

            return item;
        }
        public static ItemModPack CreateModpackCard(
            InstalledModpack pack,
            Action<InstalledModpack> onPlay,
            Action<InstalledModpack> onDelete,
            Action<InstalledModpack> onOpenFolder,
            Action<InstalledModpack> onEdit,
            Action<InstalledModpack> onShare)
        {
            var item = new ItemModPack();
            item.NameModPack.Text = pack.Name;
            item.DescriptionModPack.Text = $"{pack.LoaderType} {pack.MinecraftVersion} : {pack.LoaderVersion}";

            if (!string.IsNullOrEmpty(pack.UrlImage))
            {
                try
                {
                    Uri imageUri;
                    if (pack.UrlImage.StartsWith("http") || pack.UrlImage.StartsWith("pack://"))
                    {
                        imageUri = new Uri(pack.UrlImage);
                    }
                    else
                    {
                        imageUri = new Uri(pack.UrlImage, UriKind.Absolute);
                    }
                    item.IconModPack.Source = ImageHelper.LoadOptimizedImage(imageUri.ToString(), 64);
                }
                catch
                {
                }
            }

            item.BorderPlay.MouseLeftButtonUp += (s, e) => onPlay(pack);
            item.DeleteTXT.MouseLeftButtonUp += (s, e) => onDelete(pack);
            item.FloderPack.MouseLeftButtonUp += (s, e) => onOpenFolder(pack);
            item.EditModPackTXT.MouseLeftButtonUp += (s, e) => onEdit(pack);
            item.ShareBorder.MouseLeftButtonUp += (s, e) => onShare(pack);

            item.Cursor = System.Windows.Input.Cursors.Hand;
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            item.RenderTransform = new ScaleTransform(1.0, 1.0);
            item.MouseEnter += (s, e) => AnimationService.AnimateScale(item, 1.02, 0.15);
            item.MouseLeave += (s, e) => AnimationService.AnimateScale(item, 1.0, 0.15);

            return item;
        }
        public static string FormatNumber(object numObj)
        {
            if (numObj == null) return "0";

            if (numObj is string strVal)
            {
                strVal = strVal.Replace(" ", "").Replace(",", "").Replace(".", "").Trim();
                if (long.TryParse(strVal, out long parsed))
                    return FormatNumber(parsed);
                return strVal;
            }

            if (numObj is long || numObj is int || numObj is double)
            {
                long num = Convert.ToInt64(numObj);
                if (num >= 1_000_000) return (num / 1_000_000D).ToString("0.#") + "m";
                if (num >= 1_000) return (num / 1_000D).ToString("0.#") + "k";
                return num.ToString();
            }

            return numObj.ToString();
        }
        public static ItemManegerProfile CreateAccountControl(ProfileItem profile, int index, Action<ProfileItem> onDelete, Action<ProfileItem> onSelect)
        {
            var item = new ItemManegerProfile
            {
                NameAccount = { Text = profile.NameAccount },
                NameAccount2 = profile.NameAccount,
                UUID = profile.UUID,
                index = index,
                TypeAccount = (ItemManegerProfile.AccountType)profile.TypeAccount,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            item.IconAccountType.Source = _defaultAvatar;

            if (!string.IsNullOrEmpty(profile.ImageUrl))
            {
                try
                {
                    item.IconAccountType.Source = ImageHelper.LoadOptimizedImage(profile.ImageUrl, 32);
                }
                catch
                {
                }
            }

            item.DeleteAccount.MouseLeftButtonUp += (s, e) => onDelete(profile);

            item.ClickSelectAccount.MouseLeftButtonUp += (s, e) => onSelect(profile);

            return item;
        }
        public static ItemNews CreateNewsControl(NewsItem data, Action<NewsItem> onClick)
        {
            var item = new ItemNews();
            item.TitleUpdate.Text = data.Title;
            item.description = data.Description;

            if (!string.IsNullOrEmpty(data.IconUrl))
            {
                try
                {
                    item.ImageNews.Source = ImageHelper.LoadOptimizedImage(data.IconUrl,200);
                }
                catch
                {
                    item.ImageNews.Source = null;
                }
            }

            item.MouseLeftButtonUp += (s, e) => onClick(data);

            item.Cursor = System.Windows.Input.Cursors.Hand;
            item.RenderTransformOrigin = new Point(0.5, 0.5);
            item.RenderTransform = new ScaleTransform(1.0, 1.0);
            item.MouseEnter += (s, e) => AnimationService.AnimateScale(item, 1.02, 0.15);
            item.MouseLeave += (s, e) => AnimationService.AnimateScale(item, 1.0, 0.15);

            return item;
        }
    }
}