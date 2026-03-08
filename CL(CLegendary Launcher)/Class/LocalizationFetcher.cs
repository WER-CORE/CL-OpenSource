using CL_CLegendary_Launcher_.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public static class LocalizationFetcher
    {
        public static string LocalesFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Locales");

        public static async Task<List<LanguageItem>> GetAvailableLanguagesAsync()
        {
            try
            {
                if (!Directory.Exists(LocalesFolder))
                    Directory.CreateDirectory(LocalesFolder);

                using HttpClient client = new HttpClient();
                string json = await client.GetStringAsync($"{Secrets.LocalizationURL}?v={DateTime.Now.Ticks}");
                return JsonConvert.DeserializeObject<List<LanguageItem>>(json) ?? new List<LanguageItem>();
            }
            catch
            {
                return new List<LanguageItem>
                {
                    new LanguageItem { Code = "uk_UA", Name = "Українська", Progress = 100, FlagUrl = "https://flagcdn.com/w40/ua.png" },
                    new LanguageItem { Code = "en_US", Name = "English", Progress = 0, FlagUrl = "https://flagcdn.com/w40/us.png" }
                };
            }
        }

        public static async Task<bool> DownloadLanguageAsync(LanguageItem lang)
        {
            try
            {
                using HttpClient client = new HttpClient();
                string jsonContent = await client.GetStringAsync($"{lang.DownloadUrl}?v={DateTime.Now.Ticks}");

                string filePath = Path.Combine(LocalesFolder, $"{lang.Code}.json");
                File.WriteAllText(filePath, jsonContent);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження мови {lang.Code}: {ex.Message}");
                return false;
            }
        }
    }
}