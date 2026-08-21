using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class JavaLocatorTests
    {
        private const string ModernOutput =
            "openjdk version \"21.0.2\" 2024-01-16\n" +
            "OpenJDK Runtime Environment Temurin-21.0.2+13 (build 21.0.2+13)\n" +
            "OpenJDK 64-Bit Server VM Temurin-21.0.2+13 (build 21.0.2+13, mixed mode)";

        private const string LegacyOutput =
            "java version \"1.8.0_312\"\n" +
            "Java(TM) SE Runtime Environment (build 1.8.0_312-b07)\n" +
            "Java HotSpot(TM) 64-Bit Server VM (build 25.312-b07, mixed mode)";

        [Theory]
        [InlineData("openjdk version \"21.0.2\" 2024-01-16", 21)]
        [InlineData("openjdk version \"25.0.3\" 2025-01-01", 25)]
        [InlineData("openjdk version \"17\" 2021-09-14", 17)]
        public void ParsesModernVersions(string output, int expected)
            => Assert.Equal(expected, JavaLocator.ParseMajorVersion(output));

        [Fact]
        public void ParsesLegacyVersionAsSecondComponent()
            => Assert.Equal(8, JavaLocator.ParseMajorVersion(LegacyOutput));

        [Theory]
        [InlineData("no version information")]
        [InlineData("version without quotes 21")]
        [InlineData("")]
        public void ReturnsNullOnUnparsableOutput(string output)
            => Assert.Null(JavaLocator.ParseMajorVersion(output));

        [Theory]
        [InlineData("OpenJDK 64-Bit Server VM", "x64")]
        [InlineData("OpenJDK 32-Bit Server VM", "x86")]
        [InlineData("OpenJDK aarch64 Server VM", "arm64")]
        public void ParsesArchitecture(string output, string expected)
            => Assert.Equal(expected, JavaLocator.ParseArchitecture(output));

        [Fact]
        public void ParsesVendorLine()
            => Assert.Contains("Temurin", JavaLocator.ParseVendor(ModernOutput));

        [Fact]
        public void InspectRejectsMissingFile()
            => Assert.Null(JavaLocator.Inspect("/nonexistent/bin/java"));

        [Fact]
        public void DetectReturnsOnlyValidatedRuntimes()
        {
            foreach (var java in JavaLocator.Detect())
            {
                Assert.True(java.MajorVersion > 0);
                Assert.True(System.IO.File.Exists(java.Executable));
            }
        }
    }

    public class JavaInstallationTests
    {
        [Fact]
        public void IdIsStableAndCarriesMajorVersion()
        {
            var installation = new JavaInstallation
            {
                Executable = "/usr/lib/jvm/temurin-21/bin/java",
                MajorVersion = 21
            };

            Assert.Equal(installation.Id, installation.Id);
            Assert.StartsWith("java-21-", installation.Id);
        }

        [Fact]
        public void IdDiffersBetweenPaths()
        {
            var first = new JavaInstallation { Executable = "/a/bin/java", MajorVersion = 21 };
            var second = new JavaInstallation { Executable = "/b/bin/java", MajorVersion = 21 };

            Assert.NotEqual(first.Id, second.Id);
        }
    }
}
