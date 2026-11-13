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
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _rateLimits = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _rejectionCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _rejectionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _sessionBlacklist = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public int BoundPort { get; private set; }
    public int TargetLocalPort { get; private set; }
    public bool RequireApproval { get; set; }
    public string ExpectedSessionSecret { get; set; } = string.Empty;
    public string HostUsername { get; set; } = string.Empty;

    public event Action<P2PPlayer>? PlayerConnected;
    public event Action<P2PPlayer>? PlayerDisconnected;
    public event Action<string>? DiagnosticLogged;
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

        TargetLocalPort = targetLocalPort;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        _listener = new TcpListener(IPAddress.Any, listenPort);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        LogDiagnostic($"Proxy listener started on port {BoundPort} -> 127.0.0.1:{TargetLocalPort}");

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
        string shortId = connectionId[..8];
        NetworkStream? clientStream = null;
        TcpClient? server = null;
        NetworkStream? serverStream = null;
        P2PPlayer? player = null;

        try
        {
            LogDiagnostic($"[{shortId}] Accepted TCP connection.");
            clientStream = client.GetStream();

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            handshakeCts.CancelAfter(TimeSpan.FromSeconds(10));
            var hToken = handshakeCts.Token;

            int firstByte = await ReadByteAsync(clientStream, hToken).ConfigureAwait(false);
            if (firstByte == -1)
            {
                LogDiagnostic($"[{shortId}] Closed before sending handshake.");
                CloseQuietly(client);
                return;
            }

            if (firstByte == 0xFE)
            {
                LogDiagnostic($"[{shortId}] Legacy 0xFE ping received; connecting to 127.0.0.1:{TargetLocalPort}.");
                server = new TcpClient();
                server.NoDelay = true;
                await server.ConnectAsync(IPAddress.Loopback, TargetLocalPort, token).ConfigureAwait(false);
                serverStream = server.GetStream();

                await serverStream.WriteAsync(new byte[] { 0xFE }, token).ConfigureAwait(false);
                await serverStream.FlushAsync(token).ConfigureAwait(false);

                var c2s = PipeStreamAsync(clientStream, serverStream, token);
                var s2c = PipeStreamAsync(serverStream, clientStream, token);
                await Task.WhenAny(c2s, s2c).ConfigureAwait(false);
                return;
            }

            int packetLength = firstByte & 0x7F;
            if ((firstByte & 0x80) != 0)
            {
                int shift = 7;
                while (true)
                {
                    int b = await ReadByteAsync(clientStream, hToken).ConfigureAwait(false);
                    if (b == -1)
                    {
                        LogDiagnostic($"[{shortId}] EOF while reading handshake length.");
                        CloseQuietly(client);
                        return;
                    }
                    packetLength |= (b & 0x7F) << shift;
                    shift += 7;
                    if (shift > 35)
                    {
                        LogDiagnostic($"[{shortId}] Invalid handshake VarInt length.");
                        CloseQuietly(client);
                        return;
                    }
                    if ((b & 0x80) == 0) break;
                }
            }

            if (packetLength <= 0 || packetLength > 2048)
            {
                LogDiagnostic($"[{shortId}] Rejected handshake with length {packetLength}.");
                CloseQuietly(client);
                return;
            }

            byte[] packetBuffer = new byte[packetLength];
            int readBytes = 0;
            while (readBytes < packetLength)
            {
                int r = await clientStream.ReadAsync(packetBuffer.AsMemory(readBytes, packetLength - readBytes), hToken).ConfigureAwait(false);
                if (r <= 0)
                {
                    LogDiagnostic($"[{shortId}] EOF while reading handshake payload.");
                    CloseQuietly(client);
                    return;
                }
                readBytes += r;
            }

            if (!TryRewriteHandshakePayload(packetBuffer, TargetLocalPort, out var handshake))
            {
                LogDiagnostic($"[{shortId}] Invalid or non-0x00 handshake packet.");
                CloseQuietly(client);
                return;
            }

            LogDiagnostic(
                $"[{shortId}] Handshake: protocol={handshake.ProtocolVersion}, nextState={FormatNextState(handshake.NextState)}, ext={(handshake.ProtocolExtension ?? "none")}, target=127.0.0.1:{TargetLocalPort}");

            server = new TcpClient();
            server.NoDelay = true;
            await server.ConnectAsync(IPAddress.Loopback, TargetLocalPort, token).ConfigureAwait(false);
            serverStream = server.GetStream();
            LogDiagnostic($"[{shortId}] Connected to local LAN server 127.0.0.1:{TargetLocalPort}.");

            await serverStream.WriteAsync(handshake.RewrittenPacket, token).ConfigureAwait(false);
            await serverStream.FlushAsync(token).ConfigureAwait(false);

            if (handshake.NextState == 2)
            {
                var (loginLenOk, loginLen) = await TryReadVarIntAsync(clientStream, hToken).ConfigureAwait(false);
                if (!loginLenOk || loginLen <= 0 || loginLen > 2048)
                {
                    LogDiagnostic($"[{shortId}] Invalid login start packet length ({loginLen}).");
                    CloseQuietly(client, server);
                    return;
                }

                byte[] loginBuffer = new byte[loginLen];
                int loginRead = 0;
                while (loginRead < loginLen)
                {
                    int r = await clientStream.ReadAsync(loginBuffer.AsMemory(loginRead, loginLen - loginRead), hToken).ConfigureAwait(false);
                    if (r <= 0)
                    {
                        LogDiagnostic($"[{shortId}] EOF while reading login start packet.");
                        CloseQuietly(client, server);
                        return;
                    }
                    loginRead += r;
                }

                using var loginMs = new MemoryStream(loginBuffer, false);
                if (!TryReadVarInt(loginMs, out int loginPacketId) || loginPacketId != 0x00)
                {
                    LogDiagnostic($"[{shortId}] Expected login start packet 0x00, got 0x{loginPacketId:X2}.");
                    CloseQuietly(client, server);
                    return;
                }

                string username = ReadMinecraftString(loginMs);
                if (!await ValidateAndApproveAsync(username, clientStream, hToken).ConfigureAwait(false))
                {
                    LogDiagnostic($"[{shortId}] Connection rejected during validation/approval.");
                    CloseQuietly(client, server);
                    return;
                }

                player = new P2PPlayer
                {
                    ConnectionId = connectionId,
                    Username = username,
                    ConnectedAt = DateTime.UtcNow
                };

                await WriteVarIntAsync(serverStream, loginLen, token).ConfigureAwait(false);
                await serverStream.WriteAsync(loginBuffer.AsMemory(0, loginLen), token).ConfigureAwait(false);
                await serverStream.FlushAsync(token).ConfigureAwait(false);

                _activeConnections[connectionId] = (client, server, player);
                PlayerConnected?.Invoke(player);
            }

            var clientToServerNetty = PipeStreamAsync(clientStream, serverStream, token);
            var serverToClientNetty = PipeStreamAsync(serverStream, clientStream, token);

            await Task.WhenAny(clientToServerNetty, serverToClientNetty).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            LogDiagnostic($"[{shortId}] Connection closed due to proxy shutdown.");
        }
        catch (Exception ex)
        {
            LogDiagnostic($"[{shortId}] Connection error ({ex.GetType().Name}): {ex.Message}");
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

    private async Task<bool> ValidateAndApproveAsync(string username, Stream clientStream, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 20) return false;

        string userKey = username.Trim().ToLowerInvariant();

        if (_sessionBlacklist.ContainsKey(userKey))
        {
            byte[] disconnectPacket = CreateLoginDisconnectPacket("Підключення заблоковано хостом.");
            await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
            await clientStream.FlushAsync(token).ConfigureAwait(false);
            return false;
        }

        if (_rejectionCooldowns.TryGetValue(userKey, out var cooldownUntil) && DateTime.UtcNow < cooldownUntil)
        {
            int remainSec = Math.Max(1, (int)(cooldownUntil - DateTime.UtcNow).TotalSeconds);
            byte[] disconnectPacket = CreateLoginDisconnectPacket($"Ваш попередній запит відхилено. Зачекайте {remainSec} сек. перед повторною спробою.");
            await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
            await clientStream.FlushAsync(token).ConfigureAwait(false);
            return false;
        }

        var now = DateTime.UtcNow;
        var rate = _rateLimits.AddOrUpdate(userKey, (1, now), (_, current) =>
        {
            if ((now - current.WindowStart).TotalSeconds < 15)
            {
                return (current.Count + 1, current.WindowStart);
            }
            return (1, now);
        });

        if (rate.Count > 5)
        {
            byte[] disconnectPacket = CreateLoginDisconnectPacket("Забагато спроб підключення. Зачекайте 15 секунд.");
            await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
            await clientStream.FlushAsync(token).ConfigureAwait(false);
            return false;
        }

        foreach (var pair in _activeConnections.Values)
        {
            if (string.Equals(pair.Player.Username.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                byte[] disconnectPacket = CreateLoginDisconnectPacket($"Гравець з нікнеймом {username} вже підключений до гри.");
                await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);
                return false;
            }
        }

        if (RequireApproval && ApprovalRequested != null)
        {
            var tempPlayer = new P2PPlayer
            {
                ConnectionId = Guid.NewGuid().ToString("N"),
                Username = username,
                ConnectedAt = DateTime.UtcNow
            };

            bool isApproved = await ApprovalRequested.Invoke(tempPlayer).ConfigureAwait(false);
            if (!isApproved)
            {
                _rejectionCooldowns[userKey] = DateTime.UtcNow.AddSeconds(60);
                int rejections = _rejectionCounts.AddOrUpdate(userKey, 1, (_, cur) => cur + 1);
                if (rejections >= 3)
                {
                    _sessionBlacklist[userKey] = true;
                }

                byte[] disconnectPacket = CreateLoginDisconnectPacket("Ваш запит на підключення відхилено хостом.");
                await clientStream.WriteAsync(disconnectPacket, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);
                return false;
            }
        }

        return true;
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

    private void LogDiagnostic(string message)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[P2P.Proxy] {message}");
            DiagnosticLogged?.Invoke(message);
        }
        catch
        {
        }
    }

    internal readonly record struct HandshakeRewriteResult(
        int ProtocolVersion,
        string OriginalServerAddress,
        string ForwardedServerAddress,
        int OriginalPort,
        int TargetPort,
        int NextState,
        string? ProtocolExtension,
        byte[] RewrittenPacket);

    internal static bool TryRewriteHandshakePayload(byte[] packetPayload, int targetLocalPort, out HandshakeRewriteResult result)
    {
        result = default;
        if (packetPayload == null || packetPayload.Length == 0)
            return false;

        using var packetMs = new MemoryStream(packetPayload, false);
        if (!TryReadVarInt(packetMs, out int packetId) || packetId != 0x00)
            return false;

        if (!TryReadVarInt(packetMs, out int protocolVersion))
            return false;

        if (!TryReadMinecraftString(packetMs, out string serverAddress))
            return false;

        if (packetMs.Position + 2 > packetMs.Length)
            return false;

        int originalPort = (packetMs.ReadByte() << 8) | packetMs.ReadByte();
        if (!TryReadVarInt(packetMs, out int nextState))
            return false;

        string forwardedAddress = BuildForwardedHandshakeAddress(serverAddress);
        string? extension = DescribeProtocolExtension(serverAddress);
        byte[] rewrittenPacket = CreateHandshakePacket(protocolVersion, forwardedAddress, targetLocalPort, nextState);

        result = new HandshakeRewriteResult(
            protocolVersion,
            serverAddress,
            forwardedAddress,
            originalPort,
            targetLocalPort,
            nextState,
            extension,
            rewrittenPacket);
        return true;
    }

    internal static string BuildForwardedHandshakeAddress(string originalServerAddress)
    {
        if (string.IsNullOrEmpty(originalServerAddress))
            return "127.0.0.1";

        int nulIndex = originalServerAddress.IndexOf('\0');
        return nulIndex >= 0
            ? "127.0.0.1" + originalServerAddress.Substring(nulIndex)
            : "127.0.0.1";
    }

    internal static string? DescribeProtocolExtension(string serverAddress)
    {
        if (string.IsNullOrEmpty(serverAddress))
            return null;

        int nulIndex = serverAddress.IndexOf('\0');
        if (nulIndex < 0)
            return null;

        string suffix = serverAddress.Substring(nulIndex + 1);
        int nextNul = suffix.IndexOf('\0');
        string firstSegment = nextNul >= 0 ? suffix.Substring(0, nextNul) : suffix;
        if (string.IsNullOrWhiteSpace(firstSegment))
            return "NUL_MARKER";

        var sb = new StringBuilder(Math.Min(firstSegment.Length, 24));
        foreach (char ch in firstSegment)
        {
            if (sb.Length >= 24) break;
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.' ? ch : '?');
        }
        return sb.Length > 0 ? sb.ToString() : "NUL_MARKER";
    }

    private static string FormatNextState(int nextState) => nextState switch
    {
        1 => "Status(1)",
        2 => "Login(2)",
        3 => "Transfer(3)",
        _ => $"Unknown({nextState})"
    };

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken token)
    {
        byte[] singleByte = new byte[1];
        int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), token).ConfigureAwait(false);
        return read == 1 ? singleByte[0] : -1;
    }

    private static async Task<(bool Success, int Value)> TryReadVarIntAsync(Stream stream, CancellationToken token)
    {
        int result = 0;
        int numRead = 0;
        byte[] singleByte = new byte[1];

        while (true)
        {
            int r = await stream.ReadAsync(singleByte.AsMemory(0, 1), token).ConfigureAwait(false);
            if (r <= 0) return (false, 0);

            int b = singleByte[0];
            result |= (b & 0x7F) << (7 * numRead);
            numRead++;
            if (numRead > 5) return (false, 0);
            if ((b & 0x80) == 0) break;
        }

        return (true, result);
    }

    private static bool TryReadMinecraftString(Stream stream, out string value)
    {
        value = string.Empty;
        if (!TryReadVarInt(stream, out int length) || length < 0 || length > 32767)
            return false;

        if (length == 0)
            return true;

        if (stream.Position + length > stream.Length)
            return false;

        byte[] buffer = new byte[length];
        int read = stream.Read(buffer, 0, length);
        if (read < length)
            return false;

        value = Encoding.UTF8.GetString(buffer, 0, read);
        return true;
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
