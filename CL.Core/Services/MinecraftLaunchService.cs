using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace CL_CLegendary_Launcher_.Class
{
    public sealed class LaunchRequest
    {
        public string VersionName { get; init; } = string.Empty;
        public string Nickname { get; init; } = "Player";
        public int MemoryMb { get; init; } = 2048;
        public string? JavaPath { get; init; }
        public int ScreenWidth { get; init; } = 854;
        public int ScreenHeight { get; init; } = 480;
        public bool FullScreen { get; init; }
    }

    /// Запуск гри без залежностей від UI: усе, що потрібно, приходить у LaunchRequest.
    public sealed class MinecraftLaunchService
    {
        private readonly MinecraftLauncher _launcher;

        public MinecraftLaunchService(string launcherPath)
        {
            _launcher = new MinecraftLauncher(new MinecraftPath(launcherPath));
        }

        public async Task<Process> LaunchAsync(
            LaunchRequest request,
            IProgress<string>? status = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(request.VersionName))
                throw new ArgumentException("Не вибрано версію гри.", nameof(request));

            var options = new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(request.Nickname),
                MinimumRamMb = request.MemoryMb,
                MaximumRamMb = request.MemoryMb,
                ScreenWidth = request.ScreenWidth,
                ScreenHeight = request.ScreenHeight,
                FullScreen = request.FullScreen
            };

            if (!string.IsNullOrWhiteSpace(request.JavaPath))
                options.JavaPath = request.JavaPath;

            status?.Report($"Встановлення {request.VersionName}...");

            var process = await _launcher.InstallAndBuildProcessAsync(
                request.VersionName, options, cancellationToken: token);

            status?.Report("Запуск...");
            process.Start();

            return process;
        }
    }
}
