using CL.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class ServerListService
    {
        public async Task<List<ServerInfo>> GetServersAsync(string url, string searchQuery = null)
        {
            try
            {
                using var client = new HttpClient();
                string jsonContent = await client.GetStringAsync(url);
                var serversData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(jsonContent);

                if (serversData != null && serversData.ContainsKey("serverstest"))
                {
                    var serversList = serversData["serverstest"];
                    var result = new List<ServerInfo>();

                    foreach (var serverEntry in serversList)
                    {
                        var data = serverEntry.Value as JObject;
                        if (data == null)
                        {
                            var dict = serverEntry.Value as Dictionary<string, object>;
                            if (dict != null) data = JObject.FromObject(dict);
                        }

                        if (data == null) continue;

                        string name = data.ContainsKey("name") ? data["name"].ToString() : "Unknown";
                        
                        // Apply search filter
                        if (!string.IsNullOrEmpty(searchQuery) && !name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        bool isPartner = false;
                        if (data.TryGetValue("partner", out var partnerValue)) bool.TryParse(partnerValue?.ToString(), out isPartner);

                        int priority = 0;
                        if (data.TryGetValue("priority", out var priorityVal)) int.TryParse(priorityVal?.ToString(), out priority);
                        else if (isPartner) priority = 10;

                        var serverInfo = new ServerInfo
                        {
                            Name = name,
                            Ip = data.ContainsKey("ip") ? data["ip"].ToString() : "",
                            Port = data.ContainsKey("port") ? Convert.ToInt32(data["port"]) : 25565,
                            Version = data.ContainsKey("version") ? data["version"].ToString() : "",
                            Type = data.ContainsKey("type") ? data["type"].ToString() : "",
                            IsPartner = isPartner,
                            Priority = priority,
                            BorderColorHex = data.ContainsKey("borderColor") ? data["borderColor"].ToString() : "#FFFFFF",
                            TextColorHex = data.ContainsKey("textColor") ? data["textColor"].ToString() : null,
                            NeonEffect = data.TryGetValue("neonEffect", out var neonValue) && bool.TryParse(neonValue?.ToString(), out var neon) ? neon : false,
                            DiscordLink = data.ContainsKey("discord") ? data["discord"].ToString() : "",
                            DonateLink = data.ContainsKey("donatelink") ? data["donatelink"].ToString() : "",
                            SiteLink = data.ContainsKey("sitelink") ? data["sitelink"].ToString() : "",
                            Description = data.ContainsKey("description") ? data["description"].ToString() : "",
                            LogoUrl = data.ContainsKey("logoUrl") ? data["logoUrl"].ToString() : "",
                            BgUrl = data.ContainsKey("bgUrl") ? data["bgUrl"].ToString() : "",
                            VersionsSupported = data.ContainsKey("versions") ? (data["versions"] as Newtonsoft.Json.Linq.JArray)?.ToObject<List<string>>() : null
                        };

                        result.Add(serverInfo);
                    }

                    return result;
                }
            }
            catch
            {
                // We let the caller handle error display
                throw;
            }

            return new List<ServerInfo>();
        }
    }
}
