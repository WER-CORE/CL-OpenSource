using CL_CLegendary_Launcher_.Models;
using CmlLib.Core;
using Newtonsoft.Json.Linq;
using Optifine.Installer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class VersionService
    {
        private readonly MinecraftLauncher _launcher;

        public VersionService(string launcherPath)
        {
            _launcher = new MinecraftLauncher(new MinecraftPath(launcherPath));
        }
        public async Task<List<string>> GetLoaderVersionsAsync(int loaderType, string mcVersion)
        {
            if (loaderType == 5)
            {
                return await GetOptifineVersionsAsync(mcVersion);
            }

            return new List<string>();
        }
        public async Task<List<string>> GetFilteredVersionsAsync(string searchText, bool rel, bool snap, bool beta, bool alpha)
        {
            var versions = await _launcher.GetAllVersionsAsync();
            var regex = new Regex(string.IsNullOrEmpty(searchText) ? ".*" : Regex.Escape(searchText).Replace(@"\*", ".*"), RegexOptions.IgnoreCase);

            return versions
                .Where(v => ((v.Type == "release" && rel) ||
                             (v.Type == "snapshot" && snap) ||
                             (v.Type == "old_beta" && beta) ||
                             (v.Type == "old_alpha" && alpha)) && regex.IsMatch(v.Name))
                .Select(v => v.Name)
                .ToList();
        }
        public async Task<List<string>> GetOptifineVersionsAsync(string mcVersion)
        {
            var installer = new OptifineInstaller(new HttpClient());
            var all = await installer.GetOptifineVersionsAsync();
            return all.Where(v => v.MinecraftVersion == mcVersion).Select(v => v.Version).ToList();
        }
        public async Task<List<VersionLogItem>> GetChangeLogAsync()
        {
            var items = new List<VersionLogItem>();
            string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"CL-Legendary-Launcher/{Assembly.GetExecutingAssembly().GetName().Version}");

                    string json = await httpClient.GetStringAsync(url);
                    JObject manifest = JObject.Parse(json);
                    JArray versions = (JArray)manifest["versions"];

                    string iconBase = "pack://application:,,,/Assets/";

                    foreach (var version in versions)
                    {
                        string id = version["id"]?.ToString();
                        string type = version["type"]?.ToString();
                        string iconPath;

                        switch (type)
                        {
                            case "snapshot":
                                iconPath = iconBase + "DirtIconSnapshot.png";
                                break;
                            case "old_beta":
                                iconPath = iconBase + "StoneIconBeta.png";
                                break;
                            case "old_alpha":
                                iconPath = iconBase + "CobblestoneIconAlpha.png";
                                break;
                            case "release":
                            default:
                                iconPath = iconBase + "VanilaVersion.png";
                                break;
                        }

                        items.Add(new VersionLogItem
                        {
                            VersionId = id,
                            IconPath = iconPath,
                            VersionType = type
                        });

                        if (id == "a1.0.4") break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch changelog: {ex.Message}", ex);
            }

            return items;
        }
    }
}
