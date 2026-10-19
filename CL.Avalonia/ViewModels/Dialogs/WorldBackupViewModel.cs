using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Services;
using CL.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CL_CLegendary_Launcher_.Avalonia.Services;
using Avalonia.Platform;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public class BackupSource
{
    public string Name { get; set; }
    public string Path { get; set; }
}

public class WorldListItem
{
    public string Name { get; set; }
    public string FolderName { get; set; }
    public string FullPath { get; set; }
    public Bitmap IconBitmap { get; set; }
    public string Version { get; set; }
    public string WorldId { get; set; }
    public bool IsVersionVisible => !string.IsNullOrEmpty(Version) && Version != "Unknown" && Version != "?";
}

public partial class WorldBackupViewModel : ObservableObject, IDisposable
{
    public Action? CloseAction { get; set; }

    [ObservableProperty] private ObservableCollection<BackupSource> _sources = new();

    private BackupSource _selectedSource;
    public BackupSource SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value) && value != null)
            {
                LoadWorlds(value.Path);
            }
        }
    }

    [ObservableProperty] private ObservableCollection<WorldListItem> _worlds = new();

    private WorldListItem _selectedWorld;
    public WorldListItem SelectedWorld
    {
        get => _selectedWorld;
        set
        {
            if (SetProperty(ref _selectedWorld, value))
            {
                IsPlaceholderVisible = (value == null);
                IsContentVisible = (value != null);

                if (value != null)
                {
                    SelectedWorldVersionText = string.Format(LocalizationManager.GetString("Backups.SelectedWorldVersion", "Версія: {0}"), value.Version);
                    SelectedWorldFolderText = string.Format(LocalizationManager.GetString("Backups.SelectedWorldFolder", "Папка: {0}"), value.FolderName);
                }

                RefreshBackups();
            }
        }
    }

    [ObservableProperty] private ObservableCollection<WorldBackupInfo> _backups = new();

    [ObservableProperty] private string _createBackupBtnText;
    [ObservableProperty] private string _zipArchiveText;
    [ObservableProperty] private string _restoreBtnText;
    [ObservableProperty] private string _deleteBtnText;
    [ObservableProperty] private string _windowTitle;
    [ObservableProperty] private string _sourceLabel;
    [ObservableProperty] private string _worldsListTitle;
    [ObservableProperty] private string _noWorldsFoundText;
    [ObservableProperty] private string _placeholderText;
    [ObservableProperty] private string _historyTitle;
    [ObservableProperty] private string _noBackupsForWorldText;

    [ObservableProperty] private string _selectedWorldVersionText;
    [ObservableProperty] private string _selectedWorldFolderText;

    [ObservableProperty] private bool _isNoWorldsVisible;
    [ObservableProperty] private bool _isPlaceholderVisible = true;
    [ObservableProperty] private bool _isContentVisible = false;
    [ObservableProperty] private bool _isNoBackupsVisible;
    [ObservableProperty] private bool _isCreatingBackup;
    [ObservableProperty] private bool _isWorking;

    public WorldBackupViewModel()
    {
        ApplyLocalization();
        LoadSources();
    }

    private void ApplyLocalization()
    {
        WindowTitle = LocalizationManager.GetString("Backups.ManagerTitle", "Менеджер бекапів світів");
        SourceLabel = LocalizationManager.GetString("Backups.SourceLabel", "Джерело світів:");
        WorldsListTitle = LocalizationManager.GetString("Backups.ListTitle", "Список світів");
        NoWorldsFoundText = LocalizationManager.GetString("Backups.NoWorldsFound", "У цій папці світів немає");
        PlaceholderText = LocalizationManager.GetString("Backups.PlaceholderText", "Виберіть світ зі списку ліворуч,\nщоб керувати його копіями");
        HistoryTitle = LocalizationManager.GetString("Backups.History", "Історія відновлення");
        NoBackupsForWorldText = LocalizationManager.GetString("Backups.NoBackupsForWorld", "Для цього світу ще немає копій");

        CreateBackupBtnText = LocalizationManager.GetString("Backups.CreateBtn", "СТВОРИТИ НОВИЙ БЕКАП");
        ZipArchiveText = " • " + LocalizationManager.GetString("Backups.ZipArchiveSuffix", "zip архів");
        RestoreBtnText = LocalizationManager.GetString("Backups.RestoreBtn", "Відновити");
        DeleteBtnText = LocalizationManager.GetString("Backups.DeleteBtn", "Видалити");
    }

    private void LoadSources()
    {
        var sources = new List<BackupSource>();
        string rootPath = SettingsManager.Default.PathLacunher;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = PlatformPaths.DefaultLauncherPath();
        }

        sources.Add(new BackupSource
        {
            Name = LocalizationManager.GetString("Backups.GlobalWorlds", "Глобальні світи (Global)"),
            Path = Path.Combine(rootPath, "saves")
        });

        string versionsPath = Path.Combine(rootPath, "CLModpack");
        if (Directory.Exists(versionsPath))
        {
            var dirs = Directory.GetDirectories(versionsPath);
            foreach (var dir in dirs)
            {
                var dirInfo = new DirectoryInfo(dir);
                string verName = dirInfo.Name;

                var possiblePaths = new List<string>
                {
                    Path.Combine(dir, "saves"),
                    Path.Combine(dir, "override", "saves"),
                    Path.Combine(dir, "overrides", "saves")
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        sources.Add(new BackupSource
                        {
                            Name = string.Format(LocalizationManager.GetString("Backups.ModpackWorlds", "Збірка: {0}"), verName),
                            Path = path
                        });
                        break;
                    }
                }
            }
        }

        Sources = new ObservableCollection<BackupSource>(sources);
        if (Sources.Count > 0)
        {
            SelectedSource = Sources[0];
        }
    }

    private void LoadWorlds(string savesPath)
    {
        SelectedWorld = null;
        var list = new List<WorldListItem>();

        if (!Directory.Exists(savesPath))
        {
            try { Directory.CreateDirectory(savesPath); } catch { }
        }

        if (Directory.Exists(savesPath))
        {
            var dirs = Directory.GetDirectories(savesPath);
            foreach (var dir in dirs)
            {
                string levelDatPath = Path.Combine(dir, "level.dat");
                if (File.Exists(levelDatPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    string iconPath = Path.Combine(dir, "icon.png");

                    Bitmap bmp = null;
                    if (File.Exists(iconPath))
                    {
                        try
                        {
                            using var fs = File.OpenRead(iconPath);
                            bmp = new Bitmap(fs);
                        }
                        catch { bmp = null; }
                    }

                    if (bmp == null)
                    {
                        try
                        {
                            var uri = new Uri("avares://CL.Avalonia/Icon/IconCL(Common).png");
                            using var asset = AssetLoader.Open(uri);
                            bmp = new Bitmap(asset);
                        }
                        catch { }
                    }

                    string version = GetVersionFromLevelDat(levelDatPath);
                    string wId = WorldBackupService.GetWorldID(dir);

                    list.Add(new WorldListItem
                    {
                        Name = dirInfo.Name,
                        FolderName = dirInfo.Name,
                        FullPath = dir,
                        IconBitmap = bmp,
                        Version = version,
                        WorldId = wId
                    });
                }
            }
        }

        Worlds = new ObservableCollection<WorldListItem>(list);
        IsNoWorldsVisible = (list.Count == 0);
    }

    private string GetVersionFromLevelDat(string path)
    {
        try
        {
            using (FileStream fs = File.OpenRead(path))
            using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
            using (MemoryStream ms = new MemoryStream())
            {
                gzip.CopyTo(ms);
                byte[] data = ms.ToArray();
                for (int i = 0; i < data.Length - 10; i++)
                {
                    if (data[i] == 0x08 && data[i + 1] == 0x00 && data[i + 2] == 0x04 &&
                        data[i + 3] == (byte)'N' && data[i + 4] == (byte)'a' &&
                        data[i + 5] == (byte)'m' && data[i + 6] == (byte)'e')
                    {
                        int lenIndex = i + 7;
                        if (lenIndex + 1 >= data.Length) break;
                        short strLen = (short)((data[lenIndex] << 8) | data[lenIndex + 1]);

                        if (strLen > 0 && lenIndex + 2 + strLen <= data.Length)
                        {
                            string ver = Encoding.UTF8.GetString(data, lenIndex + 2, strLen);
                            if (char.IsDigit(ver[0]) || ver.Length < 30) return ver;
                        }
                    }
                }
            }
        }
        catch { return "Unknown"; }
        return "?";
    }

    private void RefreshBackups()
    {
        if (SelectedWorld == null)
        {
            Backups.Clear();
            IsNoBackupsVisible = false;
            return;
        }

        var list = WorldBackupService.GetBackupsForWorld(SelectedWorld.FullPath);
        Backups = new ObservableCollection<WorldBackupInfo>(list);
        IsNoBackupsVisible = (list.Count == 0);
    }

    [RelayCommand]
    private async Task CreateBackup()
    {
        if (SelectedWorld == null || IsCreatingBackup) return;
        IsCreatingBackup = true;

        try
        {
            AvaloniaSoundService.Click();
            await WorldBackupService.CreateWorldBackupAsync(SelectedWorld.FullPath);
            RefreshBackups();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvaloniaDialogService.Show(
                    LocalizationManager.GetString("Backups.CreatedSuccess", "Бекап створено успішно!"),
                    LocalizationManager.GetString("Dialogs.Success", "Успіх"),
                    MascotEmotion.Happy);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvaloniaDialogService.Show(
                    string.Format(LocalizationManager.GetString("Backups.CreateError", "Помилка створення: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Ой"),
                    MascotEmotion.Sad);
            });
        }
        finally
        {
            IsCreatingBackup = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackup(WorldBackupInfo backup)
    {
        if (backup == null || SelectedWorld == null || IsWorking) return;

        AvaloniaSoundService.Click();

        string confirmMsg = string.Format(LocalizationManager.GetString("Backups.RestoreConfirm", "Відновити світ '{0}' до стану від {1:dd.MM HH:mm}?\n\nПоточний прогрес буде втрачено."), SelectedWorld.Name, backup.CreationTime);

        bool confirm = await AvaloniaDialogService.AskAsync(confirmMsg, LocalizationManager.GetString("Backups.RestoreConfirmTitle", "Відновлення"), MascotEmotion.Alert);

        if (confirm)
        {
            IsWorking = true;
            try
            {
                string savesRoot = Directory.GetParent(SelectedWorld.FullPath).FullName;
                await WorldBackupService.RestoreWorldBackupAsync(backup.FullPath, savesRoot);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvaloniaDialogService.Show(
                        LocalizationManager.GetString("Backups.RestoreSuccess", "Світ відновлено!"),
                        LocalizationManager.GetString("Dialogs.Success", "Готово"),
                        MascotEmotion.Happy);
                });

                if (SelectedSource != null)
                {
                    LoadWorlds(SelectedSource.Path);
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvaloniaDialogService.Show(
                        string.Format(LocalizationManager.GetString("Backups.RestoreError", "Помилка: {0}"), ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Біда"),
                        MascotEmotion.Sad);
                });
            }
            finally
            {
                IsWorking = false;
            }
        }
    }

    [RelayCommand]
    private async Task DeleteBackup(WorldBackupInfo backup)
    {
        if (backup == null) return;
        AvaloniaSoundService.Click();

        bool confirm = await AvaloniaDialogService.AskAsync(
            LocalizationManager.GetString("Backups.DeleteConfirm", "Видалити цей архів?"),
            LocalizationManager.GetString("Dialogs.DeleteConfirmTitle", "Видалення"),
            MascotEmotion.Normal);

        if (confirm)
        {
            try
            {
                File.Delete(backup.FullPath);
                RefreshBackups();
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AvaloniaDialogService.Show(ex.Message, LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
                });
            }
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }

    public void Dispose()
    {
        CloseAction = null;
        foreach (var world in Worlds)
        {
            world.IconBitmap?.Dispose();
            world.IconBitmap = null!;
        }
        Worlds.Clear();
        Sources.Clear();
        Backups.Clear();
        _selectedWorld = null!;
        _selectedSource = null!;
    }
}
