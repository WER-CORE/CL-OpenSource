using System;
using System.Diagnostics;
using System.IO;

namespace CL.Core.Services
{

    public static class UrlOpener
    {
        public static Action<Exception, string>? ErrorHandler { get; set; }

        public static void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (OperatingSystem.IsLinux())
                {
                    OpenLinux(url);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    OpenMac(url);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UrlOpener] Error opening '{url}': {ex.Message}");
                ErrorHandler?.Invoke(ex, url);
            }
        }

        private static void OpenLinux(string target)
        {

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")))
            {
                if (TryRun("flatpak-spawn", "--host", "xdg-open", target)) return;
            }

            string[] launchers = { "xdg-open", "gio", "gnome-open", "kde-open5", "kde-open", "wslview" };
            foreach (var launcher in launchers)
            {
                if (launcher == "gio")
                {
                    if (TryRun("gio", "open", target)) return;
                }
                else
                {
                    if (TryRun(launcher, target)) return;
                }
            }

            if (Directory.Exists(target) || File.Exists(target))
            {
                string[] fileManagers = { "nautilus", "dolphin", "thunar", "pcmanfm" };
                foreach (var fm in fileManagers)
                {
                    if (TryRun(fm, target)) return;
                }
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        private static void OpenMac(string target)
        {
            if (TryRun("open", target)) return;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        private static bool TryRun(string command, params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo(command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }

                using var proc = Process.Start(psi);
                return proc != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
