using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core;
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
using System.Windows;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Class
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
        Custom_Local,
        OmniArchive
    }

    public class GameLaunchService
    {
        private readonly CL_Main_ _main;
        private readonly GameSessionManager _gameSessionManager;
        private readonly LastActionService _lastActionService;
        private CancellationTokenSource _cts;

        public GameLaunchService(CL_Main_ main, GameSessionManager sessionManager, LastActionService lastActionService)
        {
            _main = main;
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
                case LoaderType.Custom_Local:
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
        public async Task LaunchGameAsync(LoaderType loaderType, string minecraftVersion, string loaderVersion, string serverIp = null, int? serverPort = null)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            bool isOffline = IsOfflineMode();

            _main.Dispatcher.Invoke(() =>
            {
                _main.InstallVersionOnPlay = true;
                _main.PlayTXT.Text = isOffline ? LocalizationManager.GetString("GameLaunch.LaunchOffline", "ОФЛАЙН ЗАПУСК...") : LocalizationManager.GetString("GameLaunch.LaunchDownloading", "ЗАВАНТАЖЕННЯ...");
            });

            DowloadProgress dowloadProgress = new DowloadProgress { CTS = _cts };
            if (!isOffline) _main.Dispatcher.Invoke(() => dowloadProgress.Show());

            try
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = 256;
                var path = new MinecraftPath(SettingsManager.Default.PathLacunher);
                var httpClient = new HttpClient();
                int safeThreads = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

                var parallelInstaller = new ParallelGameInstaller(
                    maxChecker: 32,
                    maxDownloader: safeThreads,
                    boundedCapacity: 2048,
                    httpClient
                );

                var parameters = MinecraftLauncherParameters.CreateDefault(path);
                parameters.GameInstaller = parallelInstaller;
                if (isOffline)
                {
                    parameters.VersionLoader = new CmlLib.Core.VersionLoader.LocalJsonVersionLoader(path);
                }

                var launcher = new MinecraftLauncher(parameters);
                launcher.FileProgressChanged += (sender, args) =>
                {
                    _main.Dispatcher.Invoke(() =>
                    {
                        int fileProgress = args.TotalTasks > 0 ? (int)((double)args.ProgressedTasks / args.TotalTasks * 100) : 0;
                        dowloadProgress.DowloadProgressBarFileTask(args.TotalTasks, args.ProgressedTasks, args.Name);
                        string versionLabel = string.IsNullOrEmpty(loaderVersion) ? minecraftVersion : $"{minecraftVersion} ({loaderVersion})";
                        dowloadProgress.DowloadProgressBarVersion(fileProgress, versionLabel);
                    });
                };

                launcher.ByteProgressChanged += (sender, args) =>
                {
                    _main.Dispatcher.Invoke(() =>
                    {
                        int byteProgress = args.TotalBytes > 0 ? (int)((double)args.ProgressedBytes / args.TotalBytes * 100) : 0;
                        dowloadProgress.DowloadProgressBarFile(byteProgress);
                    });
                };

                string versionName = "";

                if (isOffline)
                {
                    versionName = GetOfflineVersionName(loaderType, minecraftVersion, loaderVersion);

                    string versionDir = Path.Combine(path.Versions, versionName);
                    if (!Directory.Exists(versionDir))
                    {
                        MascotMessageBox.Show(
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
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("GameLaunch.LaunchInstallFailedDesc", "Ой леле! Я намагалася встановити цю версію, але нічого не вийшло.\nСпробуй ще раз пізніше."),
                            LocalizationManager.GetString("GameLaunch.LaunchInstallFailedTitle", "Помилка встановлення"),
                            MascotEmotion.Sad);
                        return;
                    }
                }

                var launchOption = CreateLaunchOptions(serverIp, serverPort);

                if (SettingsManager.Default.EnableAutoBackup)
                {
                    _main.Dispatcher.Invoke(() => _main.PlayTXT.Text = LocalizationManager.GetString("GameLaunch.LaunchBackupWorlds", "БЕКАП СВІТІВ..."));
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
                                _main.Dispatcher.Invoke(() => NotificationService.ShowNotification(
                                    LocalizationManager.GetString("GameLaunch.LaunchBackupErrorTitle", "Йой! Помилка при створенні бекапів!"),
                                    string.Format(LocalizationManager.GetString("GameLaunch.LaunchBackupErrorDesc", "Помилка авто-бекапу: {0}"), ex.Message),
                                    _main.SnackbarPresenter, 10));
                            }
                        });
                    }
                }

                _main.Dispatcher.Invoke(() => _main.PlayTXT.Text = LocalizationManager.GetString("GameLaunch.LaunchStarting", "ЗАПУСК..."));

                Process process;
                if (isOffline)
                {
                    process = await launcher.BuildProcessAsync(versionName, launchOption);
                }
                else
                {
                    process = await launcher.InstallAndBuildProcessAsync(versionName, launchOption, token);
                }

                _main.Dispatcher.Invoke(() =>
                {
                    if (dowloadProgress.IsLoaded) dowloadProgress.Close();
                    _main.WindowState = WindowState.Minimized;
                });

                await DiscordController.UpdatePresence(string.Format(LocalizationManager.GetString("DiscordRPC.PlayingVersion", "Грає версію {0}"), versionName));

                if (SettingsManager.Default.EnableLog)
                {
                    _main.ShowGameLog(process);
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
                await _lastActionService.AddLastActionAsync(action);

                if (SettingsManager.Default.CloseLaucnher)
                {
                    _main.Dispatcher.Invoke(() => _main.Close());
                }

                _gameSessionManager.StartGameSession(loaderType == LoaderType.Vanilla && serverIp == null ? "vanilla" : "mod");
                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);
                await process.WaitForExitAsync();
            }
            catch (OperationCanceledException)
            {
                MascotMessageBox.Show(
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledDesc", "Гаразд, я зупинила завантаження.\nМи можемо спробувати знову, коли ти будеш готовий!"),
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledTitle", "Скасовано"),
                    MascotEmotion.Normal);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("GameLaunch.LaunchCrashDesc", "Ой! Сталася помилка під час запуску гри.\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.LaunchCrashTitle", "Помилка запуску"),
                    MascotEmotion.Sad);
            }
            finally
            {
                _gameSessionManager.StopGameSession();
                _main.Dispatcher.Invoke(() =>
                {
                    _main.InstallVersionOnPlay = false;

                    string savedTypeStr = SettingsManager.Default.LastSelectedType.ToString();
                    if (SettingsManager.Default.LastSelectedType == 5 && !string.IsNullOrEmpty(SettingsManager.Default.LastSelectedModVersion))
                    {
                        _main.PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), SettingsManager.Default.LastSelectedModVersion);
                    }
                    else
                    {
                        _main.PlayTXT.Text = string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), SettingsManager.Default.LastSelectedVersion);
                    }

                    if (dowloadProgress.IsLoaded) dowloadProgress.Close();
                });
            }
        }
        #region OmniArchive LogicInstall
        #endregion
        public async Task<string> InstallVersionAsync(LoaderType loaderType, string mcVersion, string loaderVersion, MinecraftLauncher launcher, CancellationToken token)
        {
            switch (loaderType)
            {
                #region OmniArchive Loader
                #endregion
                case LoaderType.Forge:
                    var forge = new ForgeInstaller(launcher);
                    return await forge.Install(mcVersion, loaderVersion, new ForgeInstallOptions { CancellationToken = token });

                case LoaderType.Fabric:
                    var fabricInstaller = new FabricInstaller(new HttpClient());
                    return await fabricInstaller.Install($"{mcVersion}", $"{loaderVersion}", launcher.MinecraftPath);

                case LoaderType.Quilt:
                    var quiltInstaller = new QuiltInstaller(new HttpClient());
                    return await quiltInstaller.Install($"{mcVersion}", $"{loaderVersion}", launcher.MinecraftPath);

                case LoaderType.NeoForge:
                    var neoForge = new NeoForgeInstaller(launcher);
                    return await neoForge.Install($"{mcVersion}", $"{loaderVersion}", new NeoForgeInstallOptions { CancellationToken = token });

                case LoaderType.Optifine:
                    {
                        var loader = new OptifineInstaller(new HttpClient());
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
                        var liteLoaderInstaller = new LiteLoaderInstaller(new HttpClient());
                        var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                        var loaderToInstall = loaders.First(loader => loader.BaseVersion == mcVersion);

                        return await liteLoaderInstaller.Install(loaderToInstall, await launcher.GetVersionAsync(mcVersion), launcher.MinecraftPath);
                    }
                case LoaderType.Custom_Local:
                    {
                        return mcVersion;
                    }
                default:
                    await launcher.InstallAsync($"{mcVersion}", token);
                    return mcVersion;
            }
        }
        public MLaunchOption CreateLaunchOptions(string serverIp, int? serverPort)
        {
            var baseOptions = new MLaunchOption
            {
                MaximumRamMb = (int)_main.OPSlider.Value,
                Session = _main.session,
                ScreenWidth = int.Parse(_main.Width.Text),
                ScreenHeight = int.Parse(_main.Height.Text),
                FullScreen = SettingsManager.Default.FullScreen,
                ServerIp = serverIp,
                ServerPort = serverPort ?? 0
            };

            if (_main.selectAccountNow == AccountType.LittleSkin)
            {
                var jvmArgs = new List<MArgument>
                {
                    new MArgument
                    {
                        Values = new[] { $@"-javaagent:{AppContext.BaseDirectory}authlib-injector-1.2.7.jar=https://littleskin.cn/api/yggdrasil" }
                    }
                };
                baseOptions.JvmArgumentOverrides = jvmArgs;
            }

            return baseOptions;
        }
    }
}