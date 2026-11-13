using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
    public class HistoricalVersionInstallerTests
    {
        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _responder(request, cancellationToken);
            }
        }

        private static string CreateTempMcDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cl_legacy03_" + Guid.NewGuid().ToString("N"));
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

        private static string ComputeSha1(string text)
        {
            return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_MojangSource_DoesNotOverwriteExistingMetadata()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                string versionDir = Path.Combine(path.Versions, "1.21.1");
                Directory.CreateDirectory(versionDir);
                string jsonPath = Path.Combine(versionDir, "1.21.1.json");
                await File.WriteAllTextAsync(jsonPath, """{"id":"1.21.1","marker":"official"}""");

                var entry = new CatalogVersionEntry(
                    Id: "1.21.1",
                    RawType: "release",
                    NormalizedFilterType: "release",
                    Era: MinecraftEra.Release,
                    Source: VersionCatalogSource.Mojang,
                    MetadataUrl: "https://piston-meta.mojang.com/v1/packages/1.21.1.json");

                var svc = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    throw new InvalidOperationException("HTTP should not be called for Mojang entry."))));

                string returnedPath = await svc.EnsureVersionMetadataAsync(entry, path);

                Assert.Equal(jsonPath, returnedPath);
                Assert.Contains("official", await File.ReadAllTextAsync(jsonPath));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_BetaCraftValidJson_SavesAtomicallyAndSupportsReinstall()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                const string validJson = """
                {
                  "id": "in-20100218-0016",
                  "type": "release",
                  "downloads": {
                    "client": {
                      "url": "https://vault.omniarchive.uk/archive/java/client-indev/in-20100218-0016.jar",
                      "size": 4
                    }
                  }
                }
                """;
                string expectedSha1 = ComputeSha1(validJson);
                string? requestedUrl = null;

                var svc = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((req, _) =>
                {
                    requestedUrl = req.RequestUri?.AbsoluteUri;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(validJson, Encoding.UTF8, "application/json")
                    });
                })));

                var entry = new CatalogVersionEntry(
                    Id: "in-20100218-0016",
                    RawType: "old_alpha",
                    NormalizedFilterType: "old_alpha",
                    Era: MinecraftEra.Indev,
                    Source: VersionCatalogSource.BetaCraft,
                    MetadataUrl: "https://files.betacraft.uk/launcher/v2/assets/jsons/in-20100218-0016.json",
                    Sha1: expectedSha1);

                string jsonPath = await svc.EnsureVersionMetadataAsync(entry, path);
                Assert.Equal("https://files.betacraft.uk/launcher/v2/assets/jsons/in-20100218-0016.json", requestedUrl);
                Assert.True(File.Exists(jsonPath));

                string jsonPath2 = await svc.EnsureVersionMetadataAsync(entry, path);
                Assert.Equal(jsonPath, jsonPath2);
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(jsonPath)!, "*.tmp"));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Theory]
        [InlineData("../evil")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("")]
        public async Task EnsureVersionMetadataAsync_UnsafeVersionId_ThrowsArgumentException(string badId)
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var svc = new HistoricalVersionInstallerService();
                var entry = new CatalogVersionEntry(badId, "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft,
                    "https://files.betacraft.uk/launcher/v2/assets/jsons/test.json");

                await Assert.ThrowsAsync<ArgumentException>(() => svc.EnsureVersionMetadataAsync(entry, path));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Theory]
        [InlineData("http://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json")]
        [InlineData("https://untrusted.example.com/c0.0.13a-dev.json")]
        [InlineData("not-a-url")]
        [InlineData(null)]
        public async Task EnsureVersionMetadataAsync_UntrustedOrInvalidUrl_ThrowsInvalidOperationException(string? badUrl)
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var svc = new HistoricalVersionInstallerService();
                var entry = new CatalogVersionEntry("c0.0.13a-dev", "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft, badUrl);

                await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnsureVersionMetadataAsync(entry, path));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task EnsureVersionMetadataAsync_HttpErrors_ThrowAndLeaveNoPartialJson(HttpStatusCode code)
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var svc = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(code)))));

                var entry = new CatalogVersionEntry("c0.0.13a-dev", "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft,
                    "https://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json");

                await Assert.ThrowsAsync<HttpRequestException>(() => svc.EnsureVersionMetadataAsync(entry, path));
                Assert.False(File.Exists(Path.Combine(path.Versions, "c0.0.13a-dev", "c0.0.13a-dev.json")));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_NetworkFailure_ThrowsHttpRequestException()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var svc = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    throw new HttpRequestException("Network unreachable"))));

                var entry = new CatalogVersionEntry("c0.0.13a-dev", "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft,
                    "https://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json");

                await Assert.ThrowsAsync<HttpRequestException>(() => svc.EnsureVersionMetadataAsync(entry, path));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_MismatchedIdOrCorruptedJsonOrSha1Mismatch_ThrowsInvalidDataException()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var entryBase = new CatalogVersionEntry("c0.0.13a-dev", "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft,
                    "https://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json");

                var svcCorrupt = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad json") }))));
                await Assert.ThrowsAsync<InvalidDataException>(() => svcCorrupt.EnsureVersionMetadataAsync(entryBase, path));

                var svcMismatch = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"other-version","downloads":{"client":{"url":"https://launcher.mojang.com/x.jar"}}}""")
                    }))));
                await Assert.ThrowsAsync<InvalidDataException>(() => svcMismatch.EnsureVersionMetadataAsync(entryBase, path));

                var entryWithWrongSha1 = entryBase with { Sha1 = "0000000000000000000000000000000000000000" };
                var svcSha = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"c0.0.13a-dev","downloads":{"client":{"url":"https://launcher.mojang.com/x.jar"}}}""")
                    }))));
                await Assert.ThrowsAsync<InvalidDataException>(() => svcSha.EnsureVersionMetadataAsync(entryWithWrongSha1, path));

                var svcNoClient = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"c0.0.13a-dev"}""")
                    }))));
                await Assert.ThrowsAsync<InvalidDataException>(() => svcNoClient.EnsureVersionMetadataAsync(entryBase, path));

                Assert.False(File.Exists(Path.Combine(path.Versions, "c0.0.13a-dev", "c0.0.13a-dev.json")));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureVersionMetadataAsync_Cancellation_CancelsCleanlyWithoutPartialFiles()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                using var cts = new CancellationTokenSource();
                var svc = new HistoricalVersionInstallerService(new HttpClient(new StubHttpMessageHandler(async (_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                })));

                var entry = new CatalogVersionEntry("c0.0.13a-dev", "old_alpha", "old_alpha", MinecraftEra.Classic, VersionCatalogSource.BetaCraft,
                    "https://files.betacraft.uk/launcher/v2/assets/jsons/c0.0.13a-dev.json");

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.EnsureVersionMetadataAsync(entry, path, cts.Token));
                Assert.False(File.Exists(Path.Combine(path.Versions, "c0.0.13a-dev", "c0.0.13a-dev.json")));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public void TryValidateInstallation_DetectsPartialOrMissingFilesAndAcceptsCompleteLegacyAndModernVersions()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                string verDir = Path.Combine(path.Versions, "c0.0.13a-dev");
                Directory.CreateDirectory(verDir);

                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                string jsonPath = Path.Combine(verDir, "c0.0.13a-dev.json");
                File.WriteAllText(jsonPath, """
                {
                  "id": "c0.0.13a-dev",
                  "downloads": { "client": { "size": 5 } },
                  "libraries": [
                    { "name": "uk.betacraft.legacyfix:legacyfix:2.0", "downloads": { "artifact": { "path": "uk/betacraft/legacyfix/2.0/legacyfix-2.0.jar" } } }
                  ],
                  "assetIndex": { "id": "empty" }
                }
                """);
                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                string jarPath = Path.Combine(verDir, "c0.0.13a-dev.jar");
                File.WriteAllBytes(jarPath, new byte[] { 1, 2 });
                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                File.WriteAllBytes(jarPath, new byte[] { 1, 2, 3, 4, 5 });
                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                string libPath = Path.Combine(path.Library, "uk", "betacraft", "legacyfix", "2.0", "legacyfix-2.0.jar");
                Directory.CreateDirectory(Path.GetDirectoryName(libPath)!);
                File.WriteAllBytes(libPath, new byte[] { 9, 9 });
                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                string indexPath = Path.Combine(path.Assets, "indexes", "empty.json");
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
                File.WriteAllText(indexPath, "{}");
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));

                string forgeDir = Path.Combine(path.Versions, "1.21.1-forge-52.1.21");
                Directory.CreateDirectory(forgeDir);
                File.WriteAllText(Path.Combine(forgeDir, "1.21.1-forge-52.1.21.json"), """{"id":"1.21.1-forge-52.1.21","inheritsFrom":"1.21.1"}""");
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("1.21.1-forge-52.1.21", path));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task RealIntegration_BetaCraftClassic_C0013aDev_InstallsCleanlyInTempPath()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                var catalog = new HistoricalVersionCatalogService(cacheDirectory: Path.Combine(root, "cache"));
                var merged = await catalog.GetMergedCatalogAsync(forceRefresh: true);
                var classicEntry = System.Linq.Enumerable.FirstOrDefault(
                    merged.Versions,
                    v => string.Equals(v.Id, "c0.0.13a-dev", StringComparison.OrdinalIgnoreCase) &&
                         v.Source == VersionCatalogSource.BetaCraft);

                Assert.NotNull(classicEntry);

                var launcher = new MinecraftLauncher(path);
                bool sawProgress = false;
                launcher.FileProgressChanged += (_, _) => sawProgress = true;

                var installer = new HistoricalVersionInstallerService();
                var result = await installer.InstallAsync(classicEntry!, launcher);

                Assert.True(result.Installed);
                Assert.True(File.Exists(result.JsonPath));
                Assert.NotNull(result.ClientJarPath);
                Assert.True(File.Exists(result.ClientJarPath));
                Assert.True(sawProgress);
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("c0.0.13a-dev", path));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureMissingAssetsFromBetaCraftAsync_WhenAssetMissingLocally_DownloadsVerifiedAssetAndLogsDiagnostics()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                byte[] assetBytes = new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x01, 0x02, 0x03, 0x04 };
                string assetHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(assetBytes)).ToLowerInvariant();

                string versionDir = Path.Combine(path.Versions, "b1.9-pre6");
                Directory.CreateDirectory(versionDir);
                await File.WriteAllBytesAsync(Path.Combine(versionDir, "b1.9-pre6.jar"), new byte[] { 0x50, 0x4B, 0x03, 0x04 });

                string versionJsonPath = Path.Combine(versionDir, "b1.9-pre6.json");
                await File.WriteAllTextAsync(versionJsonPath, """
                {
                  "id": "b1.9-pre6",
                  "type": "snapshot",
                  "assetIndex": {
                    "id": "b1.7",
                    "url": "https://files.betacraft.uk/launcher/v2/assets/indexes/b1.7.json"
                  },
                  "mainClass": "uk.betacraft.legacyfix.LegacyFixLauncher"
                }
                """);

                string indexesDir = Path.Combine(path.Assets, "indexes");
                Directory.CreateDirectory(indexesDir);
                await File.WriteAllTextAsync(Path.Combine(indexesDir, "b1.7.json"), $$"""
                {
                  "objects": {
                    "newsound/random/door_close.ogg": {
                      "hash": "{{assetHash}}",
                      "size": {{assetBytes.Length}}
                    }
                  }
                }
                """);

                Assert.False(HistoricalVersionInstallerService.TryValidateInstallation("b1.9-pre6", path, out string? beforeError));
                Assert.Contains("newsound/random/door_close.ogg", beforeError, StringComparison.OrdinalIgnoreCase);

                string expectedUrl = $"https://files.betacraft.uk/launcher/v2/assets/resources/{assetHash[..2]}/{assetHash}";
                var handler = new StubHttpMessageHandler((req, _) =>
                {
                    if (req.RequestUri?.ToString() == expectedUrl)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(assetBytes)
                        });
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                });

                var diagnostics = new System.Collections.Generic.List<string>();
                var installer = new HistoricalVersionInstallerService(new HttpClient(handler));
                installer.DiagnosticLogged += msg => diagnostics.Add(msg);

                int downloaded = await installer.EnsureMissingAssetsFromBetaCraftAsync(versionJsonPath, path);

                Assert.Equal(1, downloaded);
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("b1.9-pre6", path));
                Assert.Contains(diagnostics, d => d.Contains(expectedUrl, StringComparison.OrdinalIgnoreCase) && d.Contains("200", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public async Task EnsureMissingAssetsFromBetaCraftAsync_WhenBetaCraftSha1Mismatches_ThrowsInvalidDataException()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                byte[] expectedBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(expectedBytes)).ToLowerInvariant();
                byte[] corruptBytes = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

                string versionDir = Path.Combine(path.Versions, "b1.9-pre6");
                Directory.CreateDirectory(versionDir);
                string versionJsonPath = Path.Combine(versionDir, "b1.9-pre6.json");
                await File.WriteAllTextAsync(versionJsonPath, """
                {
                  "id": "b1.9-pre6",
                  "assetIndex": {
                    "id": "b1.7",
                    "url": "https://files.betacraft.uk/launcher/v2/assets/indexes/b1.7.json"
                  }
                }
                """);

                string indexesDir = Path.Combine(path.Assets, "indexes");
                Directory.CreateDirectory(indexesDir);
                await File.WriteAllTextAsync(Path.Combine(indexesDir, "b1.7.json"), $$"""
                {
                  "objects": {
                    "newsound/random/bow.ogg": {
                      "hash": "{{expectedHash}}",
                      "size": {{expectedBytes.Length}}
                    }
                  }
                }
                """);

                var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(corruptBytes)
                }));

                var installer = new HistoricalVersionInstallerService(new HttpClient(handler));
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    installer.EnsureMissingAssetsFromBetaCraftAsync(versionJsonPath, path));

                string objectPath = Path.Combine(path.Assets, "objects", expectedHash[..2], expectedHash);
                Assert.False(File.Exists(objectPath));
            }
            finally
            {
                CleanupDir(root);
            }
        }

        [Fact]
        public void LaunchPrep_AlreadyInstalledB19Pre6AndModern1211_PassIsVersionInstalledValidation()
        {
            string root = CreateTempMcDir();
            try
            {
                var path = new MinecraftPath(root);
                byte[] assetBytes = new byte[] { 0x10, 0x20, 0x30 };
                string assetHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(assetBytes)).ToLowerInvariant();

                string b19Dir = Path.Combine(path.Versions, "b1.9-pre6");
                Directory.CreateDirectory(b19Dir);
                File.WriteAllBytes(Path.Combine(b19Dir, "b1.9-pre6.jar"), new byte[] { 0x50, 0x4B });
                File.WriteAllText(Path.Combine(b19Dir, "b1.9-pre6.json"), """{"id":"b1.9-pre6","assetIndex":{"id":"b1.7"}}""");

                string indexesDir = Path.Combine(path.Assets, "indexes");
                Directory.CreateDirectory(indexesDir);
                File.WriteAllText(Path.Combine(indexesDir, "b1.7.json"), $$"""
                {
                  "objects": {
                    "newsound/random/door_open.ogg": { "hash": "{{assetHash}}", "size": {{assetBytes.Length}} }
                  }
                }
                """);

                Assert.False(HistoricalVersionInstallerService.IsVersionInstalled("b1.9-pre6", path));

                string objDir = Path.Combine(path.Assets, "objects", assetHash[..2]);
                Directory.CreateDirectory(objDir);
                File.WriteAllBytes(Path.Combine(objDir, assetHash), assetBytes);
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("b1.9-pre6", path));

                string modernDir = Path.Combine(path.Versions, "1.21.1");
                Directory.CreateDirectory(modernDir);
                File.WriteAllBytes(Path.Combine(modernDir, "1.21.1.jar"), new byte[] { 0x50, 0x4B });
                File.WriteAllText(Path.Combine(modernDir, "1.21.1.json"), """{"id":"1.21.1","assetIndex":{"id":"b1.7"}}""");
                Assert.True(HistoricalVersionInstallerService.IsVersionInstalled("1.21.1", path));
            }
            finally
            {
                CleanupDir(root);
            }
        }
    }
}
