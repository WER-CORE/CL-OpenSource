using CL.Core.Helpers;
using CL.Core.Models;
using Newtonsoft.Json;
using System;
using System.IO;

namespace CL.Core.Services
{
    public static class SettingsManager
    {
        private static readonly object _fileLock = new object();
        public static event Action? SettingsReloaded;

        private static string? _customFolderPath;
        public static string FolderPath
        {
            get => _customFolderPath ?? PlatformPaths.DataFolder();
            set => _customFolderPath = value;
        }

        public static string FilePath => Path.Combine(FolderPath, "UserSaves.json");
        public static AppSettings Default { get; set; } = new AppSettings();

        public static void NotifySettingsReloaded()
        {
            SettingsReloaded?.Invoke();
        }

        public static void Load()
        {
            AppSettings? loadedSettings = null;
            lock (_fileLock)
            {
                if (File.Exists(FilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(FilePath);
                        loadedSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка читання налаштувань: {ex.Message}");
                        if (Default == null)
                        {
                            Default = new AppSettings();
                        }
                    }
                }
            }

            if (loadedSettings != null)
            {
                Default = loadedSettings;
                SettingsReloaded?.Invoke();
            }
        }

        public static void Save()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!Directory.Exists(FolderPath))
                    {
                        Directory.CreateDirectory(FolderPath);
                    }

                    string json = JsonConvert.SerializeObject(Default, Formatting.Indented);
                    AtomicFileWriter.WriteAllTextAtomic(FilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка збереження налаштувань: {ex.Message}");
                }
            }
        }
    }
}
