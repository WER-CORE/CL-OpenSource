using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public static class AuthlibInjectorHelper
    {
        private const string PrimaryUrl = "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.8/authlib-injector-1.2.8.jar";
        private const string MirrorUrl = "https://authlib-injector.yushijinhun.com/artifact/53/authlib-injector-1.2.8.jar";

        public static string GetJarPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "authlib-injector-1.2.8.jar");
        }

        public static async Task EnsureDownloadedAsync(string? targetPath = null)
        {
            string path = targetPath ?? GetJarPath();
            if (File.Exists(path) && new FileInfo(path).Length > 10000)
            {
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                byte[]? data = null;
                try
                {
                    data = await client.GetByteArrayAsync(MirrorUrl).ConfigureAwait(false);
                }
                catch
                {
                    data = await client.GetByteArrayAsync(PrimaryUrl).ConfigureAwait(false);
                }

                if (data != null && data.Length > 0)
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    await File.WriteAllBytesAsync(path, data).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthlibInjectorHelper] Failed to download authlib-injector: {ex.Message}");
            }
        }

        public static void EnsureDownloaded(string? targetPath = null)
        {
            EnsureDownloadedAsync(targetPath).GetAwaiter().GetResult();
        }
    }
}
