using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CL.Core.Models;
using CL.Core.Services;
using Newtonsoft.Json;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class ScreenshotItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string CreationDate { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public Bitmap? ImagePath { get; set; }
}

public class ScreenshotSourceItem : IEquatable<ScreenshotSourceItem>
{
    public string Name { get; set; } = string.Empty;
    public string FullScreenshotsPath { get; set; } = string.Empty;

    public bool Equals(ScreenshotSourceItem? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && FullScreenshotsPath == other.FullScreenshotsPath;
    }

    public override bool Equals(object? obj) => Equals(obj as ScreenshotSourceItem);

    public override int GetHashCode() => HashCode.Combine(Name, FullScreenshotsPath);
}

public class ScreenshotService
{
    public List<ScreenshotSourceItem> GetScreenshotSources(string launcherPath)
    {
        var sources = new List<ScreenshotSourceItem>();

        string defaultScreenshots = !string.IsNullOrWhiteSpace(launcherPath)
            ? Path.Combine(launcherPath, "screenshots")
            : Path.Combine(PlatformPaths.DefaultLauncherPath(), "screenshots");

        sources.Add(new ScreenshotSourceItem
        {
            Name = LocalizationManager.GetString("Modpacks.GlobalFolder", "Глобальні (.ClMinecraft)"),
            FullScreenshotsPath = defaultScreenshots
        });

        string jsonPath = ModpackPaths.InstalledModpacksJson;
        if (!File.Exists(jsonPath))
        {
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");
            if (File.Exists(legacyPath))
                jsonPath = legacyPath;
            else if (!string.IsNullOrWhiteSpace(launcherPath))
            {
                string customPath = Path.Combine(launcherPath, "Data", "installed_modpacks.json");
                if (File.Exists(customPath))
                    jsonPath = customPath;
            }
        }

        if (File.Exists(jsonPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                var modpacks = JsonConvert.DeserializeObject<List<ModpackInfo>>(jsonContent);

                if (modpacks != null)
                {
                    foreach (var pack in modpacks.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                    {
                        string fallbackBase = !string.IsNullOrWhiteSpace(launcherPath) ? launcherPath : PlatformPaths.DefaultLauncherPath();
                        string packPath = !string.IsNullOrWhiteSpace(pack.Path) ? pack.Path : Path.Combine(fallbackBase, "CLModpack", pack.Name);
                        string overridePath = Path.Combine(packPath, "override", "screenshots");
                        string overridesPath = Path.Combine(packPath, "overrides", "screenshots");
                        string directPath = Path.Combine(packPath, "screenshots");

                        string finalPath = Directory.Exists(overridePath) ? overridePath :
                                           Directory.Exists(overridesPath) ? overridesPath :
                                           directPath;


                        sources.Add(new ScreenshotSourceItem
                        {
                            Name = string.Format(LocalizationManager.GetString("Modpacks.ModpackFolder", "{0} (Збірка)"), pack.Name),
                            FullScreenshotsPath = finalPath
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Failed to read installed modpacks: {ex.Message}");
            }
        }

        return sources;
    }

    public async Task<List<ScreenshotItem>> LoadScreenshotsAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return new List<ScreenshotItem>();

        return await Task.Run(() =>
        {
            var list = new List<ScreenshotItem>();
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                var files = dirInfo.EnumerateFiles("*.png")
                                   .Concat(dirInfo.EnumerateFiles("*.jpg"))
                                   .Concat(dirInfo.EnumerateFiles("*.jpeg"))
                                   .OrderByDescending(f => f.CreationTime);

                foreach (var fileInfo in files)
                {
                    string resolution = LocalizationManager.GetString("Generic.Unknown", "Unknown");

                    try
                    {
                        using var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var br = new BinaryReader(fs);
                        byte[] sig = br.ReadBytes(8);
                        if (sig.Length == 8 && sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47)
                        {
                            fs.Seek(16, SeekOrigin.Begin);
                            byte[] wBytes = br.ReadBytes(4);
                            byte[] hBytes = br.ReadBytes(4);
                            if (BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(wBytes);
                                Array.Reverse(hBytes);
                            }
                            int width = BitConverter.ToInt32(wBytes, 0);
                            int height = BitConverter.ToInt32(hBytes, 0);
                            resolution = $"{width}x{height}";
                        }
                    }
                    catch
                    {
                    }


                    var bitmap = AvaloniaImageHelper.LoadOptimizedImage(fileInfo.FullName, 300);

                    if (bitmap != null)
                    {
                        long length = fileInfo.Length;
                        string sizeString = length >= 1048576
                            ? $"{length / 1048576.0:F2} MB"
                            : $"{length / 1024.0:F2} KB";

                        list.Add(new ScreenshotItem
                        {
                            FilePath = fileInfo.FullName,
                            FileName = fileInfo.Name,
                            CreationDate = fileInfo.CreationTime.ToString("dd.MM.yyyy HH:mm"),
                            FileSize = sizeString,
                            Resolution = resolution,
                            ImagePath = bitmap
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Error loading screenshots: {ex.Message}");
            }

            return list;
        });
    }

    public bool DeleteScreenshot(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenshotService] Failed to delete screenshot: {ex.Message}");
        }
        return false;
    }
}
