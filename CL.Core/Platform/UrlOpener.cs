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
            bool isFlatpak = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID"));
            string fileUri = target;
            if (Path.IsPathRooted(target) && !target.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    fileUri = new Uri(target).AbsoluteUri;
                }
                catch
                {
                    fileUri = target;
                }
            }

            if (isFlatpak)
            {
                if (TryRunAndWaitSuccess("flatpak-spawn", "--host", "xdg-open", target)) return;
                if (fileUri != target && TryRunAndWaitSuccess("flatpak-spawn", "--host", "xdg-open", fileUri)) return;
                if (TryRunAndWaitSuccess("flatpak-spawn", "--host", "gio", "open", target)) return;
                if (fileUri != target && TryRunAndWaitSuccess("flatpak-spawn", "--host", "gio", "open", fileUri)) return;
            }

            if (TryRunAndWaitSuccess("xdg-open", target)) return;
            if (fileUri != target && TryRunAndWaitSuccess("xdg-open", fileUri)) return;
            if (TryRunAndWaitSuccess("gio", "open", target)) return;
            if (fileUri != target && TryRunAndWaitSuccess("gio", "open", fileUri)) return;

            bool isFile = File.Exists(target);
            bool isDir = Directory.Exists(target);

            if (isFile)
            {
                string ext = Path.GetExtension(target).ToLowerInvariant();
                if (ext is ".txt" or ".json" or ".log" or ".cfg" or ".ini" or ".md" or ".yml" or ".yaml")
                {
                    string[] editors = { "kwrite", "kate", "xed", "gedit", "mousepad", "pluma", "featherpad", "gnome-text-editor" };
                    foreach (var editor in editors)
                    {
                        if (isFlatpak && TryRun("flatpak-spawn", "--host", editor, target)) return;
                        if (TryRun(editor, target)) return;
                    }
                }
            }

            string[] launchers = { "gnome-open", "kde-open5", "kde-open", "wslview" };
            foreach (var launcher in launchers)
            {
                if (isFlatpak && TryRunAndWaitSuccess("flatpak-spawn", "--host", launcher, target)) return;
                if (TryRunAndWaitSuccess(launcher, target)) return;
            }

            if (isDir || isFile)
            {
                string dirToOpen = isDir ? target : (Path.GetDirectoryName(target) ?? target);
                string[] fileManagers = { "dolphin", "nemo", "nautilus", "thunar", "pcmanfm", "pcmanfm-qt", "caja" };
                foreach (var fm in fileManagers)
                {
                    if (isFlatpak && TryRun("flatpak-spawn", "--host", fm, dirToOpen)) return;
                    if (TryRun(fm, dirToOpen)) return;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch { }
        }

        private static void OpenMac(string target)
        {
            if (TryRunAndWaitSuccess("open", target)) return;
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch { }
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

        private static bool TryRunAndWaitSuccess(string command, params string[] args)
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
                if (proc == null) return false;

                if (proc.WaitForExit(1500))
                {
                    return proc.ExitCode == 0;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
