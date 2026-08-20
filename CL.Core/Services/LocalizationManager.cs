using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CL.Core.Services
{
    public static class LocalizationManager
    {
        private static Dictionary<string, string> _localizedStrings = new Dictionary<string, string>();

        public static string CurrentLanguage { get; private set; } = "uk_UA";

        public static void LoadLanguage(string langCode)
        {
            string localesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Locales");
            string filePath = Path.Combine(localesFolder, $"{langCode}.json");

            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(localesFolder, "uk_UA.json");
                if (File.Exists(filePath))
                {
                    LoadFile(filePath);
                }
                return;
            }

            if (LoadFile(filePath))
            {
                CurrentLanguage = langCode;
            }
        }

        private static bool LoadFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                _localizedStrings.Clear();
                if (dict != null)
                {
                    FlattenJson("", dict, _localizedStrings);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка читання перекладу: {ex.Message}");
                return false;
            }
        }

        private static void FlattenJson(string prefix, Dictionary<string, JsonElement> dict, Dictionary<string, string> target)
        {
            foreach (var kvp in dict)
            {
                string key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value.ValueKind == JsonValueKind.Object)
                {
                    var subDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(kvp.Value.GetRawText());
                    if (subDict != null)
                    {
                        FlattenJson(key, subDict, target);
                    }
                }
                else
                {
                    target[key] = kvp.Value.GetString() ?? "";
                }
            }
        }

        public static string GetString(string key, string fallback = "")
        {
            return _localizedStrings.TryGetValue(key, out string value) ? value : fallback;
        }

        public static bool IsLanguageFileExists(string langCode)
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Locales", $"{langCode}.json");
            return File.Exists(filePath);
        }
    }
}