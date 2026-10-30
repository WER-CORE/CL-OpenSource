using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models.P2P;

namespace CL.Core.Services.P2P;

public class P2PSecurityProxy : IAsyncDisposable, IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private readonly ConcurrentDictionary<string, (TcpClient Client, TcpClient Server, P2PPlayer Player)> _activeConnections = new();
    private bool _isDisposed;

    public int BoundPort { get; private set; }
    public int TargetLocalPort { get; private set; }
    public bool RequireApproval { get; set; }
    public string ExpectedSessionSecret { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;

    public event Action<P2PPlayer>? PlayerConnected;
    public event Action<P2PPlayer>? PlayerDisconnected;
    public Func<P2PPlayer, Task<bool>>? ApprovalRequested { get; set; }

    public IReadOnlyCollection<P2PPlayer> ConnectedPlayers
    {
        get
        {
            var list = new System.Collections.Generic.List<P2PPlayer>();
            foreach (var pair in _activeConnections.Values)
            {
                list.Add(pair.Player);
            }
            return list;
        }
    }

    public Task StartAsync(int listenPort, int targetLocalPort, CancellationToken token = default)
    {
        StopAsync().GetAwaiter().GetResult();

        BoundPort = listenPort;
        TargetLocalPort = targetLocalPort;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        _listener = new TcpListener(IPAddress.Any, listenPort);
        _listener.Start();

        _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var incomingClient = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                incomingClient.NoDelay = true;
                _ = Task.Run(() => HandleClientAsync(incomingClient, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                await Task.Delay(200, token).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        string connectionId = Guid.NewGuid().ToString("N");
        NetworkStream? clientStream = null;
        TcpClient? server = null;
        NetworkStream? serverStream = null;
        P2PPlayer? player = null;

        try
        {
            clientStream = client.GetStream();
            clientStream.ReadTimeout = 5000;

            // 1. DPI: Read & Validate Minecraft Handshake Packet (ID 0x00)
            if (!TryReadVarInt(clientStream, out int packetLength) || packetLength <= 0 || packetLength > 2048)
            {
                CloseQuietly(client);
                return;
            }

            byte[] packetBuffer = new byte[packetLength];
            int readBytes = 0;
            while (readBytes < packetLength)
            {
                int r = await clientStream.ReadAsync(packetBuffer.AsMemory(readBytes, packetLength - readBytes), token).ConfigureAwait(false);
                if (r <= 0) { CloseQuietly(client); return; }
                readBytes += r;
            }

            using var packetMs = new MemoryStream(packetBuffer, false);
            if (!TryReadVarInt(packetMs, out int packetId) || packetId != 0x00)
            {
                // Not a valid Minecraft handshake (HTTP bot, scanner probe, exploit)
                CloseQuietly(client);
                return;
            }

            if (!TryReadVarInt(packetMs, out int protocolVersion)) { CloseQuietly(client); return; }
            string serverAddress = ReadMinecraftString(packetMs);
            if (packetMs.Position + 2 > packetMs.Length) { CloseQuietly(client); return; }
            int port = (packetMs.ReadByte() << 8) | packetMs.ReadByte();
            if (!TryReadVarInt(packetMs, out int nextState)) { CloseQuietly(client); return; }

            // 2. Connect to local Minecraft LAN server
            server = new TcpClient();
            server.NoDelay = true;
            await server.ConnectAsync(IPAddress.Loopback, TargetLocalPort, token).ConfigureAwait(false);
            serverStream = server.GetStream();

            // Reconstruct Handshake packet targeting 127.0.0.1 loopback
            // This ensures the local IntegratedServer treats incoming connections as local loopback
            byte[] normalizedHandshake = CreateHandshakePacket(protocolVersion, "127.0.0.1", TargetLocalPort, nextState);
            await serverStream.WriteAsync(normalizedHandshake, token).ConfigureAwait(false);

            if (nextState == 2) // Login State
            {
                // Read Login Start Packet (ID 0x00)
                if (!TryReadVarInt(clientStream, out int loginLen) || loginLen <= 0 || loginLen > 2048)
                {
                    CloseQuietly(client, server);
                    return;
                }

                byte[] loginBuffer = new byte[loginLen];
                int loginRead = 0;
                while (loginRead < loginLen)
                {
                    int r = await clientStream.ReadAsync(loginBuffer.AsMemory(loginRead, loginLen - loginRead), token).ConfigureAwait(false);
                    if (r <= 0) { CloseQuietly(client, server); return; }
                    loginRead += r;
                }

                using var loginMs = new MemoryStream(loginBuffer, false);
                if (!TryReadVarInt(loginMs, out int loginPacketId) || loginPacketId != 0x00)
                {
                    CloseQuietly(client, server);
                    return;
                }

                string username = ReadMinecraftString(loginMs);
                if (string.IsNullOrWhiteSpace(username) || username.Length > 20)
                {
                    CloseQuietly(client, server);
                    return;
                }

                // Check 1: Host nickname impersonation protection
                if (!string.IsNullOrEmpty(HostUsername) &&
                    string.Equals(username.Trim(), HostUsername.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    byte[] disconnectPacket = CreateLoginDisconnectPacket("Цей нікнейм належить хосту кімнати.");
                    await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
                    await clientStream.FlushAsync(token).ConfigureAwait(false);
                    CloseQuietly(client, server);
                    return;
                }

                // Check 2: Nickname uniqueness among connected players
                bool isAlreadyConnected = false;
                foreach (var pair in _activeConnections.Values)
                {
                    if (string.Equals(pair.Player.Username.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        isAlreadyConnected = true;
                        break;
                    }
                }

                if (isAlreadyConnected)
                {
                    byte[] disconnectPacket = CreateLoginDisconnectPacket($"Гравець з нікнеймом {username} вже підключений до гри.");
                    await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
                    await clientStream.FlushAsync(token).ConfigureAwait(false);
                    CloseQuietly(client, server);
                    return;
                }

                player = new P2PPlayer
                {
                    ConnectionId = connectionId,
                    Username = username,
                    ConnectedAt = DateTime.UtcNow
                };

                // Optional approval check
                if (RequireApproval && ApprovalRequested != null)
                {
                    bool isApproved = await ApprovalRequested.Invoke(player).ConfigureAwait(false);
                    if (!isApproved)
                    {
                        byte[] disconnectPacket = CreateLoginDisconnectPacket("Ваш запит на підключення відхилено хостом.");
                        await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
                        await clientStream.FlushAsync(token).ConfigureAwait(false);
                        CloseQuietly(client, server);
                        return;
                    }
                }

                // Forward Login Start packet to game server
                await WriteVarIntAsync(serverStream, loginLen, token).ConfigureAwait(false);
                await serverStream.WriteAsync(loginBuffer.AsMemory(0, loginLen), token).ConfigureAwait(false);

                _activeConnections[connectionId] = (client, server, player);
                PlayerConnected?.Invoke(player);
            }

            // 3. Bidirectional pipe using pooled buffers
            var clientToServer = PipeStreamAsync(clientStream, serverStream, token);
            var serverToClient = PipeStreamAsync(serverStream, clientStream, token);

            await Task.WhenAny(clientToServer, serverToClient).ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            if (player != null)
            {
                _activeConnections.TryRemove(connectionId, out _);
                PlayerDisconnected?.Invoke(player);
            }
            CloseQuietly(client, server);
        }
    }

    private static async Task PipeStreamAsync(Stream input, Stream output, CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            while (!token.IsCancellationRequested)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (read <= 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void KickPlayer(string connectionId)
    {
        if (_activeConnections.TryRemove(connectionId, out var pair))
        {
            CloseQuietly(pair.Client, pair.Server);
            PlayerDisconnected?.Invoke(pair.Player);
        }
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _cts.Dispose(); } catch { }
            _cts = null;
        }

        if (_listener != null)
        {
            try { _listener.Stop(); } catch { }
            _listener = null;
        }

        foreach (var pair in _activeConnections.Values)
        {
            CloseQuietly(pair.Client, pair.Server);
        }
        _activeConnections.Clear();

        if (_acceptTask != null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch { }
            _acceptTask = null;
        }
    }

    private static void CloseQuietly(params TcpClient?[] clients)
    {
        foreach (var c in clients)
        {
            if (c == null) continue;
            try { c.GetStream().Dispose(); } catch { }
            try { c.Client?.Shutdown(SocketShutdown.Both); } catch { }
            try { c.Close(); } catch { }
            try { c.Dispose(); } catch { }
        }
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

    private static async Task WriteVarIntAsync(Stream stream, int value, CancellationToken token)
    {
        while ((value & -128) != 0)
        {
            byte b = (byte)((value & 127) | 128);
            await stream.WriteAsync(new[] { b }.AsMemory(0, 1), token).ConfigureAwait(false);
            value = (int)((uint)value >> 7);
        }
        await stream.WriteAsync(new[] { (byte)value }.AsMemory(0, 1), token).ConfigureAwait(false);
    }

    private static string ReadMinecraftString(Stream stream)
    {
        if (!TryReadVarInt(stream, out int length) || length <= 0 || length > 32767)
            return string.Empty;

        byte[] buffer = new byte[length];
        int read = stream.Read(buffer, 0, length);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static byte[] CreateHandshakePacket(int protocolVersion, string host, int port, int nextState)
    {
        using var payloadMs = new MemoryStream();
        // Packet ID 0x00 (Handshake)
        payloadMs.WriteByte(0x00);

        // Protocol version (VarInt)
        WriteVarInt(payloadMs, protocolVersion);

        // Host (String)
        byte[] hostBytes = Encoding.UTF8.GetBytes(host);
        WriteVarInt(payloadMs, hostBytes.Length);
        payloadMs.Write(hostBytes, 0, hostBytes.Length);

        // Port (Unsigned Short, Big Endian)
        payloadMs.WriteByte((byte)((port >> 8) & 0xFF));
        payloadMs.WriteByte((byte)(port & 0xFF));

        // Next state (VarInt)
        WriteVarInt(payloadMs, nextState);

        byte[] payload = payloadMs.ToArray();

        using var finalMs = new MemoryStream();
        WriteVarInt(finalMs, payload.Length);
        finalMs.Write(payload, 0, payload.Length);

        return finalMs.ToArray();
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

    private static byte[] CreateLoginDisconnectPacket(string reason)
    {
        string json = $"{{\"text\":\"{reason}\"}}";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using var ms = new MemoryStream();
        // Packet ID 0x00 (Login Disconnect)
        ms.WriteByte(0x00);

        // Write string length + string bytes
        int strLen = jsonBytes.Length;
        while ((strLen & -128) != 0)
        {
            ms.WriteByte((byte)((strLen & 127) | 128));
            strLen = (int)((uint)strLen >> 7);
        }
        ms.WriteByte((byte)strLen);
        ms.Write(jsonBytes, 0, jsonBytes.Length);

        byte[] payload = ms.ToArray();

        using var finalMs = new MemoryStream();
        int totalLen = payload.Length;
        while ((totalLen & -128) != 0)
        {
            finalMs.WriteByte((byte)((totalLen & 127) | 128));
            totalLen = (int)((uint)totalLen >> 7);
        }
        finalMs.WriteByte((byte)totalLen);
        finalMs.Write(payload, 0, payload.Length);

        return finalMs.ToArray();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
