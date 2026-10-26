using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        public static LegacyMigrationResult ImportFromFolder(string folderPath, string? targetDataDir = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = "Папка не існує." };
            }

            string dataDir = Directory.Exists(Path.Combine(folderPath, "Data"))
                ? Path.Combine(folderPath, "Data")
                : folderPath;

            targetDataDir ??= SettingsManager.FolderPath;
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

            string sourceSettings = Path.Combine(dataDir, "UserSaves.json");
            AppSettings? importedSettings = null;
            if (File.Exists(sourceSettings))
            {
                try
                {
                    string json = File.ReadAllText(sourceSettings);
                    importedSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse source UserSaves.json: {ex.Message}");
                }
            }

            List<ProfileItem>? importedProfiles = null;
            string sourceProfiles = Path.Combine(dataDir, "ProfilesManeger.json");
            if (File.Exists(sourceProfiles))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(sourceProfiles);
                    string? key = importedSettings?.EncryptKey ?? SettingsManager.Default.EncryptKey;
                    importedProfiles = ProfileManagerService.TryDecodeProfilesBytes(bytes, key);
                    if (importedProfiles != null)
                    {
                        ProfileManagerService.NormalizeProfiles(importedProfiles);
                    }
                }
                catch { }
            }

            result.FilesImported = CopyDirectoryContents(dataDir, targetDataDir);

            string targetSettings = Path.Combine(targetDataDir, "UserSaves.json");
            if (importedSettings != null)
            {
                if (string.IsNullOrWhiteSpace(importedSettings.EncryptKey))
                {
                    if (!string.IsNullOrWhiteSpace(SettingsManager.Default.EncryptKey))
                    {
                        importedSettings.EncryptKey = SettingsManager.Default.EncryptKey;
                    }
                    else
                    {
                        byte[] randomBytes = new byte[32];
                        RandomNumberGenerator.Fill(randomBytes);
                        importedSettings.EncryptKey = Convert.ToBase64String(randomBytes);
                    }
                }

                if (!string.IsNullOrWhiteSpace(SettingsManager.Default.UpdateChannel))
                {
                    importedSettings.UpdateChannel = SettingsManager.Default.UpdateChannel;
                }

                if (importedSettings.TutorialComplete)
                {
                    importedSettings.OnboardingTourCompleted = true;
                }

                SettingsManager.Default = importedSettings;
                SettingsManager.Save();
                result.SettingsImported = true;
                SettingsManager.NotifySettingsReloaded();
            }
            else if (File.Exists(targetSettings))
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

            string targetProfiles = Path.Combine(targetDataDir, "ProfilesManeger.json");
            try
            {
                var profileService = new ProfileManagerService(targetDataDir);

                if (importedProfiles != null && importedProfiles.Count > 0)
                {
                    profileService.SaveProfiles(importedProfiles);
                    result.ProfilesImported = importedProfiles.Count;
                }
                else if (File.Exists(targetProfiles))
                {
                    var loaded = profileService.LoadProfiles();
                    result.ProfilesImported = loaded.Count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Profile refresh error: {ex.Message}");
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

        public static LegacyMigrationResult ImportProfilesFromFile(string filePath, string? targetDataDir = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = "Файл не існує." };
            }

            targetDataDir ??= SettingsManager.FolderPath;
            if (!Directory.Exists(targetDataDir))
            {
                Directory.CreateDirectory(targetDataDir);
            }

            List<ProfileItem>? importedProfiles = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                importedProfiles = ProfileManagerService.TryDecodeProfilesBytes(bytes, SettingsManager.Default.EncryptKey);
            }
            catch (Exception ex)
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = $"Помилка читання файлу: {ex.Message}" };
            }

            if (importedProfiles == null || importedProfiles.Count == 0)
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = "У вибраному файлі не знайдено акаунтів для імпорту." };
            }

            ProfileManagerService.NormalizeProfiles(importedProfiles);

            var targetService = new ProfileManagerService(targetDataDir);
            var existingProfiles = targetService.LoadProfiles();

            int addedCount = 0;
            foreach (var imported in importedProfiles)
            {
                var existing = existingProfiles.FirstOrDefault(p => p.NameAccount == imported.NameAccount && p.TypeAccount == imported.TypeAccount);
                if (existing == null)
                {
                    existingProfiles.Add(imported);
                    addedCount++;
                }
                else
                {
                    existing.ImageUrl = imported.ImageUrl;
                    existing.AccessToken = imported.AccessToken;
                    existing.UUID = imported.UUID;
                    existing.LastAuthTime = imported.LastAuthTime;
                    addedCount++;
                }
            }

            targetService.SaveProfiles(existingProfiles);

            return new LegacyMigrationResult
            {
                Success = true,
                ProfilesImported = addedCount
            };
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

                    if (File.Exists(destFile))
                    {
                        var attrs = File.GetAttributes(destFile);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(destFile, attrs & ~FileAttributes.ReadOnly);
                        }
                    }

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
