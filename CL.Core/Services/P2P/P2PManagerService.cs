using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models.P2P;

namespace CL.Core.Services.P2P;

public class P2PManagerService : IAsyncDisposable, IDisposable
{
    private readonly LanWorldDiscoveryService _discovery = new();
    private readonly UpnpPortMapperService _portMapper = new();
    private readonly P2PSecurityProxy _proxy = new();
    private readonly P2PManifestService _manifestService = new();
    private readonly E4mcTunnelService _tunnelService = new();

    private P2PRoomSession? _currentSession;
    private string? _lastStoppedSecret;
    private bool _isDisposed;

    public P2PRoomSession? CurrentSession => _currentSession;
    public bool IsHosting => _currentSession != null && _currentSession.IsActive;
    public string? AssignedDomain => _tunnelService.AssignedDomain;
    public IReadOnlyCollection<P2PPlayer> ConnectedPlayers => _proxy.ConnectedPlayers;
    public IReadOnlyCollection<P2PLanWorld> DiscoveredWorlds => _discovery.DiscoveredWorlds;

    public event Action<P2PLanWorld>? WorldDiscovered;
    public event Action<P2PPlayer>? PlayerConnected;
    public event Action<P2PPlayer>? PlayerDisconnected;
    public event Action<P2PRoomSession?>? SessionStateChanged;
    public event Action<string>? DiagnosticLogged;
    public Func<P2PPlayer, Task<bool>>? ApprovalRequested { get; set; }

    public P2PManagerService()
    {
        _discovery.WorldDiscovered += world => WorldDiscovered?.Invoke(world);
        _proxy.PlayerConnected += player => PlayerConnected?.Invoke(player);
        _proxy.PlayerDisconnected += player => PlayerDisconnected?.Invoke(player);
        _proxy.DiagnosticLogged += msg => DiagnosticLogged?.Invoke(msg);
        _tunnelService.DiagnosticLogged += msg => DiagnosticLogged?.Invoke(msg);
        _proxy.ApprovalRequested = async player =>
        {
            if (ApprovalRequested != null)
            {
                return await ApprovalRequested.Invoke(player).ConfigureAwait(false);
            }
            return true;
        };
    }

    public Task StartDiscoveryAsync(CancellationToken token = default)
    {
        return _discovery.StartAsync(token);
    }

    public Task StopDiscoveryAsync()
    {
        return _discovery.StopAsync();
    }

    public LanWorldDiscoveryService DiscoveryService => _discovery;
    public Task RefreshDiscoveryAsync(CancellationToken token = default) => _discovery.ScanActiveWorldsAsync(token);
    public Task<P2PLanWorld?> ScanOrAddPortAsync(int port, CancellationToken token = default) => _discovery.ScanOrAddPortAsync(port, token);

    public async Task<P2PRoomSession?> StartHostingAsync(
        P2PLanWorld world,
        string gameVersion,
        string loaderType,
        string? loaderVersion,
        string? modsDirectory,
        bool requireApproval = false,
        string? hostUsername = null,
        CancellationToken token = default)
    {
        await StopHostingAsync().ConfigureAwait(false);

        int localPort = world.Port;
        int proxyListenPort = localPort == 25565 ? 25566 : (localPort + 100);

        string secret = Guid.NewGuid().ToString("N").Substring(0, 8);

        _proxy.RequireApproval = requireApproval;
        _proxy.ExpectedSessionSecret = secret;
        _proxy.HostUsername = hostUsername ?? string.Empty;

        await _proxy.StartAsync(proxyListenPort, localPort, token).ConfigureAwait(false);

        string hostAddress;
        string? publicDomain = null;
        int publicPort = 25565;

        try
        {
            LogDiagnostic($"Starting P2P hosting for {gameVersion} ({loaderType}) LAN:{localPort} -> proxy:{proxyListenPort}");
            publicDomain = await _tunnelService.StartTunnelAsync(proxyListenPort, token).ConfigureAwait(false);
            hostAddress = publicDomain;
            LogDiagnostic($"Tunnel active: domain={publicDomain}, publicPort={publicPort}");
        }
        catch (Exception ex)
        {
            LogDiagnostic($"e4mc tunnel failed ({ex.GetType().Name}: {ex.Message}); falling back to UPnP.");
            bool upnpSuccess = await _portMapper.OpenPortAsync(proxyListenPort, publicPort, token).ConfigureAwait(false);
            string? externalIp = await _portMapper.GetExternalIpAsync(token).ConfigureAwait(false);
            hostAddress = !string.IsNullOrEmpty(externalIp) ? externalIp : "127.0.0.1";
            if (!upnpSuccess)
            {
                publicPort = proxyListenPort;
            }
            LogDiagnostic($"Fallback hosting endpoint: publicPort={publicPort}, upnp={upnpSuccess}");
        }

        var payload = new P2PRoomPayload
        {
            HostAddress = hostAddress,
            PublicDomain = publicDomain,
            Port = publicPort,
            SessionSecret = secret,
            WorldName = world.WorldName,
            GameVersion = gameVersion,
            LoaderType = loaderType,
            LoaderVersion = loaderVersion ?? string.Empty,
            ModpackName = world.ModpackName ?? string.Empty,
            ModpackTypeSite = world.ModpackTypeSite ?? string.Empty
        };

        string roomCode = P2PRoomCodeService.GenerateRoomCode(payload);

        _currentSession = new P2PRoomSession
        {
            RoomCode = roomCode,
            HostAddress = hostAddress,
            PublicDomain = publicDomain,
            PublicPort = publicPort,
            LocalPort = localPort,
            WorldName = world.WorldName,
            GameVersion = gameVersion,
            LoaderType = loaderType,
            LoaderVersion = loaderVersion ?? string.Empty,
            ModpackName = world.ModpackName ?? string.Empty,
            ModpackTypeSite = world.ModpackTypeSite ?? string.Empty,
            SessionSecret = secret,
            RequireApproval = requireApproval,
            IsActive = true
        };

        SessionStateChanged?.Invoke(_currentSession);
        return _currentSession;
    }

    private void LogDiagnostic(string message)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[P2P.Manager] {message}");
            DiagnosticLogged?.Invoke(message);
        }
        catch
        {
        }
    }

    public async Task StopHostingAsync()
    {
        if (_currentSession == null) return;

        _lastStoppedSecret = _currentSession.SessionSecret;

        await _tunnelService.StopTunnelAsync().ConfigureAwait(false);
        await _proxy.StopAsync().ConfigureAwait(false);
        await _portMapper.ClosePortAsync().ConfigureAwait(false);

        _currentSession = null;
        SessionStateChanged?.Invoke(null);
    }

    public void KickPlayer(string connectionId)
    {
        _proxy.KickPlayer(connectionId);
    }

    public async Task<(P2PRoomPayload? Payload, bool Success, string? ErrorMessage)> ValidateRoomCodeAsync(string roomCode, CancellationToken token = default)
    {
        if (!P2PRoomCodeService.TryParseRoomCode(roomCode, out var payload) || payload == null)
        {
            return (null, false, "Невірний або недійсний код кімнати.");
        }

        if (!string.IsNullOrEmpty(_lastStoppedSecret) && _lastStoppedSecret == payload.SessionSecret && !IsHosting)
        {
            return (payload, false, "Кімнату закрито (хост зупинив спільну гру).");
        }

        if (IsHosting && _currentSession?.SessionSecret == payload.SessionSecret)
        {
            return (payload, true, null);
        }

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(payload.HostAddress, payload.Port);
            var timeoutTask = Task.Delay(2000, token);
            var completed = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (completed != connectTask || !client.Connected)
            {
                return (payload, false, "Хост недоступний або завершив спільну гру.");
            }

            return (payload, true, null);
        }
        catch
        {
            return (payload, false, "Не вдалося з'єднатися з хостом (кімнату закрито).");
        }
    }

    public async Task<(List<P2PModEntry> MissingMods, bool IsMatch)> CheckModsAsync(P2PManifest hostManifest, string localModsDir)
    {
        return await _manifestService.CompareManifestAsync(hostManifest, localModsDir).ConfigureAwait(false);
    }

    public async Task<bool> SyncModsAsync(List<P2PModEntry> missingMods, string targetModsDir, IProgress<double>? progress = null, CancellationToken token = default)
    {
        return await _manifestService.SyncMissingModsAsync(missingMods, targetModsDir, progress, token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopHostingAsync().GetAwaiter().GetResult();
        _tunnelService.Dispose();
        _discovery.Dispose();
        _portMapper.Dispose();
        _proxy.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopHostingAsync().ConfigureAwait(false);
        await _tunnelService.DisposeAsync().ConfigureAwait(false);
        await _discovery.DisposeAsync().ConfigureAwait(false);
        await _portMapper.DisposeAsync().ConfigureAwait(false);
        await _proxy.DisposeAsync().ConfigureAwait(false);
    }
}
