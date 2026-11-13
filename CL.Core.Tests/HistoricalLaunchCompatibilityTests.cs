using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using CmlLib.Core;
using CmlLib.Core.Auth;
using Xunit;

namespace CL.Core.Tests
{
    public class HistoricalLaunchCompatibilityTests
    {
        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cl_launch_compat_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CleanupDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { }
        }

        [Fact]
        public void ApplyHistoricalProcessFixes_ReplacesProfileNameAndRemovesMissingInstanceIcon_InArgumentList()
        {
            var psi = new ProcessStartInfo("java");
            psi.ArgumentList.Add("-Djava.util.Arrays.useLegacyMergeSort=true");
            psi.ArgumentList.Add("-Dlf.icon=${instance_icon}");
            psi.ArgumentList.Add("-Xdock:name=${profile_name}");
            psi.ArgumentList.Add("-Xdock:icon=${instance_icon}");
            psi.ArgumentList.Add("-Dhttp.proxyHost=betacraft.uk");
            psi.ArgumentList.Add("-Dhttp.proxyPort=11702");
            psi.ArgumentList.Add("-Djava.util.Arrays.useLegacyMergeSort=true");
            psi.ArgumentList.Add("uk.betacraft.legacyfix.LegacyFixLauncher");
            psi.ArgumentList.Add("--username");
            psi.ArgumentList.Add("TestPlayer");
            psi.ArgumentList.Add("--frameName");
            psi.ArgumentList.Add("${profile_name}");

            HistoricalLaunchCompatibilityService.ApplyHistoricalProcessFixes(psi, "in-20100110");

            Assert.DoesNotContain(psi.ArgumentList, a => a.Contains("${profile_name}", StringComparison.Ordinal));
            Assert.DoesNotContain(psi.ArgumentList, a => a.Contains("${instance_icon}", StringComparison.Ordinal));
            Assert.Contains("-Xdock:name=Minecraft in-20100110", psi.ArgumentList);
            int frameIdx = psi.ArgumentList.IndexOf("--frameName");
            Assert.True(frameIdx >= 0);
            Assert.Equal("Minecraft in-20100110", psi.ArgumentList[frameIdx + 1]);
            Assert.Equal(1, psi.ArgumentList.Count(a => a == "-Djava.util.Arrays.useLegacyMergeSort=true"));
            Assert.Equal("TestPlayer", psi.ArgumentList[psi.ArgumentList.IndexOf("--username") + 1]);
        }

        [Fact]
        public void ApplyHistoricalProcessFixes_InCommandLineString_QuotesProfileNameWithSpacesAndPreservesExactTokenCount()
        {
            var psi = new ProcessStartInfo("java")
            {
                Arguments = "-Djava.util.Arrays.useLegacyMergeSort=true -Dlf.icon=${instance_icon} -Xdock:name=${profile_name} -Djava.util.Arrays.useLegacyMergeSort=true -Dhttp.proxyPort=11705 -Dhttp.proxyPort=11705 uk.betacraft.legacyfix.LegacyFixLauncher --username Steve --frameName ${profile_name} --gameDir \"C:\\My  Spaced  Path\\$data\""
            };

            string targetGameDir = @"C:\My  Spaced  Path\$data\historical\b1.9-pre6";
            HistoricalLaunchCompatibilityService.ApplyHistoricalProcessFixes(
                psi,
                "b1.9-pre6",
                "Beta 1.9 \"Pre6\" Історія",
                historicalGameDirectory: targetGameDir);

            Assert.DoesNotContain("${profile_name}", psi.Arguments, StringComparison.Ordinal);
            Assert.DoesNotContain("${instance_icon}", psi.Arguments, StringComparison.Ordinal);

            var tokens = HistoricalLaunchCompatibilityService.TokenizeCommandLine(psi.Arguments);
            Assert.Equal(10, tokens.Count);
            Assert.Equal("-Xdock:name=Beta 1.9 \"Pre6\" Історія", tokens[1]);
            Assert.Equal("--frameName", tokens[6]);
            Assert.Equal("Beta 1.9 \"Pre6\" Історія", tokens[7]);
            Assert.Equal("--gameDir", tokens[8]);
            Assert.Equal(targetGameDir, tokens[9]);
        }

        [Fact]
        public async Task CmlLibBuildProcessAsync_WithBetaCraftFixture_UsesArgumentsStringAndQuotesProfileNameAsSingleToken()
        {
            string root = CreateTempDir();
            try
            {
                var path = new MinecraftPath(root);
                string versionDir = Path.Combine(path.Versions, "b1.9-pre6");
                Directory.CreateDirectory(versionDir);
                await File.WriteAllBytesAsync(Path.Combine(versionDir, "b1.9-pre6.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

                await File.WriteAllTextAsync(Path.Combine(versionDir, "b1.9-pre6.json"), """
                {
                  "id": "b1.9-pre6",
                  "type": "snapshot",
                  "mainClass": "uk.betacraft.legacyfix.LegacyFixLauncher",
                  "arguments": {
                    "jvm": [
                      "-Djava.library.path=${natives_directory}",
                      "-Dlf.icon=${instance_icon}",
                      "-Djava.util.Arrays.useLegacyMergeSort=true",
                      "-cp",
                      "${classpath}"
                    ],
                    "game": [
                      "--username",
                      "${auth_player_name}",
                      "--sessionid",
                      "${auth_session}",
                      "--gameDir",
                      "${game_directory}",
                      "--version",
                      "${version_name}",
                      "--frameName",
                      "${profile_name}",
                      "--width",
                      "${resolution_width}",
                      "--height",
                      "${resolution_height}"
                    ]
                  }
                }
                """);

                var launcher = new MinecraftLauncher(path);
                var config = new LaunchConfiguration
                {
                    IsOffline = true,
                    MinimumRamMb = 512,
                    MaximumRamMb = 1024,
                    ScreenWidth = 854,
                    ScreenHeight = 480,
                    Session = MSession.CreateOfflineSession("LegacyHero"),
                    AccountType = AccountType.Offline,
                    AllowOfflineLan = false
                };

                var gameLaunch = new GameLaunchService(null!, null!);
                var options = gameLaunch.CreateLaunchOptions(config, "b1.9-pre6");
                options.JavaPath = "java";

                string historicalDir = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(
                    Path.Combine(root, "spaced  $folder"), "b1.9-pre6");

                using var process = await launcher.BuildProcessAsync("b1.9-pre6", options);
                HistoricalLaunchCompatibilityService.ApplyHistoricalProcessFixes(
                    process.StartInfo,
                    "b1.9-pre6",
                    "Minecraft b1.9-pre6",
                    historicalGameDirectory: historicalDir);

                Assert.False(string.IsNullOrWhiteSpace(process.StartInfo.Arguments));
                var tokens = HistoricalLaunchCompatibilityService.TokenizeCommandLine(process.StartInfo.Arguments).ToList();

                int userIdx = tokens.IndexOf("--username");
                Assert.True(userIdx >= 0);
                Assert.Equal("LegacyHero", tokens[userIdx + 1]);

                int frameIdx = tokens.IndexOf("--frameName");
                Assert.True(frameIdx >= 0);
                Assert.Equal("Minecraft b1.9-pre6", tokens[frameIdx + 1]);
                Assert.Equal("--width", tokens[frameIdx + 2]);

                int gameDirIdx = tokens.IndexOf("--gameDir");
                Assert.True(gameDirIdx >= 0);
                Assert.Equal(historicalDir, tokens[gameDirIdx + 1]);
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Theory]
        [InlineData("rd-132211", "11701")]
        [InlineData("c0.0.13a-dev", "11701")]
        [InlineData("in-20100110", "11702")]
        [InlineData("inf-20100630-1340", "11702")]
        [InlineData("a1.1.2_01", "11702")]
        [InlineData("a1.2.6", "11705")]
        [InlineData("b1.3-demo", "11705")]
        [InlineData("b1.7.3", "11705")]
        [InlineData("1.5.2", "11707")]
        [InlineData("1.21.1", null)]
        public void GetProxyPort_MatchesExpectedBetaCraftPortsAndExcludesModern(string versionId, string? expectedPort)
        {
            Assert.Equal(expectedPort, GameLaunchService.GetProxyPort(versionId));
        }

        [Fact]
        public void SelectCompatibleJava_PrefersJava8X64_OverJava17AndJava21_AcrossDesktopPlatforms()
        {
            var javas = new List<JavaInstallation>
            {
                new() { Executable = "/opt/jdk-21/bin/java", MajorVersion = 21, Architecture = "x64" },
                new() { Executable = "/opt/jdk-8/bin/java", MajorVersion = 8, UpdateVersion = 451, Architecture = "x64" },
                new() { Executable = "/opt/jdk-17/bin/java", MajorVersion = 17, Architecture = "x64" }
            };

            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, true, true);

            foreach (var os in new[] { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.OSX })
            {
                var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(javas, req, os, Architecture.X64);
                Assert.True(result.Success);
                Assert.NotNull(result.Selected);
                Assert.Equal(8, result.Selected!.MajorVersion);
                Assert.False(result.UsesRosetta);
            }
        }

        [Fact]
        public void SelectCompatibleJava_WhenJava8MissingAndOnlyJava17Or21Present_ReturnsFalseWithoutForcingIncompatibleJava()
        {
            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, false, true);
            var onlyModernJavas = new List<JavaInstallation>
            {
                new() { Executable = "/opt/jdk-17/bin/java", MajorVersion = 17, UpdateVersion = 10, Architecture = "x64" },
                new() { Executable = "/opt/jdk-21/bin/java", MajorVersion = 21, UpdateVersion = 5, Architecture = "x64" }
            };

            var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                onlyModernJavas, req, OSPlatform.Windows, Architecture.X64);

            Assert.False(result.Success);
            Assert.Null(result.Selected);
            Assert.Contains("Java 8", result.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SelectCompatibleJava_OnWindowsX64_PrefersJava8X64WithOlderUpdate_OverJava8X86WithNewerUpdate()
        {
            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, false, true);
            var javas = new List<JavaInstallation>
            {
                new() { Executable = @"C:\Program Files (x86)\Java\jre1.8.0_451\bin\java.exe", MajorVersion = 8, UpdateVersion = 451, BuildNumber = 10, Architecture = "x86" },
                new() { Executable = @"C:\Program Files\Java\jre1.8.0_431\bin\java.exe", MajorVersion = 8, UpdateVersion = 431, BuildNumber = 10, Architecture = "x64" }
            };

            var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                javas, req, OSPlatform.Windows, Architecture.X64);

            Assert.True(result.Success);
            Assert.NotNull(result.Selected);
            Assert.Equal("x64", result.Selected!.Architecture);
            Assert.Equal(431, result.Selected.UpdateVersion);
        }

        [Fact]
        public void SelectCompatibleJava_AppleSilicon_AllowsArm64WhenArm64NativesPresent_AndRequiresRosettaWhenX64Only()
        {
            var armJava = new JavaInstallation { Executable = "/Library/Java/jdk-8-arm64/Contents/Home/bin/java", MajorVersion = 8, UpdateVersion = 432, Architecture = "arm64" };
            var x64Java = new JavaInstallation { Executable = "/Library/Java/jdk-8-x64/Contents/Home/bin/java", MajorVersion = 8, UpdateVersion = 432, Architecture = "x64" };

            var universalReq = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, HasX64OnlyOsxNatives: false, false);
            var nativeArmResult = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                new[] { armJava, x64Java }, universalReq, OSPlatform.OSX, Architecture.Arm64, isRosettaAvailable: false);
            Assert.True(nativeArmResult.Success);
            Assert.False(nativeArmResult.UsesRosetta);
            Assert.Equal("arm64", nativeArmResult.Selected!.Architecture);

            var x64OnlyReq = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, HasX64OnlyOsxNatives: true, false);
            var armRejected = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                new[] { armJava }, x64OnlyReq, OSPlatform.OSX, Architecture.Arm64, isRosettaAvailable: true);
            Assert.False(armRejected.Success);

            var noRosetta = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                new[] { armJava, x64Java }, x64OnlyReq, OSPlatform.OSX, Architecture.Arm64, isRosettaAvailable: false);
            Assert.False(noRosetta.Success);
            Assert.Contains("Rosetta 2", noRosetta.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);

            var rosettaOk = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                new[] { armJava, x64Java }, x64OnlyReq, OSPlatform.OSX, Architecture.Arm64, isRosettaAvailable: true);
            Assert.True(rosettaOk.Success);
            Assert.True(rosettaOk.UsesRosetta);
            Assert.Equal("x64", rosettaOk.Selected!.Architecture);
        }

        [Fact]
        public void InspectMacOsNativeArchive_AndReadJavaRequirement_DetectArm64AndX64OnlyMachOSlices()
        {
            string root = CreateTempDir();
            try
            {
                using var armJarStream = new MemoryStream();
                using (var zip = new ZipArchive(armJarStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = zip.CreateEntry("liblwjgl-macos-aarch64.dylib");
                    using var es = entry.Open();
                    byte[] header = new byte[16];
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), 0xfeedfacf);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), 0x0100000c);
                    es.Write(header);
                }
                armJarStream.Position = 0;
                var armInspect = HistoricalLaunchCompatibilityService.InspectMacOsNativeArchive(armJarStream);
                Assert.True(armInspect.HasMandatoryLwjglArm64);
                Assert.False(armInspect.HasMandatoryLwjglX64);

                string libsDir = Path.Combine(root, "libraries");
                string osxJarPath = Path.Combine(libsDir, "org", "lwjgl", "lwjgl", "lwjgl-platform", "2.9.0", "lwjgl-platform-2.9.0-natives-osx.jar");
                Directory.CreateDirectory(Path.GetDirectoryName(osxJarPath)!);
                using (var fs = File.Create(osxJarPath))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("liblwjgl.jnilib");
                    using var es = entry.Open();
                    byte[] fatHeader = new byte[28];
                    BinaryPrimitives.WriteUInt32BigEndian(fatHeader.AsSpan(0, 4), 0xcafebabe);
                    BinaryPrimitives.WriteUInt32BigEndian(fatHeader.AsSpan(4, 4), 1);
                    BinaryPrimitives.WriteUInt32BigEndian(fatHeader.AsSpan(8, 4), 0x01000007); // x86_64 only
                    es.Write(fatHeader);
                }

                string jsonX64Only = Path.Combine(root, "v-x64.json");
                File.WriteAllText(jsonX64Only, """
                {
                  "id": "v-x64",
                  "mainClass": "uk.betacraft.legacyfix.LegacyFixLauncher",
                  "libraries": [
                    { "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.0" }
                  ]
                }
                """);

                var reqX64 = HistoricalLaunchCompatibilityService.ReadJavaRequirement(jsonX64Only, libsDir);
                Assert.True(reqX64.HasX64OnlyOsxNatives);

                string jsonLegacyFabric = Path.Combine(root, "b1.9-pre6.json");
                File.WriteAllText(jsonLegacyFabric, """
                {
                  "id": "b1.9-pre6",
                  "mainClass": "uk.betacraft.legacyfix.LegacyFixLauncher",
                  "libraries": [
                    { "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.4+legacyfabric.17" }
                  ]
                }
                """);

                var reqLf = HistoricalLaunchCompatibilityService.ReadJavaRequirement(jsonLegacyFabric);
                Assert.False(reqLf.HasX64OnlyOsxNatives);
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public void SelectCompatibleJava_WhenEmpty_ReturnsClearMissingJavaDiagnostic()
        {
            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, true, false);
            var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                Array.Empty<JavaInstallation>(), req, OSPlatform.Linux, Architecture.X64);

            Assert.False(result.Success);
            Assert.Null(result.Selected);
            Assert.False(string.IsNullOrWhiteSpace(result.DiagnosticMessage));
        }

        [Theory]
        [InlineData("rd-132211", true)]
        [InlineData("c0.0.13a-dev", true)]
        [InlineData("in-20100110", true)]
        [InlineData("inf-20100630-1340", true)]
        [InlineData("a1.2.6", true)]
        [InlineData("b1.3-demo", true)]
        [InlineData("b1.9-pre6", true)]
        [InlineData("1.21.1", false)]
        [InlineData("1.21.1-forge-52.1.21", false)]
        public void IsHistoricalVersion_IdentifiesHistoricalErasWithoutAffectingModernVersions(string versionId, bool expected)
        {
            Assert.Equal(expected, HistoricalLaunchCompatibilityService.IsHistoricalVersion(versionId));
        }

        [Fact]
        public void ValidatePreLaunchPlatformCompatibility_DetectsMissingJarAndArm64Mismatch()
        {
            string root = CreateTempDir();
            try
            {
                var path = new MinecraftPath(root);
                string versionDir = Path.Combine(path.Versions, "a1.2.6");
                Directory.CreateDirectory(versionDir);
                string jsonPath = Path.Combine(versionDir, "a1.2.6.json");
                File.WriteAllText(jsonPath, """
                {
                  "id": "a1.2.6",
                  "mainClass": "uk.betacraft.legacyfix.LegacyFixLauncher",
                  "libraries": [
                    { "name": "org.lwjgl.lwjgl:lwjgl-platform:2.9.0" }
                  ]
                }
                """);

                string fakeJava = Path.Combine(root, "java");
                File.WriteAllText(fakeJava, string.Empty);

                var missingJarResult = HistoricalLaunchCompatibilityService.ValidatePreLaunchPlatformCompatibility(
                    "a1.2.6",
                    jsonPath,
                    path,
                    new JavaInstallation { Executable = fakeJava, MajorVersion = 8, Architecture = "x64" },
                    OSPlatform.Windows,
                    Architecture.X64);
                Assert.False(missingJarResult.IsCompatible);

                File.WriteAllBytes(Path.Combine(versionDir, "a1.2.6.jar"), new byte[] { 0x50, 0x4B });

                var armMismatch = HistoricalLaunchCompatibilityService.ValidatePreLaunchPlatformCompatibility(
                    "a1.2.6",
                    jsonPath,
                    path,
                    new JavaInstallation { Executable = fakeJava, MajorVersion = 8, Architecture = "arm64" },
                    OSPlatform.OSX,
                    Architecture.Arm64);
                Assert.False(armMismatch.IsCompatible);
                Assert.Contains("ARM64", armMismatch.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);

                var validWin = HistoricalLaunchCompatibilityService.ValidatePreLaunchPlatformCompatibility(
                    "a1.2.6",
                    jsonPath,
                    path,
                    new JavaInstallation { Executable = fakeJava, MajorVersion = 8, Architecture = "x64" },
                    OSPlatform.Windows,
                    Architecture.X64);
                Assert.True(validWin.IsCompatible);
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Theory]
        [InlineData("java version \"1.8.0_202\"\nJava(TM) SE Runtime Environment (build 1.8.0_202-b08)", 8, 0, 202, 8)]
        [InlineData("java version \"1.8.0_401\"\nJava(TM) SE Runtime Environment (build 1.8.0_401-b10)", 8, 0, 401, 10)]
        [InlineData("java version \"1.8.0_451\"\nJava(TM) SE Runtime Environment (build 1.8.0_451-b10)", 8, 0, 451, 10)]
        [InlineData("Picked up _JAVA_OPTIONS: -Xmx512M\nopenjdk version \"1.8.0_432\"\nOpenJDK Runtime Environment (Temurin)(build 1.8.0_432-b06)", 8, 0, 432, 6)]
        [InlineData("java version \"1.8.0\"", 8, 0, 0, 0)]
        [InlineData("openjdk version \"8.0.452\"", 8, 0, 452, 0)]
        [InlineData("java version \"8u401-b10\"", 8, 0, 401, 10)]
        [InlineData("openjdk version \"11.0.16.1\" 2022-08-12 LTS", 11, 0, 16, 0)]
        [InlineData("openjdk version \"21.0.5\" 2024-10-15 LTS", 21, 0, 5, 0)]
        public void ParseVersionDetails_SupportsLegacyAndVendorFormatsNumerically(
            string output,
            int expectedMajor,
            int expectedMinor,
            int expectedUpdate,
            int expectedBuild)
        {
            var parsed = JavaLocator.ParseVersionDetails(output);
            Assert.NotNull(parsed);
            Assert.Equal(expectedMajor, parsed!.Value.Major);
            Assert.Equal(expectedMinor, parsed.Value.Minor);
            Assert.Equal(expectedUpdate, parsed.Value.Update);
            Assert.Equal(expectedBuild, parsed.Value.Build);
        }

        [Theory]
        [InlineData("Error 1.2.3 occurred in module")]
        [InlineData("Python 3.11.4")]
        [InlineData("v2.0.1")]
        [InlineData("")]
        public void ParseVersionDetails_RejectsNonJavaOutput(string nonJavaText)
        {
            Assert.Null(JavaLocator.ParseVersionDetails(nonJavaText));
        }

        [Fact]
        public void RunProcessWithTimeout_WhenProcessHangs_TerminatesWithinTimeoutAndReturnsNull()
        {
            string exe = OperatingSystem.IsWindows() ? "powershell" : "sleep";
            string[] args = OperatingSystem.IsWindows()
                ? new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 10" }
                : new[] { "10" };

            var sw = Stopwatch.StartNew();
            string? output = JavaLocator.RunProcessWithTimeout(exe, 300, args);
            sw.Stop();

            Assert.Null(output);
            Assert.True(sw.ElapsedMilliseconds < 4000);
        }

        [Fact]
        public void SelectCompatibleJava_PrefersJava8Update401Plus_OverOutdated180_202()
        {
            var javas = new List<JavaInstallation>
            {
                new() { Executable = @"C:\Java\jre1.8.0_202\bin\java.exe", MajorVersion = 8, UpdateVersion = 202, BuildNumber = 8, FullVersion = "1.8.0_202", Architecture = "x64" },
                new() { Executable = @"C:\Java\jre1.8.0_401\bin\java.exe", MajorVersion = 8, UpdateVersion = 401, BuildNumber = 10, FullVersion = "1.8.0_401", Architecture = "x64" },
                new() { Executable = @"C:\Java\jre1.8.0_451\bin\java.exe", MajorVersion = 8, UpdateVersion = 451, BuildNumber = 10, FullVersion = "1.8.0_451", Architecture = "x64" }
            };

            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, true, true);
            var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(javas, req, OSPlatform.Windows, Architecture.X64);

            Assert.True(result.Success);
            Assert.NotNull(result.Selected);
            Assert.Equal(451, result.Selected!.UpdateVersion);
            Assert.True(result.Selected.IsJava8Update401OrNewer);
            Assert.Null(result.DiagnosticMessage);
        }

        [Fact]
        public void SelectCompatibleJava_WhenOnlyOutdatedJava8Exists_AllowsLaunchWithTlsWarning()
        {
            var javas = new List<JavaInstallation>
            {
                new() { Executable = @"C:\Java\jre1.8.0_202\bin\java.exe", MajorVersion = 8, UpdateVersion = 202, BuildNumber = 8, FullVersion = "1.8.0_202", Architecture = "x64" }
            };

            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, true, true);
            var result = HistoricalLaunchCompatibilityService.SelectCompatibleJava(javas, req, OSPlatform.Windows, Architecture.X64);

            Assert.True(result.Success);
            Assert.NotNull(result.Selected);
            Assert.Equal(202, result.Selected!.UpdateVersion);
            Assert.Contains("u401", result.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateExplicitJavaChoice_PreservesUserChoiceWithWarning_AndRejectsArm64Mismatch()
        {
            var req = new HistoricalJavaRequirement(8, 5, 8, "jre-legacy", true, HasX64OnlyOsxNatives: true, true);
            var oldUserJava = new JavaInstallation
            {
                Executable = @"C:\Custom\jre1.8.0_202\bin\java.exe",
                MajorVersion = 8,
                UpdateVersion = 202,
                FullVersion = "1.8.0_202",
                Architecture = "x64"
            };

            var winResult = HistoricalLaunchCompatibilityService.ValidateExplicitJavaChoice(
                oldUserJava, req, OSPlatform.Windows, Architecture.X64);
            Assert.True(winResult.Success);
            Assert.Equal(oldUserJava.Executable, winResult.Selected!.Executable);
            Assert.Contains("u401", winResult.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);

            var armJava = new JavaInstallation
            {
                Executable = "/Library/Java/arm64/java",
                MajorVersion = 8,
                UpdateVersion = 432,
                Architecture = "arm64"
            };
            var macResult = HistoricalLaunchCompatibilityService.ValidateExplicitJavaChoice(
                armJava, req, OSPlatform.OSX, Architecture.Arm64, isRosettaAvailable: true);
            Assert.False(macResult.Success);
            Assert.Contains("ARM64", macResult.DiagnosticMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HistoricalGameDirectoryIsolation_SeparatesPerVersion_BlocksPathTraversal_AndIncludesAllSavesInBackup()
        {
            string root = CreateTempDir();
            try
            {
                string modernOptions = Path.Combine(root, "options.txt");
                File.WriteAllText(modernOptions, "version:3955\nkey_key.attack:key.mouse.left\nkey_key.forward:key.keyboard.w\n");

                string existingWorldDir = Path.Combine(root, "saves", "MyModernWorld");
                Directory.CreateDirectory(existingWorldDir);
                string levelDat = Path.Combine(existingWorldDir, "level.dat");
                File.WriteAllBytes(levelDat, new byte[] { 0x1F, 0x8B, 0x08 });

                Assert.True(HistoricalLaunchCompatibilityService.DetectModernOptionsConflict(root));

                string indevDir1 = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(root, "in-20100110");
                string indevDir2 = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(root, "in-20100110");
                string betaDir = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(root, "b1.9-pre6");

                Assert.Equal(indevDir1, indevDir2);
                Assert.NotEqual(indevDir1, betaDir);
                Assert.True(Directory.Exists(indevDir1));
                Assert.True(Directory.Exists(betaDir));
                Assert.True(File.Exists(modernOptions));
                Assert.True(File.Exists(levelDat));

                string traversalDir = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(root, "../../evil/../b1.9");
                string historicalRoot = Path.GetFullPath(Path.Combine(root, "historical"));
                Assert.StartsWith(historicalRoot, traversalDir, StringComparison.OrdinalIgnoreCase);

                string betaSaves = Path.Combine(betaDir, "saves", "BetaWorld");
                Directory.CreateDirectory(betaSaves);
                var backupDirs = HistoricalLaunchCompatibilityService.GetWorldBackupDirectories(root, betaDir);
                Assert.Contains(Path.GetFullPath(Path.Combine(root, "saves")), backupDirs);
                Assert.Contains(Path.GetFullPath(Path.Combine(betaDir, "saves")), backupDirs);
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public void CreateLaunchOptions_IncludesNonProxyHostsAndSkipsAuthlibInjectorForOfflineHistorical_WithoutBreakingModern()
        {
            var gameLaunch = new GameLaunchService(null!, null!);
            var config = new LaunchConfiguration
            {
                IsOffline = true,
                MinimumRamMb = 512,
                MaximumRamMb = 1024,
                ScreenWidth = 854,
                ScreenHeight = 480,
                Session = MSession.CreateOfflineSession("IndevTester"),
                AccountType = AccountType.Offline,
                AllowOfflineLan = true,
                JavaPath = @"C:\Java\jre1.8.0_451\bin\java.exe"
            };

            var historicalOptions = gameLaunch.CreateLaunchOptions(config, "in-20100110");
            Assert.Equal(@"C:\Java\jre1.8.0_451\bin\java.exe", historicalOptions.JavaPath);

            var jvmArgs = historicalOptions.ExtraJvmArguments?.SelectMany(a => a.Values ?? Array.Empty<string>()).ToList() ?? new List<string>();
            Assert.DoesNotContain(jvmArgs, a => a.Contains("authlib-injector", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("-Dhttp.proxyHost=betacraft.uk", jvmArgs);
            Assert.Contains("-Dhttp.proxyPort=11702", jvmArgs);
            Assert.Contains($"-Dhttp.nonProxyHosts={HistoricalLaunchCompatibilityService.DefaultBetaCraftNonProxyHosts}", jvmArgs);

            var modernOptions = gameLaunch.CreateLaunchOptions(config, "1.21.1");
            var modernJvmArgs = modernOptions.ExtraJvmArguments?.SelectMany(a => a.Values ?? Array.Empty<string>()).ToList() ?? new List<string>();
            Assert.DoesNotContain(modernJvmArgs, a => a.Contains("betacraft.uk", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(modernJvmArgs, a => a.Contains("nonProxyHosts", StringComparison.OrdinalIgnoreCase));

            string diag = HistoricalLaunchCompatibilityService.BuildSafeDiagnosticSummary(
                "in-20100110",
                new JavaInstallation { Executable = @"C:\Java\jre1.8.0_451\bin\java.exe", MajorVersion = 8, UpdateVersion = 451, BuildNumber = 10, FullVersion = "1.8.0_451", Architecture = "x64" },
                @"C:\Games\CLMinecraft\historical\in-20100110",
                "11702",
                "user",
                true);
            Assert.Equal(diag, HistoricalLaunchCompatibilityService.LastDiagnosticSummary);
            Assert.Contains("1.8.0_451", diag);
            Assert.Contains("source=user", diag);
            Assert.Contains("natives=ok", diag);
            Assert.DoesNotContain("sessionid", diag, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", diag, StringComparison.OrdinalIgnoreCase);
        }
    }
}
