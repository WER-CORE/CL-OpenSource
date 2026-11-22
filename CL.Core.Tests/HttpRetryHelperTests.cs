using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using CmlLib.Core;
using Xunit;

namespace CL.Core.Tests
{
    public class HttpRetryHelperTests
    {
        private sealed class TrackingHttpContent : StringContent
        {
            private readonly Action _onDispose;

            public TrackingHttpContent(string content, Action onDispose)
                : base(content, Encoding.UTF8, "application/json")
            {
                _onDispose = onDispose;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _onDispose();
                }
                base.Dispose(disposing);
            }
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;
            public int CallCount { get; private set; }

            public FakeHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
            {
                _responder = responder;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return await _responder(request, CallCount, cancellationToken);
            }
        }

        private static Task NoOpDelay(TimeSpan _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetStringAsync_503Then200_SucceedsOnSecondAttempt()
        {
            var handler = new FakeHandler((_, attempt, _) => Task.FromResult(
                attempt == 1
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }));

            using var client = new HttpClient(handler);
            string result = await HttpRetryHelper.GetStringAsync(
                client,
                "https://example.test/data",
                delayAsync: NoOpDelay,
                jitterProvider: () => 0);

            Assert.Equal("ok", result);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task GetStringAsync_503TwiceThen200_SucceedsOnThirdAttempt()
        {
            var handler = new FakeHandler((_, attempt, _) => Task.FromResult(
                attempt < 3
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("recovered") }));

            using var client = new HttpClient(handler);
            string result = await HttpRetryHelper.GetStringAsync(
                client,
                "https://example.test/data",
                delayAsync: NoOpDelay,
                jitterProvider: () => 0);

            Assert.Equal("recovered", result);
            Assert.Equal(3, handler.CallCount);
        }

        [Fact]
        public async Task GetStringAsync_503ThreeTimes_ThrowsAfterThirdAttempt()
        {
            var handler = new FakeHandler((_, _, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            using var client = new HttpClient(handler);
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                HttpRetryHelper.GetStringAsync(
                    client,
                    "https://example.test/data",
                    delayAsync: NoOpDelay,
                    jitterProvider: () => 0));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
            Assert.Equal(3, handler.CallCount);
        }

        [Fact]
        public async Task GetStringAsync_429WithRetryAfter_RespectsDelayAndRetries()
        {
            var observedDelays = new List<TimeSpan>();
            var handler = new FakeHandler((_, attempt, _) =>
            {
                if (attempt == 1)
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                    return Task.FromResult(resp);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok-429") });
            });

            using var client = new HttpClient(handler);
            string result = await HttpRetryHelper.GetStringAsync(
                client,
                "https://example.test/rate",
                delayAsync: (d, ct) => { observedDelays.Add(d); return Task.CompletedTask; },
                jitterProvider: () => 0);

            Assert.Equal("ok-429", result);
            Assert.Equal(2, handler.CallCount);
            Assert.Single(observedDelays);
            Assert.True(observedDelays[0] >= TimeSpan.FromSeconds(5));

            var dateDelay = HttpRetryHelper.TryGetRetryAfterDelay(
                new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(7)),
                DateTimeOffset.UtcNow);
            Assert.NotNull(dateDelay);
            Assert.InRange(dateDelay!.Value.TotalSeconds, 5.5, 8.0);
        }

        [Fact]
        public async Task GetStringAsync_429WithExcessiveRetryAfter_FailsFastWithoutWaiting()
        {
            var handler = new FakeHandler((_, _, _) =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));
                return Task.FromResult(resp);
            });

            using var client = new HttpClient(handler);
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                HttpRetryHelper.GetStringAsync(
                    client,
                    "https://example.test/rate",
                    maxRetryAfter: TimeSpan.FromSeconds(15),
                    delayAsync: NoOpDelay));

            Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
            Assert.Equal(1, handler.CallCount);
            Assert.Contains("Retry-After", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task SendAsync_NonTransientStatus_ExecutesExactlyOnce(HttpStatusCode statusCode)
        {
            var handler = new FakeHandler((_, _, _) =>
                Task.FromResult(new HttpResponseMessage(statusCode)));

            using var client = new HttpClient(handler);
            using var resp = await HttpRetryHelper.SendAsync(
                client,
                () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/item"),
                delayAsync: NoOpDelay);

            Assert.Equal(statusCode, resp.StatusCode);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task GetStringAsync_408RequestTimeout_RetriesAndSucceeds()
        {
            var handler = new FakeHandler((_, attempt, _) => Task.FromResult(
                attempt == 1
                    ? new HttpResponseMessage(HttpStatusCode.RequestTimeout)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok-408") }));

            using var client = new HttpClient(handler);
            string result = await HttpRetryHelper.GetStringAsync(
                client,
                "https://example.test/timeout",
                delayAsync: NoOpDelay,
                jitterProvider: () => 0);

            Assert.Equal("ok-408", result);
            Assert.Equal(2, handler.CallCount);
        }

        [Fact]
        public async Task GetStringAsync_HttpRequestException_RetriesWithinLimit()
        {
            var handler = new FakeHandler((_, attempt, _) =>
            {
                if (attempt < 3)
                    throw new HttpRequestException("Connection reset by peer");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("recovered-net") });
            });

            using var client = new HttpClient(handler);
            string result = await HttpRetryHelper.GetStringAsync(
                client,
                "https://example.test/net",
                delayAsync: NoOpDelay,
                jitterProvider: () => 0);

            Assert.Equal("recovered-net", result);
            Assert.Equal(3, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_CancelledBeforeFirstRequest_SendsZeroRequests()
        {
            var handler = new FakeHandler((_, _, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using var client = new HttpClient(handler);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                HttpRetryHelper.SendAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/cancel"),
                    cts.Token,
                    delayAsync: NoOpDelay));

            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_CancelledDuringBackoff_DoesNotExecuteNextAttempt()
        {
            using var cts = new CancellationTokenSource();
            var handler = new FakeHandler((_, _, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            using var client = new HttpClient(handler);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                HttpRetryHelper.SendAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/backoff"),
                    cts.Token,
                    delayAsync: (_, ct) =>
                    {
                        cts.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }));

            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_CancelledDuringActiveRequest_ThrowsWithoutRetry()
        {
            using var cts = new CancellationTokenSource();
            var handler = new FakeHandler(async (_, _, ct) =>
            {
                cts.Cancel();
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            using var client = new HttpClient(handler);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                HttpRetryHelper.SendAsync(
                    client,
                    () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/inflight"),
                    cts.Token,
                    delayAsync: NoOpDelay));

            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task SendAsync_DisposesFailedResponsesBetweenRetries()
        {
            int disposedCount = 0;
            var handler = new FakeHandler((_, attempt, _) =>
            {
                if (attempt < 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
                    {
                        Content = new TrackingHttpContent("bad", () => disposedCount++)
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                });
            });

            using var client = new HttpClient(handler);
            using var finalResp = await HttpRetryHelper.SendAsync(
                client,
                () => new HttpRequestMessage(HttpMethod.Get, "https://example.test/dispose"),
                delayAsync: NoOpDelay,
                jitterProvider: () => 0);

            Assert.Equal(HttpStatusCode.OK, finalResp.StatusCode);
            Assert.Equal(2, disposedCount);
        }

        [Fact]
        public async Task BetaCraftFallback_OnMojang404_DoesNotRetry404AndFallsBackImmediately()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"cl-retry-bc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var mcPath = new MinecraftPath(dir);
                byte[] assetBytes = Encoding.UTF8.GetBytes("legacy-sound");
                string assetSha1 = Convert.ToHexString(SHA1.HashData(assetBytes)).ToLowerInvariant();
                string indexJson = $"{{\"objects\":{{\"sound.ogg\":{{\"hash\":\"{assetSha1}\",\"size\":{assetBytes.Length}}}}}}}";
                string indexSha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(indexJson))).ToLowerInvariant();

                string versionJsonPath = Path.Combine(mcPath.Versions, "b1.7.3", "b1.7.3.json");
                Directory.CreateDirectory(Path.GetDirectoryName(versionJsonPath)!);
                await File.WriteAllTextAsync(versionJsonPath, $@"{{
                    ""id"": ""b1.7.3"",
                    ""assetIndex"": {{
                        ""id"": ""bc-fallback"",
                        ""sha1"": ""{indexSha1}"",
                        ""url"": ""https://files.betacraft.uk/launcher/v2/assets/indexes/bc-fallback.json""
                    }}
                }}");

                int mojangCalls = 0;
                int betacraftAssetCalls = 0;
                using var client = new HttpClient(new FakeHandler((req, _, _) =>
                {
                    string url = req.RequestUri!.ToString();
                    if (url.EndsWith("bc-fallback.json", StringComparison.Ordinal))
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(indexJson, Encoding.UTF8, "application/json") });
                    if (url.Contains("resources.download.minecraft.net", StringComparison.Ordinal))
                    {
                        mojangCalls++;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                    }
                    if (url.Contains("files.betacraft.uk", StringComparison.Ordinal))
                    {
                        betacraftAssetCalls++;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(assetBytes) });
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                }));

                var installer = new HistoricalVersionInstallerService(client);
                int recovered = await installer.EnsureMissingAssetsFromBetaCraftAsync(versionJsonPath, mcPath);

                Assert.Equal(1, recovered);
                Assert.Equal(1, mojangCalls);
                Assert.Equal(1, betacraftAssetCalls);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_InvalidJsonOrSha1Mismatch_DoesNotRetryHttp()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"cl-retry-sha-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var mcPath = new MinecraftPath(dir);
                var handler = new FakeHandler((_, _, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("not-valid-json", Encoding.UTF8, "application/json")
                    }));

                using var client = new HttpClient(handler);
                var installer = new HistoricalVersionInstallerService(client);
                var entry = new CatalogVersionEntry(
                    Id: "b1.7.3",
                    RawType: "old_beta",
                    NormalizedFilterType: "old_beta",
                    Era: MinecraftEra.Beta,
                    Source: VersionCatalogSource.BetaCraft,
                    MetadataUrl: "https://files.betacraft.uk/launcher/v2/assets/jsons/b1.7.3.json",
                    Sha1: "0000000000000000000000000000000000000000");

                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    installer.EnsureVersionMetadataAsync(entry, mcPath));

                Assert.Equal(1, handler.CallCount);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
    }
}
