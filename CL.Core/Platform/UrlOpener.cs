using System;
using System.Diagnostics;

namespace CL.Core.Services
{
    // Ядро не показує діалогів - помилку віддаємо UI-шару через ErrorHandler
    public static class UrlOpener
    {
        public static Action<Exception, string>? ErrorHandler { get; set; }

        public static void Open(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (OperatingSystem.IsLinux())
                {
                    Start("xdg-open", url);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Start("open", url);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                ErrorHandler?.Invoke(ex, url);
            }
        }

        private static void Start(string launcher, string url)
        {
            Process.Start(new ProcessStartInfo(launcher, url)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
        }
    }
}
