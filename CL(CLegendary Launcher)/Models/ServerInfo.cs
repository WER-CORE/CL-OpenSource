using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CL_CLegendary_Launcher_.Class
{
    public class ServerInfo
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("version")] public string Version { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("ip")] public string Ip { get; set; }
        [JsonProperty("port")] public int Port { get; set; }
        [JsonProperty("priority")] public int Priority { get; set; }
        [JsonProperty("partner")] public bool IsPartner { get; set; }

        [JsonProperty("discord")] public string DiscordLink { get; set; }
        [JsonProperty("donatelink")] public string DonateLink { get; set; }
        [JsonProperty("sitelink")] public string SiteLink { get; set; }

        [JsonProperty("bgUrl")] public string BgUrl { get; set; }
        [JsonProperty("logoUrl")] public string LogoUrl { get; set; }
        [JsonProperty("borderColor")] public string BorderColor { get; set; }
        [JsonProperty("textColor")] public string TextColor { get; set; }
        [JsonProperty("neonEffect")] public bool NeonEffect { get; set; }

        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}