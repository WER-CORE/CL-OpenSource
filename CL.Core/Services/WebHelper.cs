using System;
using System.Net.Http;

namespace CL.Core.Services
{
    public static class WebHelper
    {
        private static readonly SocketsHttpHandler Handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };

        public static readonly HttpClient Client = new HttpClient(Handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

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
