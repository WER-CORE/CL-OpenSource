using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CL_CLegendary_Launcher_.Class
{
    public class OmniVersion
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public string Category { get; set; }
    }

    public class OmniArchiveService
    {
        private readonly HttpClient _httpClient;

        public static Dictionary<string, string> OmniDownloadLinks = new Dictionary<string, string>();
        public OmniArchiveService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CLegendary-Launcher/1.0");
        }

        public async Task<List<OmniVersion>> GetOmniVersionsAsync()
        {
            try
            {
                OmniDownloadLinks.Clear();

                string json = await _httpClient.GetStringAsync(Secrets.ManifestUrlOmni);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                List<OmniVersion> versions = JsonSerializer.Deserialize<List<OmniVersion>>(json, options);

                if (versions != null)
                {
                    foreach (var v in versions)
                    {
                        if (!string.IsNullOrEmpty(v.Name) && !OmniDownloadLinks.ContainsKey(v.Name))
                        {
                            OmniDownloadLinks[v.Name] = v.DownloadUrl;
                        }
                    }
                    return versions;
                }

                return new List<OmniVersion>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка завантаження маніфесту OmniArchive: {ex.Message}");
                return new List<OmniVersion>();
            }
        }
    }
}