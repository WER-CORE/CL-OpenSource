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
        public static LegacyMigrationResult ImportFromFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new LegacyMigrationResult { Success = false, ErrorMessage = "Папка не існує." };
            }

            string dataDir = Directory.Exists(Path.Combine(folderPath, "Data"))
                ? Path.Combine(folderPath, "Data")
                : folderPath;

            if (!Directory.Exists(dataDir) || !Directory.EnumerateFileSystemEntries(dataDir).Any())
            {
                return new LegacyMigrationResult
                {
                    Success = false,
                    ErrorMessage = "У вибраній папці не знайдено даних для імпорту."
                };
            }

            var result = new LegacyMigrationResult { Success = true };
            string targetDataDir = PlatformPaths.DataFolder();
            if (!Directory.Exists(targetDataDir))
            {
                Directory.CreateDirectory(targetDataDir);
            }

            string legacyProfiles = Path.Combine(dataDir, "ProfilesManeger.json");
            string legacySettings = Path.Combine(dataDir, "UserSaves.json");
            string legacyModpacks = Path.Combine(dataDir, "installed_modpacks.json");

            if (File.Exists(legacyProfiles))
            {
                try
                {
                    var profileService = new ProfileManagerService();
                    var existingProfiles = profileService.LoadProfiles();

                    byte[] legacyBytes = File.ReadAllBytes(legacyProfiles);
                    List<ProfileItem>? importedProfiles = null;

                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            byte[] decrypted = ProtectedData.Unprotect(legacyBytes, null, DataProtectionScope.CurrentUser);
                            string json = Encoding.UTF8.GetString(decrypted);
                            importedProfiles = JsonConvert.DeserializeObject<List<ProfileItem>>(json);
                        }
                        catch { }
                    }

                    if (importedProfiles == null || importedProfiles.Count == 0)
                    {
                        try
                        {
                            string text = Encoding.UTF8.GetString(legacyBytes);
                            importedProfiles = JsonConvert.DeserializeObject<List<ProfileItem>>(text);
                        }
                        catch { }
                    }

                    if (importedProfiles != null && importedProfiles.Count > 0)
                    {
                        int addedCount = 0;
                        foreach (var p in importedProfiles)
                        {
                            if (!existingProfiles.Any(x => x.NameAccount == p.NameAccount && x.TypeAccount == p.TypeAccount))
                            {
                                existingProfiles.Add(p);
                                addedCount++;
                            }
                        }

                        profileService.SaveProfiles(existingProfiles);
                        result.ProfilesImported = addedCount > 0 ? addedCount : importedProfiles.Count;
                    }
                    else
                    {
                        string targetProfilesPath = Path.Combine(targetDataDir, "ProfilesManeger.json");
                        if (!File.Exists(targetProfilesPath) || new FileInfo(targetProfilesPath).Length == 0)
                        {
                            File.Copy(legacyProfiles, targetProfilesPath, true);
                            result.ProfilesImported = 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Profile migration error: {ex.Message}");
                }
            }

            if (File.Exists(legacySettings))
            {
                try
                {
                    string targetSettingsPath = Path.Combine(targetDataDir, "UserSaves.json");
                    if (!File.Exists(targetSettingsPath))
                    {
                        File.Copy(legacySettings, targetSettingsPath, true);
                        SettingsManager.Load();
                        result.SettingsImported = true;
                    }
                    else
                    {
                        string oldJson = File.ReadAllText(legacySettings);
                        var oldSettings = JsonConvert.DeserializeObject<AppSettings>(oldJson);
                        if (oldSettings != null)
                        {
                            if (SettingsManager.Default.SelectIndexAccount < 0 && oldSettings.SelectIndexAccount >= 0)
                                SettingsManager.Default.SelectIndexAccount = oldSettings.SelectIndexAccount;
                            if (SettingsManager.Default.OP <= 0 && oldSettings.OP > 0)
                                SettingsManager.Default.OP = oldSettings.OP;
                            if (string.IsNullOrWhiteSpace(SettingsManager.Default.LastSelectedVersion) && !string.IsNullOrWhiteSpace(oldSettings.LastSelectedVersion))
                                SettingsManager.Default.LastSelectedVersion = oldSettings.LastSelectedVersion;

                            SettingsManager.Save();
                            result.SettingsImported = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Settings migration error: {ex.Message}");
                }
            }

            if (File.Exists(legacyModpacks))
            {
                try
                {
                    string targetModpacksPath = Path.Combine(targetDataDir, "installed_modpacks.json");
                    if (!File.Exists(targetModpacksPath))
                    {
                        File.Copy(legacyModpacks, targetModpacksPath, true);
                        result.ModpacksImported = 1;
                    }
                    else
                    {
                        string oldJson = File.ReadAllText(legacyModpacks);
                        var oldPacks = JsonConvert.DeserializeObject<List<InstalledModpack>>(oldJson);
                        string currJson = File.ReadAllText(targetModpacksPath);
                        var currPacks = JsonConvert.DeserializeObject<List<InstalledModpack>>(currJson) ?? new List<InstalledModpack>();

                        if (oldPacks != null && oldPacks.Count > 0)
                        {
                            int added = 0;
                            foreach (var p in oldPacks)
                            {
                                if (!currPacks.Any(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    currPacks.Add(p);
                                    added++;
                                }
                            }
                            if (added > 0)
                            {
                                File.WriteAllText(targetModpacksPath, JsonConvert.SerializeObject(currPacks, Formatting.Indented));
                                result.ModpacksImported = added;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modpacks migration error: {ex.Message}");
                }
            }

            try
            {
                var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ProfilesManeger.json",
                    "UserSaves.json",
                    "installed_modpacks.json"
                };
                result.FilesImported = CopyDirectoryContents(dataDir, targetDataDir, skip);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Data directory copy error: {ex.Message}");
            }

            return result;
        }

        private static int CopyDirectoryContents(string sourceDir, string targetDir, HashSet<string> skipFiles)
        {
            int count = 0;
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (skipFiles.Contains(fileName))
                    continue;

                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
                count++;
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(targetDir, dirName);
                count += CopyDirectoryContents(subDir, destSubDir, new HashSet<string>());
            }

            return count;
        }
    }
}
