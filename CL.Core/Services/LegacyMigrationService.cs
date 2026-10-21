using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CL.Core.Models;
using Newtonsoft.Json;

namespace CL.Core.Services
{
    public class LegacyMigrationResult
    {
        public bool Success { get; set; }
        public int ProfilesImported { get; set; }
        public bool SettingsImported { get; set; }
        public int ModpacksImported { get; set; }
        public int FilesImported { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class LegacyMigrationService
    {
        public static LegacyMigrationResult ImportFromFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = "Папка не існує." };
            }

            string dataDir = Directory.Exists(Path.Combine(folderPath, "Data"))
                ? Path.Combine(folderPath, "Data")
                : folderPath;

            string targetDataDir = PlatformPaths.DataFolder();
            if (string.Equals(Path.GetFullPath(dataDir).TrimEnd('\\', '/'), Path.GetFullPath(targetDataDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return new LegacyMigrationResult
                {
                    Success = false,
                    ErrorMessage = "Вибрана папка вже є поточною папкою Data лаунчера."
                };
            }

            if (!Directory.Exists(dataDir) || !Directory.EnumerateFileSystemEntries(dataDir).Any())
            {
                return new LegacyMigrationResult
                {
                    Success = false,
                    ErrorMessage = "У вибраній папці не знайдено даних для імпорту."
                };
            }

            if (!Directory.Exists(targetDataDir))
            {
                Directory.CreateDirectory(targetDataDir);
            }

            var result = new LegacyMigrationResult { Success = true };

            List<ProfileItem>? existingProfiles = null;
            try
            {
                var profileService = new ProfileManagerService();
                existingProfiles = profileService.LoadProfiles();
            }
            catch { }

            result.FilesImported = CopyDirectoryContents(dataDir, targetDataDir);

            string targetProfiles = Path.Combine(targetDataDir, "ProfilesManeger.json");
            if (File.Exists(targetProfiles))
            {
                try
                {
                    var profileService = new ProfileManagerService();
                    var loadedProfiles = profileService.LoadProfiles();

                    if (existingProfiles != null && existingProfiles.Count > 0)
                    {
                        bool addedAny = false;
                        foreach (var p in existingProfiles)
                        {
                            if (!loadedProfiles.Any(x => x.NameAccount == p.NameAccount && x.TypeAccount == p.TypeAccount))
                            {
                                loadedProfiles.Add(p);
                                addedAny = true;
                            }
                        }

                        if (addedAny)
                        {
                            profileService.SaveProfiles(loadedProfiles);
                        }
                    }

                    result.ProfilesImported = loadedProfiles.Count;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Profile refresh error: {ex.Message}");
                }
            }

            string targetSettings = Path.Combine(targetDataDir, "UserSaves.json");
            if (File.Exists(targetSettings))
            {
                try
                {
                    SettingsManager.Load();
                    result.SettingsImported = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Settings reload error: {ex.Message}");
                }
            }

            string targetModpacks = Path.Combine(targetDataDir, "installed_modpacks.json");
            if (File.Exists(targetModpacks))
            {
                try
                {
                    string json = File.ReadAllText(targetModpacks);
                    var packs = JsonConvert.DeserializeObject<List<InstalledModpack>>(json);
                    result.ModpacksImported = packs?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modpacks count error: {ex.Message}");
                }
            }

            return result;
        }

        private static int CopyDirectoryContents(string sourceDir, string targetDir)
        {
            int count = 0;
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetDir, fileName);
                    File.Copy(file, destFile, true);
                    count++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying file {file}: {ex.Message}");
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                try
                {
                    string dirName = Path.GetFileName(subDir);
                    string destSubDir = Path.Combine(targetDir, dirName);
                    count += CopyDirectoryContents(subDir, destSubDir);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error copying directory {subDir}: {ex.Message}");
                }
            }

            return count;
        }
    }
}
