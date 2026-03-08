using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Http;
using CL_CLegendary_Launcher_.Windows;
using System.IO;

namespace CL_CLegendary_Launcher_.Class
{
    public class ServerListService
    {
        private readonly CL_Main_ _main;
        private List<(MyItemsServer Item, int Priority)> _tempSortedList = new List<(MyItemsServer, int)>();
        private object _listLock = new object();

        public ServerListService(CL_Main_ mainWindow)
        {
            _main = mainWindow;
        }

        private async Task<Dictionary<string, Dictionary<string, object>>> LoadServersFromWebAsync(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "CL-Launcher");
                    string jsonContent = await client.GetStringAsync(url);

                    return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(jsonContent);
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Servers.FetchErrorDesc", "Ой леле! Не вдалося отримати список серверів.\nМожливо, інтернет зник або посилання застаріло?\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Servers.FetchErrorTitle", "Збій мережі"),
                    MascotEmotion.Sad);
                return null;
            }
        }

        public async Task InitializeServersAsync(bool IsServerTab, string searchQuery = null)
        {
            _main.Dispatcher.Invoke(() =>
            {
                if (IsServerTab)
                    _main.ServerList.Items.Clear();
                else
                    _main.PartnerServer.Items.Clear();

                _main.ServerSearchLoader.Visibility = Visibility.Visible;

                _main.discordLink.Clear();
                _main.donateLink.Clear();
                _main.siteLink.Clear();
            });

            var serversData = await LoadServersFromWebAsync(Secrets._serversUrl);

            if (serversData != null && serversData.ContainsKey("serverstest"))
            {
                var serversList = serversData["serverstest"];

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    serversList = serversList
                        .Where(serverEntry =>
                        {
                            if (serverEntry.Value is JObject sObj && sObj.ContainsKey("name"))
                                return sObj["name"].ToString().Contains(searchQuery, StringComparison.OrdinalIgnoreCase);

                            if (serverEntry.Value is Dictionary<string, object> sDict && sDict.ContainsKey("name"))
                                return sDict["name"].ToString().Contains(searchQuery, StringComparison.OrdinalIgnoreCase);

                            return false;
                        })
                        .ToDictionary(entry => entry.Key, entry => entry.Value);
                }

                _main.serverCount = serversList.Count;
                _main.loadedCount = 0;

                if (_main.serverCount > 0)
                {
                    foreach (var serverEntry in serversList)
                    {
                        await LoadAndDisplayServerAsync(IsServerTab, serverEntry.Value);
                    }
                }
                else
                {
                    _main.ServerSearchLoader.Visibility = Visibility.Collapsed;
                    NotificationService.ShowNotification(
                        LocalizationManager.GetString("Servers.EmptyTitle", "Пусто"),
                        LocalizationManager.GetString("Servers.EmptyDesc", "Хм, я нічого не знайшла за твоїм запитом. Спробуй змінити пошук."),
                        _main.SnackbarPresenter, 3);
                }
            }
            else
            {
                _main.ServerSearchLoader.Visibility = Visibility.Collapsed;
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Servers.FormatErrorDesc", "Дивина! Список серверів порожній або має неправильний формат."),
                    LocalizationManager.GetString("Servers.FormatErrorTitle", "Помилка даних"),
                    MascotEmotion.Confused);
            }


            _main.Dispatcher.Invoke(() =>
            {
                _main.ServerSearchLoader.Visibility = Visibility.Collapsed;
            });
        }

        private async Task LoadAndDisplayServerAsync(bool IsServerTab, object serverDataObject)
        {
            Dictionary<string, object> serverData = null;

            if (serverDataObject is JObject jObject)
                serverData = jObject.ToObject<Dictionary<string, object>>();
            else if (serverDataObject is Dictionary<string, object> dict)
                serverData = dict;
            else
                return;

            if (serverData == null) return;

            bool isPartner = false;
            if (serverData.TryGetValue("partner", out object partnerValue))
                bool.TryParse(partnerValue?.ToString(), out isPartner);

            int priority = 0;
            if (serverData.TryGetValue("priority", out object priorityVal))
                int.TryParse(priorityVal?.ToString(), out priority);
            else if (isPartner)
                priority = 10;

            _main.discordLink.Add(serverData.ContainsKey("discord") ? serverData["discord"]?.ToString() : "-");
            _main.donateLink.Add(serverData.ContainsKey("donatelink") ? serverData["donatelink"]?.ToString() : "-");
            _main.siteLink.Add(serverData.ContainsKey("sitelink") ? serverData["sitelink"]?.ToString() : "-");

            if (isPartner && !IsServerTab)
            {
                var partnerItem = await _main.CreateServerPartherItemAsync(serverData);
                _main.Dispatcher.Invoke(() => _main.PartnerServer.Items.Add(partnerItem));
            }

            if (IsServerTab)
            {
                var item = await _main.CreateServerItemAsync(serverData);

                lock (_listLock)
                {
                    _tempSortedList.Add((item, priority));
                }
            }

            _main.loadedCount++;

            _main.Dispatcher.Invoke(() =>
            {
                if (_main.loadedCount >= _main.serverCount)
                {
                    if (IsServerTab)
                    {
                        var sortedItems = _tempSortedList
                                            .OrderByDescending(x => x.Priority)
                                            .Select(x => x.Item)
                                            .ToList();

                        _main.ServerList.Items.Clear();
                        foreach (var serverItem in sortedItems)
                        {
                            _main.ServerList.Items.Add(serverItem);
                        }

                        _tempSortedList.Clear();
                    }
                }
            });
        }
    }
}