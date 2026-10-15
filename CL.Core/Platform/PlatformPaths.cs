using System;
using System.IO;

namespace CL.Core.Services
{
    public static class PlatformPaths
    {
        public static string DefaultLauncherPath()
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ClMinecraft");

            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CLMinecraft");

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clminecraft");
        }

        // На Windows дані лишаються поруч із лаунчером, як і раніше. На Linux
        // і macOS каталог програми зазвичай доступний лише для читання
        // (/opt, AppImage), тож дані живуть у профілі користувача.
        public static string AppDataDirectory()
        {
            if (OperatingSystem.IsWindows())
                return AppDomain.CurrentDomain.BaseDirectory;

            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CLLauncher");

            string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            return Path.Combine(dataHome, "cl-launcher");
        }

        public static string DataFolder() => Path.Combine(AppDataDirectory(), "Data");

        public static string LocalesFolder() => Path.Combine(AppDataDirectory(), "Locales");

        // Регістр важливий лише на Linux: .clminecraft і .ClMinecraft - різні каталоги
        public static bool PathsEqual(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            var comparison = OperatingSystem.IsLinux()
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return string.Equals(Normalize(left), Normalize(right), comparison);
        }

        private static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        public static string OsName()
        {
            if (OperatingSystem.IsWindows()) return "Windows";
            if (OperatingSystem.IsLinux()) return "Linux";
            if (OperatingSystem.IsMacOS()) return "macOS";
            return "Unknown";
        }
    }
}
