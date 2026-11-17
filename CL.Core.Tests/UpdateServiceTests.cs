using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests;

public class UpdateServiceTests
{
    private static GitHubReleaseDto MakeRelease(
        string tag,
        bool prerelease,
        bool draft = false,
        string winAssetName = "CLegendaryLauncher-Windows-Portable.zip",
        string? customUrl = null)
    {
        return new GitHubReleaseDto
        {
            TagName = tag,
            Prerelease = prerelease,
            Draft = draft,
            HtmlUrl = $"https://github.com/WER-CORE/CL-Win-Edition--Update/releases/tag/{tag}",
            Assets = string.IsNullOrEmpty(winAssetName)
                ? new List<GitHubAssetDto>()
                : new List<GitHubAssetDto>
                {
                    new GitHubAssetDto
                    {
                        Name = winAssetName,
                        BrowserDownloadUrl = customUrl ?? $"https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/{tag}/{winAssetName}"
                    }
                }
        };
    }

    [Fact]
    public void Regression_TestingChannel_DoesNotSelectNewerStable_WhenPreviewExists()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.1.0", prerelease: false),
            MakeRelease("v1.1.0-preview9.2", prerelease: true)
        };

        var result = service.SelectBestRelease(releases, UpdateChannel.Testing, "1.1.0-preview9.1", OSPlatform.Windows, Architecture.X64);

        Assert.NotNull(result);
        Assert.Equal("1.1.0-preview9.2", result!.Version);
        Assert.Contains("v1.1.0-preview9.2", result.UrlDefault);
    }

    [Fact]
    public void Regression_TestingChannel_ReturnsNull_WhenOnlyStableExists()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.1.0", prerelease: false),
            MakeRelease("v1.1.1", prerelease: false)
        };

        var result = service.SelectBestRelease(releases, UpdateChannel.Testing, "1.1.0-preview9.2", OSPlatform.Windows, Architecture.X64);

        Assert.Null(result);
    }

    [Fact]
    public void StableChannel_IgnoresPrereleases()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.2.0-preview1.0", prerelease: true),
            MakeRelease("v1.1.1", prerelease: false)
        };

        var result = service.SelectBestRelease(releases, UpdateChannel.Stable, "1.1.0", OSPlatform.Windows, Architecture.X64);

        Assert.NotNull(result);
        Assert.Equal("1.1.1", result!.Version);
    }

    [Fact]
    public void StableChannel_ReturnsNull_WhenOnlyPrereleaseExists()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.2.0-preview1.0", prerelease: true)
        };

        var result = service.SelectBestRelease(releases, UpdateChannel.Stable, "1.1.0", OSPlatform.Windows, Architecture.X64);

        Assert.Null(result);
    }

    [Fact]
    public void DraftReleases_AreIgnored_ForBothChannels()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.2.0", prerelease: false, draft: true),
            MakeRelease("v1.2.0-preview1.0", prerelease: true, draft: true)
        };

        Assert.Null(service.SelectBestRelease(releases, UpdateChannel.Stable, "1.1.0", OSPlatform.Windows, Architecture.X64));
        Assert.Null(service.SelectBestRelease(releases, UpdateChannel.Testing, "1.1.0-preview9.2", OSPlatform.Windows, Architecture.X64));
    }

    [Theory]
    [InlineData("1.1.0-preview9.10", "1.1.0-preview9.2", true)]
    [InlineData("1.1.0-preview10.1", "1.1.0-preview9.10", true)]
    [InlineData("1.1.0-preview9.3", "1.1.0-preview9.2", true)]
    [InlineData("1.1.0-beta.10", "1.1.0-beta.2", true)]
    [InlineData("1.1.1", "1.1.0", true)]
    [InlineData("1.1.0", "1.1.0", false)]
    [InlineData("1.1.0-preview9.8", "1.1.0-preview9.8", false)]
    [InlineData("1.1.0-preview9.1", "1.1.0-preview9.2", false)]
    [InlineData("1.0.9", "1.1.0", false)]
    [InlineData("1.1.0-beta.10", "1.1.0", false)]
    public void VersionComparison_HandlesClAndPrereleaseFormatsAccurately(string remote, string local, bool expectedAvailable)
    {
        var service = new UpdateService();
        Assert.Equal(expectedAvailable, service.IsUpdateAvailable(remote, local));
    }

    [Fact]
    public void SelectBestRelease_ChoosesHighestVersion_RegardlessOfArrayOrder()
    {
        var service = new UpdateService();
        var releases = new[]
        {
            MakeRelease("v1.1.0-preview9.3", prerelease: true),
            MakeRelease("v1.1.0-preview10.1", prerelease: true),
            MakeRelease("v1.1.0-preview9.10", prerelease: true)
        };

        var result = service.SelectBestRelease(releases, UpdateChannel.Testing, "1.1.0-preview9.2", OSPlatform.Windows, Architecture.X64);

        Assert.NotNull(result);
        Assert.Equal("1.1.0-preview10.1", result!.Version);
    }

    [Fact]
    public void SelectBestRelease_NeverSubstitutesHtmlUrl_WhenPlatformAssetMissingOrInsecure()
    {
        var service = new UpdateService();
        var missingAssetRelease = MakeRelease("v1.1.1", prerelease: false, winAssetName: "");
        var insecureUrlRelease = MakeRelease("v1.1.2", prerelease: false, customUrl: "http://example.com/CLegendaryLauncher-Windows-Portable.zip");

        Assert.Null(service.SelectBestRelease(new[] { missingAssetRelease, insecureUrlRelease }, UpdateChannel.Stable, "1.1.0", OSPlatform.Windows, Architecture.X64));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.Forbidden, "{\"message\":\"API rate limit exceeded\"}")]
    [InlineData(HttpStatusCode.TooManyRequests, "")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "{invalid json")]
    public async Task CheckForUpdateFromGitHubAsync_HandlesErrorsAndEmptyResponsesSafely(HttpStatusCode status, string body)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var service = new UpdateService(new HttpClient(handler));

        var stableResult = await service.CheckForUpdateFromGitHubAsync("WER-CORE/CL-Win-Edition--Update", isPreview: false, currentVersion: "1.1.0");
        var testingResult = await service.CheckForUpdateFromGitHubAsync("WER-CORE/CL-Win-Edition--Update", isPreview: true, currentVersion: "1.1.0-preview9.2");

        Assert.Null(stableResult);
        Assert.Null(testingResult);
    }

    [Fact]
    public async Task ChannelSwitching_UsesRequestedChannel_AndDownloadsSelectedReleaseAsset()
    {
        var releases = new[]
        {
            MakeRelease("v1.1.1", prerelease: false),
            MakeRelease("v1.1.0-preview9.10", prerelease: true)
        };
        string json = JsonSerializer.Serialize(releases);
        var requestedUrls = new List<string>();

        var handler = new StubHttpMessageHandler(req =>
        {
            string url = req.RequestUri!.ToString();
            requestedUrls.Add(url);

            if (url.Contains("/releases", StringComparison.Ordinal) && !url.Contains("/download/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateDummyZipBytes())
            };
        });

        var service = new UpdateService(new HttpClient(handler));

        // 1) Check Stable -> gets 1.1.1
        var stableInfo = await service.CheckForUpdateFromGitHubAsync("WER-CORE/CL-Win-Edition--Update", isPreview: false, currentVersion: "1.1.0");
        Assert.NotNull(stableInfo);
        Assert.Equal("1.1.1", stableInfo!.Version);

        // 2) Switch immediately to Testing -> gets 1.1.0-preview9.10 (no stale cache from Stable)
        var testingInfo = await service.CheckForUpdateFromGitHubAsync("WER-CORE/CL-Win-Edition--Update", isPreview: true, currentVersion: "1.1.0-preview9.2");
        Assert.NotNull(testingInfo);
        Assert.Equal("1.1.0-preview9.10", testingInfo!.Version);

        string downloadUrl = service.GetCorrectDownloadUrl(testingInfo, OSPlatform.Windows, Architecture.X64);
        await service.DownloadUpdateAsync(downloadUrl);

        Assert.Contains(requestedUrls, u => u.Contains("v1.1.0-preview9.10/CLegendaryLauncher-Windows-Portable.zip"));
        Assert.DoesNotContain(requestedUrls, u => u.Contains("v1.1.1/CLegendaryLauncher-Windows-Portable.zip"));

        string extractDir = Path.Combine(Path.GetTempPath(), "cl_update_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            service.ExtractUpdate(extractDir);
            Assert.True(File.Exists(Path.Combine(extractDir, "marker.txt")));
        }
        finally
        {
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        }
    }

    [Fact]
    public void MacOsZipFallback_SelectsPortableZipAndNeverPkg_ForBothArchitectures()
    {
        var service = new UpdateService();
        var release = new GitHubReleaseDto
        {
            TagName = "v1.1.1-preview1",
            Prerelease = true,
            HtmlUrl = "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/tag/v1.1.1-preview1",
            Assets = new List<GitHubAssetDto>
            {
                new() { Name = "CLegendaryLauncher-1.1.1-preview1-osx-arm64-full.nupkg", BrowserDownloadUrl = "https://example.com/arm64.nupkg" },
                new() { Name = "CLegendaryLauncher-osx-arm64-Portable.zip", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-arm64-Portable.zip" },
                new() { Name = "CLegendaryLauncher-osx-arm64-Setup.pkg", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-arm64-Setup.pkg" },
                new() { Name = "CLegendaryLauncher-1.1.1-preview1-osx-x64-full.nupkg", BrowserDownloadUrl = "https://example.com/x64.nupkg" },
                new() { Name = "CLegendaryLauncher-osx-x64-Portable.zip", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-x64-Portable.zip" },
                new() { Name = "CLegendaryLauncher-osx-x64-Setup.pkg", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-x64-Setup.pkg" }
            }
        };

        var armResult = service.SelectBestRelease(new[] { release }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.Arm64);
        var x64Result = service.SelectBestRelease(new[] { release }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.X64);

        Assert.NotNull(armResult);
        Assert.Equal("https://example.com/CLegendaryLauncher-osx-arm64-Portable.zip", service.GetCorrectDownloadUrl(armResult!, OSPlatform.OSX, Architecture.Arm64));

        Assert.NotNull(x64Result);
        Assert.Equal("https://example.com/CLegendaryLauncher-osx-x64-Portable.zip", service.GetCorrectDownloadUrl(x64Result!, OSPlatform.OSX, Architecture.X64));
    }

    [Fact]
    public void MacOsZipFallback_PrioritizesPortableZipOverGenericZip_RegardlessOfOrder()
    {
        var service = new UpdateService();
        var genericFirst = new GitHubReleaseDto
        {
            TagName = "v1.1.1-preview1",
            Prerelease = true,
            Assets = new List<GitHubAssetDto>
            {
                new() { Name = "CLegendaryLauncher-osx-arm64-other.zip", BrowserDownloadUrl = "https://example.com/other-arm64.zip" },
                new() { Name = "CLegendaryLauncher-osx-arm64-Setup.pkg", BrowserDownloadUrl = "https://example.com/arm64-Setup.pkg" },
                new() { Name = "CLegendaryLauncher-osx-arm64-Portable.zip", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-arm64-Portable.zip" },
                new() { Name = "CLegendaryLauncher-osx-x64-Portable.zip", BrowserDownloadUrl = "https://example.com/CLegendaryLauncher-osx-x64-Portable.zip" },
                new() { Name = "CLegendaryLauncher-osx-x64-other.zip", BrowserDownloadUrl = "https://example.com/other-x64.zip" },
                new() { Name = "CLegendaryLauncher-osx-x64-Setup.pkg", BrowserDownloadUrl = "https://example.com/x64-Setup.pkg" }
            }
        };

        var armResult = service.SelectBestRelease(new[] { genericFirst }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.Arm64);
        var x64Result = service.SelectBestRelease(new[] { genericFirst }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.X64);

        Assert.NotNull(armResult);
        Assert.Equal("https://example.com/CLegendaryLauncher-osx-arm64-Portable.zip", service.GetCorrectDownloadUrl(armResult!, OSPlatform.OSX, Architecture.Arm64));

        Assert.NotNull(x64Result);
        Assert.Equal("https://example.com/CLegendaryLauncher-osx-x64-Portable.zip", service.GetCorrectDownloadUrl(x64Result!, OSPlatform.OSX, Architecture.X64));
    }

    [Fact]
    public void MacOsZipFallback_ReturnsNull_WhenOnlyPkgExists()
    {
        var service = new UpdateService();
        var pkgOnly = new GitHubReleaseDto
        {
            TagName = "v1.1.1-preview1",
            Prerelease = true,
            HtmlUrl = "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/tag/v1.1.1-preview1",
            Assets = new List<GitHubAssetDto>
            {
                new() { Name = "CLegendaryLauncher-osx-arm64-Setup.pkg", BrowserDownloadUrl = "https://example.com/arm64.pkg" },
                new() { Name = "CLegendaryLauncher-osx-x64-Setup.pkg", BrowserDownloadUrl = "https://example.com/x64.pkg" }
            }
        };

        Assert.Null(service.SelectBestRelease(new[] { pkgOnly }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.Arm64));
        Assert.Null(service.SelectBestRelease(new[] { pkgOnly }, UpdateChannel.Testing, "1.1.0-preview9.8", OSPlatform.OSX, Architecture.X64));
    }

    private static byte[] CreateDummyZipBytes()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("marker.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("ok");
        }
        return ms.ToArray();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request));
        }
    }
}
