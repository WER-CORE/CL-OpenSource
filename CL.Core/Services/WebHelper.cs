using System;

namespace CL_CLegendary_Launcher_.Class
{
    public static class WebHelper
    {
        public static readonly System.Net.Http.HttpClient Client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        static WebHelper()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("CL-Legendary-Launcher/1.0");
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "-")
                return;

            UrlOpener.Open(url);
        }
    }
}
