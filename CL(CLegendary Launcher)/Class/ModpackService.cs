using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CurseForge.APIClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Class
{
    public class InstalledModpack
    {
        public string Name { get; set; }
        public string TypeSite { get; set; }
        public string MinecraftVersion { get; set; }
        public string LoaderVersion { get; set; }
        public string LoaderType { get; set; }
        public string Path { get; set; }
        public string PathJson { get; set; }
        public string UrlImage { get; set; }
        public bool IsConsoleLogOpened { get; set; } = false;
        public int OPack { get; set; } = 4096;
        public int Wdith { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool EnterInServer { get; set; } = false;
        public string ServerIP { get; set; } = "IP Сервера";
        public string JavaPath { get; set; } = string.Empty;
    }

    public static class ModpackPaths
    {
        public static string DataDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        public static string InstalledModpacksJson => Path.Combine(DataDirectory, "installed_modpacks.json");
    }
    public class ModpackService
    {
        private readonly CL_Main_ _main;
        private readonly GameSessionManager _gameSessionManager;
        private readonly GameLaunchService _gameLaunchService;
        private readonly ModDownloadService _modDownloadService;

        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(20);
        private static ApiClient _cfApiClientInstance;
        private readonly HttpClient _httpClient;

        public ModpackService(CL_Main_ main, GameSessionManager gameSessionManager, GameLaunchService gameLaunchService, ModDownloadService modDownloadService)
        {
            _main = main;
            _gameSessionManager = gameSessionManager;
            _gameLaunchService = gameLaunchService;
            _modDownloadService = modDownloadService;

            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            _httpClient.DefaultRequestHeaders.ConnectionClose = false;
        }
        private async Task<ApiClient> GetCfClientAsync()
        {
            if (_cfApiClientInstance != null) return _cfApiClientInstance;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("", "");
                var response = await client.GetAsync($"{Secrets.CurseForgeKey}");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    string key = data["key"]?.ToString();
                    _cfApiClientInstance = new ApiClient(key);
                    return _cfApiClientInstance;
                }
            }
            catch { }
            return null;
        }
        public List<InstalledModpack> LoadInstalledModpacks()
        {
            string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");
            if (!File.Exists(jsonPath)) return new List<InstalledModpack>();

            string json = File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<List<InstalledModpack>>(json) ?? new List<InstalledModpack>();
        }

        public void DeleteModpack(string modpackName)
        {
            string pathToJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");
            if (!File.Exists(pathToJson)) return;

            var jsonText = File.ReadAllText(pathToJson);
            var modpacks = System.Text.Json.JsonSerializer.Deserialize<List<InstalledModpack>>(jsonText);
            if (modpacks == null) return;

            var modpackToDelete = modpacks.Find(mp => mp.Name == modpackName);
            if (modpackToDelete != null)
            {
                modpacks.Remove(modpackToDelete);
                var newJson = System.Text.Json.JsonSerializer.Serialize(modpacks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(pathToJson, newJson);
            }
        }
        public void DeleteModpackFolder(InstalledModpack value)
        {
            try
            {
                string modpackFolder = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", value.Name);

                if (Directory.Exists(modpackFolder))
                {
                    Directory.Delete(modpackFolder, true);
                }

                if (!string.IsNullOrEmpty(value.Path) && Directory.Exists(value.Path))
                {
                    Directory.Delete(value.Path, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CL Launcher] Помилка видалення папки збірки: {ex.Message}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Modpacks.DeleteFolderError", "Не вдалося повністю видалити файли збірки. Можливо, якась програма (або сама гра) досі використовує ці файли.\nСпробуйте пізніше або видаліть папку вручну."),
                        LocalizationManager.GetString("Dialogs.Error", "Помилка доступу"),
                        MascotEmotion.Alert);
                });
            }
        }
        public async void PlayModPack(string version, string versionMod, string loader, string nameModPack, string pathModPack, string pathJson, string typeSite, string javaPath)
        {
            if (_main.InstallVersionOnPlay) return;

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            bool isOffline = SettingsManager.Default.OfflineModLauncher || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

            DowloadProgress versionDownloadWindow = new DowloadProgress() { CTS = cts };
            versionDownloadWindow.Title = isOffline
                ? LocalizationManager.GetString("Modpacks.OfflineLaunch", "ОФЛАЙН ЗАПУСК ЗБІРКИ...")
                : LocalizationManager.GetString("Modpacks.DownloadTitle", "DownloadProgress - Завантаження версії та модів");

            if (!isOffline) _main.Dispatcher.Invoke(() => versionDownloadWindow.Show());

            _main.InstallVersionOnPlay = true;

            try
            {
                string overridePath = Path.Combine(pathModPack, "override");
                string overridesPath = Path.Combine(pathModPack, "overrides");

                string finalModPath = overridePath;
                if (!Directory.Exists(overridePath) && Directory.Exists(overridesPath))
                {
                    finalModPath = overridesPath;
                }
                Directory.CreateDirectory(finalModPath);
                string savesPath = Path.Combine(finalModPath, "saves");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Шлях до сейвів: {savesPath}");

                if (SettingsManager.Default.EnableAutoBackup && SettingsManager.Default.EnableSubFiles_Backups)
                {
                    if (Directory.Exists(savesPath))
                    {
                        var worlds = Directory.GetDirectories(savesPath);

                        if (worlds.Length == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("[DEBUG] Папка saves є, але вона пуста.");
                        }
                        else
                        {
                            await Task.Run(async () =>
                            {
                                foreach (var world in worlds)
                                {
                                    string worldName = new DirectoryInfo(world).Name;
                                    try
                                    {
                                        await WorldBackupService.AutoBackupWorldAsync(world).ConfigureAwait(false);
                                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Бекап {worldName} готовий.");
                                    }
                                    catch (Exception ex)
                                    {
                                        _main.Dispatcher.Invoke(() =>
                                            MascotMessageBox.Show($"Не змогла зберегти {worldName}.\n{ex.Message}", "Помилка", MascotEmotion.Sad));
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Папка 'saves' відсутня. Бекапити нічого.");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Авто-бекап вимкнено в налаштуваннях.");
                }

                bool downloadSuccess = false;

                if (!isOffline)
                {
                    if (typeSite == "Modrinth")
                        downloadSuccess = await DownloadModsFromIndexJsonAsync(pathJson, finalModPath, versionDownloadWindow, token);
                    else if (typeSite == "CurseForge")
                        downloadSuccess = await DownloadModsFromManifestJsonAsync(pathJson, finalModPath, versionDownloadWindow, token);
                    else if (typeSite == "Custom")
                        downloadSuccess = await DownloadModsFromCustomJsonAsync(Path.Combine(pathModPack, "modpack.json"), finalModPath, versionDownloadWindow, token);
                }

                var installedModpack = LoadInstalledModpacks().FirstOrDefault(m => m.Name.Equals(nameModPack, StringComparison.OrdinalIgnoreCase));
                if (installedModpack == null) throw new Exception(LocalizationManager.GetString("Modpacks.MissingSettings", "Не вдалося знайти збережені налаштування збірки."));

                var path = new MinecraftPath(finalModPath);
                System.Net.ServicePointManager.DefaultConnectionLimit = 256;

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

                launcher.ByteProgressChanged += (sender, args) =>
                {
                    int byteProgress = args.TotalBytes > 0 ? (int)((double)args.ProgressedBytes / args.TotalBytes * 100) : 0;
                    if (!isOffline && versionDownloadWindow.IsLoaded) _main.Dispatcher.Invoke(() => versionDownloadWindow.DowloadProgressBarFile(byteProgress));
                };
                launcher.FileProgressChanged += (sender, args) =>
                {
                    int fileProgress = args.TotalTasks > 0 ? (int)((double)args.ProgressedTasks / args.TotalTasks * 100) : 0;
                    if (!isOffline && versionDownloadWindow.IsLoaded) _main.Dispatcher.Invoke(() =>
                    {
                        versionDownloadWindow.DowloadProgressBarFileTask(args.TotalTasks, args.ProgressedTasks, args.Name);
                        versionDownloadWindow.DowloadProgressBarVersion(fileProgress, version);
                    });
                };

                MLaunchOption mLaunch = new MLaunchOption
                {
                    MaximumRamMb = installedModpack.OPack,
                    Session = _main.session,
                    ScreenWidth = installedModpack.Wdith,
                    ScreenHeight = installedModpack.Height,
                    ServerIp = (installedModpack.EnterInServer && !string.IsNullOrWhiteSpace(installedModpack.ServerIP)) ? installedModpack.ServerIP.Split(':')[0] : null,
                    ServerPort = (installedModpack.EnterInServer && !string.IsNullOrWhiteSpace(installedModpack.ServerIP) && installedModpack.ServerIP.Contains(':') && int.TryParse(installedModpack.ServerIP.Split(':')[1], out int port)) ? port : 0,

                    JavaPath = !string.IsNullOrWhiteSpace(javaPath)
                                ? javaPath
                                : (!string.IsNullOrWhiteSpace(installedModpack.JavaPath) ? installedModpack.JavaPath : null)
                }; 
                var activeJvmArgs = new List<string>();

                if (_main.selectAccountNow == AccountType.LittleSkin)
                {
                    activeJvmArgs.Add($@"-javaagent:{AppContext.BaseDirectory}authlib-injector-1.2.7.jar=https://littleskin.cn/api/yggdrasil");
                }

                if (activeJvmArgs.Count > 0)
                {
                    mLaunch.JvmArgumentOverrides = activeJvmArgs
                        .Select(arg => new MArgument { Values = new[] { arg } })
                        .ToArray();
                }

                LoaderType loaderType;
                string lowerLoader = loader.ToLower();

                if (lowerLoader.Contains("vanilla") || lowerLoader.Contains("vanila"))
                {
                    loaderType = LoaderType.Vanilla;
                    versionMod = null;
                }
                else if (lowerLoader.Contains("quilt")) loaderType = LoaderType.Quilt;
                else if (lowerLoader.Contains("fabric")) loaderType = LoaderType.Fabric;
                else if (lowerLoader.Contains("neoforge")) loaderType = LoaderType.NeoForge;
                else if (lowerLoader.Contains("forge")) loaderType = LoaderType.Forge;
                else if (lowerLoader.Contains("optifine")) loaderType = LoaderType.Optifine;
                else if (lowerLoader.Contains("liteloader")) loaderType = LoaderType.LiteLoader;
                else
                {
                    if (Enum.TryParse(typeof(LoaderType), loader, true, out object result))
                        loaderType = (LoaderType)result;
                    else
                        loaderType = LoaderType.Vanilla;
                }

                string versionName;
                if (isOffline)
                {
                    if (version.ToLower().Contains(loader.ToLower()) || version.ToLower().Contains("optifine"))
                        versionName = version;
                    else
                    {
                        switch (loaderType)
                        {
                            case LoaderType.Fabric: versionName = $"fabric-loader-{versionMod}-{version}"; break;
                            case LoaderType.Forge: versionName = $"{version}-forge-{versionMod}"; break;
                            case LoaderType.NeoForge: versionName = $"neoforge-{versionMod}"; break;
                            case LoaderType.Quilt: versionName = $"quilt-loader-{versionMod}-{version}"; break;
                            case LoaderType.Optifine: versionName = $"{version}-OptiFine_{versionMod?.Replace("OptiFine_", "")}"; break;
                            case LoaderType.LiteLoader: versionName = $"{version}-LiteLoader{version}"; break;
                            default: versionName = version; break;
                        }
                    }
                }
                else
                {
                    versionName = await _gameLaunchService.InstallVersionAsync(loaderType, version, versionMod, launcher, token);
                }

                Process process;
                if (isOffline)
                {
                    process = await launcher.BuildProcessAsync(versionName, mLaunch);
                }
                else
                {
                    process = await launcher.InstallAndBuildProcessAsync(versionName, mLaunch, token);
                }

                if (SettingsManager.Default.EnableMod_Statistics) { _gameSessionManager.StartGameSession("mod"); }

                _main.Dispatcher.Invoke(() =>
                {
                    if (versionDownloadWindow.IsLoaded) versionDownloadWindow.Close();
                    _main.WindowState = WindowState.Minimized;
                });

                await DiscordController.UpdatePresence(string.Format(LocalizationManager.GetString("DiscordRPC.PlayingModpack", "Грає в мод-збірку {0}"), nameModPack));

                if (installedModpack.IsConsoleLogOpened)
                    _main.ShowGameLog(process);
                else
                    process.Start();

                if (SettingsManager.Default.CloseLaucnher)
                {
                    _main.Dispatcher.Invoke(() => _main.Close());
                }
                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);
                await process.WaitForExitAsync();
                int exitCode = process.ExitCode;

                if (exitCode != 0)
                {
                    _main.Dispatcher.Invoke(() =>
                    {
                        _main.Show();
                        _main.WindowState = WindowState.Normal;

                        if (!installedModpack.IsConsoleLogOpened)
                        {
                            string logFilePath = Path.Combine(finalModPath, "logs", "latest.log");
                            _main.ShowGameLogFromFile(logFilePath);
                        }

                        MascotMessageBox.Show(
                            string.Format(LocalizationManager.GetString("GameLaunch.CrashDesc", "Йой! Майнкрафт впав (Код помилки: {0}).\nЯ відкрила логи, щоб ми могли знайти конфліктний мод або помилку."), exitCode),
                            LocalizationManager.GetString("GameLaunch.CrashTitle", "Краш гри!"),
                            MascotEmotion.Sad);
                    });
                }
                else
                {
                    if (SettingsManager.Default.CloseLaucnher)
                    {
                        _main.Dispatcher.Invoke(() => Application.Current.Shutdown());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _main.Dispatcher.Invoke(() => { if (versionDownloadWindow.IsLoaded) versionDownloadWindow.Close(); });
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Modpacks.LaunchCancelledDesc", "Добре, я зупинила завантаження модпаку.\nСпробуємо іншим разом!"),
                    LocalizationManager.GetString("GameLaunch.LaunchCancelledTitle", "Скасовано"),
                    MascotEmotion.Normal);
            }
            catch (Exception ex)
            {
                _main.Dispatcher.Invoke(() => { if (versionDownloadWindow.IsLoaded) versionDownloadWindow.Close(); });
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Modpacks.LaunchCrashDesc", "Біда! Щось зламалося під час запуску модпаку.\n\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Modpacks.ConfigCorruptedTitle", "Помилка"),
                    MascotEmotion.Sad);
            }
            finally
            {
                _gameSessionManager.StopGameSession();
                _main.Dispatcher.Invoke(() =>
                {
                    _main.InstallVersionOnPlay = false;
                    _main.PlayTXT.Text = LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ");
                });
            }
        }
        private async Task<bool> DownloadModsFromManifestJsonAsync(string pathJson, string packFolder, DowloadProgress progress, CancellationToken token)
        {
            if (!File.Exists(pathJson) || SettingsManager.Default.OfflineModLauncher) return false;

            try
            {
                string json = await File.ReadAllTextAsync(pathJson);
                var manifest = JsonConvert.DeserializeObject<JObject>(json);
                var files = manifest["files"] as JArray;
                if (files == null || files.Count == 0) return false;

                int total = files.Count;
                int completed = 0;
                var downloadTasks = new List<Task>();

                foreach (var modEntry in files)
                {
                    downloadTasks.Add(Task.Run(async () =>
                    {
                        await _downloadSemaphore.WaitAsync(token);
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            int projectId = modEntry.Value<int>("projectID");
                            int fileId = modEntry.Value<int>("fileID");

                            var cfApi = await GetCfClientAsync();
                            if (cfApi == null) return;
                            var file = await cfApi.GetModFileAsync(projectId, fileId);
                            var data = file?.Data;
                            if (data == null) return;

                            string downloadUrl = data.DownloadUrl;
                            string fileName = data.FileName;

                            if (!string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(fileName))
                            {
                                string subFolder = GetFolderByFileType(fileName);
                                string targetDir = Path.Combine(packFolder, subFolder);
                                Directory.CreateDirectory(targetDir);
                                string fullPath = Path.Combine(targetDir, fileName);

                                if (!File.Exists(fullPath))
                                {
                                    _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(total, completed, fileName));
                                    bool success = await DownloadFileWithProgress(downloadUrl, fullPath, progress, token);
                                    if (!success) await HandleManualDownloadPrompt(downloadUrl, fullPath, fileName);
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            _downloadSemaphore.Release();
                            Interlocked.Increment(ref completed);
                            _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(total, completed, ""));
                        }
                    }, token));
                }
                await Task.WhenAll(downloadTasks);
                return true;
            }
            catch { return false; }
        }

        private async Task<bool> DownloadModsFromIndexJsonAsync(string pathJson, string packFolder, DowloadProgress progress, CancellationToken token)
        {
            if (!File.Exists(pathJson) || SettingsManager.Default.OfflineModLauncher) return false;
            try
            {
                string json = await File.ReadAllTextAsync(pathJson);
                JObject index = JObject.Parse(json);
                var files = index["files"] as JArray;
                if (files == null || files.Count == 0) return false;

                int total = files.Count;
                int completed = 0;
                var downloadTasks = new List<Task>();

                foreach (var file in files)
                {
                    downloadTasks.Add(Task.Run(async () =>
                    {
                        string fileName = "";

                        await _downloadSemaphore.WaitAsync(token);
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            string relativePath = file["path"]?.ToString();
                            var urls = file["downloads"] as JArray;
                            string downloadUrl = urls?[0]?.ToString();

                            if (!string.IsNullOrWhiteSpace(relativePath) && !string.IsNullOrWhiteSpace(downloadUrl))
                            {
                                fileName = Path.GetFileName(relativePath);
                                string subFolder = GetFolderByFileType(fileName);
                                string targetDir = Path.Combine(packFolder, subFolder);
                                Directory.CreateDirectory(targetDir);
                                string fullPath = Path.Combine(targetDir, fileName);

                                if (!File.Exists(fullPath))
                                {
                                    _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(total, completed, fileName));
                                    bool success = await DownloadFileWithProgress(downloadUrl, fullPath, progress, token);
                                    if (!success) await HandleManualDownloadPrompt(downloadUrl, fullPath, fileName);
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            _downloadSemaphore.Release();
                            Interlocked.Increment(ref completed);
                            _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(total, completed, fileName));
                        }
                    }, token));
                }
                await Task.WhenAll(downloadTasks);
                return true;
            }
            catch { return false; }
        }
        private async Task<bool> DownloadModsFromCustomJsonAsync(string jsonPath, string packFolder, DowloadProgress progress, CancellationToken token)
        {
            if (SettingsManager.Default.OfflineModLauncher || !File.Exists(jsonPath)) return false;
            try
            {
                string json = await File.ReadAllTextAsync(jsonPath);
                List<ModInfo> mods = new List<ModInfo>();

                try
                {
                    var manifest = JsonConvert.DeserializeObject<CustomModpackManifest>(json);
                    if (manifest != null && manifest.Files != null && manifest.Files.Count > 0)
                    {
                        mods = manifest.Files;
                    }
                }
                catch { }

                if (mods.Count == 0)
                {
                    try
                    {
                        mods = JsonConvert.DeserializeObject<List<ModInfo>>(json) ?? new List<ModInfo>();
                    }
                    catch { return false; }
                }

                if (mods.Count == 0) return false;

                var processedIds = new HashSet<string>();
                var downloadTasks = new List<Task>();
                var queue = new Queue<ModInfo>(mods);

                int totalTasks = mods.Count;
                int completed = 0;

                while (queue.Count > 0)
                {
                    var currentMod = queue.Dequeue();

                    string uniqueKey = !string.IsNullOrEmpty(currentMod.FileId) ? currentMod.FileId : currentMod.Url;
                    if (processedIds.Contains(uniqueKey)) continue;
                    processedIds.Add(uniqueKey);

                    downloadTasks.Add(Task.Run(async () =>
                    {
                        await _downloadSemaphore.WaitAsync(token);
                        try
                        {
                            token.ThrowIfCancellationRequested();

                            string subFolder = currentMod.Type switch
                            {
                                "mod" => "mods",
                                "shader" => "shaderpacks",
                                "resourcepack" => "resourcepacks",
                                _ => "mods"
                            };

                            string targetDir = Path.Combine(packFolder, subFolder);
                            Directory.CreateDirectory(targetDir);

                            string fileName = !string.IsNullOrEmpty(currentMod.FileName)
                                ? currentMod.FileName
                                : Path.GetFileName(currentMod.Url);

                            if (!fileName.EndsWith(".jar") && !fileName.EndsWith(".zip"))
                                fileName = $"{currentMod.Name.Replace(" ", "_")}.jar";

                            string filePath = Path.Combine(targetDir, fileName);

                            if (!File.Exists(filePath))
                            {
                                _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(totalTasks, completed, fileName));
                                bool success = await DownloadFileWithProgress(currentMod.Url, filePath, progress, token);
                                if (!success) await HandleManualDownloadPrompt(currentMod.Url, filePath, fileName);
                            }

                            if (SettingsManager.Default.ModDep && currentMod.Type == "mod" && !string.IsNullOrEmpty(currentMod.FileId))
                            {
                                try
                                {
                                    var versionInfo = MapModInfoToVersionInfo(currentMod);
                                    var dependencies = await _modDownloadService.GetDependenciesModInfoAsync(versionInfo, currentMod.Loader, 0);

                                    foreach (var dep in dependencies)
                                    {
                                        string depKey = !string.IsNullOrEmpty(dep.FileId) ? dep.FileId : dep.Url;
                                        if (!processedIds.Contains(depKey))
                                        {
                                            await DownloadDependencyRecursive(dep, packFolder, progress, token, processedIds);
                                        }
                                    }
                                }
                                catch (Exception ex) { Debug.WriteLine($"Dependency Error for {currentMod.Name}: {ex.Message}"); }
                            }
                        }
                        catch { }
                        finally
                        {
                            _downloadSemaphore.Release();
                            Interlocked.Increment(ref completed);
                            _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(totalTasks, completed, ""));
                        }
                    }, token));
                }

                await Task.WhenAll(downloadTasks);
                return true;
            }
            catch { return false; }
        }
        private async Task DownloadDependencyRecursive(ModInfo mod, string packFolder, DowloadProgress progress, CancellationToken token, HashSet<string> processedIds)
        {
            string uniqueKey = !string.IsNullOrEmpty(mod.FileId) ? mod.FileId : mod.Url;

            lock (processedIds)
            {
                if (processedIds.Contains(uniqueKey)) return;
                processedIds.Add(uniqueKey);
            }

            string targetDir = Path.Combine(packFolder, "mods");
            Directory.CreateDirectory(targetDir);
            string fileName = Path.GetFileName(mod.Url);
            string filePath = Path.Combine(targetDir, fileName);

            if (!File.Exists(filePath))
            {
                _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFileTask(0, 0, $"Dep: {fileName}"));
                await DownloadFileWithProgress(mod.Url, filePath, progress, token);
            }

            if (!string.IsNullOrEmpty(mod.FileId))
            {
                var versionInfo = MapModInfoToVersionInfo(mod);
                var subDeps = await _modDownloadService.GetDependenciesModInfoAsync(versionInfo, mod.Loader, 0);
                foreach (var subDep in subDeps)
                {
                    await DownloadDependencyRecursive(subDep, packFolder, progress, token, processedIds);
                }
            }
        }
        private ModVersionInfo MapModInfoToVersionInfo(ModInfo mod)
        {
            string site = "Modrinth";
            if (mod.Url.Contains("curseforge") || mod.Url.Contains("mediafile")) site = "CurseForge";

            return new ModVersionInfo
            {
                ModId = mod.ProjectId,
                VersionId = mod.FileId,
                DownloadUrl = mod.Url,
                VersionName = mod.Version,
                Site = site,
                GameVersions = new List<string> { mod.Version },
                Loaders = new List<string> { mod.Loader }
            };
        }
        private async Task<bool> DownloadFileWithProgress(string url, string savePath, DowloadProgress progress, CancellationToken token)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                bool canReport = totalBytes > 0;
                long totalRead = 0;

                string tempPath = savePath + ".tmp";

                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                using var contentStream = await response.Content.ReadAsStreamAsync(token);

                byte[] buffer = new byte[81920];
                int bytesRead;
                long lastReportedBytes = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                    totalRead += bytesRead;

                    if (canReport)
                    {
                        if (totalRead - lastReportedBytes > 102400 || totalRead == totalBytes)
                        {
                            lastReportedBytes = totalRead;
                            int percent = (int)(totalRead * 100 / totalBytes);
                            _main.Dispatcher.BeginInvoke(() => progress.DowloadProgressBarFile(percent));
                        }
                    }
                }

                await fileStream.DisposeAsync();
                File.Move(tempPath, savePath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetFolderByFileType(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();

            if (ext == ".jar")
                return "mods";

            if (ext == ".zip")
            {
                string name = fileName.ToLower();
                if (name.Contains("shader") || name.Contains("bsl") || name.Contains("seus") || name.Contains("sildur"))
                    return "shaderpacks";
                if (name.Contains("resource") || name.Contains("pack") || name.Contains("texture"))
                    return "resourcepacks";
            }

            return "mods";
        }

        private async Task HandleManualDownloadPrompt(string url, string fullPath, string filename, string errorMessage = "")
        {
            bool result = MascotMessageBox.Ask(
                            string.Format(LocalizationManager.GetString("DownloadManager.ManualDownloadPromptDesc", "Ой, я не змогла завантажити цей файл:\n{0}\n\nСпробуєш скачати його вручну?"), filename),
                            LocalizationManager.GetString("DownloadManager.ManualDownloadPromptTitle", "Помилка завантаження"),
                            MascotEmotion.Sad);

            if (result == true)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });

                    MascotMessageBox.Show(
                                    string.Format(LocalizationManager.GetString("DownloadManager.ManualDownloadInstructionDesc", "Я відкрила посилання. Будь ласка, збережи файл ось сюди:\n{0}"), fullPath),
                                    LocalizationManager.GetString("DownloadManager.ManualDownloadInstructionTitle", "Інструкція"),
                                    MascotEmotion.Alert);
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                                    string.Format(LocalizationManager.GetString("DownloadManager.ManualDownloadBrowserErrorDesc", "Не вдалося відкрити посилання у браузері.\n{0}"), ex.Message),
                                    LocalizationManager.GetString("DownloadManager.ManualDownloadBrowserErrorTitle", "Збій"),
                                    MascotEmotion.Sad);
                }
            }
        }

        public void AddModpack(InstalledModpack modpack)
        {
            string jsonPath = ModpackPaths.InstalledModpacksJson;

            List<InstalledModpack> modpacks = new();

            if (File.Exists(jsonPath))
            {
                try
                {
                    string existingJson = File.ReadAllText(jsonPath);
                    modpacks = JsonConvert.DeserializeObject<List<InstalledModpack>>(existingJson) ?? new();
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Modpacks.ConfigCorruptedDesc", "Ой! Файл конфігурації збірок пошкоджено.\n{0}"), ex.Message),
                        LocalizationManager.GetString("Modpacks.ConfigCorruptedTitle", "Помилка"),
                        MascotEmotion.Sad);
                }
            }

            if (!modpacks.Any(m => m.Name.Equals(modpack.Name, StringComparison.OrdinalIgnoreCase)))
            {
                modpacks.Add(modpack);

                string newJson = JsonConvert.SerializeObject(modpacks, Formatting.Indented);
                File.WriteAllText(jsonPath, newJson);
            }
        }
        private string FindIconInFolder(string rootFolder)
        {
            string[] commonNames = {
        "icon.png", "icon.jpg", "icon.jpeg",
        "instance.png", "instance.jpg",
        "logo.png", "pack.png", "manifest.png"
            };

            string[] foldersToCheck = {
        rootFolder,
        Path.Combine(rootFolder, "overrides"),
        Path.Combine(rootFolder, "override")
            };

            foreach (var folder in foldersToCheck)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var name in commonNames)
                {
                    string fullPath = Path.Combine(folder, name);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            foreach (var folder in foldersToCheck)
            {
                if (!Directory.Exists(folder)) continue;

                try
                {
                    var imageFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                                              .Where(s => s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                          s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                    var firstImage = imageFiles.FirstOrDefault();
                    if (firstImage != null)
                    {
                        return firstImage;
                    }
                }
                catch { }
            }

            return null;
        }
        public async Task<InstalledModpack> ImportModpackFromFileAsync(string zipFilePath)
        {
            string packName = Path.GetFileNameWithoutExtension(zipFilePath);
            string extractPath = Path.Combine(SettingsManager.Default.PathLacunher, "CLModpack", packName);

            if (Directory.Exists(extractPath))
            {
                throw new Exception(string.Format(LocalizationManager.GetString("Modpacks.ImportExists", "Збірка з назвою '{0}' вже існує! Видаліть її або перейменуйте архів."), packName));
            }
            Directory.CreateDirectory(extractPath);

            await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, extractPath));

            var rootDir = new DirectoryInfo(extractPath);
            var subDirs = rootDir.GetDirectories();
            var files = rootDir.GetFiles();

            if (files.Length == 0 && subDirs.Length == 1)
            {
                var nestedDir = subDirs[0];

                foreach (var file in nestedDir.GetFiles())
                {
                    string destFile = Path.Combine(extractPath, file.Name);
                    file.MoveTo(destFile);
                }

                foreach (var dir in nestedDir.GetDirectories())
                {
                    string destDir = Path.Combine(extractPath, dir.Name);
                    if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                    dir.MoveTo(destDir);
                }

                nestedDir.Delete();
            }
            string modrinthPath = Path.Combine(extractPath, "modrinth.index.json");
            string cursePath = Path.Combine(extractPath, "manifest.json");
            string customPath = Path.Combine(extractPath, "modpack.json");

            string foundIconPath = null;
            InstalledModpack newPack = null;

            if (File.Exists(modrinthPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(modrinthPath);
                    JObject index = JObject.Parse(json);
                    string iconFileName = index["icon"]?.ToString();

                    if (!string.IsNullOrEmpty(iconFileName))
                    {
                        string absoluteIconPath = Path.Combine(extractPath, iconFileName);
                        if (File.Exists(absoluteIconPath)) foundIconPath = absoluteIconPath;
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(foundIconPath))
            {
                foundIconPath = FindIconInFolder(extractPath);
            }

            string finalIconUrl = foundIconPath ?? "pack://application:,,,/Icon/IconCL(Common).png";

            if (File.Exists(modrinthPath))
            {
                string json = await File.ReadAllTextAsync(modrinthPath);
                JObject index = JObject.Parse(json);

                string version = index["dependencies"]?["minecraft"]?.ToString();
                var deps = index["dependencies"] as JObject;
                string loaderKey = deps?.Properties()
                    .FirstOrDefault(p => p.Name.Contains("fabric") || p.Name.Contains("forge") || p.Name.Contains("quilt") || p.Name.Contains("neoforge"))?.Name;
                string loaderVer = index["dependencies"]?[loaderKey]?.ToString();

                newPack = new InstalledModpack
                {
                    Name = packName,
                    TypeSite = "Modrinth",
                    MinecraftVersion = version ?? "Unknown",
                    LoaderType = loaderKey ?? "Unknown",
                    LoaderVersion = loaderVer ?? "Unknown",
                    Path = extractPath,
                    PathJson = modrinthPath,
                    UrlImage = finalIconUrl
                };
            }
            else if (File.Exists(cursePath))
            {
                string json = await File.ReadAllTextAsync(cursePath);
                JObject manifest = JObject.Parse(json);

                string version = manifest["minecraft"]?["version"]?.ToString();
                string loaderFull = manifest["minecraft"]?["modLoaders"]?[0]?["id"]?.ToString();
                string loader = loaderFull?.Split('-')[0];
                string loaderVer = loaderFull?.Contains("-") == true ? loaderFull.Substring(loaderFull.IndexOf('-') + 1) : loaderFull;

                newPack = new InstalledModpack
                {
                    Name = manifest["name"]?.ToString() ?? packName,
                    TypeSite = "CurseForge",
                    MinecraftVersion = version ?? "Unknown",
                    LoaderType = loader ?? "Unknown",
                    LoaderVersion = loaderVer ?? "Unknown",
                    Path = extractPath,
                    PathJson = cursePath,
                    UrlImage = finalIconUrl
                };
            }
            else if (File.Exists(customPath))
            {
                string json = await File.ReadAllTextAsync(customPath);
                CustomModpackManifest manifest = null;
                try
                {
                    manifest = JsonConvert.DeserializeObject<CustomModpackManifest>(json);
                }
                catch { }

                if (manifest != null && !string.IsNullOrEmpty(manifest.Minecraft))
                {
                    newPack = new InstalledModpack
                    {
                        Name = packName,
                        TypeSite = "Custom",
                        MinecraftVersion = manifest.Minecraft,
                        LoaderType = manifest.Loader ?? "Vanilla",
                        LoaderVersion = manifest.LoaderVersion ?? "Unknown",
                        Path = extractPath,
                        PathJson = customPath,
                        UrlImage = finalIconUrl
                    };
                }
                else
                {
                    var modList = JsonConvert.DeserializeObject<List<ModInfo>>(json);
                    string ver = (modList != null && modList.Count > 0) ? modList[0].Version : "Unknown";
                    string lType = (modList != null && modList.Count > 0 && modList[0].Type == "metadata") ? modList[0].LoaderType : "Unknown";

                    newPack = new InstalledModpack
                    {
                        Name = packName,
                        TypeSite = "Custom",
                        MinecraftVersion = ver,
                        LoaderType = lType,
                        LoaderVersion = "Unknown",
                        Path = extractPath,
                        PathJson = customPath,
                        UrlImage = finalIconUrl
                    };
                }
            }
            else
            {
                newPack = new InstalledModpack
                {
                    Name = packName,
                    TypeSite = "Manual",
                    MinecraftVersion = "Unknown",
                    LoaderType = "Vanilla",
                    LoaderVersion = "",
                    Path = extractPath,
                    PathJson = "",
                    UrlImage = finalIconUrl
                };
            }

            AddModpack(newPack);
            return newPack;
        }
    }
}