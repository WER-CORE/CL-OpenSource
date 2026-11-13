using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services
{
    public class HistoricalVersionCatalogService
    {
        public const string MojangManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        public const string BetaCraftManifestUrl = "https://files.betacraft.uk/launcher/v2/assets/version_list.json";

        private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly DateTimeOffset HistoricalSnapshotCutoff = new(2014, 7, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly SemaphoreSlim _fetchLock = new(1, 1);

        private IReadOnlyList<CatalogVersionEntry>? _cachedMojang;
        private DateTimeOffset _mojangCachedAt;
        private IReadOnlyList<CatalogVersionEntry>? _cachedBetaCraft;
        private DateTimeOffset _betaCraftCachedAt;

        public HistoricalVersionCatalogService(HttpClient? httpClient = null, string? cacheDirectory = null)
        {
            _httpClient = httpClient ?? WebHelper.Client;
            _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
                ? Path.Combine(PlatformPaths.DataFolder(), "cache", "versions")
                : cacheDirectory;
        }

        public async Task<CatalogMergeResult> GetMergedCatalogAsync(
            IEnumerable<CatalogVersionEntry>? baseMojangOrLocalVersions = null,
            bool forceRefresh = false,
            CancellationToken token = default)
        {
            await _fetchLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                IReadOnlyList<CatalogVersionEntry> primaryVersions;
                bool mojangLoaded = false;

                if (baseMojangOrLocalVersions != null)
                {
                    primaryVersions = baseMojangOrLocalVersions.ToList();
                    mojangLoaded = primaryVersions.Count > 0;
                }
                else
                {
                    var mojangResult = await LoadSourceWithCacheAsync(
                        sourceName: "mojang_manifest_v2",
                        url: MojangManifestUrl,
                        parser: ParseMojangManifest,
                        getMemoryCache: () => (_cachedMojang, _mojangCachedAt),
                        setMemoryCache: (entries, at) => { _cachedMojang = entries; _mojangCachedAt = at; },
                        forceRefresh: forceRefresh,
                        token: token).ConfigureAwait(false);

                    primaryVersions = mojangResult.Entries;
                    mojangLoaded = mojangResult.Loaded;
                }

                var betaCraftResult = await LoadSourceWithCacheAsync(
                    sourceName: "betacraft_v2_version_list",
                    url: BetaCraftManifestUrl,
                    parser: ParseBetaCraftManifest,
                    getMemoryCache: () => (_cachedBetaCraft, _betaCraftCachedAt),
                    setMemoryCache: (entries, at) => { _cachedBetaCraft = entries; _betaCraftCachedAt = at; },
                    forceRefresh: forceRefresh,
                    token: token).ConfigureAwait(false);

                var merged = MergeCatalogs(primaryVersions, betaCraftResult.Entries);
                return merged with
                {
                    MojangLoaded = mojangLoaded,
                    BetaCraftLoaded = betaCraftResult.Loaded,
                    BetaCraftError = betaCraftResult.Error
                };
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        private async Task<(IReadOnlyList<CatalogVersionEntry> Entries, bool Loaded, string? Error)> LoadSourceWithCacheAsync(
            string sourceName,
            string url,
            Func<string, IReadOnlyList<CatalogVersionEntry>> parser,
            Func<(IReadOnlyList<CatalogVersionEntry>? Entries, DateTimeOffset CachedAt)> getMemoryCache,
            Action<IReadOnlyList<CatalogVersionEntry>, DateTimeOffset> setMemoryCache,
            bool forceRefresh,
            CancellationToken token)
        {
            var (memEntries, cachedAt) = getMemoryCache();
            if (!forceRefresh && memEntries != null && (DateTimeOffset.UtcNow - cachedAt) < DefaultCacheTtl)
            {
                return (memEntries, true, null);
            }

            string cacheFilePath = Path.Combine(_cacheDirectory, $"{sourceName}.json");

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", "ClegendaryLauncher/1.0");
                using var resp = await _httpClient.SendAsync(req, token).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                string json = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);

                var parsed = parser(json);
                setMemoryCache(parsed, DateTimeOffset.UtcNow);
                TryWriteCacheFile(cacheFilePath, json);
                return (parsed, true, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                Trace.WriteLine($"[LegacyCatalog] Failed to fetch {sourceName}: {ex.Message}");

                if (memEntries != null)
                {
                    return (memEntries, true, ex.Message);
                }

                var diskEntries = TryReadCacheFile(cacheFilePath, parser);
                if (diskEntries != null)
                {
                    setMemoryCache(diskEntries, DateTimeOffset.UtcNow);
                    return (diskEntries, true, ex.Message);
                }

                return (Array.Empty<CatalogVersionEntry>(), false, ex.Message);
            }
        }

        private void TryWriteCacheFile(string path, string content)
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, content);
            }
            catch
            {
            }
        }

        private static IReadOnlyList<CatalogVersionEntry>? TryReadCacheFile(
            string path,
            Func<string, IReadOnlyList<CatalogVersionEntry>> parser)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                return parser(json);
            }
            catch
            {
                return null;
            }
        }

        public static IReadOnlyList<CatalogVersionEntry> ParseMojangManifest(string json)
        {
            return ParseStandardManifest(json, VersionCatalogSource.Mojang);
        }

        public static IReadOnlyList<CatalogVersionEntry> ParseBetaCraftManifest(string json)
        {
            return ParseStandardManifest(json, VersionCatalogSource.BetaCraft);
        }

        private static IReadOnlyList<CatalogVersionEntry> ParseStandardManifest(string json, VersionCatalogSource source)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new JsonException($"Manifest JSON for {source} is empty.");

            var root = JObject.Parse(json);
            if (root["versions"] is not JArray versionsArray)
                throw new JsonException($"Manifest JSON for {source} does not contain a 'versions' array.");

            var results = new List<CatalogVersionEntry>(versionsArray.Count);
            foreach (var item in versionsArray)
            {
                if (item is not JObject obj) continue;

                string? id = obj["id"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string rawType = obj["type"]?.ToString()?.Trim() ?? string.Empty;
                string? url = obj["url"]?.ToString()?.Trim();
                string? sha1 = obj["sha1"]?.ToString()?.Trim();
                DateTimeOffset? releaseTime = TryParseDate(obj["releaseTime"]) ?? TryParseDate(obj["time"]);

                var era = ClassifyEra(id, rawType, releaseTime);
                string normalizedType = NormalizeFilterType(era, rawType);

                results.Add(new CatalogVersionEntry(
                    Id: id,
                    RawType: string.IsNullOrEmpty(rawType) ? normalizedType : rawType,
                    NormalizedFilterType: normalizedType,
                    Era: era,
                    Source: source,
                    MetadataUrl: url,
                    Sha1: sha1,
                    ReleaseTime: releaseTime));
            }

            return results;
        }

        public static CatalogMergeResult MergeCatalogs(
            IEnumerable<CatalogVersionEntry> primaryVersions,
            IEnumerable<CatalogVersionEntry> secondaryHistoricalVersions)
        {
            var mergedById = new Dictionary<string, CatalogVersionEntry>(StringComparer.OrdinalIgnoreCase);
            var conflictLogs = new List<string>();

            foreach (var entry in primaryVersions ?? Enumerable.Empty<CatalogVersionEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry.Id)) continue;
                mergedById.TryAdd(entry.Id, entry);
            }

            foreach (var sec in secondaryHistoricalVersions ?? Enumerable.Empty<CatalogVersionEntry>())
            {
                if (string.IsNullOrWhiteSpace(sec.Id)) continue;

                if (mergedById.TryGetValue(sec.Id, out var existing))
                {
                    bool metadataDiffers =
                        !string.Equals(existing.RawType, sec.RawType, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(existing.MetadataUrl) && !string.IsNullOrEmpty(sec.MetadataUrl) &&
                         !string.Equals(existing.MetadataUrl, sec.MetadataUrl, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(existing.Sha1) && !string.IsNullOrEmpty(sec.Sha1) &&
                         !string.Equals(existing.Sha1, sec.Sha1, StringComparison.OrdinalIgnoreCase));

                    if (metadataDiffers)
                    {
                        string note = $"Kept {existing.Source} over {sec.Source} for '{sec.Id}' (type: {existing.RawType} vs {sec.RawType}).";
                        mergedById[sec.Id] = existing with { HasConflict = true, ConflictNote = note };
                        if (conflictLogs.Count < 25)
                        {
                            conflictLogs.Add(note);
                        }
                    }
                    continue;
                }

                mergedById[sec.Id] = sec;
            }

            if (conflictLogs.Count > 0)
            {
                Trace.WriteLine($"[LegacyCatalog] Resolved {conflictLogs.Count} version ID conflicts (Mojang/Local priority kept).");
            }

            var sorted = SortCatalogEntries(mergedById.Values);
            return new CatalogMergeResult(sorted, conflictLogs, MojangLoaded: true, BetaCraftLoaded: true);
        }

        public static IReadOnlyList<CatalogVersionEntry> SortCatalogEntries(IEnumerable<CatalogVersionEntry> entries)
        {
            return entries
                .OrderByDescending(e => e.ReleaseTime ?? DateTimeOffset.MinValue)
                .ThenBy(e => e.Source)
                .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static MinecraftEra ClassifyEra(string id, string? rawType = null, DateTimeOffset? releaseTime = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return MinecraftEra.Unknown;

            string s = id.Trim().ToLowerInvariant();

            if (s.StartsWith("rd-", StringComparison.Ordinal))
                return MinecraftEra.PreClassic;

            if (s.StartsWith("c0.", StringComparison.Ordinal) || s.StartsWith("c-", StringComparison.Ordinal))
                return MinecraftEra.Classic;

            if (s.StartsWith("in-", StringComparison.Ordinal))
                return MinecraftEra.Indev;

            if (s.StartsWith("inf-", StringComparison.Ordinal))
                return MinecraftEra.Infdev;

            if (s.StartsWith("a1.", StringComparison.Ordinal) || s.StartsWith("a0.", StringComparison.Ordinal))
                return MinecraftEra.Alpha;

            if (s.StartsWith("b1.", StringComparison.Ordinal))
                return MinecraftEra.Beta;

            if (string.Equals(rawType, "old_alpha", StringComparison.OrdinalIgnoreCase))
                return MinecraftEra.Alpha;

            if (string.Equals(rawType, "old_beta", StringComparison.OrdinalIgnoreCase))
                return MinecraftEra.Beta;

            if (string.Equals(rawType, "snapshot", StringComparison.OrdinalIgnoreCase))
            {
                if (releaseTime.HasValue && releaseTime.Value <= HistoricalSnapshotCutoff)
                    return MinecraftEra.HistoricalSnapshot;

                return MinecraftEra.Snapshot;
            }

            if (string.Equals(rawType, "release", StringComparison.OrdinalIgnoreCase))
                return MinecraftEra.Release;

            return MinecraftEra.Unknown;
        }

        public static string NormalizeFilterType(MinecraftEra era, string? rawType)
        {
            return era switch
            {
                MinecraftEra.PreClassic or MinecraftEra.Classic or MinecraftEra.Indev or MinecraftEra.Infdev or MinecraftEra.Alpha => "old_alpha",
                MinecraftEra.Beta => "old_beta",
                MinecraftEra.HistoricalSnapshot or MinecraftEra.Snapshot => "snapshot",
                MinecraftEra.Release => "release",
                _ => string.IsNullOrWhiteSpace(rawType) ? "release" : rawType.Trim().ToLowerInvariant()
            };
        }

        private static DateTimeOffset? TryParseDate(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            if (token.Type == JTokenType.Date)
            {
                try
                {
                    return token.ToObject<DateTimeOffset>();
                }
                catch
                {
                    return null;
                }
            }

            string? raw = token.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
                ? dt
                : null;
        }
    }
}
