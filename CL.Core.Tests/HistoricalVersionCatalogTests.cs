using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using Newtonsoft.Json;
using Xunit;

namespace CL.Core.Tests
{
    public class HistoricalVersionCatalogTests
    {
        private const string SampleMojangJson = """
        {
          "latest": { "release": "1.21.1", "snapshot": "24w33a" },
          "versions": [
            { "id": "1.21.1", "type": "release", "url": "https://piston-meta.mojang.com/v1/packages/1.21.1.json", "releaseTime": "2024-08-08T12:00:00+00:00", "sha1": "aaa111" },
            { "id": "24w33a", "type": "snapshot", "url": "https://piston-meta.mojang.com/v1/packages/24w33a.json", "releaseTime": "2024-08-14T12:00:00+00:00" },
            { "id": "b1.7.3", "type": "old_beta", "url": "https://piston-meta.mojang.com/v1/packages/b1.7.3.json", "releaseTime": "2011-07-08T12:00:00+00:00", "sha1": "bbb222" },
            { "id": "a1.1.2_01", "type": "old_alpha", "url": "https://piston-meta.mojang.com/v1/packages/a1.1.2_01.json", "releaseTime": "2010-09-23T12:00:00+00:00" },
            { "id": "inf-20100618", "type": "old_alpha", "url": "https://piston-meta.mojang.com/v1/packages/inf-20100618.json", "releaseTime": "2010-06-18T12:00:00+00:00", "sha1": "ccc333" },
            { "id": "rd-132211", "type": "old_alpha", "url": "https://piston-meta.mojang.com/v1/packages/rd-132211.json", "releaseTime": "2009-05-13T12:00:00+00:00" }
          ]
        }
        """;

        private const string SampleBetaCraftJson = """
        {
          "trim_at": "1.7.10",
          "versions": [
            { "id": "13w36a", "type": "snapshot", "url": "https://files.betacraft.uk/launcher/v2/assets/jsons/13w36a.json", "releaseTime": "2013-09-05T12:00:00+00:00" },
            { "id": "b1.7.3", "type": "old_beta", "url": "https://piston-meta.mojang.com/v1/packages/b1.7.3.json", "releaseTime": "2011-07-08T12:00:00+00:00" },
            { "id": "b1.2_02", "type": "old_beta", "url": "https://files.betacraft.uk/launcher/v2/assets/jsons/b1.2_02.json", "releaseTime": "2011-01-21T12:00:00+00:00" },
            { "id": "inf-20100618", "type": "old_alpha", "url": "https://files.betacraft.uk/launcher/v2/assets/jsons/inf-20100618.json", "releaseTime": "2010-06-18T12:00:00+00:00" },
            { "id": "in-20100218-0016", "type": "old_alpha", "url": "https://files.betacraft.uk/launcher/v2/assets/jsons/in-20100218-0016.json", "releaseTime": "2010-02-18T00:16:00+00:00" },
            { "id": "c0.0.13a-dev", "type": "old_alpha", "url": "https://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json" }
          ]
        }
        """;

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responder(request));
            }
        }

        [Fact]
        public void ParseMojangManifest_ParsesVersionsAndPreservesRelease()
        {
            var entries = HistoricalVersionCatalogService.ParseMojangManifest(SampleMojangJson);

            Assert.Equal(6, entries.Count);
            Assert.Contains(entries, e => e.Id == "1.21.1" && e.Era == MinecraftEra.Release && e.Source == VersionCatalogSource.Mojang);
            Assert.Contains(entries, e => e.Id == "rd-132211" && e.Era == MinecraftEra.PreClassic && e.NormalizedFilterType == "old_alpha");
        }

        [Fact]
        public void ParseBetaCraftManifest_ParsesMissingOptionalFieldsSafely()
        {
            var entries = HistoricalVersionCatalogService.ParseBetaCraftManifest(SampleBetaCraftJson);

            Assert.Equal(6, entries.Count);
            var classic = Assert.Single(entries, e => e.Id == "c0.0.13a-dev");
            Assert.Equal(MinecraftEra.Classic, classic.Era);
            Assert.Null(classic.ReleaseTime);
            Assert.Null(classic.Sha1);
            Assert.Equal(VersionCatalogSource.BetaCraft, classic.Source);
        }

        [Theory]
        [InlineData("rd-132211", "old_alpha", null, MinecraftEra.PreClassic, "old_alpha")]
        [InlineData("c0.0.13a-dev", "old_alpha", null, MinecraftEra.Classic, "old_alpha")]
        [InlineData("in-20100218-0016", "old_alpha", null, MinecraftEra.Indev, "old_alpha")]
        [InlineData("inf-20100618", "old_alpha", null, MinecraftEra.Infdev, "old_alpha")]
        [InlineData("a1.1.2_01", "old_alpha", null, MinecraftEra.Alpha, "old_alpha")]
        [InlineData("b1.7.3", "old_beta", null, MinecraftEra.Beta, "old_beta")]
        [InlineData("13w36a", "snapshot", "2013-09-05T12:00:00+00:00", MinecraftEra.HistoricalSnapshot, "snapshot")]
        [InlineData("24w33a", "snapshot", "2024-08-14T12:00:00+00:00", MinecraftEra.Snapshot, "snapshot")]
        [InlineData("1.21.1", "release", "2024-08-08T12:00:00+00:00", MinecraftEra.Release, "release")]
        public void ClassifyEra_AndNormalizeFilterType_ClassifiesAllHistoricalEras(
            string id,
            string rawType,
            string? releaseTimeIso,
            MinecraftEra expectedEra,
            string expectedFilterType)
        {
            DateTimeOffset? dt = releaseTimeIso != null ? DateTimeOffset.Parse(releaseTimeIso) : null;
            var era = HistoricalVersionCatalogService.ClassifyEra(id, rawType, dt);
            var filterType = HistoricalVersionCatalogService.NormalizeFilterType(era, rawType);

            Assert.Equal(expectedEra, era);
            Assert.Equal(expectedFilterType, filterType);
        }

        [Fact]
        public void MergeCatalogs_DeduplicatesIdenticalAndResolvesConflictsWithMojangPriority()
        {
            var mojang = HistoricalVersionCatalogService.ParseMojangManifest(SampleMojangJson);
            var betacraft = HistoricalVersionCatalogService.ParseBetaCraftManifest(SampleBetaCraftJson);

            var merged = HistoricalVersionCatalogService.MergeCatalogs(mojang, betacraft);

            // 6 from Mojang + 4 unique from BetaCraft (13w36a, b1.2_02, in-20100218-0016, c0.0.13a-dev)
            Assert.Equal(10, merged.Versions.Count);

            // Identical URL/type duplicate (b1.7.3) must not duplicate or flag conflict
            var b173 = Assert.Single(merged.Versions, v => v.Id == "b1.7.3");
            Assert.False(b173.HasConflict);
            Assert.Equal(VersionCatalogSource.Mojang, b173.Source);

            // Conflicting URL (inf-20100618) keeps Mojang and records conflict
            var inf = Assert.Single(merged.Versions, v => v.Id == "inf-20100618");
            Assert.True(inf.HasConflict);
            Assert.Equal(VersionCatalogSource.Mojang, inf.Source);
            Assert.Equal("https://piston-meta.mojang.com/v1/packages/inf-20100618.json", inf.MetadataUrl);
            Assert.Single(merged.ConflictLogs);
        }

        [Fact]
        public async Task GetMergedCatalogAsync_WhenBetaCraftUnavailableOrCorrupted_KeepsMojangCatalog()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "cl_legacy_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                // 1. BetaCraft returns 503
                var unavailableHandler = new StubHttpMessageHandler(req =>
                {
                    if (req.RequestUri!.AbsoluteUri.Contains("mojang"))
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleMojangJson, Encoding.UTF8, "application/json") };
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                });

                var svc1 = new HistoricalVersionCatalogService(new HttpClient(unavailableHandler), tempCache);
                var res1 = await svc1.GetMergedCatalogAsync(forceRefresh: true);

                Assert.True(res1.MojangLoaded);
                Assert.False(res1.BetaCraftLoaded);
                Assert.NotNull(res1.BetaCraftError);
                Assert.Equal(6, res1.Versions.Count);
                Assert.Contains(res1.Versions, v => v.Id == "1.21.1");

                // 2. BetaCraft returns invalid JSON
                var invalidJsonHandler = new StubHttpMessageHandler(req =>
                {
                    if (req.RequestUri!.AbsoluteUri.Contains("mojang"))
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleMojangJson, Encoding.UTF8, "application/json") };
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad json", Encoding.UTF8, "application/json") };
                });

                var svc2 = new HistoricalVersionCatalogService(new HttpClient(invalidJsonHandler), tempCache);
                var res2 = await svc2.GetMergedCatalogAsync(forceRefresh: true);

                Assert.True(res2.MojangLoaded);
                Assert.False(res2.BetaCraftLoaded);
                Assert.Equal(6, res2.Versions.Count);
            }
            finally
            {
                if (Directory.Exists(tempCache)) Directory.Delete(tempCache, true);
            }
        }

        [Fact]
        public void ParseManifest_InvalidJson_ThrowsJsonException()
        {
            Assert.ThrowsAny<JsonException>(() => HistoricalVersionCatalogService.ParseBetaCraftManifest("{not valid"));
            Assert.ThrowsAny<JsonException>(() => HistoricalVersionCatalogService.ParseMojangManifest("{}"));
        }

        [Fact]
        public void SortCatalogEntries_IsDeterministicAndNewestFirst()
        {
            var mojang = HistoricalVersionCatalogService.ParseMojangManifest(SampleMojangJson);
            var betacraft = HistoricalVersionCatalogService.ParseBetaCraftManifest(SampleBetaCraftJson);
            var merged = HistoricalVersionCatalogService.MergeCatalogs(mojang, betacraft);

            var ids = merged.Versions.Select(v => v.Id).ToList();
            Assert.Equal("24w33a", ids[0]);
            Assert.Equal("1.21.1", ids[1]);
            Assert.Equal("c0.0.13a-dev", ids[^1]); // null ReleaseTime sorts after dated entries
        }

        [Theory]
        [InlineData("rd-132211", "11701")]
        [InlineData("c0.0.13a-dev", "11701")]
        [InlineData("in-20100218-0016", "11702")]
        [InlineData("inf-20100618", "11702")]
        [InlineData("a1.1.2_01", "11702")]
        [InlineData("a1.2.6", "11705")]
        [InlineData("b1.7.3", "11705")]
        [InlineData("1.5.2", "11707")]
        [InlineData("1.21.1", null)]
        public void Legacy01B_GetProxyPort_NoRegression(string version, string? expectedPort)
        {
            Assert.Equal(expectedPort, GameLaunchService.GetProxyPort(version));
        }

        [Fact]
        public async Task LiveIntegration_MojangAndBetaCraft_MergesHistoricalEras()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "cl_legacy_live_" + Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new HistoricalVersionCatalogService(cacheDirectory: tempCache);
                var result = await svc.GetMergedCatalogAsync(forceRefresh: true);

                Assert.True(result.MojangLoaded);
                Assert.True(result.BetaCraftLoaded, $"BetaCraft load failed: {result.BetaCraftError}");
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.PreClassic);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Classic);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Indev);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Infdev);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Alpha);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Beta);
                Assert.Contains(result.Versions, v => v.Era == MinecraftEra.Release);
                Assert.True(result.Versions.Count > 1000);
            }
            finally
            {
                if (Directory.Exists(tempCache)) Directory.Delete(tempCache, true);
            }
        }
    }
}
