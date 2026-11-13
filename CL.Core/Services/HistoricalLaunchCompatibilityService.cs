using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using CmlLib.Core;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services
{
    public sealed record HistoricalJavaRequirement(
        int MajorVersion,
        int MinVersion,
        int AdvisedMaxVersion,
        string? Component,
        bool UsesLegacyFix,
        bool HasX64OnlyOsxNatives,
        bool HasJvmLegacyMergeSort);

    public sealed record JavaSelectionResult(
        bool Success,
        JavaInstallation? Selected,
        bool UsesRosetta,
        string? DiagnosticMessage);

    public sealed record PlatformCompatibilityResult(
        bool IsCompatible,
        string? DiagnosticMessage);

    public sealed record MacOsNativeArchiveInspection(
        bool HasMandatoryLwjglArm64,
        bool HasMandatoryLwjglX64,
        bool HasAnyArm64Slice,
        bool HasAnyX64Slice);

    public static class HistoricalLaunchCompatibilityService
    {
        public const string LegacyFixMainClass = "uk.betacraft.legacyfix.LegacyFixLauncher";
        public const string DefaultBetaCraftNonProxyHosts = "textures.minecraft.net|sessionserver.mojang.com|api.mojang.com|localhost|127.*|[::1]";
        private const string LegacyMergeSortArg = "-Djava.util.Arrays.useLegacyMergeSort=true";

        public static string? LastDiagnosticSummary { get; set; }

        private const uint MachOMagic64Le = 0xfeedfacf;
        private const uint MachOMagic64Be = 0xcffaedfe;
        private const uint MachOMagic32Le = 0xfeedface;
        private const uint MachOMagic32Be = 0xcefaedfe;
        private const uint FatMagicBe = 0xcafebabe;
        private const uint FatMagicLe = 0xbebafeca;
        private const uint CpuTypeX86_64 = 0x01000007;
        private const uint CpuTypeArm64 = 0x0100000c;

        public static bool IsHistoricalVersion(string? versionId, string? versionJsonPath = null)
        {
            if (!string.IsNullOrWhiteSpace(versionJsonPath) && File.Exists(versionJsonPath))
            {
                try
                {
                    var req = ReadJavaRequirement(versionJsonPath);
                    if (req.UsesLegacyFix)
                        return true;
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(versionId))
                return false;

            string id = versionId.Trim().ToLowerInvariant();
            if (id.StartsWith("rd-") ||
                id.StartsWith("c0.") ||
                id.StartsWith("in-") ||
                id.StartsWith("inf-") ||
                id.StartsWith("a1.") ||
                id.StartsWith("b1."))
            {
                return true;
            }

            return false;
        }

        public static HistoricalJavaRequirement ReadJavaRequirement(string versionJsonPath, string? librariesDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(versionJsonPath) || !File.Exists(versionJsonPath))
            {
                return new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", false, true, false);
            }

            string jsonText = File.ReadAllText(versionJsonPath);
            var root = JObject.Parse(jsonText);

            string? mainClass = root["mainClass"]?.ToString();
            bool usesLegacyFix = string.Equals(mainClass, LegacyFixMainClass, StringComparison.Ordinal);

            int major = root["javaVersion"]?["majorVersion"]?.Value<int?>() ?? 8;
            int minVersion = root["javaVersion"]?["minVersion"]?.Value<int?>() ?? 5;
            int advisedMax = root["javaVersion"]?["advisedMaxVersion"]?.Value<int?>() ?? 8;
            string? component = root["javaVersion"]?["component"]?.ToString();

            bool hasJvmLegacyMergeSort = false;
            if (root["arguments"]?["jvm"] is JArray jvmArgs)
            {
                foreach (var item in jvmArgs)
                {
                    if (item.Type == JTokenType.String &&
                        string.Equals(item.ToString(), LegacyMergeSortArg, StringComparison.Ordinal))
                    {
                        hasJvmLegacyMergeSort = true;
                        break;
                    }
                }
            }

            bool hasX64OnlyOsxNatives = DetectX64OnlyOsxNatives(root, librariesDirectory);

            return new HistoricalJavaRequirement(
                MajorVersion: major,
                MinVersion: minVersion,
                AdvisedMaxVersion: advisedMax,
                Component: component,
                UsesLegacyFix: usesLegacyFix,
                HasX64OnlyOsxNatives: hasX64OnlyOsxNatives,
                HasJvmLegacyMergeSort: hasJvmLegacyMergeSort);
        }

        private static bool DetectX64OnlyOsxNatives(JObject root, string? librariesDirectory = null)
        {
            if (root["libraries"] is not JArray libraries)
                return false;

            foreach (var lib in libraries)
            {
                string? name = lib["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!name.Contains("lwjgl", StringComparison.OrdinalIgnoreCase) ||
                    !name.Contains(":2.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(librariesDirectory) && Directory.Exists(librariesDirectory))
                {
                    string? osxJarPath = TryResolveOsxNativeJarPath(lib, name, librariesDirectory!);
                    if (!string.IsNullOrWhiteSpace(osxJarPath) && File.Exists(osxJarPath))
                    {
                        try
                        {
                            using var fs = File.OpenRead(osxJarPath);
                            var inspection = InspectMacOsNativeArchive(fs);
                            return !inspection.HasMandatoryLwjglArm64;
                        }
                        catch
                        {
                            return true;
                        }
                    }
                }

                if (name.Contains("legacyfabric", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("aarch64", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static string? TryResolveOsxNativeJarPath(JToken lib, string mavenName, string librariesDirectory)
        {
            string? explicitPath = lib["downloads"]?["classifiers"]?["natives-osx"]?["path"]?.ToString();
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return Path.Combine(librariesDirectory, explicitPath.Replace('/', Path.DirectorySeparatorChar));
            }

            var parts = mavenName.Split(':');
            if (parts.Length >= 3)
            {
                string groupPath = parts[0].Replace('.', Path.DirectorySeparatorChar);
                string artifact = parts[1];
                string version = parts[2];
                string fileName = $"{artifact}-{version}-natives-osx.jar";
                return Path.Combine(librariesDirectory, groupPath, artifact, version, fileName);
            }

            return null;
        }

        public static MacOsNativeArchiveInspection InspectMacOsNativeArchive(Stream zipStream)
        {
            if (zipStream == null)
                throw new ArgumentNullException(nameof(zipStream));

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            bool hasLwjglArm64 = false;
            bool hasLwjglX64 = false;
            bool hasAnyArm64 = false;
            bool hasAnyX64 = false;
            Span<byte> header = stackalloc byte[88];

            foreach (var entry in archive.Entries)
            {
                string fileName = entry.Name;
                if (!fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith(".jnilib", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var stream = entry.Open();
                int read = ReadHeaderBytes(stream, header);
                if (read < 8)
                    continue;

                var (arm64, x64) = InspectMachOArchitectures(header[..read]);
                if (arm64) hasAnyArm64 = true;
                if (x64) hasAnyX64 = true;

                if (fileName.Contains("lwjgl", StringComparison.OrdinalIgnoreCase))
                {
                    if (arm64) hasLwjglArm64 = true;
                    if (x64) hasLwjglX64 = true;
                }
            }

            return new MacOsNativeArchiveInspection(hasLwjglArm64, hasLwjglX64, hasAnyArm64, hasAnyX64);
        }

        private static int ReadHeaderBytes(Stream stream, Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer[total..]);
                if (read <= 0)
                    break;
                total += read;
            }
            return total;
        }

        public static (bool HasArm64, bool HasX64) InspectMachOArchitectures(ReadOnlySpan<byte> header)
        {
            if (header.Length < 8)
                return (false, false);

            uint magicBe = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            uint magicLe = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);

            if (magicLe == MachOMagic64Le || magicLe == MachOMagic32Le)
            {
                uint cpuType = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4));
                return (cpuType == CpuTypeArm64, cpuType == CpuTypeX86_64);
            }

            if (magicBe == MachOMagic64Le || magicBe == MachOMagic32Le)
            {
                uint cpuType = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4, 4));
                return (cpuType == CpuTypeArm64, cpuType == CpuTypeX86_64);
            }

            if (magicBe == FatMagicBe)
            {
                uint nfat = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4, 4));
                if (nfat > 16) nfat = 16;

                bool hasArm64 = false;
                bool hasX64 = false;
                for (int i = 0; i < nfat; i++)
                {
                    int offset = 8 + (i * 20);
                    if (offset + 4 > header.Length)
                        break;

                    uint cpuType = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(offset, 4));
                    if (cpuType == CpuTypeArm64) hasArm64 = true;
                    if (cpuType == CpuTypeX86_64) hasX64 = true;
                }

                return (hasArm64, hasX64);
            }

            return (false, false);
        }

        public static JavaSelectionResult SelectCompatibleJava(
            IReadOnlyList<JavaInstallation> availableJavas,
            HistoricalJavaRequirement requirement,
            OSPlatform targetOs,
            Architecture targetArch,
            bool isRosettaAvailable = false)
        {
            if (availableJavas == null || availableJavas.Count == 0)
            {
                return new JavaSelectionResult(
                    false,
                    null,
                    false,
                    "Не знайдено жодної сумісної інсталяції Java. Для історичних версій Minecraft (Pre-Classic – Beta) рекомендується Java 8 (x64, u401+).");
            }

            bool isAppleSilicon = targetOs == OSPlatform.OSX && targetArch == Architecture.Arm64;
            bool requiresX64Natives = requirement.HasX64OnlyOsxNatives && isAppleSilicon;

            var archFiltered = availableJavas.Where(j =>
            {
                string arch = (j.Architecture ?? string.Empty).ToLowerInvariant();
                if (isAppleSilicon)
                {
                    if (requiresX64Natives)
                        return arch == "x64";

                    if (arch == "arm64")
                        return true;
                    if (arch == "x64")
                        return isRosettaAvailable;
                    return false;
                }

                if (targetArch == Architecture.X64)
                {
                    return string.IsNullOrEmpty(arch) || arch == "x64" || (targetOs == OSPlatform.Windows && arch == "x86");
                }

                return true;
            }).ToList();

            if (isAppleSilicon && requiresX64Natives)
            {
                if (!isRosettaAvailable)
                {
                    return new JavaSelectionResult(
                        false,
                        null,
                        false,
                        "Історичні версії Minecraft зі старими x64-only LWJGL 2 natives потребують Rosetta 2 та Java 8 (x64) на macOS Apple Silicon (ARM64).");
                }

                if (archFiltered.Count == 0)
                {
                    return new JavaSelectionResult(
                        false,
                        null,
                        false,
                        "На macOS Apple Silicon знайдено лише ARM64 Java, але ця версія містить лише x64 LWJGL 2 natives. Встановіть Java 8 (x64) для запуску через Rosetta 2.");
                }
            }

            if (archFiltered.Count == 0)
            {
                return new JavaSelectionResult(
                    false,
                    null,
                    false,
                    $"Знайдені інсталяції Java не відповідають архітектурі {targetArch} для історичної версії.");
            }

            var exactMajorMatches = archFiltered
                .Where(j => j.MajorVersion == requirement.MajorVersion)
                .OrderByDescending(j => GetArchitecturePriority(j.Architecture, targetOs, targetArch, requiresX64Natives))
                .ThenByDescending(j => j.UpdateVersion)
                .ThenByDescending(j => j.BuildNumber)
                .ToList();

            if (exactMajorMatches.Count > 0)
            {
                var bestMatch = exactMajorMatches[0];
                bool usesRosetta = isAppleSilicon && string.Equals(bestMatch.Architecture, "x64", StringComparison.OrdinalIgnoreCase);
                string? warning = null;
                if (requirement.MajorVersion == 8 && bestMatch.UpdateVersion > 0 && bestMatch.UpdateVersion < 401)
                {
                    warning = $"Виявлено застарілу Java {bestMatch.FullVersion ?? $"1.8.0_{bestMatch.UpdateVersion}"}. " +
                              "Для стабільної роботи TLS у LegacyFix рекомендується Java 8 Update 401 або новіша (u401+).";
                }

                return new JavaSelectionResult(true, bestMatch, usesRosetta, warning);
            }

            var inAdvisedRange = archFiltered
                .Where(j => j.MajorVersion >= requirement.MinVersion && j.MajorVersion <= requirement.AdvisedMaxVersion)
                .OrderByDescending(j => GetArchitecturePriority(j.Architecture, targetOs, targetArch, requiresX64Natives))
                .ThenByDescending(j => j.MajorVersion)
                .ThenByDescending(j => j.UpdateVersion)
                .ThenByDescending(j => j.BuildNumber)
                .FirstOrDefault();
            if (inAdvisedRange != null)
            {
                bool usesRosetta = isAppleSilicon && string.Equals(inAdvisedRange.Architecture, "x64", StringComparison.OrdinalIgnoreCase);
                return new JavaSelectionResult(true, inAdvisedRange, usesRosetta, null);
            }

            var incompatibleFound = archFiltered.OrderBy(j => j.MajorVersion).First();
            return new JavaSelectionResult(
                false,
                null,
                false,
                $"Не знайдено сумісної Java {requirement.MajorVersion} (знайдено лише Java {incompatibleFound.MajorVersion}, яка несумісна з історичним LWJGL 2 / LegacyFix). Встановіть Java 8 (x64, u401+).");
        }

        private static int GetArchitecturePriority(
            string? javaArch,
            OSPlatform targetOs,
            Architecture targetArch,
            bool requiresX64Natives)
        {
            string arch = (javaArch ?? string.Empty).ToLowerInvariant();
            if (targetOs == OSPlatform.OSX && targetArch == Architecture.Arm64)
            {
                if (requiresX64Natives)
                    return arch == "x64" ? 2 : 0;
                if (arch == "arm64")
                    return 2;
                if (arch == "x64")
                    return 1;
                return 0;
            }

            if (targetArch == Architecture.X64)
            {
                if (arch == "x64") return 2;
                if (string.IsNullOrEmpty(arch)) return 1;
                return 0; // x86 is lowest priority on 64-bit OS
            }

            return 1;
        }

        public static bool IsRosetta2Installed()
        {
            if (!OperatingSystem.IsMacOS())
                return false;

            if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
                return false;

            try
            {
                return Directory.Exists("/Library/Apple/usr/libexec/oah") ||
                       File.Exists("/Library/Apple/System/Library/LaunchDaemons/com.apple.oahd.plist");
            }
            catch
            {
                return false;
            }
        }

        public static OSPlatform GetCurrentOSPlatform()
        {
            if (OperatingSystem.IsWindows()) return OSPlatform.Windows;
            if (OperatingSystem.IsLinux()) return OSPlatform.Linux;
            if (OperatingSystem.IsMacOS()) return OSPlatform.OSX;
            return OSPlatform.Linux;
        }

        public static JavaSelectionResult ValidateExplicitJavaChoice(
            JavaInstallation explicitJava,
            HistoricalJavaRequirement requirement,
            OSPlatform targetOs,
            Architecture targetArch,
            bool isRosettaAvailable = false)
        {
            if (explicitJava == null)
                throw new ArgumentNullException(nameof(explicitJava));

            bool isAppleSilicon = targetOs == OSPlatform.OSX && targetArch == Architecture.Arm64;
            bool requiresX64Natives = requirement.HasX64OnlyOsxNatives && isAppleSilicon;

            if (requiresX64Natives && string.Equals(explicitJava.Architecture, "arm64", StringComparison.OrdinalIgnoreCase))
            {
                return new JavaSelectionResult(
                    false,
                    explicitJava,
                    false,
                    "Обрана користувачем ARM64 Java несумісна з x64-only LWJGL 2 natives. Оберіть x64 Java 8 через Rosetta 2.");
            }

            if (isAppleSilicon &&
                string.Equals(explicitJava.Architecture, "x64", StringComparison.OrdinalIgnoreCase) &&
                !isRosettaAvailable)
            {
                return new JavaSelectionResult(
                    false,
                    explicitJava,
                    false,
                    "Для обраної x64 Java 8 на Apple Silicon потрібна Rosetta 2.");
            }

            string? warning = null;
            if (explicitJava.MajorVersion == 8 && explicitJava.UpdateVersion > 0 && explicitJava.UpdateVersion < 401)
            {
                warning = $"Обрана користувачем Java {explicitJava.FullVersion ?? $"1.8.0_{explicitJava.UpdateVersion}"} старіша за u401; " +
                          "можливі попередження TLS у LegacyFix.";
            }
            else if (explicitJava.MajorVersion > requirement.AdvisedMaxVersion)
            {
                warning = $"Обрана користувачем Java {explicitJava.MajorVersion} новіша за рекомендовану Java {requirement.MajorVersion}.";
            }

            bool usesRosetta = isAppleSilicon && string.Equals(explicitJava.Architecture, "x64", StringComparison.OrdinalIgnoreCase);
            return new JavaSelectionResult(true, explicitJava, usesRosetta, warning);
        }

        public static string ResolveHistoricalGameDirectory(string basePath, string? versionId = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path cannot be empty.", nameof(basePath));

            string fullBasePath = Path.GetFullPath(basePath);
            string historicalRoot = Path.GetFullPath(Path.Combine(fullBasePath, "historical"));
            Directory.CreateDirectory(historicalRoot);

            if (string.IsNullOrWhiteSpace(versionId))
                return historicalRoot;

            string safeSegment = SanitizeVersionDirectoryName(versionId);
            string candidate = Path.GetFullPath(Path.Combine(historicalRoot, safeSegment));

            string rootWithSep = historicalRoot.EndsWith(Path.DirectorySeparatorChar)
                ? historicalRoot
                : historicalRoot + Path.DirectorySeparatorChar;
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!candidate.StartsWith(rootWithSep, pathComparison))
                throw new InvalidOperationException($"Небезпечний ідентифікатор версії для шляху директорії: '{versionId}'.");

            Directory.CreateDirectory(candidate);
            return candidate;
        }

        public static string SanitizeVersionDirectoryName(string versionId)
        {
            if (string.IsNullOrWhiteSpace(versionId))
                throw new ArgumentException("Version ID cannot be empty.", nameof(versionId));

            string trimmed = versionId.Trim();
            var sb = new StringBuilder(trimmed.Length);
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char ch in trimmed)
            {
                if (ch == '/' || ch == '\\' || ch == ':' || Array.IndexOf(invalidChars, ch) >= 0)
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(ch);
                }
            }

            string result = sb.ToString();
            while (result.Contains("..", StringComparison.Ordinal))
            {
                result = result.Replace("..", "_", StringComparison.Ordinal);
            }

            result = result.Trim('.', ' ', '_');
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException($"Некоректний ідентифікатор версії: '{versionId}'.");

            return result;
        }

        public static IReadOnlyList<string> GetWorldBackupDirectories(string basePath, string? historicalGameDir = null)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(basePath))
                return result;

            var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            void TryAddSavesDir(string? rootDir)
            {
                if (string.IsNullOrWhiteSpace(rootDir))
                    return;
                string savesDir = Path.GetFullPath(Path.Combine(rootDir, "saves"));
                if (Directory.Exists(savesDir) && seen.Add(savesDir))
                {
                    result.Add(savesDir);
                }
            }

            TryAddSavesDir(basePath);
            TryAddSavesDir(Path.Combine(basePath, "historical"));
            TryAddSavesDir(historicalGameDir);
            return result;
        }

        public static bool DetectModernOptionsConflict(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                return false;

            string optionsPath = Path.Combine(basePath, "options.txt");
            if (!File.Exists(optionsPath))
                return false;

            try
            {
                foreach (string line in File.ReadLines(optionsPath))
                {
                    if (line.StartsWith("key_key.", StringComparison.Ordinal))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static string BuildSafeDiagnosticSummary(
            string versionId,
            JavaInstallation? java,
            string? historicalGameDir,
            string? proxyPort,
            string runtimeSource = "auto",
            bool? nativeCompatible = null)
        {
            string javaDesc = java == null
                ? "cml-default"
                : $"{java.FullVersion ?? java.MajorVersion.ToString()} (u{java.UpdateVersion}, b{java.BuildNumber}, arch={java.Architecture ?? "unknown"}, source={runtimeSource})";

            string nativeStatus = nativeCompatible.HasValue
                ? (nativeCompatible.Value ? "ok" : "mismatch")
                : "checked";

            string summary = $"[LegacyDiag] version={versionId}; java={javaDesc}; natives={nativeStatus}; gameDir={historicalGameDir ?? "default"}; proxyPort={proxyPort ?? "none"}";
            LastDiagnosticSummary = summary;
            return summary;
        }

        public static void ApplyHistoricalProcessFixes(
            ProcessStartInfo startInfo,
            string versionName,
            string? profileName = null,
            string? instanceIconPath = null,
            string? historicalGameDirectory = null)
        {
            if (startInfo == null)
                throw new ArgumentNullException(nameof(startInfo));

            string resolvedProfileName = !string.IsNullOrWhiteSpace(profileName)
                ? profileName.Trim()
                : $"Minecraft {versionName}";

            bool hasValidIcon = !string.IsNullOrWhiteSpace(instanceIconPath) && File.Exists(instanceIconPath);
            bool hasHistoricalGameDir = !string.IsNullOrWhiteSpace(historicalGameDirectory);

            if (startInfo.ArgumentList != null && startInfo.ArgumentList.Count > 0)
            {
                var updated = new List<string>(startInfo.ArgumentList.Count);
                var seenJvmFlags = new HashSet<string>(StringComparer.Ordinal);
                bool passedMainClass = false;
                bool nextIsGameDir = false;

                foreach (string rawArg in startInfo.ArgumentList)
                {
                    if (nextIsGameDir && hasHistoricalGameDir)
                    {
                        updated.Add(historicalGameDirectory!);
                        nextIsGameDir = false;
                        continue;
                    }

                    if (string.Equals(rawArg, "--gameDir", StringComparison.Ordinal))
                    {
                        nextIsGameDir = true;
                        updated.Add(rawArg);
                        continue;
                    }

                    if (string.Equals(rawArg, LegacyFixMainClass, StringComparison.Ordinal) ||
                        (!passedMainClass && !rawArg.StartsWith("-", StringComparison.Ordinal) &&
                         updated.Count > 0 && updated[^1] != "-cp" && updated[^1] != "-classpath"))
                    {
                        passedMainClass = true;
                    }

                    string arg = rawArg.Replace("${profile_name}", resolvedProfileName, StringComparison.Ordinal);

                    if (arg.Contains("${instance_icon}", StringComparison.Ordinal))
                    {
                        if (hasValidIcon)
                        {
                            arg = arg.Replace("${instance_icon}", instanceIconPath!, StringComparison.Ordinal);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (!passedMainClass && ShouldDeduplicateJvmFlag(arg))
                    {
                        if (!seenJvmFlags.Add(GetJvmFlagDeduplicationKey(arg)))
                            continue;
                    }

                    updated.Add(arg);
                }

                startInfo.ArgumentList.Clear();
                foreach (string item in updated)
                {
                    startInfo.ArgumentList.Add(item);
                }
            }
            else if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
            {
                string args = ReplacePlaceholderInCommandLine(startInfo.Arguments, "${profile_name}", resolvedProfileName);

                if (hasValidIcon)
                {
                    args = ReplacePlaceholderInCommandLine(args, "${instance_icon}", instanceIconPath!);
                }
                else
                {
                    args = RemoveUnresolvedIconFlagsFromCommandLine(args);
                }

                args = DeduplicateJvmFlagsInCommandString(args);

                if (hasHistoricalGameDir)
                {
                    string quotedDir = QuoteCommandLineArgIfNeeded(historicalGameDirectory!);
                    args = Regex.Replace(
                        args,
                        @"--gameDir\s+(?:""(?:\\.|[^""])*""|\S+)",
                        _ => $"--gameDir {quotedDir}",
                        RegexOptions.CultureInvariant);
                }

                startInfo.Arguments = args.Trim();
            }
        }

        public static string ReplacePlaceholderInCommandLine(string commandLine, string placeholder, string replacement)
        {
            if (string.IsNullOrEmpty(commandLine) || !commandLine.Contains(placeholder, StringComparison.Ordinal))
                return commandLine;

            string pattern = $@"(""(?:\\.|[^""])*{Regex.Escape(placeholder)}(?:\\.|[^""])*""|\S*{Regex.Escape(placeholder)}\S*)";
            return Regex.Replace(
                commandLine,
                pattern,
                match =>
                {
                    string token = match.Value;
                    bool isQuoted = token.Length >= 2 && token[0] == '"' && token[^1] == '"';
                    if (isQuoted)
                    {
                        string inner = token.Substring(1, token.Length - 2);
                        string escapedReplacement = EscapeInsideDoubleQuotes(replacement);
                        string replacedInner = inner.Replace(placeholder, escapedReplacement, StringComparison.Ordinal);
                        return $"\"{replacedInner}\"";
                    }
                    else
                    {
                        string rawReplaced = token.Replace(placeholder, replacement, StringComparison.Ordinal);
                        return QuoteCommandLineArgIfNeeded(rawReplaced);
                    }
                },
                RegexOptions.CultureInvariant);
        }

        public static string QuoteCommandLineArgIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            bool needsQuotes = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c) || c == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
                return value;

            return $"\"{EscapeInsideDoubleQuotes(value)}\"";
        }

        private static string EscapeInsideDoubleQuotes(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }

            if (backslashes > 0)
            {
                sb.Append('\\', backslashes * 2);
            }

            return sb.ToString();
        }

        private static string RemoveUnresolvedIconFlagsFromCommandLine(string args)
        {
            args = Regex.Replace(
                args,
                @"(?:^|\s+)(?:""-Dlf\.icon=\$\{instance_icon\}""|-Dlf\.icon=\$\{instance_icon\}|""-Xdock:icon=\$\{instance_icon\}""|-Xdock:icon=\$\{instance_icon\})",
                _ => string.Empty,
                RegexOptions.CultureInvariant);
            return args;
        }

        private static bool ShouldDeduplicateJvmFlag(string arg)
        {
            string unquoted = UnquoteToken(arg);
            return string.Equals(unquoted, LegacyMergeSortArg, StringComparison.Ordinal) ||
                   unquoted.StartsWith("-Dhttp.proxyHost=", StringComparison.Ordinal) ||
                   unquoted.StartsWith("-Dhttp.proxyPort=", StringComparison.Ordinal) ||
                   unquoted.StartsWith("-Dhttp.nonProxyHosts=", StringComparison.Ordinal);
        }

        private static string GetJvmFlagDeduplicationKey(string arg)
        {
            string unquoted = UnquoteToken(arg);
            if (unquoted.StartsWith("-Dhttp.proxyHost=", StringComparison.Ordinal))
                return "-Dhttp.proxyHost=";
            if (unquoted.StartsWith("-Dhttp.proxyPort=", StringComparison.Ordinal))
                return "-Dhttp.proxyPort=";
            if (unquoted.StartsWith("-Dhttp.nonProxyHosts=", StringComparison.Ordinal))
                return "-Dhttp.nonProxyHosts=";
            return unquoted;
        }

        private static string UnquoteToken(string token)
        {
            if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
                return token.Substring(1, token.Length - 2);
            return token;
        }

        private static string DeduplicateJvmFlagsInCommandString(string commandLine)
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            bool passedMainClass = false;
            string previousToken = string.Empty;

            var tokenMatches = Regex.Matches(commandLine, @"""(?:\\.|[^""])*""|\S+", RegexOptions.CultureInvariant);
            if (tokenMatches.Count == 0)
                return commandLine;

            var sb = new StringBuilder(commandLine.Length);
            int lastIndex = 0;

            foreach (Match m in tokenMatches)
            {
                string rawToken = m.Value;
                string unquoted = UnquoteToken(rawToken);

                if (string.Equals(unquoted, LegacyFixMainClass, StringComparison.Ordinal) ||
                    (!passedMainClass && !unquoted.StartsWith("-", StringComparison.Ordinal) &&
                     !string.IsNullOrEmpty(previousToken) && previousToken != "-cp" && previousToken != "-classpath"))
                {
                    passedMainClass = true;
                }

                if (!passedMainClass && ShouldDeduplicateJvmFlag(rawToken))
                {
                    string key = GetJvmFlagDeduplicationKey(rawToken);
                    if (!seenKeys.Add(key))
                    {
                        int removeStart = m.Index;
                        if (removeStart > lastIndex && commandLine[removeStart - 1] == ' ')
                        {
                            removeStart--;
                        }
                        sb.Append(commandLine, lastIndex, removeStart - lastIndex);
                        lastIndex = m.Index + m.Length;
                        continue;
                    }
                }

                previousToken = unquoted;
            }

            sb.Append(commandLine, lastIndex, commandLine.Length - lastIndex);
            return sb.ToString();
        }

        public static IReadOnlyList<string> TokenizeCommandLine(string? commandLine)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
                return tokens;

            var current = new StringBuilder();
            bool inQuotes = false;
            bool hasTokenContent = false;
            int backslashes = 0;

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];
                if (c == '\\')
                {
                    backslashes++;
                    hasTokenContent = true;
                    continue;
                }

                if (c == '"')
                {
                    if (backslashes % 2 == 0)
                    {
                        if (backslashes > 0)
                            current.Append('\\', backslashes / 2);
                        inQuotes = !inQuotes;
                        hasTokenContent = true;
                    }
                    else
                    {
                        current.Append('\\', backslashes / 2);
                        current.Append('"');
                        hasTokenContent = true;
                    }
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    current.Append('\\', backslashes);
                    backslashes = 0;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (hasTokenContent)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasTokenContent = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasTokenContent = true;
                }
            }

            if (backslashes > 0)
                current.Append('\\', backslashes);

            if (hasTokenContent)
                tokens.Add(current.ToString());

            return tokens;
        }

        public static PlatformCompatibilityResult ValidatePreLaunchPlatformCompatibility(
            string versionId,
            string versionJsonPath,
            MinecraftPath path,
            JavaInstallation? selectedJava,
            OSPlatform targetOs,
            Architecture targetArch)
        {
            if (string.IsNullOrWhiteSpace(versionJsonPath) || !File.Exists(versionJsonPath))
            {
                return new PlatformCompatibilityResult(false, $"Відсутній файл метаданих версії: {versionJsonPath}");
            }

            string versionJarPath = Path.Combine(path.Versions, versionId, $"{versionId}.jar");
            if (!File.Exists(versionJarPath))
            {
                return new PlatformCompatibilityResult(false, $"Відсутній основний JAR-файл клієнта: {versionJarPath}");
            }

            if (selectedJava == null || string.IsNullOrWhiteSpace(selectedJava.Executable) || !File.Exists(selectedJava.Executable))
            {
                return new PlatformCompatibilityResult(false, "Не знайдено виконуваний файл Java для запуску історичної версії.");
            }

            var req = ReadJavaRequirement(versionJsonPath, path.Library);
            if (targetOs == OSPlatform.OSX &&
                targetArch == Architecture.Arm64 &&
                req.HasX64OnlyOsxNatives &&
                string.Equals(selectedJava.Architecture, "arm64", StringComparison.OrdinalIgnoreCase))
            {
                return new PlatformCompatibilityResult(
                    false,
                    "Несумісна конфігурація: обрана ARM64 Java, але LWJGL 2 для цієї версії містить лише x64 macOS natives. Оберіть x64 Java 8 через Rosetta 2.");
            }

            return new PlatformCompatibilityResult(true, null);
        }
    }
}
