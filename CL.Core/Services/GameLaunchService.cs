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
        private readonly LastActionService _lastActionService;
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
        private string GetOfflineVersionName(LoaderType loaderType, string mcVersion, string loaderVersion)
        {
            if (mcVersion.ToLower().Contains("optifine") ||
                mcVersion.ToLower().Contains("forge") ||
                mcVersion.ToLower().Contains("fabric"))
            {
                return mcVersion;
            }

            switch (loaderType)
            {
                case LoaderType.Vanilla:
                    return mcVersion;
                case LoaderType.Optifine:
                    string cleanLoader = loaderVersion?.Replace("OptiFine_", "");
                    return $"{mcVersion}-OptiFine_{cleanLoader}";
                case LoaderType.Fabric:
                    return $"fabric-loader-{loaderVersion}-{mcVersion}";
                case LoaderType.Quilt:
                    return $"quilt-loader-{loaderVersion}-{mcVersion}";
                case LoaderType.Forge:
                    return $"{mcVersion}-forge-{loaderVersion}";
                case LoaderType.NeoForge:
                    return $"neoforge-{loaderVersion}";
                case LoaderType.LiteLoader:
                    return $"{mcVersion}-LiteLoader{mcVersion}";
                default:
                    return mcVersion;
            }
        }
        public async Task LaunchGameAsync(LoaderType loaderType, string minecraftVersion, string loaderVersion, LaunchConfiguration config)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            config.IsOffline = IsOfflineMode();

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
                launcher.FileProgressChanged += (sender, args) =>
                {
                    ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                    {
                        int fileProgress = args.TotalTasks > 0 ? (int)((double)args.ProgressedTasks / args.TotalTasks * 100) : 0;
                        dowloadProgress.UpdateFileTaskProgress(args.TotalTasks, args.ProgressedTasks, args.Name);
                        string versionLabel = string.IsNullOrEmpty(loaderVersion) ? minecraftVersion : $"{minecraftVersion} ({loaderVersion})";
                        dowloadProgress.UpdateVersionProgress(fileProgress, versionLabel);
                    });
                };

                launcher.ByteProgressChanged += (sender, args) =>
                {
                    ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                    {
                        int byteProgress = args.TotalBytes > 0 ? (int)((double)args.ProgressedBytes / args.TotalBytes * 100) : 0;
                        dowloadProgress.UpdateFileProgress(byteProgress);
                    });
                };

                string versionName = "";

                if (config.IsOffline)
                {
                    versionName = GetOfflineVersionName(loaderType, minecraftVersion, loaderVersion);

                    string versionDir = Path.Combine(path.Versions, versionName);
                    if (!Directory.Exists(versionDir))
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

                if (SettingsManager.Default.EnableAutoBackup && SettingsManager.Default.EnableSubFiles_Backups)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().SetPlayButtonText(LocalizationManager.GetString("GameLaunch.LaunchBackupWorlds", "БЕКАП СВІТІВ..."));
                    string gameDir = path.BasePath;
                    string savesPath = Path.Combine(gameDir, "saves");

                    if (Directory.Exists(savesPath))
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                var worlds = Directory.GetDirectories(savesPath);
                                foreach (var world in worlds)
                                {
                                    await WorldBackupService.AutoBackupWorldAsync(world);
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

                Process process;
                if (config.IsOffline)
                {
                    process = await launcher.BuildProcessAsync(versionName, launchOption);
                }
                else
                {
                    process = await launcher.InstallAndBuildProcessAsync(versionName, launchOption, token);
                }

                ServiceLocator.Current.GetService<IDispatcherService>().Invoke(() =>
                {
                    if (dowloadProgress.IsLoaded) dowloadProgress.CloseProgressWindow();
                    ServiceLocator.Current.GetService<IMainWindowController>().Minimize();
                });

                await DiscordController.UpdatePresence(string.Format(LocalizationManager.GetString("DiscordRPC.PlayingVersion", "Грає версію {0}"), versionName));

                if (SettingsManager.Default.EnableLog)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().ShowGameLog(process);
                }
                else
                {
                    process.Start();
                }

                string loaderName = loaderType.ToString();
                var action = new Dictionary<string, string>
                {
                    ["type"] = loaderType == LoaderType.Vanilla ? "version" : "version",
                    ["name"] = loaderName,
                    ["version"] = minecraftVersion,
                    ["loader"] = loaderName.ToLower(),
                    ["loaderVersion"] = loaderVersion
                };
                if (SettingsManager.Default.EnableMod_LatestActions) { await _lastActionService.AddLastActionAsync(action); }

                if (SettingsManager.Default.CloseLaucnher)
                {
                    ServiceLocator.Current.GetService<IMainWindowController>().Close();
                }

                if (SettingsManager.Default.EnableMod_Statistics) { _gameSessionManager.StartGameSession(!string.IsNullOrEmpty(config.ServerIp) ? "server" : (loaderType == LoaderType.Vanilla ? "vanilla" : "mod")); }
                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);
                await process.WaitForExitAsync();
                int exitCode = process.ExitCode;

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
                }
            }
            catch (OperationCanceledException)
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledDesc", "Гаразд, я зупинила завантаження.\nМи можемо спробувати знову, коли ти будеш готовий!"),
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledTitle", "Скасовано"),
                    MascotEmotion.Normal);
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    string.Format(LocalizationManager.GetString("GameLaunch.LaunchCrashDesc", "Ой! Сталася помилка під час запуску гри.\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.LaunchCrashTitle", "Помилка запуску"),
                    MascotEmotion.Sad);
            }
            finally
            {
                _gameSessionManager.StopGameSession();
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

                    if (dowloadProgress.IsLoaded) dowloadProgress.CloseProgressWindow();
                });
            }
        }
        public async Task<string> InstallVersionAsync(LoaderType loaderType, string mcVersion, string loaderVersion, MinecraftLauncher launcher, CancellationToken token)
        {
            switch (loaderType)
            {
                case LoaderType.Forge:
                    var forge = new NoAdForgeInstaller(launcher);
                    return await forge.Install(mcVersion, loaderVersion, new CmlLib.Core.Installer.Forge.ForgeInstallOptions { CancellationToken = token });

                case LoaderType.Fabric:
                    var fabricInstaller = new FabricInstaller(WebHelper.Client);
                    return await fabricInstaller.Install($"{mcVersion}", $"{loaderVersion}", launcher.MinecraftPath);

                case LoaderType.Quilt:
                    var quiltInstaller = new QuiltInstaller(WebHelper.Client);
                    return await quiltInstaller.Install($"{mcVersion}", $"{loaderVersion}", launcher.MinecraftPath);

                case LoaderType.NeoForge:
                    var neoForge = new NoAdNeoForgeInstaller(launcher);
                    return await neoForge.Install(mcVersion, loaderVersion, new CmlLib.Core.Installer.NeoForge.Installers.NeoForgeInstallOptions { CancellationToken = token });

                case LoaderType.Optifine:
                    {
                        var loader = new OptifineInstaller(WebHelper.Client);
                        var versions = await loader.GetOptifineVersionsAsync();
                        var selectedVersion = versions.FirstOrDefault(x => x.Version == loaderVersion);

                        if (selectedVersion == null)
                            throw new Exception(LocalizationManager.GetString("GameLaunch.OptifineNotFound", "Обрана версія Optifine не знайдена."));

                        await launcher.InstallAsync(selectedVersion.MinecraftVersion, token);

                        var optifineVersionName = $"{selectedVersion.MinecraftVersion}-OptiFine_{selectedVersion.OptifineEdition}";
                        var optifineDir = Path.Combine(launcher.MinecraftPath.Versions, optifineVersionName);
                        var jarPath = Path.Combine(optifineDir, $"{optifineVersionName}.jar");

                        string finalVersionName = optifineVersionName;

                        if (!File.Exists(jarPath))
                        {
                            finalVersionName = await loader.InstallOptifineAsync(launcher.MinecraftPath.BasePath, selectedVersion);

                            if (!File.Exists(jarPath))
                            {
                                await Task.Delay(2000);
                                if (!File.Exists(jarPath))
                                {
                                    throw new Exception(LocalizationManager.GetString("GameLaunch.OptifineJarMissing", "Інсталятор Optifine завершився, але .jar файл не знайдено."));
                                }
                            }
                        }

                        return finalVersionName;
                    }
                case LoaderType.LiteLoader:
                    {
                        var liteLoaderInstaller = new LiteLoaderInstaller(WebHelper.Client);
                        var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                        var loaderToInstall = loaders.First(loader => loader.BaseVersion == mcVersion);

                        return await liteLoaderInstaller.Install(loaderToInstall, await launcher.GetVersionAsync(mcVersion), launcher.MinecraftPath);
                    }
                default:
                    await launcher.InstallAsync($"{mcVersion}", token);
                    return mcVersion;
            }
        }
        public MLaunchOption CreateLaunchOptions(LaunchConfiguration config, string versionName = "")
        {
            var baseOptions = new MLaunchOption
            {
                MinimumRamMb = config.MinimumRamMb,
                MaximumRamMb = config.MinimumRamMb,
                Session = config.Session,
                ScreenWidth = config.ScreenWidth,
                ScreenHeight = config.ScreenHeight,
                FullScreen = SettingsManager.Default.FullScreen,
                ServerIp = config.ServerIp,
                ServerPort = config.ServerPort,
            };

            if (config.AccountType == AccountType.LittleSkin)
            {
                var jvmArgs = new List<MArgument>
                {
                    new MArgument
                    {
                        Values = new[] { $@"-javaagent:{AppContext.BaseDirectory}authlib-injector-1.2.8.jar=https://littleskin.cn/api/yggdrasil" }
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

                    argsList.Add(new MArgument("-Dhttp.proxyHost=betacraft.uk"));
                    argsList.Add(new MArgument($"-Dhttp.proxyPort={proxyPort}"));
                    argsList.Add(new MArgument("-Djava.util.Arrays.useLegacyMergeSort=true"));

                    baseOptions.ExtraJvmArguments = argsList;
                }
            }

            return baseOptions;
        }

        public static string GetProxyPort(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            
            version = version.ToLower();

            if (version.StartsWith("rd-") || version.StartsWith("c0.0") || version.StartsWith("c0.2") || version.StartsWith("c0.30"))
                return "11701";

            if (version.StartsWith("in-") || version.StartsWith("inf-") || version.StartsWith("a1.0") || version.StartsWith("a1.1"))
                return "11702";

            if (version.StartsWith("a1.2") || version.StartsWith("b1.3") || version.StartsWith("b1.4") || version.StartsWith("b1.5") || version.StartsWith("b1.6") || version.StartsWith("b1.7") || version.StartsWith("b1.8"))
                return "11705";

            if (version.StartsWith("1.0") || version.StartsWith("1.1") || version.StartsWith("1.2") || version.StartsWith("1.3") || version.StartsWith("1.4") || version.StartsWith("1.5"))
                return "11707";

            return null; 
        }
    }
}