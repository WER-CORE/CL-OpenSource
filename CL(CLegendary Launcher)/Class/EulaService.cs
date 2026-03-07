using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class EulaConfig
    {
        [JsonProperty("last_updated")]
        public string LastUpdatedStr { get; set; }

        [JsonProperty("mascot_message")]
        public string MascotMessage { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonIgnore]
        public DateTime LastUpdated
        {
            get
            {
                if (DateTime.TryParseExact(LastUpdatedStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                    return date;
                return DateTime.MinValue;
            }
        }
    }

    public static class EulaService
    {
        public static async Task<EulaConfig> GetEulaAsync(string langCode = "uk_UA")
        {
            try
            {
                using HttpClient client = new HttpClient();

                string json = await client.GetStringAsync($"{Secrets.EulaUrl}?v={DateTime.Now.Ticks}");
                return JsonConvert.DeserializeObject<EulaConfig>(json);
            }
            catch
            {
                if (langCode != "uk_UA")
                    return await GetEulaAsync("uk_UA");

                return null;
            }
        }

        public static bool IsEulaOutdated(DateTime serverDate)
        {
            return SettingsManager.Default.EulaAcceptedDate < serverDate;
        }
    }
}