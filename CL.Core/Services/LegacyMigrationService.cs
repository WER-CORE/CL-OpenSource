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

    public class LegacyDataInspectionResult
    {
        public bool IsValid { get; set; }
        public string? ResolvedDataPath { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasSettings { get; set; }
        public int ProfilesCount { get; set; }
        public int ModpacksCount { get; set; }
        public int SupportedFilesCount { get; set; }
        public List<string> CorruptedFiles { get; set; } = new List<string>();
        public List<string> Conflicts { get; set; } = new List<string>();
    }

    public static class LegacyMigrationService
    {
        private static readonly HashSet<string> SupportedDataFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UserSaves.json",
            "ProfilesManeger.json",
            "installed_modpacks.json",
            "LastActions.json",
            "server_cache.json",
            "knowledge_base.json",
            "cml_token_cache.bin"
        };

        public static string? FindLegacyWpfDataCandidate(string? targetDataDir = null)
        {
            targetDataDir ??= SettingsManager.FolderPath ?? PlatformPaths.DataFolder();
            var candidateFolders = new List<string>();

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    string appDataCl = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        ".ClMinecraft",
                        "Data");
                    candidateFolders.Add(appDataCl);

                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (Directory.Exists(desktop))
                    {
                        foreach (string dir in Directory.GetDirectories(desktop, "CL*"))
                        {
                            candidateFolders.Add(Path.Combine(dir, "Data"));
                        }
                    }
                }

                string? baseParent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppDomain.CurrentDomain.BaseDirectory));
                if (!string.IsNullOrEmpty(baseParent) && Directory.Exists(baseParent))
                {
                    foreach (string dir in Directory.GetDirectories(baseParent, "CL*"))
                    {
                        candidateFolders.Add(Path.Combine(dir, "Data"));
                    }
                }
            }
            catch
            {
            }

            foreach (string candidate in candidateFolders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (PlatformPaths.PathsEqual(candidate, targetDataDir))
                    continue;

                if (!Directory.Exists(candidate))
                    continue;

                bool hasMarkers = File.Exists(Path.Combine(candidate, "UserSaves.json"))
                               || File.Exists(Path.Combine(candidate, "ProfilesManeger.json"))
                               || File.Exists(Path.Combine(candidate, "installed_modpacks.json"));

                if (hasMarkers)
                {
                    var inspection = InspectLegacyDataFolder(candidate, targetDataDir);
                    if (inspection.IsValid && inspection.CorruptedFiles.Count == 0)
                        return inspection.ResolvedDataPath;
                }
            }

            return null;
        }

        public static LegacyDataInspectionResult InspectLegacyDataFolder(string folderPath, string? targetDataDir = null)
        {
            var result = new LegacyDataInspectionResult();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                result.ErrorMessage = "Вибрана папка не існує.";
                return result;
            }

            string dataDir = Directory.Exists(Path.Combine(folderPath, "Data"))
                ? Path.Combine(folderPath, "Data")
                : folderPath;

            targetDataDir ??= SettingsManager.FolderPath ?? PlatformPaths.DataFolder();
            if (PlatformPaths.PathsEqual(dataDir, targetDataDir))
            {
                result.ErrorMessage = "Вибрана папка вже є поточною папкою Data лаунчера.";
                return result;
            }

            result.ResolvedDataPath = dataDir;

            try
            {
                foreach (string fileName in SupportedDataFiles)
                {
                    string filePath = Path.Combine(dataDir, fileName);
                    if (!File.Exists(filePath))
                        continue;

                    result.SupportedFilesCount++;

                    if (File.Exists(Path.Combine(targetDataDir, fileName)))
                    {
                        result.Conflicts.Add(fileName);
                    }

                    if (fileName.Equals("UserSaves.json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string json = File.ReadAllText(filePath);
                            var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                            if (settings == null)
                                result.CorruptedFiles.Add(fileName);
                            else
                                result.HasSettings = true;
                        }
                        catch
                        {
                            result.CorruptedFiles.Add(fileName);
                        }
                    }
                    else if (fileName.Equals("ProfilesManeger.json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(filePath);
                            string? key = null;
                            string settingsPath = Path.Combine(dataDir, "UserSaves.json");
                            if (File.Exists(settingsPath))
                            {
                                try
                                {
                                    key = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(settingsPath))?.EncryptKey;
                                }
                                catch { }
                            }
                            var profiles = ProfileManagerService.TryDecodeProfilesBytes(bytes, key ?? SettingsManager.Default.EncryptKey);
                            if (profiles == null)
                                result.CorruptedFiles.Add(fileName);
                            else
                                result.ProfilesCount = profiles.Count;
                        }
                        catch
                        {
                            result.CorruptedFiles.Add(fileName);
                        }
                    }
                    else if (fileName.Equals("installed_modpacks.json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string json = File.ReadAllText(filePath);
                            var modpacks = JsonConvert.DeserializeObject<List<InstalledModpack>>(json);
                            if (modpacks == null)
                                result.CorruptedFiles.Add(fileName);
                            else
                                result.ModpacksCount = modpacks.Count;
                        }
                        catch
                        {
                            result.CorruptedFiles.Add(fileName);
                        }
                    }
                    else if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(filePath));
                        }
                        catch
                        {
                            result.CorruptedFiles.Add(fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Помилка доступу до папки: {ex.Message}";
                return result;
            }

            if (result.SupportedFilesCount == 0)
            {
                result.ErrorMessage = "У вибраній папці не знайдено підтримуваних файлів Clegendary Launcher (UserSaves.json, ProfilesManeger.json, installed_modpacks.json).";
                return result;
            }

            result.IsValid = true;
            return result;
        }

        public static string? CreateDataBackup(string? targetDataDir = null)
        {
            targetDataDir ??= SettingsManager.FolderPath ?? PlatformPaths.DataFolder();
            if (!Directory.Exists(targetDataDir))
                return null;

            string backupDir = Path.Combine(Path.GetTempPath(), $"cl_data_backup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupDir);
            CopyDirectoryContents(targetDataDir, backupDir);
            return backupDir;
        }

        public static bool RestoreDataBackup(string? backupDir, string? targetDataDir = null)
        {
            if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                return false;

            targetDataDir ??= SettingsManager.FolderPath ?? PlatformPaths.DataFolder();
            try
            {
                if (!Directory.Exists(targetDataDir))
                    Directory.CreateDirectory(targetDataDir);
                CopyDirectoryContents(backupDir, targetDataDir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static LegacyMigrationResult ImportSupportedDataSafely(string folderPath, string? targetDataDir = null)
        {
            targetDataDir ??= SettingsManager.FolderPath ?? PlatformPaths.DataFolder();
            var inspection = InspectLegacyDataFolder(folderPath, targetDataDir);
            if (!inspection.IsValid || string.IsNullOrEmpty(inspection.ResolvedDataPath))
            {
                return new LegacyMigrationResult
                {
                    Success = false,
                    ErrorMessage = inspection.ErrorMessage ?? "Некоректна папка Data."
                };
            }

            if (inspection.CorruptedFiles.Count > 0)
            {
                return new LegacyMigrationResult
                {
                    Success = false,
                    ErrorMessage = "Виявлено пошкоджені файли JSON: " + string.Join(", ", inspection.CorruptedFiles)
                };
            }

            string? backupDir = null;
            try
            {
                backupDir = CreateDataBackup(targetDataDir);
                string stagingDir = Path.Combine(Path.GetTempPath(), $"cl_import_stage_{Guid.NewGuid():N}");
                Directory.CreateDirectory(stagingDir);
                try
                {
                    foreach (string fileName in SupportedDataFiles)
                    {
                        string src = Path.Combine(inspection.ResolvedDataPath, fileName);
                        if (File.Exists(src))
                        {
                            File.Copy(src, Path.Combine(stagingDir, fileName), true);
                        }
                    }

                    var result = ImportFromFolder(stagingDir, targetDataDir);
                    if (!result.Success && backupDir != null)
                    {
                        RestoreDataBackup(backupDir, targetDataDir);
                    }
                    return result;
                }
                finally
                {
                    try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (backupDir != null)
                {
                    RestoreDataBackup(backupDir, targetDataDir);
                }
                return new LegacyMigrationResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                try { if (backupDir != null && Directory.Exists(backupDir)) Directory.Delete(backupDir, true); } catch { }
            }
        }

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

                bool preserveAvaloniaTour = SettingsManager.Default.OnboardingTourCompleted;
                if (preserveAvaloniaTour || importedSettings.OnboardingTourCompleted)
                {
                    importedSettings.OnboardingTourCompleted = true;
                    importedSettings.TutorialComplete = true;
                }
                else
                {
                    importedSettings.TutorialComplete = false;
                    importedSettings.OnboardingTourCompleted = false;
                }

                if (SettingsManager.Default.WpfImportPromptHandled)
                {
                    importedSettings.WpfImportPromptHandled = true;
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
