using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CL.Core.Models.P2P;
using CL.Core.Services.P2P;
using Xunit;

namespace CL.Core.Tests
{
    public class P2PRoomTests
    {
        [Fact]
        public void GenerateAndParseRoomCode_Roundtrip_Succeeds()
        {
            var payload = new P2PRoomPayload
            {
                HostAddress = "192.168.1.100",
                Port = 25565,
                WorldName = "TestWorld",
                GameVersion = "1.20.1",
                LoaderType = "Fabric",
                LoaderVersion = "0.15.11",
                ModpackName = "TestModpack",
                SessionSecret = Guid.NewGuid().ToString("N")
            };

            string code = P2PRoomCodeService.GenerateRoomCode(payload);

            Assert.NotNull(code);
            Assert.StartsWith("CL-", code);

            bool success = P2PRoomCodeService.TryParseRoomCode(code, out var decoded);

            Assert.True(success);
            Assert.NotNull(decoded);
            Assert.Equal(payload.HostAddress, decoded!.HostAddress);
            Assert.Equal(payload.Port, decoded.Port);
            Assert.Equal(payload.WorldName, decoded.WorldName);
            Assert.Equal(payload.GameVersion, decoded.GameVersion);
            Assert.Equal(payload.LoaderType, decoded.LoaderType);
            Assert.Equal(payload.LoaderVersion, decoded.LoaderVersion);
            Assert.Equal(payload.ModpackName, decoded.ModpackName);
            Assert.Equal(payload.SessionSecret, decoded.SessionSecret);
        }

        [Fact]
        public void TryParseRoomCode_UriProtocolPrefix_Succeeds()
        {
            var payload = new P2PRoomPayload
            {
                HostAddress = "10.0.0.5",
                Port = 25577,
                WorldName = "UriWorld",
                GameVersion = "1.21",
                LoaderType = "Vanilla",
                SessionSecret = "secret123"
            };

            string code = P2PRoomCodeService.GenerateRoomCode(payload);
            string uriCode = "clegendary://join/" + code;

            bool success = P2PRoomCodeService.TryParseRoomCode(uriCode, out var decoded);

            Assert.True(success);
            Assert.NotNull(decoded);
            Assert.Equal(payload.WorldName, decoded!.WorldName);
            Assert.Equal(payload.Port, decoded.Port);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("CL-INVALID")]
        [InlineData("NOT-A-VALID-CODE-AT-ALL")]
        public void TryParseRoomCode_InvalidInput_ReturnsFalse(string? invalidCode)
        {
            bool success = P2PRoomCodeService.TryParseRoomCode(invalidCode!, out var decoded);
            Assert.False(success);
            Assert.Null(decoded);
        }

        [Fact]
        public async Task ManifestService_CompareManifestAsync_DetectsMissingMods()
        {
            var service = new P2PManifestService();
            string tempDir = Path.Combine(Path.GetTempPath(), "p2p_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var hostManifest = new P2PManifest
                {
                    GameVersion = "1.20.1",
                    LoaderType = "Fabric",
                    Mods = new List<P2PModEntry>
                    {
                        new() { Name = "sodium", FileName = "sodium.jar", Sha1 = "hash1" },
                        new() { Name = "lithium", FileName = "lithium.jar", Sha1 = "hash2" }
                    }
                };

                var (missing, isMatch) = await service.CompareManifestAsync(hostManifest, tempDir);

                Assert.False(isMatch);
                Assert.Equal(2, missing.Count);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }
}
