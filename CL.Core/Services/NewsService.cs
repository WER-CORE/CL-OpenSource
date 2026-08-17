using CL_CLegendary_Launcher_.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class NewsService
    {
        public async Task<List<NewsItem>> GetNewsAsync()
        {
            try
            {
                string json = await WebHelper.Client.GetStringAsync(Secrets.NewsUrl);

                var news = JsonConvert.DeserializeObject<List<NewsItem>>(json);
                return news ?? new List<NewsItem>();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(LocalizationManager.GetString("News.DownloadError", "Помилка завантаження новин: {0}"), ex.Message));
            }
        }
    }
}