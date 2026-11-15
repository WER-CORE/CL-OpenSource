using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public static class AvaloniaImageHelper
{
    private static readonly object _cacheLock = new();
    private static readonly Dictionary<string, (Bitmap Bitmap, DateTime LastAccess)> _bitmapCache = new();
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const int MaxCacheCapacity = 16;

    static AvaloniaImageHelper()
    {
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) CLegendaryLauncher/1.0");
        }
        catch
        {
        }
    }

    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _bitmapCache.Clear();
        }
    }

    public static void TrimCache(int keepCount = 8)
    {
        lock (_cacheLock)
        {
            if (_bitmapCache.Count <= keepCount) return;

            var keysToRemove = _bitmapCache
                .OrderBy(kv => kv.Value.LastAccess)
                .Take(_bitmapCache.Count - keepCount)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _bitmapCache.Remove(key);
            }
        }
    }

    private static void PutInCache(string key, Bitmap bitmap)
    {
        lock (_cacheLock)
        {
            if (_bitmapCache.Count >= MaxCacheCapacity)
            {
                var oldestKey = _bitmapCache.OrderBy(kv => kv.Value.LastAccess).First().Key;
                _bitmapCache.Remove(oldestKey);
            }
            _bitmapCache[key] = (bitmap, DateTime.UtcNow);
        }
    }

    private static bool TryGetFromCache(string key, out Bitmap? bitmap)
    {
        lock (_cacheLock)
        {
            if (_bitmapCache.TryGetValue(key, out var entry))
            {
                _bitmapCache[key] = (entry.Bitmap, DateTime.UtcNow);
                bitmap = entry.Bitmap;
                return true;
            }
            bitmap = null;
            return false;
        }
    }

    public static async Task<Bitmap?> LoadOptimizedImageAsync(string? path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (path.Contains("Flag_of_Belarus", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("bchb", StringComparison.OrdinalIgnoreCase))
        {
            path = "avares://CL.Avalonia/Assets/Flags/bchb.webp";
        }

        string cacheKey = $"{path}_{decodeWidth}";
        if (TryGetFromCache(cacheKey, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            Bitmap? bitmap = null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                byte[] data = await _httpClient.GetByteArrayAsync(path).ConfigureAwait(false);
                bitmap = LoadFromBytes(data, decodeWidth);
            }
            else
            {
                bitmap = await Task.Run(() => LoadOptimizedImage(path, decodeWidth)).ConfigureAwait(false);
            }

            if (bitmap != null)
            {
                PutInCache(cacheKey, bitmap);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static Bitmap? LoadOptimizedImage(string? path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (path.Contains("Flag_of_Belarus", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("bchb", StringComparison.OrdinalIgnoreCase))
        {
            path = "avares://CL.Avalonia/Assets/Flags/bchb.webp";
        }

        string cacheKey = $"{path}_{decodeWidth}";
        if (TryGetFromCache(cacheKey, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            Bitmap? bitmap = null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                byte[] data = _httpClient.GetByteArrayAsync(path).GetAwaiter().GetResult();
                bitmap = LoadFromBytes(data, decodeWidth);
            }
            else if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                bitmap = LoadFromResourceUri(path, decodeWidth);
            }
            else
            {
                string localPath = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

                if (File.Exists(localPath))
                {
                    using var stream = File.OpenRead(localPath);
                    bitmap = decodeWidth > 0
                        ? Bitmap.DecodeToWidth(stream, decodeWidth)
                        : new Bitmap(stream);
                }
                else if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    bitmap = decodeWidth > 0
                        ? Bitmap.DecodeToWidth(stream, decodeWidth)
                        : new Bitmap(stream);
                }
                else if (TryFindCaseInsensitiveFile(localPath, out var matchedFile))
                {
                    using var stream = File.OpenRead(matchedFile);
                    bitmap = decodeWidth > 0
                        ? Bitmap.DecodeToWidth(stream, decodeWidth)
                        : new Bitmap(stream);
                }
                else if (path.StartsWith("/") || path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Icon", StringComparison.OrdinalIgnoreCase))
                {
                    bitmap = LoadFromResourceUri(path, decodeWidth);
                }
            }

            if (bitmap != null)
            {
                PutInCache(cacheKey, bitmap);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static Bitmap? LoadFromBytes(byte[]? imageData, int decodeWidth = 0)
    {
        if (imageData == null || imageData.Length == 0) return null;

        try
        {
            using var stream = new MemoryStream(imageData);
            return decodeWidth > 0
                ? Bitmap.DecodeToWidth(stream, decodeWidth)
                : new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadFromResourceUri(string rawPath, int decodeWidth)
    {
        try
        {
            string relativePath = rawPath;
            if (relativePath.StartsWith("pack://application:,,,/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring("pack://application:,,,/".Length);
            }
            else if (relativePath.StartsWith("pack://application:,,,", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring("pack://application:,,,".Length);
            }

            int componentIdx = relativePath.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
            if (componentIdx >= 0)
            {
                relativePath = relativePath.Substring(componentIdx + ";component/".Length);
            }

            relativePath = relativePath.TrimStart('/');
            string fileName = Path.GetFileName(relativePath);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(relativePath);

            if (fileNameNoExt.Contains("steve", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = "Assets/default_steve.webp";
                fileName = "default_steve.webp";
            }
            else if (fileNameNoExt.Contains("LittleSkin", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = "Assets/LittleSkinAccount.webp";
                fileName = "LittleSkinAccount.webp";
            }
            else if (fileNameNoExt.Contains("IconCL", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = "Icon/IconCL(Common).webp";
                fileName = "IconCL(Common).webp";
            }

            string webpRaw = rawPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? rawPath.Substring(0, rawPath.Length - 4) + ".webp"
                : rawPath;
            string webpRelative = relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? relativePath.Substring(0, relativePath.Length - 4) + ".webp"
                : relativePath;
            string webpFileName = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4) + ".webp"
                : fileName;

            string[] candidates =
            {
                webpRaw.StartsWith("avares://") ? webpRaw : $"avares://CL.Avalonia/{webpRelative}",
                $"avares://CL.Avalonia/Icon/{webpRelative}",
                $"avares://CL.Avalonia/Icon/{webpFileName}",
                $"avares://CL.Avalonia/Assets/{webpRelative}",
                $"avares://CL.Avalonia/Assets/{webpFileName}",
                $"avares://CL.Avalonia/Assets/Backgrounds/{webpRelative}",
                rawPath.StartsWith("avares://") ? rawPath : $"avares://CL.Avalonia/{relativePath}",
                $"avares://CL.Avalonia/Icon/{relativePath}",
                $"avares://CL.Avalonia/Icon/{fileName}",
                $"avares://CL.Avalonia/Assets/{relativePath}",
                $"avares://CL.Avalonia/Assets/{fileName}",
                $"avares://CL.Avalonia/Assets/Backgrounds/{relativePath}"
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var uri = new Uri(candidate, UriKind.Absolute);
                    if (AssetLoader.Exists(uri))
                    {
                        using var stream = AssetLoader.Open(uri);
                        return decodeWidth > 0
                            ? Bitmap.DecodeToWidth(stream, decodeWidth)
                            : new Bitmap(stream);
                    }
                }
                catch { }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryFindCaseInsensitiveFile(string path, out string matchedPath)
    {
        matchedPath = string.Empty;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !string.IsNullOrEmpty(fileName))
            {
                var files = Directory.GetFiles(dir);
                var found = Array.Find(files, f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                if (found == null && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    string webpName = Path.GetFileNameWithoutExtension(fileName) + ".webp";
                    found = Array.Find(files, f => string.Equals(Path.GetFileName(f), webpName, StringComparison.OrdinalIgnoreCase));
                }
                if (found != null)
                {
                    matchedPath = found;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }
}
