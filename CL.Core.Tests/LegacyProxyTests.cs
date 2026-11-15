using System.Linq;
using CL.Core.Models;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class LegacyProxyTests
    {
        [Theory]
        [InlineData("rd-132211", "11701")]
        [InlineData("c0.30_01c", "11701")]
        [InlineData("in-20100223", "11702")]
        [InlineData("inf-20100618", "11702")]
        [InlineData("a1.0.4", "11702")]
        [InlineData("a1.2.6", "11705")]
        [InlineData("b1.0", "11705")]
        [InlineData("b1.1_02", "11705")]
        [InlineData("b1.2_02", "11705")]
        [InlineData("b1.7.3", "11705")]
        [InlineData("b1.8.1", "11705")]
        [InlineData("1.0", "11707")]
        [InlineData("1.2.5", "11707")]
        [InlineData("1.4.7", "11707")]
        [InlineData("1.5.2", "11707")]
        public void GetProxyPort_ReturnsExpectedPortForHistoricalVersions(string version, string expectedPort)
        {
            Assert.Equal(expectedPort, GameLaunchService.GetProxyPort(version));
        }

        [Theory]
        [InlineData("1.6.4")]
        [InlineData("1.7.10")]
        [InlineData("1.8.9")]
        [InlineData("1.10.2")]
        [InlineData("1.12.2")]
        [InlineData("1.16.5")]
        [InlineData("1.20.1")]
        [InlineData("1.21.1")]
        [InlineData("1.21.11")]
        [InlineData("26.3")]
        public void GetProxyPort_ReturnsNullForModernVersions(string version)
        {
            Assert.Null(GameLaunchService.GetProxyPort(version));
        }

        [Theory]
        [InlineData("1.10.2")]
        [InlineData("1.12.2")]
        [InlineData("1.20.1")]
        [InlineData("1.21.1")]
        [InlineData("1.21.11")]
        [InlineData("26.3")]
        public void CreateLaunchOptions_DoesNotAddBetacraftProxyForModernVersions(string version)
        {
            var service = new GameLaunchService(null!, null!);
            var config = new LaunchConfiguration
            {
                AccountType = AccountType.Microsoft,
                AllowOfflineLan = false,
                MinimumRamMb = 1024,
                MaximumRamMb = 2048
            };

            var options = service.CreateLaunchOptions(config, version);

            var jvmValues = options.ExtraJvmArguments?
                .SelectMany(arg => arg.Values ?? System.Array.Empty<string>())
                .ToList() ?? new System.Collections.Generic.List<string>();

            Assert.DoesNotContain(jvmValues, v => v.Contains("betacraft.uk"));
            Assert.DoesNotContain(jvmValues, v => v.Contains("-Dhttp.proxyPort="));
            Assert.DoesNotContain(jvmValues, v => v.Contains("useLegacyMergeSort"));
        }

        [Theory]
        [InlineData("b1.0", "11705")]
        [InlineData("1.2.5", "11707")]
        public void CreateLaunchOptions_AddsBetacraftProxyForHistoricalVersions(string version, string expectedPort)
        {
            var service = new GameLaunchService(null!, null!);
            var config = new LaunchConfiguration
            {
                AccountType = AccountType.Microsoft,
                AllowOfflineLan = false,
                MinimumRamMb = 512,
                MaximumRamMb = 1024
            };

            var options = service.CreateLaunchOptions(config, version);

            var jvmValues = options.ExtraJvmArguments?
                .SelectMany(arg => arg.Values ?? System.Array.Empty<string>())
                .ToList() ?? new System.Collections.Generic.List<string>();

            Assert.Contains("-Dhttp.proxyHost=betacraft.uk", jvmValues);
            Assert.Contains($"-Dhttp.proxyPort={expectedPort}", jvmValues);
            Assert.Contains("-Djava.util.Arrays.useLegacyMergeSort=true", jvmValues);
        }
    }
}
