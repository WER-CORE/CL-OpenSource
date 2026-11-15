using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class ModpacksPageViewModel : ObservableObject, ITrimmablePage
{
    public void TrimMemory()
    {
    }

    public void Unload()
    {
        foreach (var card in ModpackCards)
        {
            card.Dispose();
        }
        ModpackCards.Clear();
        _allInstalledModpacks.Clear();
    }
    private readonly ModpackService? _modpackService;
    private readonly GameSessionManager? _gameSessionManager;
    private readonly AccountService? _accountService;

    private List<InstalledModpack> _allInstalledModpacks = new();

    [ObservableProperty] private ObservableCollection<InstalledModpack> _modpacks = new();
    [ObservableProperty] private ObservableCollection<ModpackCardViewModel> _modpackCards = new();

    [ObservableProperty] private string _modpackSearchText = string.Empty;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string _exportProgressText = string.Empty;
    [ObservableProperty] private double _exportProgressValue;

    public static event Action<bool, string, double>? ExportProgressChanged;
    private static CancellationTokenSource? _exportCts;

    public static void CancelActiveExport()
    {
        try
        {
            _exportCts?.Cancel();
        }
        catch
        {
        }
    }

    public ModpacksPageViewModel() : this(
        ResolveService<ModpackService>(),
        ResolveService<GameSessionManager>(),
        ResolveService<AccountService>())
    {
    }

    public ModpacksPageViewModel(
        ModpackService? modpackService,
        GameSessionManager? gameSessionManager,
        AccountService? accountService)
    {
        _modpackService = modpackService;
        _gameSessionManager = gameSessionManager;
        _accountService = accountService;

        _ = RefreshModpacksListAsync();
    }

    private static T? ResolveService<T>() where T : class
    {
        try
        {
            var app = Application.Current as App;
            if (app?.ServiceProvider?.GetService(typeof(T)) is T service)
                return service;

            return ServiceLocator.Current?.GetService<T>();
        }
        catch
        {
            return null;
        }
    }

    partial void OnModpackSearchTextChanged(string value)
    {
        _ = FilterModpacksAsync();
    }

    public async Task RefreshModpacksListAsync()
    {
        if (_modpackService == null) return;

        var packs = await Task.Run(() =>
        {
            var loaded = _modpackService.LoadInstalledModpacks();
            return loaded.Where(x => !string.IsNullOrEmpty(x.Path) && Directory.Exists(x.Path)).ToList();
        }).ConfigureAwait(false);

        _allInstalledModpacks = packs;
        await FilterModpacksAsync();
    }

    private async Task FilterModpacksAsync()
    {
        string query = ModpackSearchText?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? _allInstalledModpacks.ToList()
            : _allInstalledModpacks
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) &&
                            m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var newModpacks = new ObservableCollection<InstalledModpack>(filtered);
            var newCards = new ObservableCollection<ModpackCardViewModel>();

            foreach (var p in filtered)
            {
                var cardVm = new ModpackCardViewModel(
                    p,
                    pack => _ = PlayModpackAsync(pack),
                    pack => _ = DeleteModpackAsync(pack),
                    pack => OpenModpackFolder(pack),
                    pack => _ = EditModpackAsync(pack),
                    pack => _ = ExportModpackAsync(pack));

                newCards.Add(cardVm);
            }

            Modpacks = newModpacks;
            ModpackCards = newCards;
        });
    }

    [RelayCommand]
    private async Task PlayModpackAsync(InstalledModpack? pack)
    {
        if (pack == null || _modpackService == null) return;

        var settings = SettingsManager.Default;

        if (_accountService != null && (_accountService.CurrentSession == null || _accountService.CurrentProfile == null))
        {
            var profiles = _accountService.GetProfiles();
            if (profiles is { Count: > 0 })
            {
                int savedIndex = settings.SelectIndexAccount;
                var prof = (savedIndex >= 0 && savedIndex < profiles.Count) ? profiles[savedIndex] : profiles[0];
                await _accountService.SelectProfileAsync(prof);
            }
        }

        if (_accountService?.CurrentSession == null || _accountService.CurrentProfile == null)
        {
            var dialogService = ResolveService<IDialogService>();
            if (dialogService != null)
            {
                await dialogService.ShowMessageAsync(
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Агов! Будь ласка, оберіть акаунт перед початком гри!"),
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Акаунт не обраний"),
                    MascotEmotion.Sad);
            }
            return;
        }

        int ram = settings.OP > 0 ? settings.OP : 2048;
        var config = new LaunchConfiguration
        {
            MinimumRamMb = ram,
            MaximumRamMb = ram,
            ScreenWidth = settings.width > 0 ? settings.width : 854,
            ScreenHeight = settings.height > 0 ? settings.height : 480,
            IsFullscreen = settings.FullScreen,
            IsOffline = settings.OfflineModLauncher,
            Session = _accountService.CurrentSession,
            AccountType = _accountService.CurrentProfile.TypeAccount
        };

        _modpackService.PlayModPack(
            pack.MinecraftVersion,
            pack.LoaderVersion,
            pack.LoaderType,
            pack.Name,
            pack.Path,
            pack.PathJson,
            pack.TypeSite,
            pack.JavaPath,
            config
        );
    }

    [RelayCommand]
    private async Task DeleteModpackAsync(InstalledModpack? pack)
    {
        if (pack == null || _modpackService == null) return;

        var dialogService = ResolveService<IDialogService>();
        if (dialogService != null)
        {
            bool confirm = await dialogService.AskQuestionAsync(
                string.Format(LocalizationManager.GetString("Modpacks.DeleteConfirmMsg", "Ви дійсно бажаєте видалити збірку {0}?"), pack.Name),
                LocalizationManager.GetString("Dialogs.Confirm", "Підтвердження"),
                MascotEmotion.Confused);

            if (!confirm) return;
        }

        _allInstalledModpacks.RemoveAll(x =>
            string.Equals(x.Name, pack.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Path, pack.Path, StringComparison.OrdinalIgnoreCase));

        await FilterModpacksAsync();

        await Task.Run(() =>
        {
            try
            {
                _modpackService.DeleteModpack(pack.Name);
                _modpackService.DeleteModpackFolder(pack);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting modpack files: {ex.Message}");
            }
        });

        await RefreshModpacksListAsync();
    }

    [RelayCommand]
    private void OpenModpackFolder(InstalledModpack? pack)
    {
        if (pack != null && Directory.Exists(pack.Path))
        {
            UrlOpener.Open(pack.Path);
        }
    }

    [RelayCommand]
    private async Task ExportModpackAsync(InstalledModpack? pack)
    {
        if (pack == null) return;

        var dialogService = ResolveService<IDialogService>();
        if (dialogService == null) return;

        string? savePath = await dialogService.SaveFileDialogAsync(
            $"Export {pack.Name}",
            $"{pack.Name}_{DateTime.Now:yyyy-MM-dd}.zip",
            ".zip");

        if (!string.IsNullOrEmpty(savePath))
        {
            bool exported = await ExecuteExportToPathAsync(pack, savePath);
            if (exported)
            {
                await dialogService.ShowMessageAsync(
                    LocalizationManager.GetString("Modpacks.ExportSuccess", "Збірку успішно експортовано!"),
                    LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                    MascotEmotion.Normal);
            }
        }
    }

    public async Task<bool> ExecuteExportToPathAsync(InstalledModpack pack, string saveFilePath)
    {
        if (!Directory.Exists(pack.Path)) return false;

        _exportCts?.Cancel();
        _exportCts?.Dispose();
        _exportCts = new CancellationTokenSource();
        var token = _exportCts.Token;

        IsExporting = true;
        string prepText = LocalizationManager.GetString("Modpacks.ExportPreparing", "Підготовка файлів...");
        ExportProgressText = prepText;
        ExportProgressValue = 0;
        Dispatcher.UIThread.Post(() => ExportProgressChanged?.Invoke(true, prepText, 0));

        bool success = false;
        try
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(saveFilePath)) File.Delete(saveFilePath);

                var manifest = new CustomModpackManifest
                {
                    Name = pack.Name,
                    Author = Environment.UserName,
                    Version = "1.0.0",
                    Minecraft = pack.MinecraftVersion,
                    Loader = pack.LoaderType,
                    LoaderVersion = pack.LoaderVersion,
                    HasCustomIcon = File.Exists(Path.Combine(pack.Path, "icon.png")),
                    Files = new List<ModInfo>()
                };

                string existingJsonPath = Path.Combine(pack.Path, "modpack.json");
                if (File.Exists(existingJsonPath))
                {
                    try
                    {
                        string oldJson = File.ReadAllText(existingJsonPath);
                        var tokenJson = JToken.Parse(oldJson);

                        if (tokenJson is JArray array)
                        {
                            var oldList = array.ToObject<List<ModInfo>>();
                            if (oldList != null) manifest.Files.AddRange(oldList);
                        }
                        else if (tokenJson is JObject obj && obj["Files"] is JArray filesArray)
                        {
                            var oldList = filesArray.ToObject<List<ModInfo>>();
                            if (oldList != null) manifest.Files.AddRange(oldList);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Manifest parse warning: {ex.Message}");
                    }
                }

                string modsFolder = Path.Combine(pack.Path, "override", "mods");
                if (!Directory.Exists(modsFolder))
                    modsFolder = Path.Combine(pack.Path, "overrides", "mods");

                if (Directory.Exists(modsFolder))
                {
                    var physicalMods = new DirectoryInfo(modsFolder).GetFiles("*.jar", SearchOption.TopDirectoryOnly);
                    foreach (var file in physicalMods)
                    {
                        bool exists = manifest.Files.Any(m => m.FileName == file.Name || m.Name + ".jar" == file.Name);
                        if (!exists)
                        {
                            manifest.Files.Add(new ModInfo
                            {
                                Name = Path.GetFileNameWithoutExtension(file.Name),
                                FileName = file.Name
                            });
                        }
                    }
                }

                File.WriteAllText(existingJsonPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));

                var dirInfo = new DirectoryInfo(pack.Path);
                string[] excludedDirs =
                {
                    "logs", "crash-reports", ".mixin.out", "saves", "backups",
                    ".cache", "webcache2", "libraries", "assets", "versions",
                    "runtime", "natives", "screenshots", "downloads", "distant_horizons_lod"
                };

                HashSet<string> preCompressedExts = new(StringComparer.OrdinalIgnoreCase)
                {
                    ".jar", ".zip", ".mrpack", ".png", ".jpg", ".jpeg", ".webp", ".ogg", ".mp3", ".gz", ".7z"
                };

                var filesToArchive = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        if (f.Name.Equals("options.txt", StringComparison.OrdinalIgnoreCase)) return false;
                        string relative = Path.GetRelativePath(dirInfo.FullName, f.FullName);
                        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        return !parts.Any(p => excludedDirs.Contains(p, StringComparer.OrdinalIgnoreCase));
                    })
                    .ToList();

                int totalFiles = filesToArchive.Count;
                int processedFiles = 0;
                int lastReportedPct = -1;

                using (var archive = ZipFile.Open(saveFilePath, ZipArchiveMode.Create))
                {
                    foreach (var file in filesToArchive)
                    {
                        token.ThrowIfCancellationRequested();

                        string entryName = Path.GetRelativePath(dirInfo.FullName, file.FullName).Replace('\\', '/');
                        var level = preCompressedExts.Contains(file.Extension)
                            ? CompressionLevel.NoCompression
                            : CompressionLevel.Fastest;

                        archive.CreateEntryFromFile(file.FullName, entryName, level);
                        processedFiles++;

                        int pct = totalFiles > 0 ? (int)((double)processedFiles / totalFiles * 100) : 100;
                        if (pct != lastReportedPct)
                        {
                            lastReportedPct = pct;
                            string format = LocalizationManager.GetString("Modpacks.ExportProgress", "Експорт: {0:F0}%");
                            string progressMsg = string.Format(format, pct);
                            Dispatcher.UIThread.Post(() =>
                            {
                                ExportProgressText = progressMsg;
                                ExportProgressValue = pct;
                                ExportProgressChanged?.Invoke(true, progressMsg, pct);
                            });
                        }
                    }
                }

                success = true;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (File.Exists(saveFilePath))
                {
                    File.Delete(saveFilePath);
                }
            }
            catch (Exception delEx)
            {
                Debug.WriteLine($"Failed to delete cancelled export zip: {delEx.Message}");
            }

            AvaloniaNotificationService.ShowNotification(
                LocalizationManager.GetString("Generic.Cancel", "Скасувати"),
                LocalizationManager.GetString("Modpacks.ExportCancelled", "Експорт збірки скасовано, тимчасовий архів видалено."),
                sec: 3);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Export error: {ex.Message}");
            try
            {
                if (File.Exists(saveFilePath))
                {
                    File.Delete(saveFilePath);
                }
            }
            catch
            {
            }
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsExporting = false;
                ExportProgressValue = 0;
                ExportProgressChanged?.Invoke(false, string.Empty, 0);
            });
        }

        return success;
    }

    [RelayCommand]
    private async Task EditModpackAsync(InstalledModpack? pack)
    {
        if (pack == null) return;

        AvaloniaSoundService.Click();

        var modpackInfo = new ModpackInfo
        {
            Name = pack.Name,
            Path = pack.Path,
            PathJson = pack.PathJson,
            TypeSite = pack.TypeSite,
            LoaderType = pack.LoaderType,
            MinecraftVersion = pack.MinecraftVersion,
            LoaderVersion = pack.LoaderVersion,
            UrlImage = pack.UrlImage,
            ServerIP = pack.ServerIP,
            EnterInServer = pack.EnterInServer,
            Wdith = pack.Wdith,
            Height = pack.Height,
            IsConsoleLogOpened = pack.IsConsoleLogOpened,
            OPack = pack.OPack,
            JavaPath = pack.JavaPath
        };

        if (Application.Current is not App { ServiceProvider: { } sp } app) return;
        if (app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }) return;

        var dialog = new EditModpackDialog();
        var vm = sp.GetRequiredService<EditModpackDialogViewModel>();
        vm.CloseAction = () => dialog.Close();
        dialog.DataContext = vm;

        _ = vm.InitializeAsync(modpackInfo);

        await dialog.ShowDialog(mainWindow);
        await RefreshModpacksListAsync();
    }

    public async Task ImportModpackAsync(string zipFilePath)
    {
        if (_modpackService == null) return;
        try
        {
            await _modpackService.ImportModpackFromFileAsync(zipFilePath);
            await RefreshModpacksListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Import Error: {ex.Message}");
        }
    }
}
