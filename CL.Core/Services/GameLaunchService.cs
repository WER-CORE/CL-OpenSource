using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Interfaces;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using CmlLib.Core.ProcessBuilder;
using Optifine.Installer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace CL.Core.Services
{
    public enum LoaderType
    {
        Vanilla,
        Forge,
        Fabric,
        Quilt,
        Optifine,
        NeoForge,
        LiteLoader,
    }

    public class GameLaunchService
    {
        private readonly GameSessionManager _gameSessionManager;
        public static string? ActiveMinecraftVersion { get; set; }
        public static LoaderType ActiveLoaderType { get; set; } = LoaderType.Vanilla;
        public static string? ActiveLoaderVersion { get; set; }
        public static string? ActiveModpackName { get; set; }
        public static string? ActiveModpackTypeSite { get; set; }
        public static string? ActiveGamePath { get; set; }
        public static Process? ActiveProcess { get; set; }

        private readonly LastActionService _lastActionService;
        private readonly HistoricalVersionCatalogService _historicalCatalogService = new();
        private readonly HistoricalVersionInstallerService _historicalInstallerService = new();

        private CancellationTokenSource _cts;

        public GameLaunchService(GameSessionManager sessionManager, LastActionService lastActionService)
        {
            _gameSessionManager = sessionManager;
            _lastActionService = lastActionService;
        }

        private bool IsOfflineMode()
        {
            return SettingsManager.Default.OfflineModLauncher || !NetworkInterface.GetIsNetworkAvailable();
        }

        private static bool IsAutoLoaderVersion(string? loaderVersion)
        {
            return string.IsNullOrWhiteSpace(loaderVersion) ||
                   loaderVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                   loaderVersion.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        }

        private string? TryGetPreinstalledVersionName(LoaderType loaderType, string mcVersion, string? loaderVersion, MinecraftPath path)
        {
            bool canPredictName = loaderType is LoaderType.Vanilla or LoaderType.LiteLoader ||
                                  !IsAutoLoaderVersion(loaderVersion) ||
                                  mcVersion.Contains("optifine", StringComparison.OrdinalIgnoreCase) ||
                                  mcVersion.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
                                  mcVersion.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
                                  mcVersion.Contains("quilt", StringComparison.OrdinalIgnoreCase) ||
                                  mcVersion.Contains("liteloader", StringComparison.OrdinalIgnoreCase);
            if (!canPredictName)
            {
                return null;
            }

            string candidate = GetOfflineVersionName(loaderType, mcVersion, loaderVersion);
            return HistoricalVersionInstallerService.IsVersionInstalled(candidate, path) ? candidate : null;
        }

        private string GetOfflineVersionName(LoaderType loaderType, string mcVersion, string? loaderVersion)
            => ResolveLoaderVersionName(loaderType, mcVersion, loaderVersion);

        public static string ResolveLoaderVersionName(LoaderType loaderType, string mcVersion, string? loaderVersion)
        {
            if (mcVersion.Contains("optifine", StringComparison.OrdinalIgnoreCase) ||
                mcVersion.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
                mcVersion.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
                mcVersion.Contains("quilt", StringComparison.OrdinalIgnoreCase) ||
                mcVersion.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
            {
                return mcVersion;
            }

            string rawLoader = loaderVersion?.Trim() ?? string.Empty;
            switch (loaderType)
            {
                case LoaderType.Vanilla:
                    return mcVersion;

                case LoaderType.Optifine:
                    string cleanOptifine = rawLoader;
                    if (cleanOptifine.StartsWith($"{mcVersion}-", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanOptifine = cleanOptifine[(mcVersion.Length + 1)..];
                    }
                    if (cleanOptifine.StartsWith("OptiFine_", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanOptifine = cleanOptifine["OptiFine_".Length..];
                    }
                    if (cleanOptifine.StartsWith($"{mcVersion}_", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanOptifine = cleanOptifine[(mcVersion.Length + 1)..];
                    }
                    return $"{mcVersion}-OptiFine_{cleanOptifine}";

                case LoaderType.Fabric:
                    return $"fabric-loader-{rawLoader}-{mcVersion}";

                case LoaderType.Quilt:
                    return $"quilt-loader-{rawLoader}-{mcVersion}";

                case LoaderType.Forge:
                    string cleanForge = rawLoader;
                    if (cleanForge.StartsWith($"{mcVersion}-forge-", StringComparison.OrdinalIgnoreCase))
                    {
                        return cleanForge;
                    }
                    if (cleanForge.StartsWith($"{mcVersion}-", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanForge = cleanForge[(mcVersion.Length + 1)..];
                    }
                    if (cleanForge.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanForge = cleanForge["forge-".Length..];
                    }
                    return $"{mcVersion}-forge-{cleanForge}";

                case LoaderType.NeoForge:
                    string cleanNeoForge = rawLoader;
                    if (cleanNeoForge.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanNeoForge = cleanNeoForge["neoforge-".Length..];
                    }
                    return $"neoforge-{cleanNeoForge}";

                case LoaderType.LiteLoader:
                    return $"{mcVersion}-LiteLoader{mcVersion}";

                default:
                    return mcVersion;
            }
        }

        public async Task LaunchGameAsync(LoaderType loaderType, string minecraftVersion, string? loaderVersion, LaunchConfiguration config)
        {
            _cts = new CancellationTokenSource();
            var launchCts = _cts;
            var token = launchCts.Token;
            Process? launchedProcess = null;
            config.IsOffline = IsOfflineMode();
            ActiveMinecraftVersion = minecraftVersion;
            ActiveLoaderType = loaderType;
            ActiveLoaderVersion = loaderVersion;
            ActiveModpackName = null;
            ActiveGamePath = SettingsManager.Default.PathLacunher;

            ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
            {
                ServiceLocator.Current.GetService<IMainWindowController>().SetInstallVersionOnPlay(true);
                ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(config.IsOffline ? LocalizationManager.GetString("GameLaunch.LaunchOffline", "ОФЛАЙН ЗАПУСК...") : LocalizationManager.GetString("GameLaunch.LaunchDownloading", "ЗАВАНТАЖЕННЯ..."));
            });

            var dowloadProgress = ServiceLocator.Current.GetService<ITaskProgressService>();
            dowloadProgress.CTS = _cts;
            if (!config.IsOffline) dowloadProgress.ShowProgressWindow();

            try
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = 256;
                var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                var httpClient = WebHelper.Client;
                int safeThreads = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

                var parallelInstaller = new ParallelGameInstaller(
                    maxChecker: 32,
                    maxDownloader: safeThreads,
                    boundedCapacity: 2048,
                    httpClient
                );

                var parameters = MinecraftLauncherParameters.CreateDefault(path);
                parameters.GameInstaller = parallelInstaller;

                if (config.IsOffline)
                {
                    parameters.VersionLoader = new CmlLib.Core.VersionLoader.LocalJsonVersionLoader(path);
                }

                var launcher = new MinecraftLauncher(parameters);

                string versionLabel = string.IsNullOrEmpty(loaderVersion) ? minecraftVersion : $"{minecraftVersion} ({loaderVersion})";
                launcher.FileProgressChanged += (sender, args) =>
                {
                    int fileProgress = args.TotalTasks > 0 ? (int)((double)args.ProgressedTasks / args.TotalTasks * 100) : 0;
                    dowloadProgress.UpdateFileTaskProgress(args.TotalTasks, args.ProgressedTasks, args.Name);
                    dowloadProgress.UpdateVersionProgress(fileProgress, versionLabel);
                };

                launcher.ByteProgressChanged += (sender, args) =>
                {
                    int byteProgress = args.TotalBytes > 0 ? (int)((double)args.ProgressedBytes / args.TotalBytes * 100) : 0;
                    dowloadProgress.UpdateFileProgress(byteProgress);
                };

                string versionName = "";

                if (config.IsOffline)
                {
                    versionName = GetOfflineVersionName(loaderType, minecraftVersion, loaderVersion);
                    if (!HistoricalVersionInstallerService.IsVersionInstalled(versionName, path))
                    {
                        ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                            LocalizationManager.GetString("GameLaunch.LaunchNeedInternetDesc", "Ой! Схоже, у вас немає інтернету, а ця версія ще жодного разу не запускалася.\n\nЩоб грати без мережі, спочатку завантажте цю версію онлайн!"),
                            LocalizationManager.GetString("GameLaunch.LaunchNeedInternetTitle", "Потрібен інтернет"),
                            MascotEmotion.Sad);
                        return;
                    }
                }
                else
                {
                    versionName = await InstallVersionAsync(loaderType, minecraftVersion, loaderVersion, launcher, token);
                    if (string.IsNullOrEmpty(versionName))
                    {
                        ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                            LocalizationManager.GetString("GameLaunch.LaunchInstallFailedDesc", "Ой леле! Я намагалася встановити цю версію, але нічого не вийшло.\nСпробуй ще раз пізніше."),
                            LocalizationManager.GetString("GameLaunch.LaunchInstallFailedTitle", "Помилка встановлення"),
                            MascotEmotion.Sad);
                        return;
                    }
                }
                var launchOption = CreateLaunchOptions(config, versionName);
                string versionJsonPath = Path.Combine(path.Versions, versionName, $"{versionName}.json");
                bool isHistorical = HistoricalLaunchCompatibilityService.IsHistoricalVersion(versionName, versionJsonPath);
                HistoricalLaunchCompatibilityService.LastDiagnosticSummary = null;
                JavaInstallation? selectedHistoricalJava = null;
                string? historicalGameDir = null;
                if (isHistorical)
                {
                    historicalGameDir = HistoricalLaunchCompatibilityService.ResolveHistoricalGameDirectory(path.BasePath, versionName);
                    if (HistoricalLaunchCompatibilityService.DetectModernOptionsConflict(path.BasePath))
                    {
                        System.Diagnostics.Trace.WriteLine($"[LegacyLaunch] Виявлено сучасний options.txt у {path.BasePath}; історичні налаштування ізольовано в {historicalGameDir}");
                    }

                    var javaReq = HistoricalLaunchCompatibilityService.ReadJavaRequirement(versionJsonPath, path.Library);
                    var currentOs = HistoricalLaunchCompatibilityService.GetCurrentOSPlatform();
                    var currentArch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
                    bool rosettaAvailable = HistoricalLaunchCompatibilityService.IsRosetta2Installed();
                    string runtimeSource = "auto";

                    if (!string.IsNullOrWhiteSpace(config.JavaPath))
                    {
                        runtimeSource = "user";
                        var explicitJava = JavaLocator.Inspect(config.JavaPath, "user")
                                           ?? new JavaInstallation { Executable = config.JavaPath, MajorVersion = javaReq.MajorVersion, Source = "user" };
                        var explicitCheck = HistoricalLaunchCompatibilityService.ValidateExplicitJavaChoice(
                            explicitJava, javaReq, currentOs, currentArch, rosettaAvailable);
                        if (!explicitCheck.Success && !string.IsNullOrWhiteSpace(explicitCheck.DiagnosticMessage))
                        {
                            ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                                explicitCheck.DiagnosticMessage,
                                LocalizationManager.GetString("GameLaunch.LaunchErrorTitle", "Помилка запуску"),
                                MascotEmotion.Sad);
                            return;
                        }

                        selectedHistoricalJava = explicitCheck.Selected;
                        launchOption.JavaPath = config.JavaPath;
                        if (!string.IsNullOrWhiteSpace(explicitCheck.DiagnosticMessage))
                        {
                            System.Diagnostics.Trace.WriteLine($"[LegacyLaunch] {explicitCheck.DiagnosticMessage}");
                        }
                    }
                    else
                    {
                        var detectedJavas = JavaLocator.Detect(path.BasePath);
                        if (detectedJavas.Count > 0)
                        {
                            var javaSelection = HistoricalLaunchCompatibilityService.SelectCompatibleJava(
                                detectedJavas,
                                javaReq,
                                currentOs,
                                currentArch,
                                rosettaAvailable);

                            if (!javaSelection.Success && !string.IsNullOrWhiteSpace(javaSelection.DiagnosticMessage))
                            {
                                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                                    javaSelection.DiagnosticMessage,
                                    LocalizationManager.GetString("GameLaunch.LaunchErrorTitle", "Помилка запуску"),
                                    MascotEmotion.Sad);
                                return;
                            }

                            if (javaSelection.Selected != null)
                            {
                                selectedHistoricalJava = javaSelection.Selected;
                                launchOption.JavaPath = javaSelection.Selected.Executable;
                                if (!string.IsNullOrWhiteSpace(javaSelection.DiagnosticMessage))
                                {
                                    System.Diagnostics.Trace.WriteLine($"[LegacyLaunch] {javaSelection.DiagnosticMessage}");
                                }
                            }
                        }
                    }

                    bool? nativeCompatible = null;
                    if (selectedHistoricalJava != null && File.Exists(versionJsonPath))
                    {
                        var preLaunchCheck = HistoricalLaunchCompatibilityService.ValidatePreLaunchPlatformCompatibility(
                            versionName,
                            versionJsonPath,
                            path,
                            selectedHistoricalJava,
                            currentOs,
                            currentArch);
                        nativeCompatible = preLaunchCheck.IsCompatible;
                        if (!preLaunchCheck.IsCompatible && !string.IsNullOrWhiteSpace(preLaunchCheck.DiagnosticMessage))
                        {
                            ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                                preLaunchCheck.DiagnosticMessage,
                                LocalizationManager.GetString("GameLaunch.LaunchErrorTitle", "Помилка запуску"),
                                MascotEmotion.Sad);
                            return;
                        }
                    }

                    System.Diagnostics.Trace.WriteLine(HistoricalLaunchCompatibilityService.BuildSafeDiagnosticSummary(
                        versionName,
                        selectedHistoricalJava,
                        historicalGameDir,
                        GetProxyPort(versionName),
                        runtimeSource,
                        nativeCompatible));
                }
                if (SettingsManager.Default.EnableAutoBackup && SettingsManager.Default.EnableSubFiles_Backups)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(LocalizationManager.GetString("GameLaunch.LaunchBackupWorlds", "БЕКАП СВІТІВ..."));
                    var backupSavesDirs = HistoricalLaunchCompatibilityService.GetWorldBackupDirectories(path.BasePath, historicalGameDir);
                    if (backupSavesDirs.Count > 0)
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                foreach (var savesDir in backupSavesDirs)
                                {
                                    var worlds = Directory.GetDirectories(savesDir);
                                    foreach (var world in worlds)
                                    {
                                        await WorldBackupService.AutoBackupWorldAsync(world);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ServiceLocator.Current.GetService<IMainWindowController>().ShowSnackbar(
                                    LocalizationManager.GetString("GameLaunch.LaunchBackupErrorTitle", "Упс! Сталася помилка бекапу!"),
                                    string.Format(LocalizationManager.GetString("GameLaunch.LaunchBackupErrorDesc", "Помилка бекапу: {0}"), ex.Message),
                                    10);
                            }
                        });
                    }
                }

                ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(LocalizationManager.GetString("GameLaunch.LaunchStarting", "ЗАПУСК..."));

                Process process = await BuildGameProcessAsync(
                    launcher,
                    versionName,
                    launchOption,
                    config.ServerIp,
                    config.ServerPort,
                    minecraftVersion,
                    isVersionValidated: true,
                    cancellationToken: token);

                if (isHistorical)
                {
                    HistoricalLaunchCompatibilityService.ApplyHistoricalProcessFixes(
                        process.StartInfo,
                        versionName,
                        $"Minecraft {versionName}",
                        historicalGameDirectory: historicalGameDir);
                }
                ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                {
                    if (dowloadProgress.IsLoaded) dowloadProgress.CloseProgressWindow(launchCts);
                    ServiceLocator.Current.GetService<IMainWindowController>().Minimize();
                });

                await DiscordController.UpdatePresence(string.Format(LocalizationManager.GetString("DiscordRPC.PlayingVersion", "Грає версію {0}"), versionName), resetTimer: true);

                if (SettingsManager.Default.EnableLog)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().ShowGameLog(process);
                }
                else
                {
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.RedirectStandardError = false;
                    process.Start();
                }
                ActiveProcess = process;
                launchedProcess = process;

                string loaderName = loaderType.ToString();
                var action = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(config.ServerIp))
                {
                    action["type"] = "server";
                    action["name"] = !string.IsNullOrEmpty(config.ServerName) ? config.ServerName : config.ServerIp;
                    action["version"] = minecraftVersion;
                    action["ip"] = config.ServerIp;
                    action["port"] = config.ServerPort.ToString();
                    action["loader"] = loaderName.ToLower();
                    action["loaderVersion"] = loaderVersion;
                }
                else
                {
                    action["type"] = "version";
                    action["name"] = loaderName;
                    action["version"] = minecraftVersion;
                    action["loader"] = loaderName.ToLower();
                    action["loaderVersion"] = loaderVersion;
                }

                if (SettingsManager.Default.EnableMod_LatestActions) { await _lastActionService.AddLastActionAsync(action); }

                if (SettingsManager.Default.CloseLaucnher)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().Close();
                }

                if (SettingsManager.Default.EnableMod_Statistics) { _gameSessionManager.StartGameSession(!string.IsNullOrEmpty(config.ServerIp) ? "server" : (loaderType == LoaderType.Vanilla ? "vanilla" : "mod")); }

                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);

                await process.WaitForExitAsync();
                ClearActiveProcessState(process);

                int exitCode = process.ExitCode;

                if (!SettingsManager.Default.CloseLaucnher)
                {
                    _ = DiscordController.UpdatePresence(LocalizationManager.GetString("DiscordRPC.InHome", "В головному вікні"), resetTimer: true);
                }

                if (exitCode != 0)
                {
                    ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                    {
                        ServiceLocator.Current.GetService<IMainWindowController>().Restore();
                        if (!SettingsManager.Default.EnableLog)
                        {
                            string logFilePath = Path.Combine(path.BasePath, "logs", "latest.log");
                            ServiceLocator.Current.GetService<IMainWindowController>().ShowGameLogFromFile(logFilePath);
                        }
                        ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                            string.Format(LocalizationManager.GetString("GameLaunch.CrashDesc", "Йой! Майнкрафт впав (Код помилки: {0}).\nЯ відкрила логи, щоб ми могли знайти конфліктний мод або помилку."), exitCode),
                            LocalizationManager.GetString("GameLaunch.CrashTitle", "Краш гри!"),
                            MascotEmotion.Sad);
                    });
                }
                else
                {

                    if (SettingsManager.Default.CloseLaucnher)
                    {
                        ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() => Environment.Exit(0));
                    }
                    else
                    {
                        ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                        {
                            ServiceLocator.Current.GetService<IMainWindowController>().Restore();
                        });
                    }
                }
            }

            catch (OperationCanceledException)
            {
                ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().Restore();
                });
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledDesc", "Гаразд, я зупинила завантаження.\nМи можемо спробувати знову, коли ти будеш готовий!"),
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledTitle", "Скасовано"),
                    MascotEmotion.Normal);
            }

            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().Restore();
                });
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    string.Format(LocalizationManager.GetString("GameLaunch.LaunchCrashDesc", "Ой! Сталася помилка під час запуску гри.\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.LaunchCrashTitle", "Помилка запуску"),
                    MascotEmotion.Sad);
            }

            finally
            {
                _gameSessionManager.StopGameSession();
                if (launchedProcess != null)
                {
                    ClearActiveProcessState(launchedProcess);
                    launchedProcess.Dispose();
                }

                ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().SetInstallVersionOnPlay(false);
                    string savedTypeStr = SettingsManager.Default.LastSelectedType.ToString();
                    if (SettingsManager.Default.LastSelectedType == 5 && !string.IsNullOrEmpty(SettingsManager.Default.LastSelectedModVersion))
                    {
                        ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), SettingsManager.Default.LastSelectedModVersion));
                    }
                    else
                    {
                        ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), SettingsManager.Default.LastSelectedVersion));
                    }
                    if (dowloadProgress.IsLoaded) dowloadProgress.CloseProgressWindow(launchCts);
                });
                launchCts.Dispose();
            }
        }

        public static void ClearActiveProcessState(Process process)
        {
            if (!ReferenceEquals(ActiveProcess, process)) return;
            ActiveMinecraftVersion = null;
            ActiveLoaderType = LoaderType.Vanilla;
            ActiveLoaderVersion = null;
            ActiveModpackName = null;
            ActiveModpackTypeSite = null;
            ActiveGamePath = null;
            ActiveProcess = null;
        }

        public static async Task<Process> BuildGameProcessAsync(
            MinecraftLauncher launcher,
            string versionName,
            MLaunchOption launchOption,
            string? serverIp,
            int serverPort,
            string? minecraftVersion,
            bool isVersionValidated = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MinecraftLauncher processLauncher = launcher;
            if (launcher.Versions == null &&
                launcher.VersionLoader is not CmlLib.Core.VersionLoader.LocalJsonVersionLoader &&
                (isVersionValidated || HistoricalVersionInstallerService.IsVersionInstalled(versionName, launcher.MinecraftPath)))
            {
                var localParams = MinecraftLauncherParameters.CreateDefault(launcher.MinecraftPath);
                localParams.VersionLoader = new CmlLib.Core.VersionLoader.LocalJsonVersionLoader(launcher.MinecraftPath);
                localParams.JavaPathResolver = launcher.JavaPathResolver;
                localParams.FileExtractors = launcher.FileExtractors;
                localParams.GameInstaller = launcher.GameInstaller;
                localParams.NativeLibraryExtractor = launcher.NativeLibraryExtractor;
                localParams.RulesEvaluator = launcher.RulesEvaluator;
                processLauncher = new MinecraftLauncher(localParams)
                {
                    RulesContext = launcher.RulesContext
                };
            }

            var process = await processLauncher.BuildProcessAsync(versionName, launchOption, cancellationToken);
            if (!string.IsNullOrEmpty(serverIp))
            {
                EnsureServerConnectionArguments(process, serverIp, serverPort, minecraftVersion);
            }
            return process;
        }

        private static async Task EnsureVersionFilesInstalledAsync(string versionName, MinecraftLauncher launcher, CancellationToken token)
        {
            if (!HistoricalVersionInstallerService.IsVersionInstalled(versionName, launcher.MinecraftPath))
            {
                await launcher.InstallAsync(versionName, token);
            }
        }

        private static async Task<string?> ResolveAutoLoaderVersionAsync(string? loaderVersion, string metadataUrl, CancellationToken token)
        {
            if (!IsAutoLoaderVersion(loaderVersion))
            {
                return loaderVersion;
            }

            try
            {
                string json = await WebHelper.Client.GetStringAsync(metadataUrl, token);
                var arr = Newtonsoft.Json.Linq.JArray.Parse(json);
                var firstVer = arr.FirstOrDefault()?["loader"]?["version"]?.ToString();
                if (!string.IsNullOrEmpty(firstVer))
                {
                    return firstVer;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }

            return loaderVersion;
        }

        private static async Task<string> InstallProfileLoaderAsync(
            string mcVersion,
            string? loaderVersion,
            string profilePrefix,
            string metadataUrl,
            Func<string, string, MinecraftPath, Task<string>> installProfileAsync,
            MinecraftLauncher launcher,
            CancellationToken token)
        {
            bool wasAuto = IsAutoLoaderVersion(loaderVersion);
            string? resolvedLoader = await ResolveAutoLoaderVersionAsync(loaderVersion, metadataUrl, token);
            if (wasAuto && !IsAutoLoaderVersion(resolvedLoader))
            {
                string expectedName = $"{profilePrefix}-{resolvedLoader}-{mcVersion}";
                if (HistoricalVersionInstallerService.IsVersionInstalled(expectedName, launcher.MinecraftPath))
                {
                    return expectedName;
                }
            }

            token.ThrowIfCancellationRequested();
            string installedName = await installProfileAsync(mcVersion, $"{resolvedLoader}", launcher.MinecraftPath);
            token.ThrowIfCancellationRequested();
            await launcher.InstallAsync(installedName, token);
            return installedName;
        }

        public async Task<string> InstallVersionAsync(LoaderType loaderType, string mcVersion, string? loaderVersion, MinecraftLauncher launcher, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            string? preinstalled = TryGetPreinstalledVersionName(loaderType, mcVersion, loaderVersion, launcher.MinecraftPath);
            if (!string.IsNullOrEmpty(preinstalled))
            {
                return preinstalled;
            }

            switch (loaderType)
            {
                case LoaderType.Forge:
                    var forge = new NoAdForgeInstaller(launcher);
                    string forgeVersionName = await forge.Install(mcVersion, loaderVersion, new CmlLib.Core.Installer.Forge.ForgeInstallOptions { CancellationToken = token });
                    await EnsureVersionFilesInstalledAsync(forgeVersionName, launcher, token);
                    return forgeVersionName;

                case LoaderType.Fabric:
                    var fabricInstaller = new FabricInstaller(WebHelper.Client);
                    return await InstallProfileLoaderAsync(
                        mcVersion,
                        loaderVersion,
                        "fabric-loader",
                        $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}",
                        (mc, loader, path) => fabricInstaller.Install(mc, loader, path),
                        launcher,
                        token);

                case LoaderType.Quilt:
                    var quiltInstaller = new QuiltInstaller(WebHelper.Client);
                    return await InstallProfileLoaderAsync(
                        mcVersion,
                        loaderVersion,
                        "quilt-loader",
                        $"https://meta.quiltmc.org/v3/versions/loader/{mcVersion}",
                        (mc, loader, path) => quiltInstaller.Install(mc, loader, path),
                        launcher,
                        token);

                case LoaderType.NeoForge:
                    var neoForge = new NoAdNeoForgeInstaller(launcher);
                    string neoForgeVersionName = await neoForge.Install(mcVersion, loaderVersion, new CmlLib.Core.Installer.NeoForge.Installers.NeoForgeInstallOptions { CancellationToken = token });
                    await EnsureVersionFilesInstalledAsync(neoForgeVersionName, launcher, token);
                    return neoForgeVersionName;

                case LoaderType.Optifine:
                    {
                        var loader = new OptifineInstaller(WebHelper.Client);
                        var versions = await loader.GetOptifineVersionsAsync();
                        var selectedVersion = versions.FirstOrDefault(x => x.Version == loaderVersion);
                        if (selectedVersion == null)
                            throw new Exception(LocalizationManager.GetString("GameLaunch.OptifineNotFound", "Обрана версія Optifine не знайдена."));

                        var optifineVersionName = $"{selectedVersion.MinecraftVersion}-OptiFine_{selectedVersion.OptifineEdition}";
                        var optifineDir = Path.Combine(launcher.MinecraftPath.Versions, optifineVersionName);
                        var jarPath = Path.Combine(optifineDir, $"{optifineVersionName}.jar");
                        bool hasOptifineJar = File.Exists(jarPath) && new FileInfo(jarPath).Length > 0;

                        if (IsAutoLoaderVersion(loaderVersion) &&
                            hasOptifineJar &&
                            HistoricalVersionInstallerService.IsVersionInstalled(optifineVersionName, launcher.MinecraftPath))
                        {
                            return optifineVersionName;
                        }

                        await EnsureVersionFilesInstalledAsync(selectedVersion.MinecraftVersion, launcher, token);

                        string finalVersionName = optifineVersionName;
                        if (!hasOptifineJar)
                        {
                            token.ThrowIfCancellationRequested();
                            finalVersionName = await loader.InstallOptifineAsync(launcher.MinecraftPath.BasePath, selectedVersion);
                            if (!File.Exists(jarPath) || new FileInfo(jarPath).Length == 0)
                            {
                                await Task.Delay(2000, token);
                                if (!File.Exists(jarPath) || new FileInfo(jarPath).Length == 0)
                                {
                                    throw new Exception(LocalizationManager.GetString("GameLaunch.OptifineJarMissing", "Інсталятор Optifine завершився, але .jar файл не знайдено."));
                                }
                            }
                        }

                        await launcher.InstallAsync(finalVersionName, token);
                        return finalVersionName;
                    }
                case LoaderType.LiteLoader:
                    {
                        var liteLoaderInstaller = new LiteLoaderInstaller(WebHelper.Client);
                        var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                        var loaderToInstall = loaders.First(loader => loader.BaseVersion == mcVersion);
                        token.ThrowIfCancellationRequested();
                        string liteVersionName = await liteLoaderInstaller.Install(loaderToInstall, await launcher.GetVersionAsync(mcVersion), launcher.MinecraftPath);
                        token.ThrowIfCancellationRequested();
                        await EnsureVersionFilesInstalledAsync(liteVersionName, launcher, token);
                        return liteVersionName;
                    }
                default:
                    {
                        var catalog = await _historicalCatalogService.GetMergedCatalogAsync(token: token);
                        var betaCraftEntry = catalog.Versions.FirstOrDefault(v =>
                            string.Equals(v.Id, mcVersion, StringComparison.OrdinalIgnoreCase) &&
                            v.Source == VersionCatalogSource.BetaCraft);

                        if (betaCraftEntry != null)
                        {
                            await _historicalInstallerService.InstallAsync(betaCraftEntry, launcher, token);
                        }
                        else
                        {
                            await launcher.InstallAsync($"{mcVersion}", token);
                        }
                        return mcVersion;
                    }
            }
        }

        public static void EnsureServerConnectionArguments(Process process, string? serverIp, int serverPort, string? minecraftVersion)
        {
            if (string.IsNullOrWhiteSpace(serverIp) || process == null) return;
            int port = serverPort > 0 ? serverPort : 25565;
            bool isModern = false;
            if (!string.IsNullOrEmpty(minecraftVersion))
            {
                var match = System.Text.RegularExpressions.Regex.Match(minecraftVersion, @"1\.(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int minor))
                {
                    isModern = minor >= 20;
                }
            }
            if (process.StartInfo.ArgumentList != null && process.StartInfo.ArgumentList.Count > 0)
            {
                bool hasServer = process.StartInfo.ArgumentList.Contains("--server") ||
                                 process.StartInfo.ArgumentList.Contains("--quickPlayMultiplayer");
                if (!hasServer)
                {
                    if (isModern)
                    {
                        process.StartInfo.ArgumentList.Add("--quickPlayMultiplayer");
                        process.StartInfo.ArgumentList.Add($"{serverIp}:{port}");
                    }
                    process.StartInfo.ArgumentList.Add("--server");
                    process.StartInfo.ArgumentList.Add(serverIp);
                    process.StartInfo.ArgumentList.Add("--port");
                    process.StartInfo.ArgumentList.Add(port.ToString());
                }
            }

            else if (!string.IsNullOrEmpty(process.StartInfo.Arguments))
            {
                bool hasServer = process.StartInfo.Arguments.Contains("--server") ||
                                 process.StartInfo.Arguments.Contains("--quickPlayMultiplayer");
                if (!hasServer)
                {
                    string extra = isModern
                        ? $" --quickPlayMultiplayer {serverIp}:{port} --server {serverIp} --port {port}"
                        : $" --server {serverIp} --port {port}";
                    process.StartInfo.Arguments += extra;
                }
            }
        }

        public MLaunchOption CreateLaunchOptions(LaunchConfiguration config, string versionName = "")
        {
            var baseOptions = new MLaunchOption
            {
                MinimumRamMb = config.MinimumRamMb,
                MaximumRamMb = config.MaximumRamMb,
                Session = config.Session,
                ScreenWidth = config.ScreenWidth,
                ScreenHeight = config.ScreenHeight,
                FullScreen = SettingsManager.Default.FullScreen,
                ServerIp = config.ServerIp,
                ServerPort = config.ServerPort,
            };
            if (!string.IsNullOrWhiteSpace(config.JavaPath))
            {
                baseOptions.JavaPath = config.JavaPath;
            }
            bool isHistoricalOffline = config.AccountType == AccountType.Offline &&
                                       HistoricalLaunchCompatibilityService.IsHistoricalVersion(versionName);
            if (!isHistoricalOffline && config.AccountType != AccountType.LittleSkin && config.AllowOfflineLan && (config.AccountType == AccountType.Offline || config.JoinServer))
            {

                try
                {
                    AuthlibInjectorHelper.EnsureDownloaded();
                    string jarPath = AuthlibInjectorHelper.GetJarPath();
                    if (File.Exists(jarPath))
                    {
                        var mockServer = CL.Core.Services.P2P.P2PYggdrasilMockServer.Instance;
                        mockServer.Start();
                        if (mockServer.IsRunning && mockServer.Port > 0)
                        {
                            if (config.Session != null && !string.IsNullOrWhiteSpace(config.Session.Username))
                            {
                                mockServer.RegisterProfile(config.Session.UUID, config.Session.Username);
                            }

                            var jvmArgs = baseOptions.ExtraJvmArguments != null
                                ? baseOptions.ExtraJvmArguments.ToList()
                                : new List<MArgument>();
                            jvmArgs.Add(new MArgument
                            {
                                Values = new[] { $"-javaagent:{jarPath}=http://127.0.0.1:{mockServer.Port}/api/yggdrasil" }
                            });
                            baseOptions.ExtraJvmArguments = jvmArgs;
                        }
                    }
                }
                catch
                {
                }
            }
            if (config.AccountType == AccountType.LittleSkin)
            {
                AuthlibInjectorHelper.EnsureDownloaded();
                string jarPath = AuthlibInjectorHelper.GetJarPath();
                var jvmArgs = new List<MArgument>
                {
                    new MArgument
                    {
                        Values = new[] { $@"-javaagent:{jarPath}=https://littleskin.cn/api/yggdrasil" }
                    }
                };
                baseOptions.ExtraJvmArguments = jvmArgs;
            }
            if (!string.IsNullOrEmpty(versionName))
            {
                string proxyPort = GetProxyPort(versionName);
                if (proxyPort != null)
                {
                    var argsList = baseOptions.ExtraJvmArguments != null
                        ? baseOptions.ExtraJvmArguments.ToList()
                        : new List<MArgument>();
                    AddUniqueJvmArg(argsList, "-Dhttp.proxyHost=betacraft.uk");
                    AddUniqueJvmArg(argsList, $"-Dhttp.proxyPort={proxyPort}");
                    AddUniqueJvmArg(argsList, $"-Dhttp.nonProxyHosts={HistoricalLaunchCompatibilityService.DefaultBetaCraftNonProxyHosts}");
                    AddUniqueJvmArg(argsList, "-Djava.util.Arrays.useLegacyMergeSort=true");
                    baseOptions.ExtraJvmArguments = argsList;
                }
            }
            return baseOptions;
        }

        private static void AddUniqueJvmArg(List<MArgument> argsList, string value)
        {
            if (!argsList.Any(a => a.Values != null && a.Values.Contains(value, StringComparer.Ordinal)))
            {
                argsList.Add(new MArgument(value));
            }
        }

        public static string GetProxyPort(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            version = version.Trim().ToLowerInvariant();
            if (version.StartsWith("rd-") || version.StartsWith("c0.0") || version.StartsWith("c0.2") || version.StartsWith("c0.30"))
                return "11701";

            if (version.StartsWith("in-") || version.StartsWith("inf-") || version.StartsWith("a1.0") || version.StartsWith("a1.1"))
                return "11702";

            if (version.StartsWith("a1.2") || Regex.IsMatch(version, @"^b1\.[0-8](?:$|[^0-9])"))
                return "11705";

            if (Regex.IsMatch(version, @"^1\.[0-5](?:$|[^0-9])"))
                return "11707";
            return null;
        }
    }
}
