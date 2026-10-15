using System;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class PlatformPathsTests
    {
        [Fact]
        public void DefaultLauncherPathIsAbsolute()
            => Assert.True(System.IO.Path.IsPathRooted(PlatformPaths.DefaultLauncherPath()));

        [Fact]
        public void OsNameMatchesCurrentPlatform()
        {
            string expected =
                OperatingSystem.IsWindows() ? "Windows" :
                OperatingSystem.IsLinux() ? "Linux" :
                OperatingSystem.IsMacOS() ? "macOS" : "Unknown";

            Assert.Equal(expected, PlatformPaths.OsName());
        }

        [Fact]
        public void IdenticalPathsAreEqual()
        {
            string path = PlatformPaths.DefaultLauncherPath();
            Assert.True(PlatformPaths.PathsEqual(path, path));
        }

        [Fact]
        public void TrailingSeparatorIsIgnored()
        {
            string path = PlatformPaths.DefaultLauncherPath();
            Assert.True(PlatformPaths.PathsEqual(path, path + System.IO.Path.DirectorySeparatorChar));
        }

        [Fact]
        public void CaseSensitivityFollowsFileSystem()
        {
            bool equal = PlatformPaths.PathsEqual("/home/user/.clminecraft", "/home/user/.ClMinecraft");

            if (OperatingSystem.IsLinux())
                Assert.False(equal);
            else
                Assert.True(equal);
        }

        [Theory]
        [InlineData(null, "/tmp")]
        [InlineData("/tmp", null)]
        [InlineData("", "/tmp")]
        [InlineData("   ", "/tmp")]
        public void BlankInputIsNeverEqual(string? left, string? right)
            => Assert.False(PlatformPaths.PathsEqual(left, right));

        [Fact]
        public void AppDataDirectoryIsAbsolute()
            => Assert.True(System.IO.Path.IsPathRooted(PlatformPaths.AppDataDirectory()));

        [Fact]
        public void DataAndLocalesLiveInsideAppDataDirectory()
        {
            string root = PlatformPaths.AppDataDirectory();

            Assert.True(PlatformPaths.PathsEqual(root, System.IO.Path.GetDirectoryName(PlatformPaths.DataFolder())));
            Assert.True(PlatformPaths.PathsEqual(root, System.IO.Path.GetDirectoryName(PlatformPaths.LocalesFolder())));
        }

        [Fact]
        public void AppDataDirectoryFollowsXdgDataHomeOnLinux()
        {
            if (!OperatingSystem.IsLinux())
                return;

            string? previous = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            try
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/tmp/xdg-test");
                Assert.Equal("/tmp/xdg-test/cl-launcher", PlatformPaths.AppDataDirectory());

                // Відносний шлях специфікація XDG вимагає ігнорувати.
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", "relative/path");
                Assert.EndsWith("/.local/share/cl-launcher", PlatformPaths.AppDataDirectory());
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", previous);
            }
        }

        [Fact]
        public void LegacyDataIsCopiedOnlyWhereNothingExistsYet()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cl-migrate-" + Guid.NewGuid().ToString("N"));
            string legacy = System.IO.Path.Combine(root, "legacy");
            string appData = System.IO.Path.Combine(root, "appdata");

            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(legacy, "Data", "nested"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(legacy, "Data", "UserSaves.json"), "old");
                System.IO.File.WriteAllText(System.IO.Path.Combine(legacy, "Data", "nested", "file.txt"), "nested");
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(legacy, "Locales"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(legacy, "Locales", "uk_UA.json"), "old");

                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(appData, "Locales"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(appData, "Locales", "uk_UA.json"), "new");

                PlatformPaths.MigrateLegacyAppData(legacy, appData);

                Assert.Equal("old", System.IO.File.ReadAllText(System.IO.Path.Combine(appData, "Data", "UserSaves.json")));
                Assert.Equal("nested", System.IO.File.ReadAllText(System.IO.Path.Combine(appData, "Data", "nested", "file.txt")));
                Assert.Equal("new", System.IO.File.ReadAllText(System.IO.Path.Combine(appData, "Locales", "uk_UA.json")));
            }
            finally
            {
                if (System.IO.Directory.Exists(root))
                    System.IO.Directory.Delete(root, true);
            }
        }

        [Fact]
        public void MigrationIsSkippedWhenBothRootsAreTheSame()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cl-same-" + Guid.NewGuid().ToString("N"));
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "Data"));
                PlatformPaths.MigrateLegacyAppData(root, root + System.IO.Path.DirectorySeparatorChar);
                Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(root, "Data")));
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }
    }
}
