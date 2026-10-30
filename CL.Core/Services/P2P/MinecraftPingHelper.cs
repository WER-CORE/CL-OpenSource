using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services.P2P;

public static class MinecraftPingHelper
{
    public static async Task<(string Version, string Loader)?> TryGetServerVersionAsync(int port, CancellationToken token = default)
    {
        var info = await TryGetServerInfoAsync(port, token).ConfigureAwait(false);
        if (info == null) return null;
        return (info.Value.Version, info.Value.Loader);
    }

    public static async Task<(string Version, string Loader, string Motd)?> TryGetServerInfoAsync(int port, CancellationToken token = default)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            var timeoutTask = Task.Delay(1500, token);
            if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) != connectTask || !client.Connected)
            {
                return null;
            }

            using var stream = client.GetStream();
            stream.ReadTimeout = 2000;
            stream.WriteTimeout = 2000;

            using var ms = new MemoryStream();
            WriteVarInt(ms, 0x00);
            WriteVarInt(ms, 47);
            WriteString(ms, "127.0.0.1");
            ms.WriteByte((byte)(port >> 8));
            ms.WriteByte((byte)(port & 0xFF));
            WriteVarInt(ms, 1);

            byte[] handshakeData = ms.ToArray();
            using var handshakePacket = new MemoryStream();
            WriteVarInt(handshakePacket, handshakeData.Length);
            handshakePacket.Write(handshakeData, 0, handshakeData.Length);
            byte[] packetBytes = handshakePacket.ToArray();
            await stream.WriteAsync(packetBytes, token).ConfigureAwait(false);

            byte[] statusRequest = new byte[] { 0x01, 0x00 };
            await stream.WriteAsync(statusRequest, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);

            if (!TryReadVarInt(stream, out int respLen) || respLen <= 0 || respLen > 65535)
                return null;

            if (!TryReadVarInt(stream, out int packetId) || packetId != 0x00)
                return null;

            if (!TryReadVarInt(stream, out int jsonLen) || jsonLen <= 0 || jsonLen > 65535)
                return null;

            byte[] jsonBuffer = new byte[jsonLen];
            int readTotal = 0;
            while (readTotal < jsonLen)
            {
                int r = await stream.ReadAsync(jsonBuffer.AsMemory(readTotal, jsonLen - readTotal), token).ConfigureAwait(false);
                if (r <= 0) break;
                readTotal += r;
            }

            string jsonStr = Encoding.UTF8.GetString(jsonBuffer, 0, readTotal);
            var parsed = JObject.Parse(jsonStr);

            string? verName = parsed["version"]?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(verName)) return null;

            string loader = "Vanilla";
            if (verName.Contains("Fabric", StringComparison.OrdinalIgnoreCase)) loader = "Fabric";
            else if (verName.Contains("NeoForge", StringComparison.OrdinalIgnoreCase)) loader = "NeoForge";
            else if (verName.Contains("Forge", StringComparison.OrdinalIgnoreCase)) loader = "Forge";
            else if (verName.Contains("Quilt", StringComparison.OrdinalIgnoreCase)) loader = "Quilt";

            string cleanVersion = verName;
            var parts = verName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (char.IsDigit(part[0]) && part.Contains('.'))
                {
                    cleanVersion = part;
                    break;
                }
            }

            string motd = string.Empty;
            var descToken = parsed["description"];
            if (descToken != null)
            {
                if (descToken is JValue jVal) motd = jVal.ToString();
                else if (descToken is JObject jObj) motd = jObj["text"]?.ToString() ?? string.Empty;
                motd = System.Text.RegularExpressions.Regex.Replace(motd, @"§[0-9a-fk-or]", string.Empty).Trim();
            }

            return (cleanVersion, loader, motd);
        }
        catch
        {
            return null;
        }
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

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static bool TryReadVarInt(Stream stream, out int result)
    {
        result = 0;
        int numRead = 0;
        int read;
        do
        {
            read = stream.ReadByte();
            if (read == -1) return false;
            int value = (read & 0x7F);
            result |= (value << (7 * numRead));
            numRead++;
            if (numRead > 5) return false;
        } while ((read & 0x80) != 0);

        return true;
    }
}
