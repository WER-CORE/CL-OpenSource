using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;
using CL.Core.Services.P2P;
using Newtonsoft.Json;
using Xunit;

namespace CL.Core.Tests
{
    public class P2PHandshakeAndLoaderTests
    {
        private static byte[] BuildHandshakeFrame(int protocolVersion, string serverAddress, int port, int nextState)
        {
            byte[] payload = BuildHandshakePayload(protocolVersion, serverAddress, port, nextState);
            using var ms = new MemoryStream();
            WriteVarInt(ms, payload.Length);
            ms.Write(payload, 0, payload.Length);
            return ms.ToArray();
        }

        private static byte[] BuildHandshakePayload(int protocolVersion, string serverAddress, int port, int nextState)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x00);
            WriteVarInt(ms, protocolVersion);
            byte[] addrBytes = Encoding.UTF8.GetBytes(serverAddress);
            WriteVarInt(ms, addrBytes.Length);
            ms.Write(addrBytes, 0, addrBytes.Length);
            ms.WriteByte((byte)((port >> 8) & 0xFF));
            ms.WriteByte((byte)(port & 0xFF));
            WriteVarInt(ms, nextState);
            return ms.ToArray();
        }

        private static byte[] BuildLoginStartFrame(string username)
        {
            using var payloadMs = new MemoryStream();
            payloadMs.WriteByte(0x00);
            byte[] nameBytes = Encoding.UTF8.GetBytes(username);
            WriteVarInt(payloadMs, nameBytes.Length);
            payloadMs.Write(nameBytes, 0, nameBytes.Length);
            // 1.19.3+/1.21 Login Start includes player UUID (16 bytes)
            payloadMs.Write(new byte[16], 0, 16);

            byte[] payload = payloadMs.ToArray();
            using var frameMs = new MemoryStream();
            WriteVarInt(frameMs, payload.Length);
            frameMs.Write(payload, 0, payload.Length);
            return frameMs.ToArray();
        }

        private static byte[] BuildStatusRequestFrame()
        {
            return new byte[] { 0x01, 0x00 };
        }

        private static (int PacketId, int ProtocolVersion, string ServerAddress, int Port, int NextState) DecodeHandshakeFrame(byte[] frame)
        {
            using var ms = new MemoryStream(frame, false);
            int len = ReadVarInt(ms);
            Assert.Equal(frame.Length - (int)ms.Position, len);
            int packetId = ReadVarInt(ms);
            int protocol = ReadVarInt(ms);
            int addrLen = ReadVarInt(ms);
            byte[] addrBytes = new byte[addrLen];
            int read = ms.Read(addrBytes, 0, addrLen);
            Assert.Equal(addrLen, read);
            string addr = Encoding.UTF8.GetString(addrBytes);
            int port = (ms.ReadByte() << 8) | ms.ReadByte();
            int nextState = ReadVarInt(ms);
            return (packetId, protocol, addr, port, nextState);
        }

        private static async Task<byte[]> ReadFramedPacketAsync(Stream stream, CancellationToken token)
        {
            int len = await ReadVarIntAsync(stream, token);
            Assert.True(len > 0);
            byte[] buf = new byte[len];
            int readTotal = 0;
            while (readTotal < len)
            {
                int r = await stream.ReadAsync(buf.AsMemory(readTotal, len - readTotal), token);
                Assert.True(r > 0);
                readTotal += r;
            }
            return buf;
        }

        private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken token)
        {
            int result = 0;
            int shift = 0;
            byte[] single = new byte[1];
            while (true)
            {
                int r = await stream.ReadAsync(single.AsMemory(0, 1), token);
                Assert.Equal(1, r);
                int b = single[0];
                result |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) == 0) break;
            }
            return result;
        }

        private static void WriteVarInt(Stream stream, int value)
        {
            while ((value & -128) != 0)
            {
                stream.WriteByte((byte)((value & 127) | 128));
                value = (int)((uint)value >> 7);
            }
            stream.WriteByte((byte)value);
        }

        private static int ReadVarInt(Stream stream)
        {
            int result = 0;
            int shift = 0;
            while (true)
            {
                int b = stream.ReadByte();
                Assert.NotEqual(-1, b);
                result |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) == 0) break;
            }
            return result;
        }

        [Theory]
        [InlineData("vanilla.eu.e4mc.link", "127.0.0.1", null)]
        [InlineData("forge.eu.e4mc.link\0FORGE", "127.0.0.1\0FORGE", "FORGE")]
        [InlineData("forge.eu.e4mc.link\0FORGE1", "127.0.0.1\0FORGE1", "FORGE1")]
        [InlineData("legacy.eu.e4mc.link\0FML\0", "127.0.0.1\0FML\0", "FML")]
        [InlineData("modern.eu.e4mc.link\0FML2\0", "127.0.0.1\0FML2\0", "FML2")]
        [InlineData("modern3.eu.e4mc.link\0FML3\0", "127.0.0.1\0FML3\0", "FML3")]
        [InlineData("custom.eu.e4mc.link\0fabric_proxy\0token", "127.0.0.1\0fabric_proxy\0token", "fabric_proxy")]
        public void TryRewriteHandshakePayload_PreservesProtocolSuffixes(string incomingAddress, string expectedAddress, string? expectedExtension)
        {
            byte[] payload = BuildHandshakePayload(767, incomingAddress, 25565, 2);

            bool ok = P2PSecurityProxy.TryRewriteHandshakePayload(payload, 54321, out var result);

            Assert.True(ok);
            Assert.Equal(767, result.ProtocolVersion);
            Assert.Equal(25565, result.OriginalPort);
            Assert.Equal(54321, result.TargetPort);
            Assert.Equal(2, result.NextState);
            Assert.Equal(expectedAddress, result.ForwardedServerAddress);
            Assert.Equal(expectedExtension, result.ProtocolExtension);

            var decoded = DecodeHandshakeFrame(result.RewrittenPacket);
            Assert.Equal(0x00, decoded.PacketId);
            Assert.Equal(767, decoded.ProtocolVersion);
            Assert.Equal(expectedAddress, decoded.ServerAddress);
            Assert.Equal(54321, decoded.Port);
            Assert.Equal(2, decoded.NextState);
        }

        [Fact]
        public void TryRewriteHandshakePayload_RejectsNonHandshakePacket()
        {
            byte[] invalidPayload = new byte[] { 0x05, 0x01, 0x02 };
            bool ok = P2PSecurityProxy.TryRewriteHandshakePayload(invalidPayload, 54321, out _);
            Assert.False(ok);
        }

        [Fact]
        public async Task Proxy_Forge1211HandshakeAndLogin_PreservesForgeMarkerOverLoopback()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var lanListener = new TcpListener(IPAddress.Loopback, 0);
            lanListener.Start();
            int lanPort = ((IPEndPoint)lanListener.LocalEndpoint).Port;

            var logs = new ConcurrentQueue<string>();
            await using var proxy = new P2PSecurityProxy { HostUsername = "WER" };
            proxy.DiagnosticLogged += msg => logs.Enqueue(msg);
            await proxy.StartAsync(0, lanPort, cts.Token);

            var serverTask = Task.Run(async () =>
            {
                using var serverClient = await lanListener.AcceptTcpClientAsync(cts.Token);
                using var stream = serverClient.GetStream();

                byte[] hsPayload = await ReadFramedPacketAsync(stream, cts.Token);
                using var hsWithLen = new MemoryStream();
                WriteVarInt(hsWithLen, hsPayload.Length);
                hsWithLen.Write(hsPayload, 0, hsPayload.Length);
                var hs = DecodeHandshakeFrame(hsWithLen.ToArray());

                Assert.Equal(767, hs.ProtocolVersion);
                Assert.Equal("127.0.0.1\0FORGE", hs.ServerAddress);
                Assert.Equal(lanPort, hs.Port);
                Assert.Equal(2, hs.NextState);

                byte[] loginPayload = await ReadFramedPacketAsync(stream, cts.Token);
                Assert.Equal(0x00, loginPayload[0]);

                // Simulate Forge server response (e.g. Login Plugin Request 0x04 / payload)
                byte[] reply = new byte[] { 0x03, 0x04, 0x2A, 0x7F };
                await stream.WriteAsync(reply, cts.Token);
                await stream.FlushAsync(cts.Token);
            }, cts.Token);

            using var guest = new TcpClient();
            await guest.ConnectAsync(IPAddress.Loopback, proxy.BoundPort, cts.Token);
            using var guestStream = guest.GetStream();

            await guestStream.WriteAsync(BuildHandshakeFrame(767, "secret-room.eu.e4mc.link\0FORGE", 25565, 2), cts.Token);
            await guestStream.WriteAsync(BuildLoginStartFrame("Test"), cts.Token);
            await guestStream.FlushAsync(cts.Token);

            byte[] responsePayload = await ReadFramedPacketAsync(guestStream, cts.Token);
            Assert.Equal(new byte[] { 0x04, 0x2A, 0x7F }, responsePayload);

            await serverTask;
            Assert.Contains(logs, l => l.Contains("ext=FORGE") && l.Contains("nextState=Login(2)"));
            Assert.DoesNotContain(logs, l => l.Contains("secret-room.eu.e4mc.link"));
        }

        [Fact]
        public async Task Proxy_StatusPingAndLegacy0xFEPing_ForwardCorrectly()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var lanListener = new TcpListener(IPAddress.Loopback, 0);
            lanListener.Start();
            int lanPort = ((IPEndPoint)lanListener.LocalEndpoint).Port;

            await using var proxy = new P2PSecurityProxy();
            await proxy.StartAsync(0, lanPort, cts.Token);

            // 1. Modern Status Ping
            var modernServer = Task.Run(async () =>
            {
                using var c = await lanListener.AcceptTcpClientAsync(cts.Token);
                using var s = c.GetStream();
                byte[] hsPayload = await ReadFramedPacketAsync(s, cts.Token);
                using var hsFrame = new MemoryStream();
                WriteVarInt(hsFrame, hsPayload.Length);
                hsFrame.Write(hsPayload, 0, hsPayload.Length);
                var hs = DecodeHandshakeFrame(hsFrame.ToArray());
                Assert.Equal("127.0.0.1", hs.ServerAddress);
                Assert.Equal(lanPort, hs.Port);
                Assert.Equal(1, hs.NextState);

                byte[] statusReq = await ReadFramedPacketAsync(s, cts.Token);
                Assert.Equal(new byte[] { 0x00 }, statusReq);

                byte[] pong = new byte[] { 0x03, 0x00, 0x7B, 0x7D };
                await s.WriteAsync(pong, cts.Token);
                await s.FlushAsync(cts.Token);
            }, cts.Token);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, proxy.BoundPort, cts.Token);
                using var s = client.GetStream();
                await s.WriteAsync(BuildHandshakeFrame(767, "fabric.eu.e4mc.link", 25565, 1), cts.Token);
                await s.WriteAsync(BuildStatusRequestFrame(), cts.Token);
                await s.FlushAsync(cts.Token);

                byte[] resp = await ReadFramedPacketAsync(s, cts.Token);
                Assert.Equal(new byte[] { 0x00, 0x7B, 0x7D }, resp);
            }
            await modernServer;

            // 2. Legacy 0xFE 0x01 Ping
            var legacyServer = Task.Run(async () =>
            {
                using var c = await lanListener.AcceptTcpClientAsync(cts.Token);
                using var s = c.GetStream();
                byte[] buf = new byte[2];
                int r1 = await s.ReadAsync(buf.AsMemory(0, 1), cts.Token);
                int r2 = await s.ReadAsync(buf.AsMemory(1, 1), cts.Token);
                Assert.Equal(1, r1);
                Assert.Equal(1, r2);
                Assert.Equal(new byte[] { 0xFE, 0x01 }, buf);

                await s.WriteAsync(new byte[] { 0xFF, 0x00, 0x01, 0x00, 0x41 }, cts.Token);
                await s.FlushAsync(cts.Token);
            }, cts.Token);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, proxy.BoundPort, cts.Token);
                using var s = client.GetStream();
                await s.WriteAsync(new byte[] { 0xFE, 0x01 }, cts.Token);
                await s.FlushAsync(cts.Token);

                byte[] resp = new byte[5];
                int read = 0;
                while (read < resp.Length)
                {
                    int r = await s.ReadAsync(resp.AsMemory(read, resp.Length - read), cts.Token);
                    Assert.True(r > 0);
                    read += r;
                }
                Assert.Equal(new byte[] { 0xFF, 0x00, 0x01, 0x00, 0x41 }, resp);
            }
            await legacyServer;
        }

        [Fact]
        public void MinecraftPingHelper_ParseStatusJson_DetectsLoadersCorrectly()
        {
            var vanilla = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"1.21.1"},"description":{"text":"§aVanilla World"}}""");
            Assert.NotNull(vanilla);
            Assert.Equal(("1.21.1", "Vanilla", "Vanilla World"), vanilla!.Value);

            var forge1211 = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"1.21.1"},"description":{"text":"WER - New World"},"forgeData":{"fmlNetworkVersion":4,"channels":[],"mods":[{"modId":"minecraft"},{"modId":"forge"}]}}""");
            Assert.NotNull(forge1211);
            Assert.Equal(("1.21.1", "Forge", "WER - New World"), forge1211!.Value);

            var forge1122 = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"1.12.2"},"description":"Legacy Forge","modinfo":{"type":"FML","modList":[]}}""");
            Assert.NotNull(forge1122);
            Assert.Equal(("1.12.2", "Forge", "Legacy Forge"), forge1122!.Value);

            var neoforge = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"1.21.1"},"description":{"text":"Neo World"},"isModded":true}""");
            Assert.NotNull(neoforge);
            Assert.Equal(("1.21.1", "NeoForge", "Neo World"), neoforge!.Value);

            var fabric = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"Fabric 1.21.1"},"description":{"text":"Fabric World"}}""");
            Assert.NotNull(fabric);
            Assert.Equal(("1.21.1", "Fabric", "Fabric World"), fabric!.Value);

            var quilt = MinecraftPingHelper.ParseStatusJson("""{"version":{"name":"Quilt 1.20.1"},"description":{"text":"Quilt World"}}""");
            Assert.NotNull(quilt);
            Assert.Equal(("1.20.1", "Quilt", "Quilt World"), quilt!.Value);
        }

        [Fact]
        public async Task CustomModpack_RoundTripAndNoBlindAutoDownload()
        {
            var manifest = new CustomModpackManifest
            {
                Name = "Test",
                Author = "WER",
                Version = "1.0.0",
                Minecraft = "1.21.1",
                Loader = "Forge",
                LoaderVersion = "52.1.21",
                Files =
                {
                    new ModInfo
                    {
                        Name = "SampleMod",
                        Loader = "Forge",
                        LoaderType = "Forge",
                        FileName = "sample-1.0.0.jar"
                    }
                }
            };

            string json = JsonConvert.SerializeObject(manifest);
            var roundTripped = JsonConvert.DeserializeObject<CustomModpackManifest>(json);

            Assert.NotNull(roundTripped);
            Assert.Equal("1.21.1", roundTripped!.Minecraft);
            Assert.Equal("Forge", roundTripped.Loader);
            Assert.Equal("52.1.21", roundTripped.LoaderVersion);
            Assert.Single(roundTripped.Files);

            var service = new ModpackService(null!, null!, null!);
            var downloaded = await service.AutoDownloadAndInstallModpackAsync(
                "Test",
                "1.21.1",
                "Forge",
                typeSite: "Custom");

            Assert.Null(downloaded);
        }
    }
}
