using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;
using Xunit;

namespace CL.Core.Tests;

public class VelopackChannelTests
{
    private const string RepoUrl = "https://github.com/WER-CORE/CL-Win-Edition--Update";

    private sealed class FakeFileDownloader : IFileDownloader
    {
        private readonly Dictionary<string, string> _textResponses;

        public FakeFileDownloader(Dictionary<string, string> textResponses)
        {
            _textResponses = textResponses;
        }

        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            if (_textResponses.TryGetValue(url, out var txt))
                return Task.FromResult(Encoding.UTF8.GetBytes(txt));
            throw new InvalidOperationException($"Unexpected URL requested: {url}");
        }

        public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            foreach (var kvp in _textResponses)
            {
                if (url.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(kvp.Value);
            }
            throw new InvalidOperationException($"Unexpected URL requested: {url}");
        }

        public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default) =>
            Task.CompletedTask;
    }

    private static Dictionary<string, string> BuildFixtureResponses(bool includePreview)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append("""
        {
          "name": "v1.1.0",
          "prerelease": false,
          "published_at": "2026-11-16T12:00:00Z",
          "assets": [
            {
              "name": "releases.win.json",
              "url": "https://api.github.com/assets/stable/releases.win.json",
              "browser_download_url": "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0/releases.win.json"
            },
            {
              "name": "CLegendaryLauncher-1.1.0-full.nupkg",
              "url": "https://api.github.com/assets/stable/CLegendaryLauncher-1.1.0-full.nupkg",
              "browser_download_url": "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0/CLegendaryLauncher-1.1.0-full.nupkg"
            }
          ]
        }
        """);

        if (includePreview)
        {
            sb.Append(',');
            sb.Append("""
            {
              "name": "v1.1.0-preview9.8",
              "prerelease": true,
              "published_at": "2026-11-15T12:00:00Z",
              "assets": [
                {
                  "name": "releases.win.json",
                  "url": "https://api.github.com/assets/preview/releases.win.json",
                  "browser_download_url": "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0-preview9.8/releases.win.json"
                },
                {
                  "name": "CLegendaryLauncher-1.1.0-preview9.8-full.nupkg",
                  "url": "https://api.github.com/assets/preview/CLegendaryLauncher-1.1.0-preview9.8-full.nupkg",
                  "browser_download_url": "https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0-preview9.8/CLegendaryLauncher-1.1.0-preview9.8-full.nupkg"
                }
              ]
            }
            """);
        }
        sb.Append(']');

        var stableFeed = """
        {
          "Assets": [
            {
              "PackageId": "CLegendaryLauncher",
              "Version": "1.1.0",
              "Type": "Full",
              "FileName": "CLegendaryLauncher-1.1.0-full.nupkg",
              "SHA1": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
              "Size": 1000
            }
          ]
        }
        """;

        var previewFeed = """
        {
          "Assets": [
            {
              "PackageId": "CLegendaryLauncher",
              "Version": "1.1.0-preview9.8",
              "Type": "Full",
              "FileName": "CLegendaryLauncher-1.1.0-preview9.8-full.nupkg",
              "SHA1": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
              "Size": 1000
            }
          ]
        }
        """;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://api.github.com/repos/WER-CORE/CL-Win-Edition--Update/releases"] = sb.ToString(),
            ["https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0/releases.win.json"] = stableFeed,
            ["https://github.com/WER-CORE/CL-Win-Edition--Update/releases/download/v1.1.0-preview9.8/releases.win.json"] = previewFeed
        };
    }

    [Fact]
    public async Task StockVelopackGithubSource_IncludesStableInPrereleaseMode_ProvingRootCause()
    {
        var downloader = new FakeFileDownloader(BuildFixtureResponses(includePreview: true));
        var stockSource = new GithubSource(RepoUrl, null, prerelease: true, downloader: downloader);

        VelopackAssetFeed feed = await stockSource.GetReleaseFeed(NullVelopackLogger.Instance, "CLegendaryLauncher", "win");

        Assert.Contains(feed.Assets, a => a.Version.ToString() == "1.1.0");
    }

    [Fact]
    public async Task ChannelStrictGithubSource_TestingChannel_ExcludesStableReleases()
    {
        var downloader = new FakeFileDownloader(BuildFixtureResponses(includePreview: true));
        var testingSource = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: true, downloader: downloader);

        VelopackAssetFeed feed = await testingSource.GetReleaseFeed(NullVelopackLogger.Instance, "CLegendaryLauncher", "win");

        Assert.Single(feed.Assets);
        Assert.Equal("1.1.0-preview9.8", feed.Assets[0].Version.ToString());
        Assert.DoesNotContain(feed.Assets, a => a.Version.ToString() == "1.1.0");
    }

    [Fact]
    public async Task ChannelStrictGithubSource_StableChannel_ExcludesPrereleases()
    {
        var downloader = new FakeFileDownloader(BuildFixtureResponses(includePreview: true));
        var stableSource = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: false, downloader: downloader);

        VelopackAssetFeed feed = await stableSource.GetReleaseFeed(NullVelopackLogger.Instance, "CLegendaryLauncher", "win");

        Assert.Single(feed.Assets);
        Assert.Equal("1.1.0", feed.Assets[0].Version.ToString());
        Assert.DoesNotContain(feed.Assets, a => a.Version.ToString() == "1.1.0-preview9.8");
    }

    [Fact]
    public async Task ChannelStrictGithubSource_TestingChannel_ReturnsEmptyFeed_WhenOnlyStableExists()
    {
        var downloader = new FakeFileDownloader(BuildFixtureResponses(includePreview: false));
        var testingSource = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: true, downloader: downloader);

        VelopackAssetFeed feed = await testingSource.GetReleaseFeed(NullVelopackLogger.Instance, "CLegendaryLauncher", "win");

        Assert.Empty(feed.Assets);
    }

    private sealed class ThrowingFileDownloader : IFileDownloader
    {
        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30) =>
            throw new HttpRequestException("Simulated Velopack network failure");

        public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30) =>
            throw new HttpRequestException("Simulated Velopack network failure");

        public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default) =>
            throw new HttpRequestException("Simulated Velopack network failure");
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public CountingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    [Theory]
    [InlineData(UpdateChannel.Testing)]
    [InlineData(UpdateChannel.Stable)]
    public async Task Coordinator_VelopackInstalled_DoesNotFallbackToZipAndLogs_WhenVelopackThrows(UpdateChannel channel)
    {
        string tempPkgDir = Path.Combine(Path.GetTempPath(), "cl_vpk_err_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPkgDir);
        try
        {
            var locator = new TestVelopackLocator("CLegendaryLauncher", "1.1.0-preview9.2", tempPkgDir, NullVelopackLogger.Instance);
            var logs = new List<string>();

            var zipHandler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                [
                  {
                    "tag_name": "v1.1.1-preview1",
                    "prerelease": true,
                    "draft": false,
                    "assets": [
                      {
                        "name": "CLegendaryLauncher-Windows-Portable.zip",
                        "browser_download_url": "https://example.com/CLegendaryLauncher-Windows-Portable.zip"
                      }
                    ]
                  },
                  {
                    "tag_name": "v1.1.1",
                    "prerelease": false,
                    "draft": false,
                    "assets": [
                      {
                        "name": "CLegendaryLauncher-Windows-Portable.zip",
                        "browser_download_url": "https://example.com/CLegendaryLauncher-Windows-Portable.zip"
                      }
                    ]
                  }
                ]
                """, Encoding.UTF8, "application/json")
            });
            var zipService = new UpdateService(new HttpClient(zipHandler));

            var result = await LauncherUpdateCoordinator.CheckForUpdatesInternalAsync(
                respectSkippedVersion: false,
                channel: channel,
                skippedVersion: string.Empty,
                localVersion: "1.1.0-preview9.2",
                velopackManagerFactory: isPreview =>
                {
                    var source = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: isPreview, downloader: new ThrowingFileDownloader());
                    return new UpdateManager(source, null, locator);
                },
                updateService: zipService,
                log: logs.Add);

            Assert.Null(result);
            Assert.Equal(0, zipHandler.CallCount);
            Assert.Contains(logs, m => m.Contains("ZIP fallback suppressed", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempPkgDir)) Directory.Delete(tempPkgDir, true);
        }
    }

    [Fact]
    public async Task Coordinator_VelopackInstalled_UsesVelopackOnlyAndPreservesTestingIsolation()
    {
        string tempPkgDir = Path.Combine(Path.GetTempPath(), "cl_vpk_ok_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPkgDir);
        try
        {
            var locator = new TestVelopackLocator("CLegendaryLauncher", "1.1.0-preview9.2", tempPkgDir, NullVelopackLogger.Instance);
            var zipHandler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var zipService = new UpdateService(new HttpClient(zipHandler));

            // Testing із preview9.8 + Stable 1.1.0 -> обирає лише preview9.8 через Velopack, 0 викликів ZIP fallback
            var previewResult = await LauncherUpdateCoordinator.CheckForUpdatesInternalAsync(
                respectSkippedVersion: false,
                channel: UpdateChannel.Testing,
                skippedVersion: string.Empty,
                localVersion: "1.1.0-preview9.2",
                velopackManagerFactory: isPreview =>
                {
                    var source = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: isPreview, downloader: new FakeFileDownloader(BuildFixtureResponses(includePreview: true)));
                    return new UpdateManager(source, new UpdateOptions { ExplicitChannel = "win" }, locator);
                },
                updateService: zipService);

            Assert.NotNull(previewResult);
            Assert.True(previewResult!.IsVelopack);
            Assert.Equal("1.1.0-preview9.8", previewResult.TargetVersion);
            Assert.Equal(0, zipHandler.CallCount);

            // Testing, коли є лише Stable 1.1.0 -> повертає null і не переходить на ZIP fallback
            var onlyStableResult = await LauncherUpdateCoordinator.CheckForUpdatesInternalAsync(
                respectSkippedVersion: false,
                channel: UpdateChannel.Testing,
                skippedVersion: string.Empty,
                localVersion: "1.1.0-preview9.2",
                velopackManagerFactory: isPreview =>
                {
                    var source = new ChannelStrictGithubSource(RepoUrl, null, onlyPrerelease: isPreview, downloader: new FakeFileDownloader(BuildFixtureResponses(includePreview: false)));
                    return new UpdateManager(source, new UpdateOptions { ExplicitChannel = "win" }, locator);
                },
                updateService: zipService);

            Assert.Null(onlyStableResult);
            Assert.Equal(0, zipHandler.CallCount);
        }
        finally
        {
            if (Directory.Exists(tempPkgDir)) Directory.Delete(tempPkgDir, true);
        }
    }

    [Fact]
    public async Task Coordinator_PortableNotInstalled_UsesZipFallback_WithStrictChannelIsolation()
    {
        var zipHandler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            [
              {
                "tag_name": "v1.1.0",
                "prerelease": false,
                "draft": false,
                "assets": [
                  {
                    "name": "CLegendaryLauncher-Windows-Portable.zip",
                    "browser_download_url": "https://example.com/stable/CLegendaryLauncher-Windows-Portable.zip"
                  },
                  {
                    "name": "CLegendaryLauncher-osx-arm64-Portable.zip",
                    "browser_download_url": "https://example.com/stable/CLegendaryLauncher-osx-arm64-Portable.zip"
                  },
                  {
                    "name": "CLegendaryLauncher-x64.AppImage",
                    "browser_download_url": "https://example.com/stable/CLegendaryLauncher-x64.AppImage"
                  }
                ]
              },
              {
                "tag_name": "v1.1.1-preview1",
                "prerelease": true,
                "draft": false,
                "assets": [
                  {
                    "name": "CLegendaryLauncher-Windows-Portable.zip",
                    "browser_download_url": "https://example.com/preview/CLegendaryLauncher-Windows-Portable.zip"
                  },
                  {
                    "name": "CLegendaryLauncher-osx-arm64-Portable.zip",
                    "browser_download_url": "https://example.com/preview/CLegendaryLauncher-osx-arm64-Portable.zip"
                  },
                  {
                    "name": "CLegendaryLauncher-x64.AppImage",
                    "browser_download_url": "https://example.com/preview/CLegendaryLauncher-x64.AppImage"
                  }
                ]
              }
            ]
            """, Encoding.UTF8, "application/json")
        });
        var zipService = new UpdateService(new HttpClient(zipHandler));

        // 1) Portable + Testing -> обирає v1.1.1-preview1 через ZIP fallback
        var testingResult = await LauncherUpdateCoordinator.CheckForUpdatesInternalAsync(
            respectSkippedVersion: false,
            channel: UpdateChannel.Testing,
            skippedVersion: string.Empty,
            localVersion: "1.1.0-preview9.8",
            velopackManagerFactory: _ => null,
            updateService: zipService);

        Assert.NotNull(testingResult);
        Assert.False(testingResult!.IsVelopack);
        Assert.Equal("1.1.1-preview1", testingResult.TargetVersion);
        Assert.Contains("/preview/", testingResult.DownloadUrl);

        // 2) Portable + Stable -> ігнорує v1.1.1-preview1, повертає v1.1.0
        var stableResult = await LauncherUpdateCoordinator.CheckForUpdatesInternalAsync(
            respectSkippedVersion: false,
            channel: UpdateChannel.Stable,
            skippedVersion: string.Empty,
            localVersion: "1.0.9",
            velopackManagerFactory: _ => null,
            updateService: zipService);

        Assert.NotNull(stableResult);
        Assert.False(stableResult!.IsVelopack);
        Assert.Equal("1.1.0", stableResult.TargetVersion);
        Assert.Contains("/stable/", stableResult.DownloadUrl);
        Assert.Equal(2, zipHandler.CallCount);
    }
}
