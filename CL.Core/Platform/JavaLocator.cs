using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CL.Core.Services
{
    public static class JavaLocator
    {
        public static string ExecutableName => OperatingSystem.IsWindows() ? "java.exe" : "java";

        public static string GameExecutableName => OperatingSystem.IsWindows() ? "javaw.exe" : "java";

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
                .ThenByDescending(j => string.Equals(j.Architecture, "x64", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(j.Architecture, "arm64", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(j => j.UpdateVersion)
                .ThenByDescending(j => j.BuildNumber)
                .ThenBy(j => j.Executable, StringComparer.Ordinal)
                .ToList();
        }

        public static List<string> FindInstalled(string? launcherPath)
            => Detect(launcherPath).Select(j => j.Executable).ToList();

        public static JavaInstallation? Inspect(string executable, string source = "manual")
        {
            if (!File.Exists(executable))
                return null;

            string? output = RunJava(executable, "-version");
            if (output == null)
                return null;

            var parsed = ParseVersionDetails(output);
            if (parsed == null)
                return null;

            return new JavaInstallation
            {
                Executable = executable,
                MajorVersion = parsed.Value.Major,
                MinorVersion = parsed.Value.Minor,
                UpdateVersion = parsed.Value.Update,
                BuildNumber = parsed.Value.Build,
                FullVersion = parsed.Value.FullVersion,
                Vendor = ParseVendor(output),
                Architecture = ParseArchitecture(output) ?? ProbeArchitecture(executable),
                Source = source
            };
        }

        private static string? RunJava(string executable, params string[] args)
            => RunProcessWithTimeout(executable, 5000, args);

        internal static string? RunProcessWithTimeout(string executable, int timeoutMs, params string[] args)
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

                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return null;
                }

                System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { stderrTask, stdoutTask }, 1000);

                string stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
                string stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty;

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

        internal static int? ParseMajorVersion(string text)
            => ParseVersionDetails(text)?.Major;

        internal static (int Major, int Minor, int Update, int Build, string FullVersion)? ParseVersionDetails(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string? rawVersion = null;
            foreach (var rawLine in text.Split('\n'))
            {
                if (!rawLine.Contains("version", StringComparison.OrdinalIgnoreCase))
                    continue;

                int start = rawLine.IndexOf('"');
                if (start >= 0)
                {
                    int end = rawLine.IndexOf('"', start + 1);
                    if (end > start)
                    {
                        rawVersion = rawLine.Substring(start + 1, end - start - 1).Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(rawVersion) && LooksLikeJavaOutput(text))
            {
                var bareMatch = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"\b(?:1\.8\.0(?:_\d+)?(?:-b\d+)?|8u\d+(?:-b\d+)?|\d+\.\d+\.\d+(?:[._+]\d+)?)\b",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (bareMatch.Success)
                    rawVersion = bareMatch.Value;
            }

            if (string.IsNullOrEmpty(rawVersion))
                return null;

            int build = ParseBuildNumber(text, rawVersion);

            var shortJava8 = System.Text.RegularExpressions.Regex.Match(
                rawVersion,
                @"^8u(\d+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (shortJava8.Success && int.TryParse(shortJava8.Groups[1].Value, out int u8))
            {
                return (8, 0, u8, build, rawVersion);
            }

            var legacyMatch = System.Text.RegularExpressions.Regex.Match(
                rawVersion,
                @"^1\.(\d+)\.(\d+)(?:_(\d+))?",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (legacyMatch.Success && int.TryParse(legacyMatch.Groups[1].Value, out int legacyMajor))
            {
                int.TryParse(legacyMatch.Groups[2].Value, out int legacyMinor);
                int legacyUpdate = 0;
                if (legacyMatch.Groups[3].Success)
                    int.TryParse(legacyMatch.Groups[3].Value, out legacyUpdate);
                return (legacyMajor, legacyMinor, legacyUpdate, build, rawVersion);
            }

            var modernMatch = System.Text.RegularExpressions.Regex.Match(
                rawVersion,
                @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (modernMatch.Success && int.TryParse(modernMatch.Groups[1].Value, out int major))
            {
                int minor = modernMatch.Groups[2].Success && int.TryParse(modernMatch.Groups[2].Value, out int m) ? m : 0;
                int update = modernMatch.Groups[3].Success && int.TryParse(modernMatch.Groups[3].Value, out int u) ? u : 0;
                return (major, minor, update, build, rawVersion);
            }

            return null;
        }

        private static int ParseBuildNumber(string fullOutput, string rawVersion)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                rawVersion,
                @"-b(\d+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int b1))
                return b1;

            var lineMatch = System.Text.RegularExpressions.Regex.Match(
                fullOutput,
                @"\(build\s+[^)]*-b(\d+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int b2))
                return b2;

            var plusMatch = System.Text.RegularExpressions.Regex.Match(
                fullOutput,
                @"\+(\d+)\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (plusMatch.Success && int.TryParse(plusMatch.Groups[1].Value, out int b3))
                return b3;

            return 0;
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

        private static bool LooksLikeJavaOutput(string text)
        {
            return text.Contains("java", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("openjdk", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Runtime Environment", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Server VM", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Client VM", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("HotSpot", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Corretto", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Temurin", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("GraalVM", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Zulu", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectCandidates(string root, SortedSet<string> output)
        {
            if (!Directory.Exists(root))
                return;

            CollectCandidatesRecursive(root, output, currentDepth: 0, maxDepth: 4);
        }

        private static void CollectCandidatesRecursive(string directory, SortedSet<string> output, int currentDepth, int maxDepth)
        {
            string binCandidate = Path.Combine(directory, "bin", ExecutableName);
            if (File.Exists(binCandidate))
                output.Add(binCandidate);

            string macCandidate = Path.Combine(directory, "Contents", "Home", "bin", ExecutableName);
            if (File.Exists(macCandidate))
                output.Add(macCandidate);

            if (currentDepth >= maxDepth)
                return;

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(directory);
            }
            catch
            {
                return;
            }

            foreach (var sub in subdirectories)
            {
                CollectCandidatesRecursive(sub, output, currentDepth + 1, maxDepth);
            }
        }
    }
}
