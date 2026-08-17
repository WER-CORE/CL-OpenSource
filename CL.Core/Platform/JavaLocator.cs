using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CL_CLegendary_Launcher_.Class
{
    public static class JavaLocator
    {
        public static string ExecutableName => OperatingSystem.IsWindows() ? "java.exe" : "java";

        // javaw не відкриває консольне вікно - саме ним запускають гру
        public static string GameExecutableName => OperatingSystem.IsWindows() ? "javaw.exe" : "java";

        // Кожен кандидат перевіряється запуском, тому повертає лише робочі рантайми
        public static List<JavaInstallation> Detect(string? launcherPath = null)
        {
            var candidates = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var path in FromEnvironmentPath())
                candidates.Add(path);

            foreach (var variable in new[] { "JAVA_HOME", "JDK_HOME" })
            {
                string? home = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(home))
                    candidates.Add(Path.Combine(home, "bin", ExecutableName));
            }

            foreach (var root in StandardRoots(launcherPath))
                CollectCandidates(root, candidates);

            var canonical = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
                canonical.Add(Canonicalize(candidate));

            var found = new List<JavaInstallation>();
            foreach (var path in canonical)
            {
                var installation = Inspect(path, "system");
                if (installation != null)
                    found.Add(installation);
            }

            return found
                .OrderByDescending(j => j.MajorVersion)
                .ThenBy(j => j.Executable, StringComparer.Ordinal)
                .ToList();
        }

        public static List<string> FindInstalled(string? launcherPath)
            => Detect(launcherPath).Select(j => j.Executable).ToList();

        // null, якщо файла немає або це не робоча Java
        public static JavaInstallation? Inspect(string executable, string source = "manual")
        {
            if (!File.Exists(executable))
                return null;

            string? output = RunJava(executable, "-version");
            if (output == null)
                return null;

            int? major = ParseMajorVersion(output);
            if (major == null)
                return null;

            return new JavaInstallation
            {
                Executable = executable,
                MajorVersion = major.Value,
                Vendor = ParseVendor(output),
                Architecture = ParseArchitecture(output) ?? ProbeArchitecture(executable),
                Source = source
            };
        }

        private static string? RunJava(string executable, params string[] args)
        {
            try
            {
                var info = new ProcessStartInfo(executable)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in args)
                    info.ArgumentList.Add(arg);

                using var process = Process.Start(info);
                if (process == null)
                    return null;

                string stderr = process.StandardError.ReadToEnd();
                string stdout = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(true); } catch { }
                    return null;
                }

                // `java -version` історично пише у stderr
                return string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            }
            catch
            {
                return null;
            }
        }

        private static string? ProbeArchitecture(string executable)
        {
            string? output = RunJava(executable, "-XshowSettings:properties", "-version");
            if (output == null)
                return null;

            foreach (var line in output.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("os.arch = ", StringComparison.Ordinal))
                    return NormalizeArch(trimmed.Substring("os.arch = ".Length).Trim());
            }

            return null;
        }

        private static string NormalizeArch(string raw) => raw switch
        {
            "amd64" or "x86_64" => "x64",
            "i386" or "i686" => "x86",
            "aarch64" => "arm64",
            _ => raw
        };

        // "21.0.2" → 21, legacy "1.8.0_312" → 8
        internal static int? ParseMajorVersion(string text)
        {
            string? line = text.Split('\n').FirstOrDefault(l => l.Contains("version", StringComparison.Ordinal));
            if (line == null)
                return null;

            int start = line.IndexOf('"');
            if (start < 0)
                return null;

            int end = line.IndexOf('"', start + 1);
            if (end < 0)
                return null;

            var parts = line.Substring(start + 1, end - start - 1).Split('.');
            if (parts.Length == 0 || !int.TryParse(parts[0], out int first))
                return null;

            if (first != 1)
                return first;

            return parts.Length > 1 && int.TryParse(parts[1], out int second) ? second : null;
        }

        internal static string? ParseVendor(string text)
            => text.Split('\n')
                   .FirstOrDefault(l => l.Contains("Runtime Environment", StringComparison.Ordinal))
                   ?.Trim();

        internal static string? ParseArchitecture(string text)
        {
            string? line = text.Split('\n').FirstOrDefault(l => l.Contains("VM", StringComparison.Ordinal));
            if (line == null)
                return null;

            if (line.Contains("64-Bit", StringComparison.Ordinal)) return "x64";
            if (line.Contains("32-Bit", StringComparison.Ordinal)) return "x86";
            if (line.Contains("aarch64", StringComparison.Ordinal)) return "arm64";
            return null;
        }

        // Симлінк може бути на будь-якому рівні: і /usr/bin/java,
        // і java-25-openjdk → java-1.25.0-openjdk ведуть до одного рантайму
        private static string Canonicalize(string path)
        {
            try
            {
                string resolved = Path.GetFullPath(path);

                var file = new FileInfo(resolved);
                if (file.LinkTarget != null)
                    resolved = file.ResolveLinkTarget(true)?.FullName ?? resolved;

                string fileName = Path.GetFileName(resolved);
                string? directory = Path.GetDirectoryName(resolved);

                return directory == null
                    ? resolved
                    : Path.GetFullPath(Path.Combine(ResolveDirectory(directory), fileName));
            }
            catch
            {
                return path;
            }
        }

        private static string ResolveDirectory(string directory)
        {
            var info = new DirectoryInfo(directory);
            if (info.LinkTarget != null)
                return info.ResolveLinkTarget(true)?.FullName ?? directory;

            string? parent = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent) || parent == directory)
                return directory;

            string resolvedParent = ResolveDirectory(parent);
            return resolvedParent == parent
                ? directory
                : Path.Combine(resolvedParent, info.Name);
        }

        private static IEnumerable<string> FromEnvironmentPath()
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                yield break;

            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(dir, ExecutableName);
                }
                catch
                {
                    continue;
                }

                if (File.Exists(candidate))
                    yield return candidate;
            }
        }

        private static IEnumerable<string> StandardRoots(string? launcherPath)
        {
            if (!string.IsNullOrWhiteSpace(launcherPath))
                yield return Path.Combine(launcherPath, "runtime");

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (OperatingSystem.IsWindows())
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                yield return Path.Combine(pf, "Java");
                yield return Path.Combine(pf, "Eclipse Adoptium");
                yield return Path.Combine(pf, "Microsoft");
                yield return Path.Combine(pf, "AdoptOpenJDK");
                yield return Path.Combine(pf, "BellSoft");
                yield return Path.Combine(pf86, "Java");
                yield break;
            }

            if (OperatingSystem.IsMacOS())
            {
                yield return "/Library/Java/JavaVirtualMachines";
                yield return "/System/Library/Java/JavaVirtualMachines";
                yield return Path.Combine(home, "Library/Java/JavaVirtualMachines");
                yield break;
            }

            yield return "/usr/lib/jvm";
            yield return "/usr/lib64/jvm";
            yield return "/usr/java";
            yield return "/opt/java";
            yield return Path.Combine(home, ".sdkman/candidates/java");
            yield return Path.Combine(home, ".jdks");
        }

        private static void CollectCandidates(string root, SortedSet<string> output)
        {
            if (!Directory.Exists(root))
                return;

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(root);
            }
            catch
            {
                return;
            }

            foreach (var dir in directories)
            {
                Add(Path.Combine(dir, "bin", ExecutableName));

                // macOS: .../temurin-21.jdk/Contents/Home/bin/java
                Add(Path.Combine(dir, "Contents", "Home", "bin", ExecutableName));

                // Adoptium та рантайми Mojang тримають JDK на рівень глибше
                try
                {
                    foreach (var nested in Directory.GetDirectories(dir))
                    {
                        Add(Path.Combine(nested, "bin", ExecutableName));
                        Add(Path.Combine(nested, "Contents", "Home", "bin", ExecutableName));
                    }
                }
                catch
                {
                }
            }

            void Add(string candidate)
            {
                if (File.Exists(candidate))
                    output.Add(candidate);
            }
        }
    }
}
