using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models.P2P;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services.P2P;

public class P2PManifestService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<P2PManifest> BuildManifestAsync(string gameVersion, string loaderType, string? modsDirectory)
    {
        var manifest = new P2PManifest
        {
            GameVersion = gameVersion,
            LoaderType = loaderType
        };

        if (string.IsNullOrEmpty(modsDirectory) || !Directory.Exists(modsDirectory))
        {
            return manifest;
        }

        var jarFiles = Directory.GetFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly);
        foreach (var jarPath in jarFiles)
        {
            try
            {
                var fileInfo = new FileInfo(jarPath);
                string sha1 = await ComputeSha1Async(jarPath).ConfigureAwait(false);
                string modName = Path.GetFileNameWithoutExtension(jarPath);

                try
                {
                    var meta = await ModMetadataReader.ReadMetadataAsync(jarPath).ConfigureAwait(false);
                    if (meta != null && !string.IsNullOrWhiteSpace(meta.Name))
                    {
                        modName = meta.Name;
                    }
                }
                catch { }

                manifest.Mods.Add(new P2PModEntry
                {
                    Name = modName,
                    FileName = fileInfo.Name,
                    Sha1 = sha1,
                    SizeBytes = fileInfo.Length
                });
            }
            catch { }
        }

        return manifest;
    }

    public async Task<(List<P2PModEntry> MissingMods, bool IsMatch)> CompareManifestAsync(P2PManifest hostManifest, string localModsDirectory)
    {
        var missing = new List<P2PModEntry>();
        if (hostManifest.Mods == null || hostManifest.Mods.Count == 0)
        {
            return (missing, true);
        }

        var localHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(localModsDirectory))
        {
            var localJars = Directory.GetFiles(localModsDirectory, "*.jar", SearchOption.TopDirectoryOnly);
            foreach (var jar in localJars)
            {
                try
                {
                    string hash = await ComputeSha1Async(jar).ConfigureAwait(false);
                    localHashes.Add(hash);
                }
                catch { }
            }
        }

        foreach (var mod in hostManifest.Mods)
        {
            if (!localHashes.Contains(mod.Sha1))
            {
                missing.Add(mod);
            }
        }

        return (missing, missing.Count == 0);
    }

    public async Task<bool> SyncMissingModsAsync(List<P2PModEntry> missingMods, string targetModsFolder, IProgress<double>? progress = null, CancellationToken token = default)
    {
        if (missingMods == null || missingMods.Count == 0) return true;
        Directory.CreateDirectory(targetModsFolder);

        var hashes = missingMods.Select(m => m.Sha1).ToList();
        var downloadMap = await QueryModrinthFilesByHashAsync(hashes, token).ConfigureAwait(false);

        int total = missingMods.Count;
        int completed = 0;

        foreach (var mod in missingMods)
        {
            token.ThrowIfCancellationRequested();

            string? downloadUrl = mod.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl) && downloadMap.TryGetValue(mod.Sha1, out var resolvedUrl))
            {
                downloadUrl = resolvedUrl;
            }

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                string targetPath = Path.Combine(targetModsFolder, mod.FileName);
                await DownloadFileAsync(downloadUrl, targetPath, token).ConfigureAwait(false);
            }

            completed++;
            progress?.Report((double)completed / total * 100.0);
        }

        return true;
    }

    private static async Task<Dictionary<string, string>> QueryModrinthFilesByHashAsync(List<string> hashes, CancellationToken token)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (hashes.Count == 0) return result;

        try
        {
            var payload = new
            {
                hashes = hashes,
                algorithm = "sha1"
            };

            string json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.modrinth.com/v2/version_files", content, token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string resJson = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var jObj = JObject.Parse(resJson);

                foreach (var prop in jObj.Properties())
                {
                    string hash = prop.Name;
                    var fileObj = prop.Value as JObject;
                    var filesArr = fileObj?["files"] as JArray;
                    if (filesArr != null && filesArr.Count > 0)
                    {
                        string? url = filesArr[0]?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            result[hash] = url;
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }

    private static async Task DownloadFileAsync(string url, string destPath, CancellationToken token)
    {
        using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        using var inStream = await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await inStream.CopyToAsync(outStream, token).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha1Async(string filePath)
    {
        using var sha1 = SHA1.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        byte[] hash = await sha1.ComputeHashAsync(stream).ConfigureAwait(false);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
