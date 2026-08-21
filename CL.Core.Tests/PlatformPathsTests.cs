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
    }
}
