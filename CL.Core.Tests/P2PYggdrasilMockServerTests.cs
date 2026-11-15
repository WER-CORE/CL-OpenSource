using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CL.Core.Services.P2P;
using Xunit;

namespace CL.Core.Tests
{
    public class P2PYggdrasilMockServerTests
    {
        private static string ComputeOfflineUuid(string username)
        {
            byte[] input = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
            byte[] hash = MD5.HashData(input);
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        [Fact]
        public async Task ProfileEndpoint_RegisteredWerUuid_ReturnsWerName()
        {
            using var server = new P2PYggdrasilMockServer();
            server.Start();
            Assert.True(server.IsRunning);

            string werUuid = ComputeOfflineUuid("WER");
            server.RegisterProfile(werUuid, "WER");

            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{server.Port}/api/yggdrasil/sessionserver/session/minecraft/profile/{werUuid}?unsigned=false");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = JsonDocument.Parse(body);
            Assert.Equal(werUuid, doc.RootElement.GetProperty("id").GetString());
            Assert.Equal("WER", doc.RootElement.GetProperty("name").GetString());
        }

        [Fact]
        public async Task ProfileEndpoint_UnknownUuid_DoesNotReturnPlayerProfile()
        {
            using var server = new P2PYggdrasilMockServer();
            server.Start();
            Assert.True(server.IsRunning);

            server.RegisterProfile(ComputeOfflineUuid("WER"), "WER");
            string unknownUuid = Guid.NewGuid().ToString("N");

            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{server.Port}/api/yggdrasil/sessionserver/session/minecraft/profile/{unknownUuid}");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(string.IsNullOrEmpty(body));
            Assert.DoesNotContain("Player", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProfileEndpoint_TwoDifferentUuids_ReturnRespectiveNames()
        {
            using var server = new P2PYggdrasilMockServer();
            server.Start();
            Assert.True(server.IsRunning);

            string werUuid = ComputeOfflineUuid("WER");
            string alexUuid = ComputeOfflineUuid("Alex");
            string dashedAlexUuid = Guid.ParseExact(alexUuid, "N").ToString("D").ToUpperInvariant();

            server.RegisterProfile(werUuid, "WER");
            server.RegisterProfile(dashedAlexUuid, "Alex");

            using var http = new HttpClient();

            var werResponse = await http.GetAsync($"http://127.0.0.1:{server.Port}/session/minecraft/profile/{werUuid}");
            var alexResponse = await http.GetAsync($"http://127.0.0.1:{server.Port}/session/minecraft/profile/{alexUuid}");

            Assert.Equal(HttpStatusCode.OK, werResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, alexResponse.StatusCode);

            using var werDoc = JsonDocument.Parse(await werResponse.Content.ReadAsStringAsync());
            using var alexDoc = JsonDocument.Parse(await alexResponse.Content.ReadAsStringAsync());

            Assert.Equal("WER", werDoc.RootElement.GetProperty("name").GetString());
            Assert.Equal("Alex", alexDoc.RootElement.GetProperty("name").GetString());
        }

        [Fact]
        public async Task JoinAndHasJoinedAndMetadata_ContinueToWork()
        {
            using var server = new P2PYggdrasilMockServer();
            server.Start();
            Assert.True(server.IsRunning);

            using var http = new HttpClient();

            var metaResponse = await http.GetAsync($"http://127.0.0.1:{server.Port}/api/yggdrasil");
            string metaBody = await metaResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, metaResponse.StatusCode);
            Assert.Contains("CL-P2P", metaBody);

            var joinResponse = await http.PostAsync(
                $"http://127.0.0.1:{server.Port}/api/yggdrasil/sessionserver/session/minecraft/join",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NoContent, joinResponse.StatusCode);

            var hasJoinedResponse = await http.GetAsync(
                $"http://127.0.0.1:{server.Port}/api/yggdrasil/sessionserver/session/minecraft/hasJoined?username=WER&serverId=12345");
            string hasJoinedBody = await hasJoinedResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, hasJoinedResponse.StatusCode);
            using var doc = JsonDocument.Parse(hasJoinedBody);
            string expectedUuid = ComputeOfflineUuid("WER");
            Assert.Equal(expectedUuid, doc.RootElement.GetProperty("id").GetString());
            Assert.Equal("WER", doc.RootElement.GetProperty("name").GetString());
        }
    }
}
