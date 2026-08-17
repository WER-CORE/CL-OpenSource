using CL_CLegendary_Launcher_.Models;
using Newtonsoft.Json;
using System;
using System.IO;

namespace CL_CLegendary_Launcher_.Class
{
    public static class SettingsManager
    {
        private static readonly string FolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly string FilePath = Path.Combine(FolderPath, "UserSaves.json");
        public static AppSettings Default { get; set; } = new AppSettings();
        public static void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var loadedSettings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (loadedSettings != null)
                    {
                        Default = loadedSettings;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка читання налаштувань: {ex.Message}");
                    Default = new AppSettings();
                }
            }
        }
        public static void Save()
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }

                string json = JsonConvert.SerializeObject(Default, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження налаштувань: {ex.Message}");
            }
        }
    }
}