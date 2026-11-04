#pragma warning disable CA2252, CA1416

using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL.Core.Services.P2P;

public class E4mcRelayInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("host")]
    public string Host { get; set; } = string.Empty;

    [JsonProperty("port")]
    public int Port { get; set; }
}

public class E4mcTunnelService : IAsyncDisposable, IDisposable
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string DefaultBrokerUrl = "https://broker.e4mc.link/getBestRelay";

    private QuicConnection? _quicConnection;
    private QuicStream? _controlStream;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private Task? _controlLoopTask;
    private bool _isDisposed;

    public string? AssignedDomain { get; private set; }
    public bool IsActive => _quicConnection != null && !string.IsNullOrEmpty(AssignedDomain);

    public event Action<string>? DomainAssigned;
    public event Action? TunnelClosed;

    public async Task<string> StartTunnelAsync(int localProxyPort, CancellationToken token = default)
    {
        await StopTunnelAsync().ConfigureAwait(false);

        if (!QuicConnection.IsSupported)
        {
            throw new NotSupportedException("QUIC не підтримується на цьому пристрої або платформі.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        var relay = await ResolveBestRelayAsync(_cts.Token).ConfigureAwait(false);

        var ipAddresses = await Dns.GetHostAddressesAsync(relay.Host, _cts.Token).ConfigureAwait(false);
        if (ipAddresses.Length == 0)
        {
            throw new InvalidOperationException($"Не вдалося визначити IP-адресу релею {relay.Host}.");
        }

        var endPoint = new IPEndPoint(ipAddresses[0], relay.Port);

        var connectionOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endPoint,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                TargetHost = relay.Host,
                ApplicationProtocols = new System.Collections.Generic.List<SslApplicationProtocol>
                {
                    new("quiclime")
                }
            },
            MaxInboundBidirectionalStreams = 512,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0
        };

        _quicConnection = await QuicConnection.ConnectAsync(connectionOptions, _cts.Token).ConfigureAwait(false);

        _controlStream = await _quicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, _cts.Token).ConfigureAwait(false);

        await SendControlJsonAsync(_controlStream, new { kind = "probe_capabilities" }, _cts.Token).ConfigureAwait(false);
        await SendControlJsonAsync(_controlStream, new { kind = "request_domain_assignment" }, _cts.Token).ConfigureAwait(false);

        var domainTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _controlLoopTask = Task.Run(() => RunControlLoopAsync(_controlStream, domainTcs, _cts.Token), _cts.Token);

        var timeoutTask = Task.Delay(10000, _cts.Token);
        var completedTask = await Task.WhenAny(domainTcs.Task, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
        {
            await StopTunnelAsync().ConfigureAwait(false);
            throw new TimeoutException("Час очікування виділення домену від сервера e4mc вичерпано.");
        }

        AssignedDomain = await domainTcs.Task.ConfigureAwait(false);
        DomainAssigned?.Invoke(AssignedDomain);

        _acceptTask = Task.Run(() => AcceptInboundStreamsLoopAsync(localProxyPort, _cts.Token), _cts.Token);

        return AssignedDomain;
    }

    private async Task<E4mcRelayInfo> ResolveBestRelayAsync(CancellationToken token)
    {
        try
        {
            var response = await HttpClient.GetStringAsync(DefaultBrokerUrl, token).ConfigureAwait(false);
            var parsed = JsonConvert.DeserializeObject<E4mcRelayInfo>(response);
            if (parsed != null && !string.IsNullOrEmpty(parsed.Host) && parsed.Port > 0)
            {
                return parsed;
            }
        }
        catch
        {
        }

        return new E4mcRelayInfo
        {
            Id = "eu",
            Host = "eu.e4mc.link",
            Port = 25575
        };
    }

    private async Task RunControlLoopAsync(QuicStream stream, TaskCompletionSource<string> domainTcs, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var messageJson = await ReadControlJsonAsync(stream, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(messageJson)) break;

                var jObj = JObject.Parse(messageJson);
                string? kind = jObj.Value<string>("kind");

                if (kind == "domain_assignment_complete")
                {
                    string? domain = jObj.Value<string>("domain");
                    if (!string.IsNullOrEmpty(domain) && !domainTcs.Task.IsCompleted)
                    {
                        domainTcs.TrySetResult(domain);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            domainTcs.TrySetException(ex);
        }
        finally
        {
            TunnelClosed?.Invoke();
        }
    }

    private async Task AcceptInboundStreamsLoopAsync(int localProxyPort, CancellationToken token)
    {
        while (!token.IsCancellationRequested && _quicConnection != null)
        {
            try
            {
                var inboundStream = await _quicConnection.AcceptInboundStreamAsync(token).ConfigureAwait(false);
                _ = Task.Run(() => HandleInboundPlayerStreamAsync(inboundStream, localProxyPort, token), token);
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

    private static async Task HandleInboundPlayerStreamAsync(QuicStream quicStream, int localProxyPort, CancellationToken token)
    {
        TcpClient? localTcp = null;
        try
        {
            localTcp = new TcpClient();
            localTcp.NoDelay = true;
            await localTcp.ConnectAsync(IPAddress.Loopback, localProxyPort, token).ConfigureAwait(false);
            var tcpStream = localTcp.GetStream();

            var quicToTcp = PipeAsync(quicStream, tcpStream, token);
            var tcpToQuic = PipeAsync(tcpStream, quicStream, token);

            await Task.WhenAny(quicToTcp, tcpToQuic).ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            try { await quicStream.DisposeAsync().ConfigureAwait(false); } catch { }
            try { localTcp?.Close(); localTcp?.Dispose(); } catch { }
        }
    }

    private static async Task PipeAsync(Stream input, Stream output, CancellationToken token)
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

    private static async Task SendControlJsonAsync(QuicStream stream, object payload, CancellationToken token)
    {
        string json = JsonConvert.SerializeObject(payload);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using var ms = new MemoryStream();
        WriteVarInt(ms, jsonBytes.Length);
        ms.Write(jsonBytes, 0, jsonBytes.Length);

        byte[] frame = ms.ToArray();
        await stream.WriteAsync(frame.AsMemory(0, frame.Length), token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<string?> ReadControlJsonAsync(QuicStream stream, CancellationToken token)
    {
        int length = await ReadVarIntAsync(stream, token).ConfigureAwait(false);
        if (length <= 0 || length > 65536) return null;

        byte[] buffer = new byte[length];
        int readTotal = 0;
        while (readTotal < length)
        {
            int r = await stream.ReadAsync(buffer.AsMemory(readTotal, length - readTotal), token).ConfigureAwait(false);
            if (r <= 0) return null;
            readTotal += r;
        }

        return Encoding.UTF8.GetString(buffer, 0, readTotal);
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

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken token)
    {
        int result = 0;
        int numRead = 0;
        byte[] singleByte = new byte[1];

        while (true)
        {
            int r = await stream.ReadAsync(singleByte.AsMemory(0, 1), token).ConfigureAwait(false);
            if (r <= 0) return -1;

            byte b = singleByte[0];
            int value = b & 0x7F;
            result |= (value << (7 * numRead));
            numRead++;

            if (numRead > 5) return -1;
            if ((b & 0x80) == 0) break;
        }

        return result;
    }

    public async Task StopTunnelAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _cts.Dispose(); } catch { }
            _cts = null;
        }

        if (_controlStream != null)
        {
            try { await _controlStream.DisposeAsync().ConfigureAwait(false); } catch { }
            _controlStream = null;
        }

        if (_quicConnection != null)
        {
            try { await _quicConnection.DisposeAsync().ConfigureAwait(false); } catch { }
            _quicConnection = null;
        }

        if (_acceptTask != null)
        {
            try { await _acceptTask.ConfigureAwait(false); } catch { }
            _acceptTask = null;
        }

        if (_controlLoopTask != null)
        {
            try { await _controlLoopTask.ConfigureAwait(false); } catch { }
            _controlLoopTask = null;
        }

        AssignedDomain = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopTunnelAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopTunnelAsync().ConfigureAwait(false);
    }
}