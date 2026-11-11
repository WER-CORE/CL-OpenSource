using CL.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class AllMcServerDto
    {
        [JsonProperty("con")]
        [JsonPropertyName("con")]
        public string? Con { get; set; }

        [JsonProperty("ver")]
        [JsonPropertyName("ver")]
        public string? Ver { get; set; }

        [JsonProperty("onl")]
        [JsonPropertyName("onl")]
        public int Onl { get; set; }

        [JsonProperty("url")]
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonProperty("ico")]
        [JsonPropertyName("ico")]
        public string? Ico { get; set; }

        [JsonProperty("web")]
        [JsonPropertyName("web")]
        public string? Web { get; set; }

        [JsonProperty("dis")]
        [JsonPropertyName("dis")]
        public string? Dis { get; set; }
    }

    public class ServerListService
    {
        public async Task<List<ServerInfo>> GetServersAsync(string url, string? searchQuery = null)
        {
            try
            {
                string jsonContent = await WebHelper.Client.GetStringAsync(url).ConfigureAwait(false);
                return ParseServersJson(jsonContent, searchQuery);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ServerListService] Failed to load servers from {url}: {ex.Message}");
                return new List<ServerInfo>();
            }
        }

        public static List<ServerInfo> ParseServersJson(string jsonContent, string? searchQuery = null)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new List<ServerInfo>();
            }

            try
            {
                string trimmed = jsonContent.TrimStart();
                if (!trimmed.StartsWith("["))
                {
                    return new List<ServerInfo>();
                }

                var dtoList = JsonConvert.DeserializeObject<List<AllMcServerDto>>(jsonContent);
                if (dtoList == null || dtoList.Count == 0)
                {
                    return new List<ServerInfo>();
                }

                var result = new List<ServerInfo>();
                foreach (var dto in dtoList)
                {
                    string con = dto.Con?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(con)) continue;

                    string ip = con;
                    int port = 25565;

                    int colonIdx = con.LastIndexOf(':');
                    if (colonIdx > 0 && int.TryParse(con.Substring(colonIdx + 1), out int customPort))
                    {
                        ip = con.Substring(0, colonIdx);
                        port = customPort;
                    }

                    string version = dto.Ver?.Trim() ?? string.Empty;
                    string name = con;

                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        bool matches = name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                       ip.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                       version.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                        if (!matches) continue;
                    }

                    var server = new ServerInfo
                    {
                        Name = name,
                        Ip = ip,
                        Port = port,
                        Version = version,
                        OnlinePlayers = dto.Onl,
                        LogoUrl = EnsureHttps(dto.Ico),
                        SiteLink = EnsureHttps(dto.Web),
                        DiscordLink = EnsureHttps(dto.Dis),
                        AllMcUrl = EnsureHttps(dto.Url)
                    };

                    result.Add(server);
                }

                return result.OrderByDescending(s => s.OnlinePlayers).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ServerListService] Failed to parse JSON: {ex.Message}");
                return new List<ServerInfo>();
            }
        }

        public static string EnsureHttps(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            return "https://" + url;
        }
    }
}
