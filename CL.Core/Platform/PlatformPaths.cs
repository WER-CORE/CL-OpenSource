using CL.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CL.Core.Services
{
    public static class PlatformPaths
    {
        public static string DefaultLauncherPath()
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");

            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CLMinecraft");

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clminecraft");
        }

        public static bool IsPortableRoot(string baseDir, out string portableRoot)
        {
            if (File.Exists(Path.Combine(baseDir, ".portable")) || File.Exists(Path.Combine(baseDir, "portable.mode")))
            {
                portableRoot = baseDir;
                return true;
            }

            try
            {
                string parent = Path.GetFullPath(Path.Combine(baseDir, ".."));
                if (File.Exists(Path.Combine(parent, ".portable")) || File.Exists(Path.Combine(parent, "portable.mode")))
                {
                    portableRoot = parent;
                    return true;
                }
            }
            catch { }

            portableRoot = string.Empty;
            return false;
        }

        public static string AppDataDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (IsPortableRoot(baseDir, out string portableRoot))
                return portableRoot;

            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");

            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CLLauncher");

            string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            return Path.Combine(dataHome, "cl-launcher");
        }

        public static string DataFolder() => Path.Combine(AppDataDirectory(), "Data");

        public static string LocalesFolder() => Path.Combine(AppDataDirectory(), "Locales");

        public static void MigrateLegacyAppData()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string appDataRoot = AppDataDirectory();

                var candidates = new List<string> { baseDir };
                try
                {
                    string parent = Path.GetFullPath(Path.Combine(baseDir, ".."));
                    if (!candidates.Any(c => PathsEqual(c, parent)))
                        candidates.Add(parent);
                }
                catch { }

                string targetData = DataFolder();
                string markerFile = Path.Combine(targetData, ".legacy_migrated");

                if (File.Exists(markerFile))
                    return;

                string targetProfiles = Path.Combine(targetData, "ProfilesManeger.json");
                if (File.Exists(targetProfiles) && new FileInfo(targetProfiles).Length > 0)
                {
                    try
                    {
                        File.WriteAllText(markerFile, DateTime.UtcNow.ToString("o"));
                    }
                    catch { }
                    return;
                }

                foreach (var candidate in candidates)
                {
                    if (PathsEqual(candidate, appDataRoot))
                        continue;

                    string candidateData = Path.Combine(candidate, "Data");
                    if (Directory.Exists(candidateData) && !PathsEqual(candidateData, targetData))
                    {
                        bool hasLegacyFiles = File.Exists(Path.Combine(candidateData, "ProfilesManeger.json"))
                                           || File.Exists(Path.Combine(candidateData, "UserSaves.json"))
                                           || File.Exists(Path.Combine(candidateData, "installed_modpacks.json"));

                        if (hasLegacyFiles)
                        {
                            var result = LegacyMigrationService.ImportFromFolder(candidateData);
                            if (result.Success)
                            {
                                try
                                {
                                    if (!Directory.Exists(targetData))
                                        Directory.CreateDirectory(targetData);
                                    File.WriteAllText(markerFile, DateTime.UtcNow.ToString("o"));
                                }
                                catch { }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MigrateLegacyAppData] Error: {ex.Message}");
            }
        }

        public static void MigrateLegacyAppData(string legacyRoot, string appDataRoot)
        {
            if (PathsEqual(legacyRoot, appDataRoot))
                return;

            foreach (var folder in new[] { "Data", "Locales" })
            {
                string source = Path.Combine(legacyRoot, folder);
                string target = Path.Combine(appDataRoot, folder);

                if (Directory.Exists(source) && !Directory.Exists(target))
                {
                    try
                    {
                        DirectoryHelper.CopyDirectoryParallel(source, target);
                    }
                    catch { }
                }
            }
        }

        public static void BackupUserDataForPreBeta()
        {
            string dataFolder = DataFolder();
            if (!Directory.Exists(dataFolder)) return;

            string backupFolder = Path.Combine(AppDataDirectory(), "Data_backup_pre_beta");
            try
            {
                DirectoryHelper.CopyDirectoryParallel(dataFolder, backupFolder);
            }
            catch { }
        }

        public static bool PathsEqual(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            var comparison = OperatingSystem.IsLinux()
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return string.Equals(Normalize(left), Normalize(right), comparison);
        }

        private static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        public static string OsName()
        {
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsLinux()) return "Linux";
            if (OperatingSystem.IsMacOS()) return "macOS";
            return "Unknown";
        }
    }
}
