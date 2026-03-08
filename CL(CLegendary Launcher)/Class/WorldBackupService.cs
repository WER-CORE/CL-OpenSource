using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class WorldBackupInfo
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public DateTime CreationTime { get; set; }
        public long SizeBytes { get; set; }
        public string SizeString
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = SizeBytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }

    public static class WorldBackupService
    {
        private const string ID_FILE_NAME = "cl_backup_id.txt";
        public static string GetWorldID(string worldFolderPath)
        {
            string idFilePath = Path.Combine(worldFolderPath, ID_FILE_NAME);

            if (File.Exists(idFilePath))
            {
                try
                {
                    string id = File.ReadAllText(idFilePath).Trim();
                    if (!string.IsNullOrEmpty(id)) return id;
                }
                catch { }
            }

            string newID = DateTime.Now.Ticks.ToString();

            try
            {
                File.WriteAllText(idFilePath, newID);
                File.SetAttributes(idFilePath, File.GetAttributes(idFilePath) | FileAttributes.Hidden);
            }
            catch { }

            return newID;
        }
        private static string GetSmartBackupRoot(string worldFolderPath)
        {
            try
            {
                DirectoryInfo worldDir = new DirectoryInfo(worldFolderPath);

                DirectoryInfo savesDir = worldDir.Parent;
                if (savesDir == null) return null;

                DirectoryInfo instanceDir = savesDir.Parent;
                if (instanceDir == null) return null;

                if (instanceDir.Name.Equals("override", StringComparison.OrdinalIgnoreCase) ||
                    instanceDir.Name.Equals("overrides", StringComparison.OrdinalIgnoreCase))
                {
                    instanceDir = instanceDir.Parent;
                }

                string backupsDir = Path.Combine(instanceDir.FullName, "backups");

                if (!Directory.Exists(backupsDir))
                {
                    Directory.CreateDirectory(backupsDir);
                }

                return backupsDir;
            }
            catch
            {
                string globalBackups = Path.Combine(SettingsManager.Default.PathLacunher, "backups");
                if (!Directory.Exists(globalBackups)) Directory.CreateDirectory(globalBackups);
                return globalBackups;
            }
        }
        private static string GetSpecificBackupFolder(string worldFolderPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(worldFolderPath);
            if (!dirInfo.Exists) return null;

            string worldName = dirInfo.Name;
            string worldID = GetWorldID(worldFolderPath);

            string specificFolderName = $"{worldName}_{worldID}";

            string rootBackupsFolder = GetSmartBackupRoot(worldFolderPath);
            if (string.IsNullOrEmpty(rootBackupsFolder)) return null;

            string finalPath = Path.Combine(rootBackupsFolder, "worlds", specificFolderName);

            if (!Directory.Exists(finalPath)) Directory.CreateDirectory(finalPath);

            return finalPath;
        }
        public static async Task CreateWorldBackupAsync(string worldFolderPath)
        {
            await Task.Run(() =>
            {
                DirectoryInfo dirInfo = new DirectoryInfo(worldFolderPath);
                if (!dirInfo.Exists) throw new DirectoryNotFoundException(LocalizationManager.GetString("Backups.WorldNotFound", "Світ не знайдено!"));

                string targetFolder = GetSpecificBackupFolder(worldFolderPath);
                if (targetFolder == null) return;

                string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string zipName = $"{dirInfo.Name}_{timeStamp}.zip";
                string finalZipPath = Path.Combine(targetFolder, zipName);
                string tempZipPath = finalZipPath + ".tmp";

                try
                {
                    CreateZipFromWorld(worldFolderPath, tempZipPath);

                    if (File.Exists(finalZipPath)) File.Delete(finalZipPath);
                    File.Move(tempZipPath, finalZipPath);
                }
                catch
                {
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                    throw;
                }
            });
        }
        public static async Task AutoBackupWorldAsync(string worldPath)
        {
            if (!SettingsManager.Default.EnableAutoBackup) return;

            await Task.Run(() =>
            {
                try
                {
                    var dirInfo = new DirectoryInfo(worldPath);
                    if (!dirInfo.Exists) return;

                    string targetFolder = GetSpecificBackupFolder(worldPath);
                    if (targetFolder == null) return;

                    string levelDat = Path.Combine(worldPath, "level.dat");
                    if (!File.Exists(levelDat)) return;

                    DateTime lastWorldChange = File.GetLastWriteTime(levelDat);

                    var existingAutoBackups = Directory.GetFiles(targetFolder, "[Auto]*")
                                                       .Select(f => new FileInfo(f))
                                                       .OrderByDescending(f => f.CreationTime)
                                                       .ToList();

                    if (existingAutoBackups.Count > 0 && existingAutoBackups[0].CreationTime >= lastWorldChange)
                    {
                        return;
                    }

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string zipName = $"[Auto] {timestamp}.zip";
                    string finalZipPath = Path.Combine(targetFolder, zipName);
                    string tempZipPath = finalZipPath + ".tmp";

                    CreateZipFromWorld(worldPath, tempZipPath);

                    if (File.Exists(finalZipPath)) File.Delete(finalZipPath);
                    File.Move(tempZipPath, finalZipPath);

                    int maxBackups = SettingsManager.Default.MaxAutoBackups;
                    if (maxBackups < 1) maxBackups = 1;

                    var allAutoBackups = Directory.GetFiles(targetFolder, "[Auto]*")
                                                  .Select(f => new FileInfo(f))
                                                  .OrderByDescending(f => f.CreationTime)
                                                  .ToList();

                    if (allAutoBackups.Count > maxBackups)
                    {
                        var toDelete = allAutoBackups.Skip(maxBackups).ToList();
                        foreach (var file in toDelete)
                        {
                            try { file.Delete(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoBackup Failed: {ex.Message}");
                }
            });
        }
        private static void CreateZipFromWorld(string sourcePath, string destinationZip)
        {
            using (FileStream zipToOpen = new FileStream(destinationZip, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (string filePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    FileInfo fi = new FileInfo(filePath);

                    if (fi.Name == "session.lock") continue;
                    if (fi.Name == ID_FILE_NAME) continue;

                    string relativePath = Path.GetRelativePath(sourcePath, filePath);

                    if (relativePath.StartsWith("backups", StringComparison.OrdinalIgnoreCase)) continue;

                    archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Fastest);
                }
            }
        }
        public static List<WorldBackupInfo> GetBackupsForWorld(string worldFolderPath)
        {
            string folder = GetSpecificBackupFolder(worldFolderPath);
            var list = new List<WorldBackupInfo>();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return list;

            foreach (var file in Directory.GetFiles(folder, "*.zip"))
            {
                var info = new FileInfo(file);
                list.Add(new WorldBackupInfo
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    CreationTime = info.CreationTime,
                    SizeBytes = info.Length
                });
            }

            return list.OrderByDescending(x => x.CreationTime).ToList();
        }
        public static async Task RestoreWorldBackupAsync(string zipPath, string savesFolderPath)
        {
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                string backupFolder = Path.GetDirectoryName(zipPath);

                string parentFolderName = new DirectoryInfo(backupFolder).Name;

                int lastUnderscore = parentFolderName.LastIndexOf('_');
                string realWorldName = (lastUnderscore > 0) ? parentFolderName.Substring(0, lastUnderscore) : parentFolderName;
                string originalID = (lastUnderscore > 0) ? parentFolderName.Substring(lastUnderscore + 1) : null;

                string targetWorldPath = Path.Combine(savesFolderPath, realWorldName);
                string trashPath = Path.Combine(savesFolderPath, $"{realWorldName}_OLD_{DateTime.Now.Ticks}");

                try
                {
                    if (Directory.Exists(targetWorldPath))
                    {
                        try
                        {
                            Directory.Move(targetWorldPath, trashPath);
                        }
                        catch (IOException)
                        {
                            throw new IOException(LocalizationManager.GetString("Backups.WorldRestoreLockError", "Не можу замінити файли світу. Можливо, гра ще запущена? Закрийте Minecraft."));
                        }
                    }

                    Directory.CreateDirectory(targetWorldPath);
                    ZipFile.ExtractToDirectory(zipPath, targetWorldPath);

                    if (originalID != null)
                    {
                        string idPath = Path.Combine(targetWorldPath, ID_FILE_NAME);
                        File.WriteAllText(idPath, originalID);
                        File.SetAttributes(idPath, File.GetAttributes(idPath) | FileAttributes.Hidden);
                    }

                    if (Directory.Exists(trashPath))
                    {
                        try { Directory.Delete(trashPath, true); } catch { }
                    }
                }
                catch
                {
                    if (Directory.Exists(targetWorldPath)) Directory.Delete(targetWorldPath, true);
                    if (Directory.Exists(trashPath)) Directory.Move(trashPath, targetWorldPath);
                    throw;
                }
            });
        }
    }
}