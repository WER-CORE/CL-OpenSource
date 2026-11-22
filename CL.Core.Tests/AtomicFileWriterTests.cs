using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Helpers;
using CL.Core.Services;
using CmlLib.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CL.Core.Tests
{
    public class AtomicFileWriterTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_responder(request));
            }
        }

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"cl-atomic-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CleanupDir(string dir)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Fact]
        public async Task WriteAllTextAtomicAsync_CreatesNewJsonAndLeavesNoTempFiles()
        {
            string dir = CreateTempDir();
            try
            {
                string target = Path.Combine(dir, "nested", "index.json");
                const string json = "{\"objects\":{\"test\":{\"hash\":\"abc\",\"size\":3}}}";

                await AtomicFileWriter.WriteAllTextAtomicAsync(target, json);

                Assert.True(File.Exists(target));
                var parsed = JObject.Parse(await File.ReadAllTextAsync(target));
                Assert.Equal("abc", parsed["objects"]?["test"]?["hash"]?.ToString());
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(target)!, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task WriteAllTextAtomicAsync_ReplacesExistingFileAndLeavesNoTempFiles()
        {
            string dir = CreateTempDir();
            try
            {
                string target = Path.Combine(dir, "cache.json");
                await File.WriteAllTextAsync(target, "{\"version\":1}");

                await AtomicFileWriter.WriteAllTextAtomicAsync(target, "{\"version\":2}");

                var parsed = JObject.Parse(await File.ReadAllTextAsync(target));
                Assert.Equal(2, parsed["version"]?.Value<int>());
                Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task WriteAllTextAtomicAsync_WhenCancelled_PreservesExistingFileAndCleansTempFiles()
        {
            string dir = CreateTempDir();
            try
            {
                string target = Path.Combine(dir, "index.json");
                const string original = "{\"intact\":true}";
                await File.WriteAllTextAsync(target, original);

                using var cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    AtomicFileWriter.WriteAllTextAtomicAsync(target, "{\"corrupt\":true}", cts.Token));

                Assert.Equal(original, await File.ReadAllTextAsync(target));
                Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task WriteAllTextAtomicAsync_ConcurrentWrites_DoNotCollideOrLeaveTempFiles()
        {
            string dir = CreateTempDir();
            try
            {
                string target = Path.Combine(dir, "concurrent.json");
                var tasks = Enumerable.Range(0, 12).Select(i =>
                    AtomicFileWriter.WriteAllTextAtomicAsync(target, $"{{\"run\":{i}}}"));

                await Task.WhenAll(tasks);

                Assert.True(File.Exists(target));
                var parsed = JObject.Parse(await File.ReadAllTextAsync(target));
                Assert.True(parsed["run"]?.Value<int>() is >= 0 and < 12);
                Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task EnsureMissingAssetsFromBetaCraftAsync_WritesAssetIndexAtomicallyAndCleansUpTemp()
        {
            string dir = CreateTempDir();
            try
            {
                var mcPath = new MinecraftPath(dir);
                byte[] assetBytes = Encoding.UTF8.GetBytes("sound-data");
                string assetSha1 = Convert.ToHexString(SHA1.HashData(assetBytes)).ToLowerInvariant();

                string indexJson = $"{{\"objects\":{{\"sounds/test.ogg\":{{\"hash\":\"{assetSha1}\",\"size\":{assetBytes.Length}}}}}}}";
                string indexSha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(indexJson))).ToLowerInvariant();

                string versionJsonPath = Path.Combine(mcPath.Versions, "b1.7.3", "b1.7.3.json");
                Directory.CreateDirectory(Path.GetDirectoryName(versionJsonPath)!);
                await File.WriteAllTextAsync(versionJsonPath, $@"{{
                    ""id"": ""b1.7.3"",
                    ""assetIndex"": {{
                        ""id"": ""legacy-test"",
                        ""sha1"": ""{indexSha1}"",
                        ""url"": ""https://files.betacraft.uk/launcher/v2/assets/indexes/legacy-test.json""
                    }}
                }}");

                using var httpClient = new HttpClient(new FakeHttpMessageHandler(req =>
                {
                    string url = req.RequestUri!.ToString();
                    if (url.EndsWith("legacy-test.json", StringComparison.Ordinal))
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(indexJson, Encoding.UTF8, "application/json") };
                    if (url.Contains("resources.download.minecraft.net", StringComparison.Ordinal))
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    if (url.Contains("files.betacraft.uk", StringComparison.Ordinal))
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(assetBytes) };
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }));

                var installer = new HistoricalVersionInstallerService(httpClient);
                int recovered = await installer.EnsureMissingAssetsFromBetaCraftAsync(versionJsonPath, mcPath);

                Assert.Equal(1, recovered);
                string indexPath = Path.Combine(mcPath.Assets, "indexes", "legacy-test.json");
                Assert.True(File.Exists(indexPath));
                Assert.Equal(indexJson, await File.ReadAllTextAsync(indexPath));
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(indexPath)!, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task EnsureMissingAssetsFromBetaCraftAsync_Sha1Mismatch_DoesNotWriteIndexOrTempFile()
        {
            string dir = CreateTempDir();
            try
            {
                var mcPath = new MinecraftPath(dir);
                string versionJsonPath = Path.Combine(mcPath.Versions, "b1.7.3", "b1.7.3.json");
                Directory.CreateDirectory(Path.GetDirectoryName(versionJsonPath)!);
                await File.WriteAllTextAsync(versionJsonPath, @"{
                    ""id"": ""b1.7.3"",
                    ""assetIndex"": {
                        ""id"": ""bad-sha"",
                        ""sha1"": ""0000000000000000000000000000000000000000"",
                        ""url"": ""https://files.betacraft.uk/launcher/v2/assets/indexes/bad-sha.json""
                    }
                }");

                using var httpClient = new HttpClient(new FakeHttpMessageHandler(_ =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"objects\":{}}", Encoding.UTF8, "application/json") }));

                var installer = new HistoricalVersionInstallerService(httpClient);
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    installer.EnsureMissingAssetsFromBetaCraftAsync(versionJsonPath, mcPath));

                string indexPath = Path.Combine(mcPath.Assets, "indexes", "bad-sha.json");
                Assert.False(File.Exists(indexPath));
            }
            finally
            {
                CleanupDir(dir);
            }
        }

        [Fact]
        public async Task HistoricalCatalogService_WritesCacheAtomicallyAndDoesNotFailOnUnwritableCache()
        {
            string dir = CreateTempDir();
            try
            {
                const string mojangJson = """{"latest":{"release":"1.21.1"},"versions":[{"id":"1.21.1","type":"release","url":"https://piston-meta.mojang.com/1.21.1.json","releaseTime":"2024-08-08T12:00:00+00:00"}]}""";
                const string bcJson = """{"versions":[{"id":"b1.7.3","type":"old_beta","url":"https://files.betacraft.uk/b1.7.3.json"}]}""";

                using var httpClient = new HttpClient(new FakeHttpMessageHandler(req =>
                {
                    string url = req.RequestUri!.ToString();
                    string body = url.Contains("betacraft", StringComparison.OrdinalIgnoreCase) ? bcJson : mojangJson;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                }));

                // Make target cache file path conflict with a directory to simulate unwritable cache path.
                string blockedCacheDir = Path.Combine(dir, "blocked");
                Directory.CreateDirectory(Path.Combine(blockedCacheDir, "mojang_manifest_v2.json"));

                var catalogService = new HistoricalVersionCatalogService(httpClient, blockedCacheDir);
                var result = await catalogService.GetMergedCatalogAsync();

                Assert.True(result.MojangLoaded);
                Assert.True(result.BetaCraftLoaded);
                Assert.Empty(Directory.GetFiles(blockedCacheDir, "*.tmp"));

                // Normal cache directory writes valid JSON atomically without leftover .tmp files.
                string normalCacheDir = Path.Combine(dir, "normal");
                var normalService = new HistoricalVersionCatalogService(httpClient, normalCacheDir);
                var normalResult = await normalService.GetMergedCatalogAsync();

                Assert.True(normalResult.MojangLoaded);
                string cachedMojang = Path.Combine(normalCacheDir, "mojang_manifest_v2.json");
                Assert.True(File.Exists(cachedMojang));
                JObject.Parse(await File.ReadAllTextAsync(cachedMojang));
                Assert.Empty(Directory.GetFiles(normalCacheDir, "*.tmp"));
            }
            finally
            {
                CleanupDir(dir);
            }
        }
    }
}
