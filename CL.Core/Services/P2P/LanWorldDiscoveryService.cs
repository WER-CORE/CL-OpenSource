using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Models.P2P;

namespace CL.Core.Services.P2P;

public class LanWorldDiscoveryService : IAsyncDisposable, IDisposable
{
    private const string MulticastGroupAddress = "224.0.2.60";
    private const int MulticastPort = 4445;
    private static readonly Regex LanPattern = new(@"\[MOTD\](?<motd>.*?)\[/MOTD\]\[AD\](?<port>\d+)\[/AD\]", RegexOptions.Compiled);
    private static readonly Regex LogLanPattern = new(@"(?:Started local game on|Started serving on|Hosted LAN server on|Opened LAN on port)\s+(?<port>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FabricLogPattern = new(@"Fabric Loader\s+([0-9a-zA-Z\.\+\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ForgeLogPattern = new(@"(?:MinecraftForge|Forge mod loading).*?(?:v|version)\s+([0-9a-zA-Z\.\+\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NeoForgeLogPattern = new(@"NeoForge mod loading.*?version\s+([0-9a-zA-Z\.\+\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QuiltLogPattern = new(@"Quilt Loader\s+([0-9a-zA-Z\.\+\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ConcurrentDictionary<int, P2PLanWorld> _worlds = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _scanTask;
    private bool _isDisposed;

    public event Action<P2PLanWorld>? WorldDiscovered;

    public IReadOnlyCollection<P2PLanWorld> DiscoveredWorlds => _worlds.Values.ToArray();

    public Task StartAsync(CancellationToken token = default)
    {
        if (_listenTask != null && !_listenTask.IsCompleted) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _worlds.Clear();

        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
            _udpClient.MulticastLoopback = true;
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));

            try
            {
                _udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastGroupAddress));
            }
            catch { }

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.SupportsMulticast)
                    {
                        var ipProps = ni.GetIPProperties();
                        foreach (var unicast in ipProps.UnicastAddresses)
                        {
                            if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                            {
                                try
                                {
                                    _udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastGroupAddress), unicast.Address);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        catch
        {
            try { _udpClient?.Dispose(); } catch { }
            _udpClient = null;
        }

        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        _scanTask = Task.Run(() => ScanLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(token);
                string message = Encoding.UTF8.GetString(result.Buffer);

                var match = LanPattern.Match(message);
                if (match.Success && int.TryParse(match.Groups["port"].Value, out int port))
                {
                    string motd = match.Groups["motd"].Value;
                    string? modpackName = GameLaunchService.ActiveModpackName;
                    string? modpackTypeSite = GameLaunchService.ActiveModpackTypeSite;
                    string? modsDir = !string.IsNullOrEmpty(GameLaunchService.ActiveGamePath)
                        ? Path.Combine(GameLaunchService.ActiveGamePath, "mods")
                        : null;

                    string? gameVer = GameLaunchService.ActiveMinecraftVersion;
                    string loaderType = GameLaunchService.ActiveLoaderType.ToString();
                    string? loaderVer = GameLaunchService.ActiveLoaderVersion;

                    var world = new P2PLanWorld
                    {
                        WorldName = motd,
                        Port = port,
                        DiscoveredAt = DateTime.UtcNow,
                        ModpackName = modpackName,
                        ModpackTypeSite = modpackTypeSite,
                        GameVersion = gameVer,
                        LoaderType = loaderType,
                        LoaderVersion = loaderVer,
                        ModsDirectory = (modsDir != null && Directory.Exists(modsDir)) ? modsDir : null
                    };

                    bool isNew = !_worlds.ContainsKey(port);
                    _worlds.AddOrUpdate(port, world, (_, existing) =>
                    {
                        existing.WorldName = motd;
                        existing.DiscoveredAt = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(modpackName)) existing.ModpackName = modpackName;
                        if (!string.IsNullOrEmpty(modpackTypeSite)) existing.ModpackTypeSite = modpackTypeSite;
                        if (!string.IsNullOrEmpty(world.GameVersion)) existing.GameVersion = world.GameVersion;
                        if (!string.IsNullOrEmpty(world.LoaderType)) existing.LoaderType = world.LoaderType;
                        if (!string.IsNullOrEmpty(world.LoaderVersion)) existing.LoaderVersion = world.LoaderVersion;
                        if (!string.IsNullOrEmpty(world.ModsDirectory)) existing.ModsDirectory = world.ModsDirectory;
                        return existing;
                    });

                    if (isNew)
                    {
                        WorldDiscovered?.Invoke(world);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                await Task.Delay(500, token).ConfigureAwait(false);
            }
        }
    }

    private async Task ScanLoopAsync(CancellationToken token)
    {
        await ScanActiveWorldsAsync(token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token).ConfigureAwait(false);
                await ScanActiveWorldsAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
            }
        }
    }

    public async Task ScanActiveWorldsAsync(CancellationToken token = default)
    {
        try
        {
            var candidateLogs = new List<(string LogPath, string? ModpackName, string? ModpackRoot)>();

            if (!string.IsNullOrEmpty(GameLaunchService.ActiveGamePath))
            {
                string activeLog = Path.Combine(GameLaunchService.ActiveGamePath, "logs", "latest.log");
                candidateLogs.Add((activeLog, GameLaunchService.ActiveModpackName, GameLaunchService.ActiveGamePath));
            }

            string basePath = SettingsManager.Default.PathLacunher;
            if (!string.IsNullOrEmpty(basePath))
            {
                candidateLogs.Add((Path.Combine(basePath, "logs", "latest.log"), null, basePath));

                string clModpackRoot = Path.Combine(basePath, "CLModpack");
                if (Directory.Exists(clModpackRoot))
                {
                    try
                    {
                        var packDirs = Directory.GetDirectories(clModpackRoot);
                        foreach (var packDir in packDirs)
                        {
                            string packName = Path.GetFileName(packDir);
                            string overrideLog = Path.Combine(packDir, "override", "logs", "latest.log");
                            string overridesLog = Path.Combine(packDir, "overrides", "logs", "latest.log");
                            string directLog = Path.Combine(packDir, "logs", "latest.log");

                            if (File.Exists(overrideLog)) candidateLogs.Add((overrideLog, packName, Path.Combine(packDir, "override")));
                            else if (File.Exists(overridesLog)) candidateLogs.Add((overridesLog, packName, Path.Combine(packDir, "overrides")));
                            else if (File.Exists(directLog)) candidateLogs.Add((directLog, packName, packDir));
                        }
                    }
                    catch { }
                }
            }

            var recentCutoff = DateTime.UtcNow.AddHours(-3);

            foreach (var (logPath, modpackName, modpackRoot) in candidateLogs)
            {
                if (token.IsCancellationRequested) break;
                if (!File.Exists(logPath)) continue;

                var fi = new FileInfo(logPath);
                if (fi.LastWriteTimeUtc < recentCutoff) continue;

                int detectedPort = TryExtractLanPortFromLog(logPath);
                if (detectedPort > 0 && detectedPort <= 65535)
                {
                    await TryRegisterActivePortAsync(detectedPort, modpackName, modpackRoot, logPath, token).ConfigureAwait(false);
                }
            }
        }
        catch
        {
        }
    }

    private static int TryExtractLanPortFromLog(string logPath)
    {
        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long length = fs.Length;
            if (length <= 0) return 0;

            int bytesToRead = (int)Math.Min(length, 65536);
            fs.Seek(-bytesToRead, SeekOrigin.End);

            byte[] buffer = new byte[bytesToRead];
            int read = fs.Read(buffer, 0, bytesToRead);
            string tail = Encoding.UTF8.GetString(buffer, 0, read);

            var matches = LogLanPattern.Matches(tail);
            if (matches.Count > 0)
            {
                var lastMatch = matches[^1];
                if (int.TryParse(lastMatch.Groups["port"].Value, out int port))
                {
                    return port;
                }
            }
        }
        catch { }

        return 0;
    }

    private static (string? Loader, string? Version) TryDetectLoaderFromLog(string logPath)
    {
        try
        {
            if (!File.Exists(logPath)) return (null, null);
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] buf = new byte[Math.Min(fs.Length, 65536)];
            int read = fs.Read(buf, 0, buf.Length);
            string head = Encoding.UTF8.GetString(buf, 0, read);

            var neoMatch = NeoForgeLogPattern.Match(head);
            if (neoMatch.Success) return ("NeoForge", neoMatch.Groups[1].Value);

            var forgeMatch = ForgeLogPattern.Match(head);
            if (forgeMatch.Success) return ("Forge", forgeMatch.Groups[1].Value);

            var fabricMatch = FabricLogPattern.Match(head);
            if (fabricMatch.Success) return ("Fabric", fabricMatch.Groups[1].Value);

            var quiltMatch = QuiltLogPattern.Match(head);
            if (quiltMatch.Success) return ("Quilt", quiltMatch.Groups[1].Value);
        }
        catch { }
        return (null, null);
    }

    private async Task TryRegisterActivePortAsync(int port, string? modpackName, string? modpackRoot, string? logPath, CancellationToken token)
    {
        if (_worlds.TryGetValue(port, out var existing) && !string.IsNullOrEmpty(existing.ModpackName) && !string.IsNullOrEmpty(existing.LoaderVersion))
        {
            return;
        }

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync("127.0.0.1", port);
            if (await Task.WhenAny(connectTask, Task.Delay(300, token)).ConfigureAwait(false) != connectTask || !tcp.Connected)
            {
                return;
            }

            var pingInfo = await MinecraftPingHelper.TryGetServerInfoAsync(port, token).ConfigureAwait(false);

            string? modsDir = !string.IsNullOrEmpty(modpackRoot) ? Path.Combine(modpackRoot, "mods") : null;
            string motd = pingInfo?.Motd ?? string.Empty;
            string worldName = !string.IsNullOrWhiteSpace(motd)
                ? motd
                : (!string.IsNullOrWhiteSpace(modpackName) ? $"{modpackName} (LAN)" : "Локальний світ (LAN)");

            string gameVersion = pingInfo?.Version ?? GameLaunchService.ActiveMinecraftVersion ?? "1.20.1";
            string loaderType = "Vanilla";
            string? loaderVersion = null;

            string? modpackTypeSite = GameLaunchService.ActiveModpackTypeSite;

            if (!string.IsNullOrEmpty(GameLaunchService.ActiveMinecraftVersion))
            {
                loaderType = GameLaunchService.ActiveLoaderType.ToString();
                loaderVersion = GameLaunchService.ActiveLoaderVersion;
            }
            else if (!string.IsNullOrEmpty(modpackName))
            {
                try
                {
                    string jsonPath = ModpackPaths.InstalledModpacksJson;
                    if (File.Exists(jsonPath))
                    {
                        var packs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<InstalledModpack>>(File.ReadAllText(jsonPath));
                        var pack = packs?.FirstOrDefault(p => string.Equals(p.Name, modpackName, StringComparison.OrdinalIgnoreCase));
                        if (pack != null)
                        {
                            if (!string.IsNullOrEmpty(pack.MinecraftVersion)) gameVersion = pack.MinecraftVersion;
                            if (!string.IsNullOrEmpty(pack.LoaderType)) loaderType = pack.LoaderType;
                            if (!string.IsNullOrEmpty(pack.LoaderVersion)) loaderVersion = pack.LoaderVersion;
                            if (!string.IsNullOrEmpty(pack.TypeSite)) modpackTypeSite = pack.TypeSite;
                        }
                    }
                }
                catch { }
            }

            if (pingInfo != null && !string.IsNullOrEmpty(pingInfo.Value.Loader) && pingInfo.Value.Loader != "Vanilla")
            {
                loaderType = pingInfo.Value.Loader;
            }

            if (string.IsNullOrEmpty(loaderVersion) && !string.IsNullOrEmpty(logPath))
            {
                var (logLoader, logVer) = TryDetectLoaderFromLog(logPath);
                if (!string.IsNullOrEmpty(logLoader) && (loaderType == "Vanilla" || loaderType == logLoader)) loaderType = logLoader;
                if (!string.IsNullOrEmpty(logVer)) loaderVersion = logVer;
            }

            var world = new P2PLanWorld
            {
                WorldName = worldName,
                Port = port,
                DiscoveredAt = DateTime.UtcNow,
                ModpackName = modpackName,
                ModpackTypeSite = modpackTypeSite,
                GameVersion = gameVersion,
                LoaderType = loaderType,
                LoaderVersion = loaderVersion,
                ModsDirectory = (modsDir != null && Directory.Exists(modsDir)) ? modsDir : null
            };

            bool isNew = !_worlds.ContainsKey(port);
            _worlds.AddOrUpdate(port, world, (_, prev) =>
            {
                prev.WorldName = worldName;
                prev.DiscoveredAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(modpackName)) prev.ModpackName = modpackName;
                if (!string.IsNullOrEmpty(modpackTypeSite)) prev.ModpackTypeSite = modpackTypeSite;
                if (!string.IsNullOrEmpty(world.GameVersion)) prev.GameVersion = world.GameVersion;
                if (!string.IsNullOrEmpty(world.LoaderType)) prev.LoaderType = world.LoaderType;
                if (!string.IsNullOrEmpty(world.LoaderVersion)) prev.LoaderVersion = world.LoaderVersion;
                if (!string.IsNullOrEmpty(world.ModsDirectory)) prev.ModsDirectory = world.ModsDirectory;
                return prev;
            });

            if (isNew)
            {
                WorldDiscovered?.Invoke(world);
            }
        }
        catch { }
    }

    public async Task<P2PLanWorld?> ScanOrAddPortAsync(int port, CancellationToken token = default)
    {
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync("127.0.0.1", port);
            if (await Task.WhenAny(connectTask, Task.Delay(500, token)).ConfigureAwait(false) != connectTask || !tcp.Connected)
            {
                return null;
            }

            var pingInfo = await MinecraftPingHelper.TryGetServerInfoAsync(port, token).ConfigureAwait(false);
            string modpackName = GameLaunchService.ActiveModpackName ?? string.Empty;
            string? modpackTypeSite = GameLaunchService.ActiveModpackTypeSite;
            string? modsDir = !string.IsNullOrEmpty(GameLaunchService.ActiveGamePath)
                ? Path.Combine(GameLaunchService.ActiveGamePath, "mods")
                : null;

            string loaderType = GameLaunchService.ActiveLoaderType.ToString();
            string? loaderVersion = GameLaunchService.ActiveLoaderVersion;
            string gameVersion = pingInfo?.Version ?? GameLaunchService.ActiveMinecraftVersion ?? "1.20.1";

            if (pingInfo != null && !string.IsNullOrEmpty(pingInfo.Value.Loader) && pingInfo.Value.Loader != "Vanilla")
            {
                loaderType = pingInfo.Value.Loader;
            }

            if (!string.IsNullOrEmpty(modpackName))
            {
                try
                {
                    string jsonPath = ModpackPaths.InstalledModpacksJson;
                    if (File.Exists(jsonPath))
                    {
                        var packs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<InstalledModpack>>(File.ReadAllText(jsonPath));
                        var pack = packs?.FirstOrDefault(p => string.Equals(p.Name, modpackName, StringComparison.OrdinalIgnoreCase));
                        if (pack != null)
                        {
                            if (string.IsNullOrEmpty(loaderVersion))
                            {
                                if (!string.IsNullOrEmpty(pack.LoaderType)) loaderType = pack.LoaderType;
                                if (!string.IsNullOrEmpty(pack.LoaderVersion)) loaderVersion = pack.LoaderVersion;
                            }
                            if (string.IsNullOrEmpty(modpackTypeSite) && !string.IsNullOrEmpty(pack.TypeSite))
                            {
                                modpackTypeSite = pack.TypeSite;
                            }
                        }
                    }
                }
                catch { }
            }

            string worldName = !string.IsNullOrWhiteSpace(pingInfo?.Motd)
                ? pingInfo.Value.Motd
                : (!string.IsNullOrWhiteSpace(modpackName) ? $"{modpackName} (LAN)" : $"Локальний світ (Порт {port})");

            var world = new P2PLanWorld
            {
                WorldName = worldName,
                Port = port,
                DiscoveredAt = DateTime.UtcNow,
                ModpackName = modpackName,
                ModpackTypeSite = modpackTypeSite,
                GameVersion = gameVersion,
                LoaderType = loaderType,
                LoaderVersion = loaderVersion,
                ModsDirectory = (modsDir != null && Directory.Exists(modsDir)) ? modsDir : null
            };

            _worlds.AddOrUpdate(port, world, (_, _) => world);
            WorldDiscovered?.Invoke(world);
            return world;
        }
        catch
        {
            return null;
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

        if (_udpClient != null)
        {
            try
            {
                _udpClient.DropMulticastGroup(IPAddress.Parse(MulticastGroupAddress));
            }
            catch { }

            try
            {
                _udpClient.Close();
                _udpClient.Dispose();
            }
            catch { }
            _udpClient = null;
        }

        if (_listenTask != null)
        {
            try { await _listenTask; } catch { }
            _listenTask = null;
        }

        if (_scanTask != null)
        {
            try { await _scanTask; } catch { }
            _scanTask = null;
        }

        _worlds.Clear();
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
        await StopAsync();
    }
}
