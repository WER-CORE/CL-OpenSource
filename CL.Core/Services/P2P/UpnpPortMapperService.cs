using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mono.Nat;

namespace CL.Core.Services.P2P;

public class UpnpPortMapperService : IAsyncDisposable, IDisposable
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private INatDevice? _device;
    private Mapping? _activeMapping;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDisposed;

    public bool IsPortMapped => _activeMapping != null;
    public int? MappedPublicPort => _activeMapping?.PublicPort;

    public async Task<INatDevice?> DiscoverDeviceAsync(int timeoutMs = 4000, CancellationToken token = default)
    {
        if (_device != null) return _device;

        var tcs = new TaskCompletionSource<INatDevice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = token.Register(() => tcs.TrySetResult(null));

        EventHandler<DeviceEventArgs> handler = (sender, args) =>
        {
            if (args.Device != null)
            {
                tcs.TrySetResult(args.Device);
            }
        };

        try
        {
            NatUtility.DeviceFound += handler;
            NatUtility.StartDiscovery();

            var timeoutTask = Task.Delay(timeoutMs, token);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

            if (completed == tcs.Task)
            {
                _device = await tcs.Task.ConfigureAwait(false);
            }
        }
        catch
        {
            _device = null;
        }
        finally
        {
            NatUtility.DeviceFound -= handler;
            try { NatUtility.StopDiscovery(); } catch { }
        }

        return _device;
    }

    public async Task<bool> OpenPortAsync(int localPort, int publicPort, CancellationToken token = default)
    {
        await _lock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await ClosePortInternalAsync().ConfigureAwait(false);

            var device = await DiscoverDeviceAsync(3500, token).ConfigureAwait(false);
            if (device == null) return false;

            var mapping = new Mapping(Protocol.Tcp, localPort, publicPort, 7200, "CLegendary-P2P");
            await device.CreatePortMapAsync(mapping).ConfigureAwait(false);
            _activeMapping = mapping;
            return true;
        }
        catch
        {
            _activeMapping = null;
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetExternalIpAsync(CancellationToken token = default)
    {
        if (_device != null)
        {
            try
            {
                var ip = await _device.GetExternalIPAsync().ConfigureAwait(false);
                if (ip != null && !IPAddress.IsLoopback(ip) && ip.ToString() != "0.0.0.0")
                {
                    return ip.ToString();
                }
            }
            catch { }
        }

        string[] providers = new[]
        {
            "https://api.ipify.org",
            "https://icanhazip.com",
            "https://checkip.amazonaws.com"
        };

        foreach (var url in providers)
        {
            try
            {
                string res = await _httpClient.GetStringAsync(url, token).ConfigureAwait(false);
                string cleanIp = res.Trim();
                if (IPAddress.TryParse(cleanIp, out var parsed))
                {
                    return parsed.ToString();
                }
            }
            catch
            {
                if (token.IsCancellationRequested) break;
            }
        }

        return null;
    }

    public async Task ClosePortAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ClosePortInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ClosePortInternalAsync()
    {
        if (_device != null && _activeMapping != null)
        {
            try
            {
                await _device.DeletePortMapAsync(_activeMapping).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                _activeMapping = null;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        ClosePortAsync().GetAwaiter().GetResult();
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await ClosePortAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
