using CL_CLegendary_Launcher_.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_.Class
{
    public class ScreenshotService
    {
        public List<ScreenshotSourceItem> GetScreenshotSources(string launcherPath)
        {
            var sources = new List<ScreenshotSourceItem>
            {
                new ScreenshotSourceItem
                {
                    Name = LocalizationManager.GetString("Modpacks.GlobalFolder", "Глобальні (.ClMinecraft)"),
                    FullScreenshotsPath = Path.Combine(launcherPath, "screenshots")
                }
            };

            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");

            if (!File.Exists(jsonPath)) return sources;

            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                var modpacks = JsonConvert.DeserializeObject<List<ModpackInfo>>(jsonContent);

                if (modpacks != null)
                {
                    foreach (var pack in modpacks.Where(p => !string.IsNullOrWhiteSpace(p.Path)))
                    {
                        string overridePath = Path.Combine(pack.Path, "override", "screenshots");
                        string overridesPath = Path.Combine(pack.Path, "overrides", "screenshots");

                        string finalPath = Directory.Exists(overridePath) ? overridePath :
                                           Directory.Exists(overridesPath) ? overridesPath : null;

                        if (finalPath != null)
                        {
                            sources.Add(new ScreenshotSourceItem
                            {
                                Name = string.Format(LocalizationManager.GetString("Modpacks.ModpackFolder", "{0} (Збірка)"), pack.Name),
                                FullScreenshotsPath = finalPath
                            });
                        }
                    }
                }
            }
            catch
            {
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
                var dirInfo = new DirectoryInfo(folderPath);

                var files = dirInfo.EnumerateFiles("*.png")
                                   .OrderByDescending(f => f.CreationTime);

                foreach (var fileInfo in files)
                {
                    string resolution = LocalizationManager.GetString("Generic.Unknown", "Unknown");
                    try
                    {
                        using (var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                            resolution = $"{frame.PixelWidth}x{frame.PixelHeight}";
                        }
                    }
                    catch { }

                    var bitmap = ImageHelper.LoadOptimizedImage(fileInfo.FullName, 300);

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
            catch { }
            return false;
        }
    }
}