using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CL.Core.Services.P2P;

public class P2PYggdrasilMockServer : IDisposable
{
    private static P2PYggdrasilMockServer? _instance;
    public static P2PYggdrasilMockServer Instance => _instance ??= new P2PYggdrasilMockServer();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly object _lock = new();

    public int Port { get; private set; }
    public bool IsRunning => _listener != null && _listener.IsListening;

    public void Start()
    {
        lock (_lock)
        {
            if (IsRunning) return;

            for (int tryPort = 25585; tryPort < 25600; tryPort++)
            {
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{tryPort}/");
                    listener.Start();

                    _listener = listener;
                    Port = tryPort;
                    break;
                }
                catch
                {
                }
            }

            if (_listener == null)
            {
                var fallbackListener = new HttpListener();
                fallbackListener.Prefixes.Add("http://127.0.0.1:0/");
                try
                {
                    fallbackListener.Start();
                    _listener = fallbackListener;
                }
                catch
                {
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ProcessRequestsAsync(_cts.Token), _cts.Token);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            _listener = null;
        }
    }

    private async Task ProcessRequestsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleContextAsync(context), token);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            var req = context.Request;
            var res = context.Response;

            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            string rawUrl = req.RawUrl ?? "/";
            string path = req.Url?.AbsolutePath ?? "/";

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            if (path == "/" || path == "/api/yggdrasil" || path == "/api/yggdrasil/")
            {
                string metaJson = "{\"meta\":{\"serverName\":\"CL-Launcher\",\"implementationName\":\"CL-P2P\",\"implementationVersion\":\"1.0.0\"},\"skinDomains\":[\"mojang.com\",\"minecraft.net\"]}";
                await WriteJsonAsync(res, metaJson, 200).ConfigureAwait(false);
                return;
            }

            if (path.EndsWith("/session/minecraft/join", StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            if (path.Contains("/session/minecraft/hasJoined", StringComparison.OrdinalIgnoreCase))
            {
                string username = "Player";
                var match = Regex.Match(rawUrl, @"username=([^&]+)");
                if (match.Success)
                {
                    username = Uri.UnescapeDataString(match.Groups[1].Value);
                }

                string uuid = GenerateOfflineUuid(username);
                string profileJson = $"{{\"id\":\"{uuid}\",\"name\":\"{username}\",\"properties\":[]}}";
                await WriteJsonAsync(res, profileJson, 200).ConfigureAwait(false);
                return;
            }

            if (path.Contains("/session/minecraft/profile/", StringComparison.OrdinalIgnoreCase))
            {
                string lastPart = path.Substring(path.LastIndexOf('/') + 1);
                string profileJson = $"{{\"id\":\"{lastPart}\",\"name\":\"Player\",\"properties\":[]}}";
                await WriteJsonAsync(res, profileJson, 200).ConfigureAwait(false);
                return;
            }

            res.StatusCode = 404;
            res.Close();
        }
        catch
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, string json, int statusCode)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        res.StatusCode = statusCode;
        res.ContentType = "application/json; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        res.OutputStream.Close();
    }

    private static string GenerateOfflineUuid(string username)
    {
        byte[] input = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        byte[] hash = MD5.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        Stop();
    }
}
