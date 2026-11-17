using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CL.Core.Models;
using CL.Core.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public sealed class LauncherUpdateCheckResult
{
    public UpdateChannel Channel { get; init; }
    public string TargetVersion { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public bool IsVelopack => VelopackManager != null && VelopackUpdateInfo != null;
    public Velopack.UpdateManager? VelopackManager { get; init; }
    public Velopack.UpdateInfo? VelopackUpdateInfo { get; init; }
    public UpdateInfo? FallbackInfo { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public string DownloadedFolderName { get; init; } = "win-x64";
}

public static class LauncherUpdateCoordinator
{
    public const string UpdateRepoUrl = "https://github.com/WER-CORE/CL-Win-Edition--Update";
    public const string UpdateRepoSlug = "WER-CORE/CL-Win-Edition--Update";

    public static UpdateChannel GetCurrentChannel() =>
        UpdateService.ResolveChannel(SettingsManager.Default.UpdateChannel);

    public static Task<LauncherUpdateCheckResult?> CheckForUpdatesAsync(bool respectSkippedVersion) =>
        CheckForUpdatesInternalAsync(
            respectSkippedVersion,
            GetCurrentChannel(),
            UpdateService.NormalizeVersionTag(SettingsManager.Default.SkippedUpdateVersion),
            UpdateService.GetCurrentLauncherVersion(),
            isPreview => new Velopack.UpdateManager(new ChannelStrictGithubSource(UpdateRepoUrl, null, isPreview)),
            updateService: null,
            log: msg => Debug.WriteLine(msg));

    internal static async Task<LauncherUpdateCheckResult?> CheckForUpdatesInternalAsync(
        bool respectSkippedVersion,
        UpdateChannel channel,
        string skippedVersion,
        string localVersion,
        Func<bool, Velopack.UpdateManager?> velopackManagerFactory,
        UpdateService? updateService,
        Action<string>? log = null)
    {
        bool isPreview = channel == UpdateChannel.Testing;
        string normalizedSkipped = UpdateService.NormalizeVersionTag(skippedVersion);

        Velopack.UpdateManager? mgr = null;
        bool isVelopackInstalled = false;
        try
        {
            mgr = velopackManagerFactory(isPreview);
            isVelopackInstalled = mgr?.IsInstalled == true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[LauncherUpdateCoordinator] Velopack initialization failed ({channel}): {ex.GetType().Name}: {ex.Message}");
            mgr = null;
            isVelopackInstalled = false;
        }

        if (isVelopackInstalled && mgr != null)
        {
            try
            {
                var vInfo = await mgr.CheckForUpdatesAsync();
                if (vInfo?.TargetFullRelease != null)
                {
                    string targetVer = UpdateService.NormalizeVersionTag(vInfo.TargetFullRelease.Version.ToString());
                    string currentVer = UpdateService.NormalizeVersionTag(mgr.CurrentVersion?.ToString() ?? localVersion);

                    if (UpdateService.CompareVersions(targetVer, currentVer) <= 0)
                        return null;

                    if (respectSkippedVersion && !string.IsNullOrEmpty(normalizedSkipped) &&
                        string.Equals(targetVer, normalizedSkipped, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    return new LauncherUpdateCheckResult
                    {
                        Channel = channel,
                        TargetVersion = targetVer,
                        CurrentVersion = currentVer,
                        VelopackManager = mgr,
                        VelopackUpdateInfo = vInfo
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[LauncherUpdateCoordinator] Velopack check failed ({channel}); ZIP fallback suppressed for installed build: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        try
        {
            var service = updateService ?? new UpdateService();
            string localVer = string.IsNullOrWhiteSpace(localVersion)
                ? UpdateService.GetCurrentLauncherVersion()
                : localVersion;
            var info = await service.CheckForUpdateFromGitHubAsync(UpdateRepoSlug, isPreview, localVer);

            if (info == null || string.IsNullOrEmpty(info.Version))
                return null;

            string remoteVer = UpdateService.NormalizeVersionTag(info.Version);
            if (!service.IsUpdateAvailable(remoteVer, localVer))
                return null;

            if (respectSkippedVersion && !string.IsNullOrEmpty(normalizedSkipped) &&
                string.Equals(remoteVer, normalizedSkipped, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string downloadUrl = service.GetCorrectDownloadUrl(info);
            if (!UpdateService.IsValidHttpsUrl(downloadUrl))
                return null;

            return new LauncherUpdateCheckResult
            {
                Channel = channel,
                TargetVersion = remoteVer,
                CurrentVersion = UpdateService.NormalizeVersionTag(localVer),
                FallbackInfo = info,
                DownloadUrl = downloadUrl,
                DownloadedFolderName = service.GetDownloadedFolderName()
            };
        }
        catch (Exception ex)
        {
            log?.Invoke($"[LauncherUpdateCoordinator] Portable fallback check failed ({channel}): {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
