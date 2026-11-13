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
        private readonly MinecraftLauncher _launcher;
        private readonly HistoricalVersionCatalogService _historicalCatalogService;

        public VersionService(string launcherPath)
            : this(launcherPath, new HistoricalVersionCatalogService())
        {
        }

        public VersionService(string launcherPath, HistoricalVersionCatalogService historicalCatalogService)
        {
            _launcher = new MinecraftLauncher(new MinecraftPath(launcherPath));
            _historicalCatalogService = historicalCatalogService ?? new HistoricalVersionCatalogService();
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
            var filteredEntries = await GetFilteredCatalogEntriesAsync(searchText, rel, snap, beta, alpha).ConfigureAwait(false);
            return filteredEntries.Select(v => v.Id).ToList();
        }

        public async Task<IReadOnlyList<CatalogVersionEntry>> GetFilteredCatalogEntriesAsync(string searchText, bool rel, bool snap, bool beta, bool alpha)
        {
            var versions = await _launcher.GetAllVersionsAsync().ConfigureAwait(false);
            var baseEntries = versions.Select(v =>
            {
                string rawType = v.Type ?? string.Empty;
                DateTimeOffset? releaseTime = v.ReleaseTime;
                var era = HistoricalVersionCatalogService.ClassifyEra(v.Name, rawType, releaseTime);
                string normalizedType = HistoricalVersionCatalogService.NormalizeFilterType(era, rawType);
                return new CatalogVersionEntry(
                    Id: v.Name,
                    RawType: rawType,
                    NormalizedFilterType: normalizedType,
                    Era: era,
                    Source: VersionCatalogSource.Mojang,
                    ReleaseTime: releaseTime);
            });

            var merged = await _historicalCatalogService
                .GetMergedCatalogAsync(baseEntries)
                .ConfigureAwait(false);

            var regex = new Regex(string.IsNullOrEmpty(searchText) ? ".*" : Regex.Escape(searchText).Replace(@"\*", ".*"), RegexOptions.IgnoreCase);

            return merged.Versions
                .Where(v => ((v.NormalizedFilterType == "release" && rel) ||
                             (v.NormalizedFilterType == "snapshot" && snap) ||
                             (v.NormalizedFilterType == "old_beta" && beta) ||
                             (v.NormalizedFilterType == "old_alpha" && alpha)) && regex.IsMatch(v.Id))
                .ToList();
        }
        public async Task<List<string>> GetOptifineVersionsAsync(string mcVersion)
        {
            var installer = new OptifineInstaller(WebHelper.Client);
            var all = await installer.GetOptifineVersionsAsync();
            return all.Where(v => v.MinecraftVersion == mcVersion).Select(v => v.Version).ToList();
        }
        private List<(string Id, string Type)>? _cachedVersionManifest;
        private readonly object _manifestLock = new();

        public bool HasMoreChangeLogs(int currentCount)
        {
            lock (_manifestLock)
            {
                return _cachedVersionManifest != null && currentCount < _cachedVersionManifest.Count;
            }
        }

        public async Task<List<VersionLogItem>> GetChangeLogAsync(int skip = 0, int take = 10)
        {
            List<(string Id, string Type)> targetSlice;

            if (_cachedVersionManifest == null)
            {
                string url = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
                try
                {
                    string json = await WebHelper.Client.GetStringAsync(url);
                    JObject manifest = JObject.Parse(json);
                    JArray versions = (JArray)manifest["versions"];

                    var manifestList = new List<(string Id, string Type)>();
                    foreach (var version in versions)
                    {
                        string? id = version["id"]?.ToString();
                        string? type = version["type"]?.ToString();
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type)) continue;

                        manifestList.Add((id, type));

                        if (id == "a1.0.4") break;
                    }

                    lock (_manifestLock)
                    {
                        _cachedVersionManifest = manifestList;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to fetch changelog: {ex.Message}", ex);
                }
            }

            lock (_manifestLock)
            {
                targetSlice = _cachedVersionManifest.Skip(skip).Take(take).ToList();
            }

            string iconBase = $"{CL.Core.Platform.UIConfig.AssetScheme}/Assets/";
            var items = new List<VersionLogItem>(targetSlice.Count);

            foreach (var (id, type) in targetSlice)
            {
                string iconPath = type switch
                {
                    "snapshot" => iconBase + "DirtIconSnapshot.png",
                    "old_beta" => iconBase + "StoneIconBeta.png",
                    "old_alpha" => iconBase + "CobblestoneIconAlpha.png",
                    _ => iconBase + "VanilaVersion.png"
                };

                items.Add(new VersionLogItem
                {
                    VersionId = id,
                    IconPath = iconPath,
                    VersionType = type
                });
            }

            return items;
        }
    }
}
