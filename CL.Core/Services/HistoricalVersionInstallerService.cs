using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CmlLib.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services
{
    public sealed record HistoricalInstallResult(
        string VersionId,
        VersionCatalogSource Source,
        bool Installed,
        string VersionDirectory,
        string JsonPath,
        string? ClientJarPath);

    public class HistoricalVersionInstallerService
    {
        private static readonly HashSet<string> TrustedMetadataHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "piston-meta.mojang.com",
            "launchermeta.mojang.com",
            "files.betacraft.uk"
        };

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> InstallLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly HttpClient _httpClient;

        public event Action<string>? DiagnosticLogged;

        public HistoricalVersionInstallerService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? WebHelper.Client;
        }

        private void LogDiagnostic(string message)
        {
            try
            {
                Trace.WriteLine($"[LegacyInstaller] {message}");
                DiagnosticLogged?.Invoke(message);
            }
            catch
            {
            }
        }

        public static bool IsSafeVersionId(string? versionId)
        {
            if (string.IsNullOrWhiteSpace(versionId))
                return false;

            string trimmed = versionId.Trim();
            if (trimmed is "." or ".." || trimmed.Contains("..", StringComparison.Ordinal))
                return false;

            if (trimmed.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                return false;

            foreach (char ch in trimmed)
            {
                if (char.IsControl(ch))
                    return false;
            }

            return true;
        }

        public static bool IsTrustedMetadataUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            return TrustedMetadataHosts.Contains(uri.Host);
        }

        public async Task<string> EnsureVersionMetadataAsync(
            CatalogVersionEntry entry,
            MinecraftPath path,
            CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!IsSafeVersionId(entry.Id))
                throw new ArgumentException($"Небезпечний або некоректний ідентифікатор версії: '{entry.Id}'.", nameof(entry));

            string versionDir = Path.Combine(path.Versions, entry.Id);
            string jsonPath = Path.Combine(versionDir, $"{entry.Id}.json");

            if (entry.Source != VersionCatalogSource.BetaCraft)
            {
                return jsonPath;
            }

            if (!IsTrustedMetadataUrl(entry.MetadataUrl))
                throw new InvalidOperationException($"Недозволений або некоректний URL метаданих для версії '{entry.Id}'.");

            using var req = new HttpRequestMessage(HttpMethod.Get, entry.MetadataUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "ClegendaryLauncher/1.0");
            using var resp = await _httpClient.SendAsync(req, token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            string json = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException($"Сервер повернув порожній JSON метаданих для версії '{entry.Id}'.");

            if (!string.IsNullOrWhiteSpace(entry.Sha1))
            {
                string actualSha1 = ComputeSha1Hex(System.Text.Encoding.UTF8.GetBytes(json));
                if (!string.Equals(actualSha1, entry.Sha1.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Контрольна сума SHA1 JSON для '{entry.Id}' не збігається.");
                }
            }

            JObject parsed;
            try
            {
                parsed = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Пошкоджений JSON метаданих для версії '{entry.Id}': {ex.Message}", ex);
            }

            string? jsonId = parsed["id"]?.ToString()?.Trim();
            if (!string.Equals(jsonId, entry.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"ID версії в метаданих ('{jsonId ?? "null"}') не відповідає вибраній версії ('{entry.Id}').");
            }

            string? clientUrl = parsed["downloads"]?["client"]?["url"]?.ToString();
            string? inheritsFrom = parsed["inheritsFrom"]?.ToString();
            if (string.IsNullOrWhiteSpace(clientUrl) && string.IsNullOrWhiteSpace(inheritsFrom))
            {
                throw new InvalidDataException($"У метаданих версії '{entry.Id}' відсутній URL клієнтського JAR.");
            }

            Directory.CreateDirectory(versionDir);
            string tempPath = Path.Combine(versionDir, $"{entry.Id}.{Guid.NewGuid():N}.json.tmp");
            try
            {
                await File.WriteAllTextAsync(tempPath, json, token).ConfigureAwait(false);
                File.Move(tempPath, jsonPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }

            return jsonPath;
        }

        public async Task<HistoricalInstallResult> InstallAsync(
            CatalogVersionEntry entry,
            MinecraftLauncher launcher,
            CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (launcher == null) throw new ArgumentNullException(nameof(launcher));
            if (!IsSafeVersionId(entry.Id))
                throw new ArgumentException($"Небезпечний ідентифікатор версії: '{entry.Id}'.", nameof(entry));

            var versionLock = InstallLocks.GetOrAdd(entry.Id, _ => new SemaphoreSlim(1, 1));
            await versionLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                LogDiagnostic($"Preparing metadata for '{entry.Id}' ({entry.Source})");
                string jsonPath = await EnsureVersionMetadataAsync(entry, launcher.MinecraftPath, token).ConfigureAwait(false);

                try
                {
                    LogDiagnostic($"Installing '{entry.Id}' via CmlLib.Core");
                    await launcher.InstallAsync(entry.Id, token).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (IsHttp404(ex))
                {
                    LogDiagnostic($"CmlLib.Core hit HTTP 404 while installing '{entry.Id}': {ex.Message}. Attempting BetaCraft asset fallback.");
                    int recovered = await EnsureMissingAssetsFromBetaCraftAsync(jsonPath, launcher.MinecraftPath, token).ConfigureAwait(false);
                    if (recovered <= 0)
                    {
                        throw;
                    }

                    LogDiagnostic($"Recovered {recovered} missing asset(s) from BetaCraft mirror for '{entry.Id}'. Retrying CmlLib.Core install.");
                    await launcher.InstallAsync(entry.Id, token).ConfigureAwait(false);
                }

                if (!TryValidateInstallation(entry.Id, launcher.MinecraftPath, out string? validationError))
                {
                    throw new InvalidOperationException(
                        $"Встановлення '{entry.Id}' завершилося неповно: {validationError}");
                }

                string versionDir = Path.Combine(launcher.MinecraftPath.Versions, entry.Id);
                string jarPath = Path.Combine(versionDir, $"{entry.Id}.jar");
                return new HistoricalInstallResult(
                    VersionId: entry.Id,
                    Source: entry.Source,
                    Installed: true,
                    VersionDirectory: versionDir,
                    JsonPath: jsonPath,
                    ClientJarPath: File.Exists(jarPath) ? jarPath : null);
            }
            finally
            {
                versionLock.Release();
            }
        }

        public static bool IsVersionInstalled(string versionId, MinecraftPath path)
        {
            return TryValidateInstallation(versionId, path, out _);
        }

        public static bool TryValidateInstallation(string versionId, MinecraftPath path, out string? errorReason)
        {
            errorReason = null;

            if (!IsSafeVersionId(versionId))
            {
                errorReason = "некоректний ID версії";
                return false;
            }

            if (path == null)
            {
                errorReason = "не вказано шлях MinecraftPath";
                return false;
            }

            string versionDir = Path.Combine(path.Versions, versionId);
            string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
            if (!File.Exists(jsonPath))
            {
                errorReason = $"відсутній файл метаданих '{jsonPath}'";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(jsonPath));
            }
            catch (Exception ex)
            {
                errorReason = $"пошкоджений JSON '{jsonPath}': {ex.Message}";
                return false;
            }

            string? inheritsFrom = root["inheritsFrom"]?.ToString();
            bool hasClientSection = root["downloads"]?["client"] != null;
            if (string.IsNullOrWhiteSpace(inheritsFrom))
            {
                string jarPath = Path.Combine(versionDir, $"{versionId}.jar");
                if (!File.Exists(jarPath))
                {
                    errorReason = $"відсутній клієнтський JAR '{jarPath}'";
                    return false;
                }

                var jarInfo = new FileInfo(jarPath);
                if (jarInfo.Length == 0)
                {
                    errorReason = $"клієнтський JAR порожній '{jarPath}'";
                    return false;
                }

                if (hasClientSection)
                {
                    long? expectedSize = root["downloads"]?["client"]?["size"]?.Value<long?>();
                    if (expectedSize.HasValue && expectedSize.Value > 0 && jarInfo.Length != expectedSize.Value)
                    {
                        errorReason = $"розмір JAR ({jarInfo.Length}) не збігається з очікуваним ({expectedSize.Value})";
                        return false;
                    }
                }
            }

            if (root["libraries"] is JArray libs)
            {
                foreach (var libToken in libs)
                {
                    if (libToken is not JObject libObj) continue;
                    if (!IsLibraryAllowedForCurrentOs(libObj["rules"] as JArray)) continue;

                    string? artifactRelPath = libObj["downloads"]?["artifact"]?["path"]?.ToString();
                    string? libName = libObj["name"]?.ToString();

                    if (string.IsNullOrWhiteSpace(artifactRelPath) && libObj["downloads"]?["artifact"] != null)
                    {
                        artifactRelPath = TryMavenNameToRelativePath(libName);
                    }

                    if (!string.IsNullOrWhiteSpace(artifactRelPath))
                    {
                        string fullLibPath = Path.Combine(path.Library, artifactRelPath.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fullLibPath) || new FileInfo(fullLibPath).Length == 0)
                        {
                            errorReason = $"відсутня обов'язкова бібліотека '{libName ?? artifactRelPath}'";
                            return false;
                        }
                    }
                }
            }

            string? assetIndexId = root["assetIndex"]?["id"]?.ToString();
            if (!string.IsNullOrWhiteSpace(assetIndexId))
            {
                string indexPath = Path.Combine(path.Assets, "indexes", $"{assetIndexId}.json");
                if (!File.Exists(indexPath))
                {
                    errorReason = $"відсутній індекс ресурсів '{indexPath}'";
                    return false;
                }

                try
                {
                    var indexRoot = JObject.Parse(File.ReadAllText(indexPath));
                    if (indexRoot["objects"] is JObject objects)
                    {
                        foreach (var prop in objects.Properties())
                        {
                            string? hash = prop.Value?["hash"]?.ToString();
                            if (string.IsNullOrWhiteSpace(hash) || hash.Length < 2) continue;

                            string objPath = Path.Combine(path.Assets, "objects", hash[..2], hash);
                            if (!File.Exists(objPath) || new FileInfo(objPath).Length == 0)
                            {
                                errorReason = $"відсутній ресурс '{prop.Name}' ({hash})";
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorReason = $"пошкоджений індекс ресурсів '{indexPath}': {ex.Message}";
                    return false;
                }
            }

            return true;
        }

        internal async Task<int> EnsureMissingAssetsFromBetaCraftAsync(
            string versionJsonPath,
            MinecraftPath path,
            CancellationToken token = default)
        {
            if (!File.Exists(versionJsonPath)) return 0;

            var versionRoot = JObject.Parse(await File.ReadAllTextAsync(versionJsonPath, token).ConfigureAwait(false));
            string? assetIndexId = versionRoot["assetIndex"]?["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(assetIndexId)) return 0;

            string indexPath = Path.Combine(path.Assets, "indexes", $"{assetIndexId}.json");
            if (!File.Exists(indexPath))
            {
                string? indexUrl = versionRoot["assetIndex"]?["url"]?.ToString();
                if (!IsTrustedMetadataUrl(indexUrl))
                    return 0;

                LogDiagnostic($"Downloading asset index '{assetIndexId}' from {indexUrl}");
                using var idxReq = new HttpRequestMessage(HttpMethod.Get, indexUrl);
                idxReq.Headers.TryAddWithoutValidation("User-Agent", "ClegendaryLauncher/1.0");
                using var idxResp = await _httpClient.SendAsync(idxReq, token).ConfigureAwait(false);
                LogDiagnostic($"Asset index GET {indexUrl} -> HTTP {(int)idxResp.StatusCode}");
                idxResp.EnsureSuccessStatusCode();

                string idxJson = await idxResp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                string? expectedIdxSha1 = versionRoot["assetIndex"]?["sha1"]?.ToString();
                if (!string.IsNullOrWhiteSpace(expectedIdxSha1))
                {
                    string actualIdxSha1 = ComputeSha1Hex(System.Text.Encoding.UTF8.GetBytes(idxJson));
                    if (!string.Equals(actualIdxSha1, expectedIdxSha1.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA1 індексу ресурсів '{assetIndexId}' не збігається.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
                await File.WriteAllTextAsync(indexPath, idxJson, token).ConfigureAwait(false);
            }

            var indexObj = JObject.Parse(await File.ReadAllTextAsync(indexPath, token).ConfigureAwait(false));
            if (indexObj["objects"] is not JObject objects) return 0;

            int recoveredCount = 0;
            foreach (var prop in objects.Properties())
            {
                token.ThrowIfCancellationRequested();

                string? hash = prop.Value?["hash"]?.ToString()?.Trim().ToLowerInvariant();
                long? expectedSize = prop.Value?["size"]?.Value<long?>();
                if (string.IsNullOrWhiteSpace(hash) || hash.Length < 2) continue;

                string objDir = Path.Combine(path.Assets, "objects", hash[..2]);
                string objPath = Path.Combine(objDir, hash);

                if (File.Exists(objPath))
                {
                    var fi = new FileInfo(objPath);
                    if (fi.Length > 0 && (!expectedSize.HasValue || expectedSize.Value <= 0 || fi.Length == expectedSize.Value))
                    {
                        continue;
                    }
                }

                string mojangUrl = $"https://resources.download.minecraft.net/{hash[..2]}/{hash}";
                byte[]? data = await TryDownloadVerifiedAssetAsync(mojangUrl, hash, expectedSize, allow404: true, token).ConfigureAwait(false);

                if (data == null)
                {
                    string betaCraftUrl = $"https://files.betacraft.uk/launcher/v2/assets/resources/{hash[..2]}/{hash}";
                    data = await TryDownloadVerifiedAssetAsync(betaCraftUrl, hash, expectedSize, allow404: false, token).ConfigureAwait(false);
                    if (data != null)
                    {
                        recoveredCount++;
                    }
                }

                if (data != null)
                {
                    Directory.CreateDirectory(objDir);
                    string tmpPath = Path.Combine(objDir, $"{hash}.{Guid.NewGuid():N}.tmp");
                    try
                    {
                        await File.WriteAllBytesAsync(tmpPath, data, token).ConfigureAwait(false);
                        File.Move(tmpPath, objPath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(tmpPath))
                        {
                            try { File.Delete(tmpPath); } catch { }
                        }
                    }
                }
            }

            return recoveredCount;
        }

        private async Task<byte[]?> TryDownloadVerifiedAssetAsync(
            string url,
            string expectedSha1,
            long? expectedSize,
            bool allow404,
            CancellationToken token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "ClegendaryLauncher/1.0");
            using var resp = await _httpClient.SendAsync(req, token).ConfigureAwait(false);

            LogDiagnostic($"Asset GET {url} -> HTTP {(int)resp.StatusCode}");

            if (allow404 && resp.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            resp.EnsureSuccessStatusCode();
            byte[] bytes = await resp.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);

            if (expectedSize.HasValue && expectedSize.Value > 0 && bytes.Length != expectedSize.Value)
            {
                throw new InvalidDataException($"Розмір ресурсу {url} ({bytes.Length}) не збігається з очікуваним ({expectedSize.Value}).");
            }

            string actualSha1 = ComputeSha1Hex(bytes);
            if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Контрольна сума SHA1 ресурсу {url} не збігається.");
            }

            return bytes;
        }

        private static bool IsHttp404(HttpRequestException ex)
        {
            return ex.StatusCode == HttpStatusCode.NotFound ||
                   ex.Message.Contains("404", StringComparison.Ordinal);
        }

        private static bool IsLibraryAllowedForCurrentOs(JArray? rules)
        {
            if (rules == null || rules.Count == 0) return true;

            string currentOs = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : "linux";

            bool allowed = false;
            foreach (var r in rules)
            {
                if (r is not JObject ruleObj) continue;
                string? action = ruleObj["action"]?.ToString();
                string? osName = ruleObj["os"]?["name"]?.ToString();

                bool applies = string.IsNullOrWhiteSpace(osName) ||
                               string.Equals(osName, currentOs, StringComparison.OrdinalIgnoreCase);
                if (applies)
                {
                    allowed = string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase);
                }
            }

            return allowed;
        }

        private static string? TryMavenNameToRelativePath(string? mavenName)
        {
            if (string.IsNullOrWhiteSpace(mavenName)) return null;
            var parts = mavenName.Split(':');
            if (parts.Length < 3) return null;

            string groupPath = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length >= 4 ? $"-{parts[3]}" : string.Empty;
            return $"{groupPath}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
        }

        private static string ComputeSha1Hex(byte[] data)
        {
            byte[] hash = SHA1.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
