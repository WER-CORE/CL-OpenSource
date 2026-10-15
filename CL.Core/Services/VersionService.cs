using CL.Core.Models;
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

namespace CL.Core.Services
{
    public class VersionService
    {
        private static HashSet<string>? _cachedOptifineVersions;
        private readonly string _launcherPath;
        private readonly MinecraftLauncher _launcher;

        public VersionService(string launcherPath)
        {
            _launcherPath = launcherPath;
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
            var installer = new OptifineInstaller(WebHelper.Client);
            var all = await installer.GetOptifineVersionsAsync();
            return all.Where(v => v.MinecraftVersion == mcVersion).Select(v => v.Version).ToList();
        }
        public List<string> GetLocalVersions(string? keyword = null)
        {
            var localVersions = new List<string>();
            var path = new MinecraftPath(_launcherPath);

            if (System.IO.Directory.Exists(path.Versions))
            {
                var dirs = System.IO.Directory.GetDirectories(path.Versions);
                foreach (var dir in dirs)
                {
                    string verName = System.IO.Path.GetFileName(dir);

                    if (string.IsNullOrEmpty(keyword) || verName.ToLower().Contains(keyword.ToLower()))
                    {
                        localVersions.Add(verName);
                    }
                }
            }
            return localVersions;
        }
        public async Task<HashSet<string>> GetOptifineSupportedVersionsAsync()
        {
            if (_cachedOptifineVersions != null)
                return _cachedOptifineVersions;

            var fallbackVersions = new HashSet<string>
            {
                "1.21.1", "1.21",
                "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.20",
                "1.19.4", "1.19.3", "1.19.2", "1.19",
                "1.18.2", "1.18.1", "1.18",
                "1.17.1", "1.17",
                "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1",
                "1.15.2", "1.15.1", "1.15",
                "1.14.4", "1.14.3", "1.14.2", "1.14.1", "1.14",
                "1.13.2", "1.13.1", "1.13",
                "1.12.2", "1.12.1", "1.12",
                "1.11.2", "1.11",
                "1.10.2", "1.10",
                "1.9.4", "1.9.2", "1.9",
                "1.8.9", "1.8.8", "1.8.7", "1.8.1", "1.8",
                "1.7.10", "1.7.2"
            };

            try
            {
                var optifineInstaller = new OptifineInstaller(WebHelper.Client);
                var optifineList = await optifineInstaller.GetOptifineVersionsAsync();

                var gameVersions = optifineList
                    .Select(x => x.MinecraftVersion)
                    .Distinct()
                    .OrderByDescending(v =>
                    {
                        if (System.Version.TryParse(v, out var ver))
                            return ver;
                        return new System.Version(0, 0, 0);
                    });

                var versionsHashSet = new HashSet<string>(gameVersions);

                if (versionsHashSet.Count > 0)
                {
                    _cachedOptifineVersions = versionsHashSet;
                    return _cachedOptifineVersions;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching OptiFine versions: {ex.Message}");
            }

            return fallbackVersions;
        }
        public async Task<List<VersionLogItem>> GetChangeLogAsync()
        {
            var items = new List<VersionLogItem>();
            string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

            try
            {
                    string json = await WebHelper.Client.GetStringAsync(url);
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
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch changelog: {ex.Message}", ex);
            }

            return items;
        }
    }
}
