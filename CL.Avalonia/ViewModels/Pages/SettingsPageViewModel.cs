using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Models;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.Views.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public class UpdateChannelOption
{
    public string Key { get; }
    public string Display { get; }

    public UpdateChannelOption(string key, string display)
    {
        Key = key;
        Display = display;
    }

    public override string ToString() => Display;
}

public class UiScaleOption
{
    public double Scale { get; }
    public string Display { get; }

    public UiScaleOption(double scale, string display)
    {
        Scale = scale;
        Display = display;
    }

    public override string ToString() => Display;
}

public partial class FontOption : ObservableObject
{
    public string Key { get; }

    [ObservableProperty]
    private string _displayName;

    public FontOption(string key, string displayName)
    {
        Key = key;
        _displayName = displayName;
    }

    public override string ToString() => DisplayName;
}

public partial class ThemeOption : ObservableObject
{
    public string Key { get; }

    [ObservableProperty]
    private string _displayName;

    public ThemeOption(string key, string displayName)
    {
        Key = key;
        _displayName = displayName;
    }

    public override string ToString() => DisplayName;
}

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly AvaloniaThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly ISoundService _soundService;

    public static event Action? ModulesConfigurationChanged;
    public static event Action<double>? UiScaleConfigurationChanged;
    public static event Action? LanguageConfigurationChanged;

    [ObservableProperty] private bool _enableLog;
    [ObservableProperty] private bool _closeLauncher;
    [ObservableProperty] private bool _modDep;
    [ObservableProperty] private bool _enableGlassEffect;
    [ObservableProperty] private bool _fullScreen;
    [ObservableProperty] private bool _enableAutoBackup;
    [ObservableProperty] private bool _isCrashReportingEnabled;
    [ObservableProperty] private bool _enableSound;

    [ObservableProperty] private int _ramAllocation = 4096;
    [ObservableProperty] private int _maxRamAllocation = 8192;
    [ObservableProperty] private string _ramAllocationText = "4096 MB";

    [ObservableProperty] private int _windowWidth = 800;
    [ObservableProperty] private int _windowHeight = 600;
    [ObservableProperty] private string _widthText = "800";
    [ObservableProperty] private string _heightText = "600";
    [ObservableProperty] private string _resolutionDisplayText = "800x600";
    [ObservableProperty] private bool _isResolutionPickerOpen;
    [ObservableProperty] private string? _selectedResolutionPreset;

    public ObservableCollection<string> ResolutionPresets { get; } = new()
    {
        "1920x1080",
        "1600x900",
        "1366x768",
        "1280x720",
        "800x600"
    };

    [ObservableProperty] private string _launcherPath = string.Empty;

    [ObservableProperty] private byte _maxAutoBackups = 3;

    public ObservableCollection<UpdateChannelOption> AvailableUpdateChannels { get; } = new();
    [ObservableProperty] private UpdateChannelOption? _selectedUpdateChannelOption;
    [ObservableProperty] private string _updateStatusText = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdates;
    private bool _suppressChannelChange;

    public ObservableCollection<LanguageItem> Languages { get; } = new();
    [ObservableProperty] private LanguageItem? _selectedLanguage;
    [ObservableProperty] private bool _isLoadingLanguage;
    private bool _suppressLanguageChange;

    public ObservableCollection<FontOption> AvailableFonts { get; } = new()
    {
        new("Inter", "Inter"),
        new("Geist", "Geist")
    };
    [ObservableProperty] private FontOption? _selectedFontOption;
    private bool _suppressFontChange;

    public ObservableCollection<UiScaleOption> AvailableUiScales { get; } = new()
    {
        new UiScaleOption(1.0, "1.0x"),
        new UiScaleOption(1.25, "1.25x"),
        new UiScaleOption(1.5, "1.5x"),
        new UiScaleOption(1.75, "1.75x"),
        new UiScaleOption(2.0, "2.0x")
    };
    [ObservableProperty] private UiScaleOption? _selectedUiScale;
    private bool _suppressScaleChange;

    public ObservableCollection<ThemeOption> AvailableThemes { get; } = new()
    {
        new("Dark", "Темна"),
        new("Light", "Світла"),
        new("Ocean", "Океан"),
        new("Forest", "Ліс"),
        new("Sunset", "Захід Сонця"),
        new("Custom", "Кастомна")
    };
    [ObservableProperty] private ThemeOption? _selectedThemeOption;
    [ObservableProperty] private string _selectedTheme = "Dark";
    private bool _suppressThemeChange;

    [ObservableProperty] private string _sectionColor = "#303030";
    [ObservableProperty] private string _backgroundColor = "#FF202020";
    [ObservableProperty] private string _additionalColor = "#FF1863C9";
    [ObservableProperty] private string _textColor = "#FFFFFFFF";
    [ObservableProperty] private string _buttonColor = "#FF202020";
    [ObservableProperty] private string _backgroundImagePath = string.Empty;
    [ObservableProperty] private string _themeCode = string.Empty;

    [ObservableProperty] private string _loadScreenColor = "#00BEFF";
    [ObservableProperty] private string _loadScreenBackground = string.Empty;
    [ObservableProperty] private string _loadScreenCode = string.Empty;

    [ObservableProperty] private bool _enableModpacks;
    [ObservableProperty] private bool _enableMods;
    [ObservableProperty] private bool _enableServers;
    [ObservableProperty] private bool _enableGallery;
    [ObservableProperty] private bool _enableAIAgent;

    [ObservableProperty] private bool _enableSubFiles;
    [ObservableProperty] private bool _enableSubFilesBackups;

    [ObservableProperty] private bool _enableSubInfoBug;
    [ObservableProperty] private bool _enableSubInfoNews;
    [ObservableProperty] private bool _enableSubInfoWiki;
    [ObservableProperty] private bool _enableSubInfoGithub;
    [ObservableProperty] private bool _enableSubInfoCredits;
    [ObservableProperty] private bool _enableSubInfoSupport;

    [ObservableProperty] private bool _enableModChangelog;
    [ObservableProperty] private bool _enableModLatestActions;
    [ObservableProperty] private bool _enableModDiscordRPC;
    [ObservableProperty] private bool _enableModStatistics;

    private bool _suppressModuleNotifications;

    public SettingsPageViewModel() : this(
        (Application.Current as App)?.ServiceProvider?.GetService(typeof(AvaloniaThemeService)) as AvaloniaThemeService
            ?? new AvaloniaThemeService(new AvaloniaDialogService(), new AvaloniaWindowController()),
        (Application.Current as App)?.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService
            ?? new AvaloniaDialogService(),
        (Application.Current as App)?.ServiceProvider?.GetService(typeof(ISoundService)) as ISoundService
            ?? new AvaloniaSoundService())
    {
    }

    public SettingsPageViewModel(
        AvaloniaThemeService themeService,
        IDialogService dialogService,
        ISoundService soundService)
    {
        _themeService = themeService;
        _dialogService = dialogService;
        _soundService = soundService;

        DetectSystemMemory();
        LoadSettings();
        _ = LoadLanguagesAsync();

        SettingsManager.SettingsReloaded += OnSettingsReloaded;
    }

    private void OnSettingsReloaded()
    {
        Dispatcher.UIThread.Post(() => LoadSettings());
    }

    private void DetectSystemMemory()
    {
        try
        {
            var memInfo = GC.GetGCMemoryInfo();
            long totalBytes = memInfo.TotalAvailableMemoryBytes;
            if (totalBytes > 0)
            {
                int totalMb = (int)(totalBytes / (1024 * 1024));
                MaxRamAllocation = Math.Max(8192, Math.Min(32768, totalMb));
                return;
            }
        }
        catch { }
        MaxRamAllocation = 8192;
    }

    public void LoadSettings()
    {
        _suppressThemeChange = true;
        _suppressScaleChange = true;
        _suppressFontChange = true;
        _suppressModuleNotifications = true;

        EnableLog = SettingsManager.Default.EnableLog;
        CloseLauncher = SettingsManager.Default.CloseLaucnher;
        ModDep = SettingsManager.Default.ModDep;
        EnableGlassEffect = !SettingsManager.Default.DisableGlassEffect;
        FullScreen = SettingsManager.Default.FullScreen;
        EnableAutoBackup = SettingsManager.Default.EnableAutoBackup;
        MaxAutoBackups = SettingsManager.Default.MaxAutoBackups > 0 ? SettingsManager.Default.MaxAutoBackups : (byte)3;
        IsCrashReportingEnabled = SettingsManager.Default.IsCrashReportingEnabled;
        EnableSound = SettingsManager.Default.EnableSound;

        RamAllocation = SettingsManager.Default.OP > 0 ? SettingsManager.Default.OP : 4096;
        RamAllocationText = $"{RamAllocation} MB";

        WindowWidth = SettingsManager.Default.width > 0 ? SettingsManager.Default.width : 800;
        WindowHeight = SettingsManager.Default.height > 0 ? SettingsManager.Default.height : 600;
        WidthText = WindowWidth.ToString();
        HeightText = WindowHeight.ToString();
        ResolutionDisplayText = $"{WindowWidth}x{WindowHeight}";

        LauncherPath = string.IsNullOrWhiteSpace(SettingsManager.Default.PathLacunher)
            ? PlatformPaths.DefaultLauncherPath()
            : SettingsManager.Default.PathLacunher;

        string currentFont = string.IsNullOrWhiteSpace(SettingsManager.Default.SelectedFont) ? "Inter" : SettingsManager.Default.SelectedFont;
        SelectedFontOption = AvailableFonts.FirstOrDefault(f => f.Key.Equals(currentFont, StringComparison.OrdinalIgnoreCase)) ?? AvailableFonts[0];

        double currentScale = SettingsManager.Default.UIScale > 0.1 ? SettingsManager.Default.UIScale : 1.0;
        SelectedUiScale = AvailableUiScales.FirstOrDefault(s => Math.Abs(s.Scale - currentScale) < 0.05)
                          ?? AvailableUiScales[0];

        SelectedTheme = string.IsNullOrWhiteSpace(SettingsManager.Default.Them) ? "Dark" : SettingsManager.Default.Them;
        UpdateLocalizedThemeOptionNames();
        SelectedThemeOption = AvailableThemes.FirstOrDefault(t => t.Key.Equals(SelectedTheme, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes[0];
        SyncThemePropertiesFromSettings();
        UpdateLocalizedUpdateChannels();

        LoadScreenColor = string.IsNullOrWhiteSpace(SettingsManager.Default.LoadScreenBarColor) ? "#00BEFF" : SettingsManager.Default.LoadScreenBarColor;
        LoadScreenBackground = SettingsManager.Default.LoadScreenBackground ?? string.Empty;

        EnableModpacks = SettingsManager.Default.EnableModpacks;
        EnableMods = SettingsManager.Default.EnableMods;
        EnableServers = SettingsManager.Default.EnableServers;
        EnableGallery = SettingsManager.Default.EnableGallery;
        EnableAIAgent = SettingsManager.Default.EnableAIAgent;

        EnableSubFiles = SettingsManager.Default.EnableSubFiles;
        EnableSubFilesBackups = SettingsManager.Default.EnableSubFiles_Backups;

        EnableSubInfoBug = SettingsManager.Default.EnableSubInfo_Bug;
        EnableSubInfoNews = SettingsManager.Default.EnableSubInfo_News;
        EnableSubInfoWiki = SettingsManager.Default.EnableSubInfo_Wiki;
        EnableSubInfoGithub = SettingsManager.Default.EnableSubInfo_Github;
        EnableSubInfoCredits = SettingsManager.Default.EnableSubInfo_Credits;
        EnableSubInfoSupport = SettingsManager.Default.EnableSubInfo_Support;

        EnableModChangelog = SettingsManager.Default.EnableMod_Changelog;
        EnableModLatestActions = SettingsManager.Default.EnableMod_LatestActions;
        EnableModDiscordRPC = SettingsManager.Default.EnableMod_DiscordRPC;
        EnableModStatistics = SettingsManager.Default.EnableMod_Statistics;

        _suppressThemeChange = false;
        _suppressScaleChange = false;
        _suppressFontChange = false;
        _suppressModuleNotifications = false;
    }

    private void SyncThemePropertiesFromSettings()
    {
        SectionColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Section_colour) ? "#303030" : SettingsManager.Default.Section_colour;
        BackgroundColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Background_colour) ? "#FF202020" : SettingsManager.Default.Background_colour;
        AdditionalColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Additional_colour) ? "#FF1863C9" : SettingsManager.Default.Additional_colour;
        TextColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Text_colour) ? "#FFFFFFFF" : SettingsManager.Default.Text_colour;
        ButtonColor = string.IsNullOrWhiteSpace(SettingsManager.Default.Button_colour) ? "#FF202020" : SettingsManager.Default.Button_colour;
        BackgroundImagePath = SettingsManager.Default.bgImage ?? string.Empty;
    }

    private static CancellationTokenSource? _saveDebounceCts;

    private static void DebouncedSave(int delayMs = 300)
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token);
                if (!token.IsCancellationRequested)
                {
                    SettingsManager.Save();
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void Save() => SettingsManager.Save();

    private void NotifyModulesChanged()
    {
        if (_suppressModuleNotifications) return;
        Save();
        ModulesConfigurationChanged?.Invoke();
        _ = MemoryCleaner.FlushMemoryAsync(true);
    }

    private static void ApplyUiScaleToMainWindow(double newScale)
    {
        UiScaleConfigurationChanged?.Invoke(newScale);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWin })
        {
            if (mainWin.DataContext is CL_MainViewModel mainVm)
            {
                bool isFullscreen = mainWin.WindowState == WindowState.Maximized || mainWin.WindowState == WindowState.FullScreen;
                mainVm.UiScale = isFullscreen ? newScale : 1.0;
            }
        }
    }

    #region Property Changed Handlers

    partial void OnSelectedUpdateChannelOptionChanged(UpdateChannelOption? value)
    {
        if (value == null || _suppressChannelChange) return;
        _soundService.PlayClick();
        if (value.Key.Equals("Preview", StringComparison.OrdinalIgnoreCase) && !string.Equals(SettingsManager.Default.UpdateChannel, "Preview", StringComparison.OrdinalIgnoreCase))
        {
            PlatformPaths.BackupUserDataForPreBeta();
        }
        SettingsManager.Default.UpdateChannel = value.Key;
        Save();
    }

    partial void OnEnableLogChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.EnableLog = value;
        Save();
    }

    partial void OnCloseLauncherChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.CloseLaucnher = value;
        Save();
    }

    partial void OnModDepChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.ModDep = value;
        Save();
    }

    partial void OnEnableGlassEffectChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.DisableGlassEffect = !value;
        Save();
        _themeService.ToggleGlassEffect(!value);
    }

    partial void OnFullScreenChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.FullScreen = value;
        Save();
    }

    partial void OnEnableAutoBackupChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.EnableAutoBackup = value;
        Save();
    }

    partial void OnIsCrashReportingEnabledChanged(bool value)
    {
        _soundService.PlayClick();
        SettingsManager.Default.IsCrashReportingEnabled = value;
        Save();

        if (value)
            CrashReportManager.Enable();
        else
            CrashReportManager.Disable();
    }

    partial void OnEnableSoundChanged(bool value)
    {
        SettingsManager.Default.EnableSound = value;
        Save();
        if (value) _soundService.PlayClick();
    }

    partial void OnRamAllocationChanged(int value)
    {
        RamAllocationText = $"{value} MB";
        SettingsManager.Default.OP = value;
        DebouncedSave();
    }

    partial void OnSelectedResolutionPresetChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        _soundService.PlayClick();

        var parts = value.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
        {
            WindowWidth = w;
            WindowHeight = h;
            WidthText = w.ToString();
            HeightText = h.ToString();
            ResolutionDisplayText = value;

            SettingsManager.Default.width = w;
            SettingsManager.Default.height = h;
            Save();
        }
    }

    partial void OnWidthTextChanged(string value)
    {
        string digits = Regex.Replace(value ?? string.Empty, @"[^\d]", "");
        if (int.TryParse(digits, out int w))
        {
            w = Math.Clamp(w, 800, 3840);
            WindowWidth = w;
            ResolutionDisplayText = $"{WindowWidth}x{WindowHeight}";
            SettingsManager.Default.width = w;
            Save();
        }
    }

    partial void OnHeightTextChanged(string value)
    {
        string digits = Regex.Replace(value ?? string.Empty, @"[^\d]", "");
        if (int.TryParse(digits, out int h))
        {
            h = Math.Clamp(h, 600, 2160);
            WindowHeight = h;
            ResolutionDisplayText = $"{WindowWidth}x{WindowHeight}";
            SettingsManager.Default.height = h;
            Save();
        }
    }

    partial void OnSelectedFontOptionChanged(FontOption? value)
    {
        if (_suppressFontChange || value == null) return;
        _soundService.PlayClick();

        SettingsManager.Default.SelectedFont = value.Key;
        Save();
        _themeService.ApplyFont(value.Key);
    }

    partial void OnSelectedUiScaleChanged(UiScaleOption? value)
    {
        if (_suppressScaleChange || value == null) return;
        _soundService.PlayClick();

        SettingsManager.Default.UIScale = value.Scale;
        Save();
        ApplyUiScaleToMainWindow(value.Scale);
    }

    partial void OnSelectedThemeOptionChanged(ThemeOption? value)
    {
        if (_suppressThemeChange || value == null) return;
        SelectedTheme = value.Key;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (_suppressThemeChange || string.IsNullOrEmpty(value)) return;
        _soundService.PlayClick();

        SettingsManager.Default.Them = value;
        Save();

        _suppressThemeChange = true;
        SelectedThemeOption = AvailableThemes.FirstOrDefault(t => t.Key.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes[0];
        _suppressThemeChange = false;

        if (value.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            ApplyCurrentCustomTheme();
        }
        else
        {
            _themeService.ApplyPredefinedTheme(value);
            SyncThemePropertiesFromSettings();
        }
    }

    partial void OnEnableModpacksChanged(bool value) { SettingsManager.Default.EnableModpacks = value; NotifyModulesChanged(); }
    partial void OnEnableModsChanged(bool value) { SettingsManager.Default.EnableMods = value; NotifyModulesChanged(); }
    partial void OnEnableServersChanged(bool value) { SettingsManager.Default.EnableServers = value; NotifyModulesChanged(); }
    partial void OnEnableGalleryChanged(bool value) { SettingsManager.Default.EnableGallery = value; NotifyModulesChanged(); }
    partial void OnEnableAIAgentChanged(bool value) { SettingsManager.Default.EnableAIAgent = value; NotifyModulesChanged(); }

    partial void OnEnableSubFilesChanged(bool value) { SettingsManager.Default.EnableSubFiles = value; NotifyModulesChanged(); }
    partial void OnEnableSubFilesBackupsChanged(bool value) { SettingsManager.Default.EnableSubFiles_Backups = value; NotifyModulesChanged(); }

    partial void OnEnableSubInfoBugChanged(bool value) { SettingsManager.Default.EnableSubInfo_Bug = value; NotifyModulesChanged(); }
    partial void OnEnableSubInfoNewsChanged(bool value) { SettingsManager.Default.EnableSubInfo_News = value; NotifyModulesChanged(); }
    partial void OnEnableSubInfoWikiChanged(bool value) { SettingsManager.Default.EnableSubInfo_Wiki = value; NotifyModulesChanged(); }
    partial void OnEnableSubInfoGithubChanged(bool value) { SettingsManager.Default.EnableSubInfo_Github = value; NotifyModulesChanged(); }
    partial void OnEnableSubInfoCreditsChanged(bool value) { SettingsManager.Default.EnableSubInfo_Credits = value; NotifyModulesChanged(); }
    partial void OnEnableSubInfoSupportChanged(bool value) { SettingsManager.Default.EnableSubInfo_Support = value; NotifyModulesChanged(); }

    partial void OnEnableModChangelogChanged(bool value) { SettingsManager.Default.EnableMod_Changelog = value; NotifyModulesChanged(); }
    partial void OnEnableModLatestActionsChanged(bool value) { SettingsManager.Default.EnableMod_LatestActions = value; NotifyModulesChanged(); }
    partial void OnEnableModDiscordRPCChanged(bool value)
    {
        SettingsManager.Default.EnableMod_DiscordRPC = value;
        NotifyModulesChanged();
        if (value)
        {
            _ = DiscordController.Initialize(LocalizationManager.GetString("DiscordRPC.InSettings", "В налаштуваннях"));
        }
        else
        {
            DiscordController.Deinitialize();
        }
    }
    partial void OnEnableModStatisticsChanged(bool value) { SettingsManager.Default.EnableMod_Statistics = value; NotifyModulesChanged(); }

    #endregion

    #region Resolution & RAM Commands

    [RelayCommand]
    private void ToggleResolutionPicker()
    {
        _soundService.PlayClick();
        IsResolutionPickerOpen = !IsResolutionPickerOpen;
    }

    [RelayCommand]
    private void ResetResolution()
    {
        _soundService.PlayClick();
        WindowWidth = 800;
        WindowHeight = 600;
        WidthText = "800";
        HeightText = "600";
        ResolutionDisplayText = "800x600";
        SettingsManager.Default.width = 800;
        SettingsManager.Default.height = 600;
        Save();
    }

    [RelayCommand]
    private void ResetRam()
    {
        _soundService.PlayClick();
        RamAllocation = 4096;
        RamAllocationText = "4096 MB";
        SettingsManager.Default.OP = 4096;
        Save();
    }

    #endregion

    #region Launcher Path Commands (Fixed for Linux / macOS / Windows)

    [RelayCommand]
    private async Task ChangeLauncherPathAsync()
    {
        _soundService.PlayClick();
        var selectedFolder = await _dialogService.OpenFolderDialogAsync(
            LocalizationManager.GetString("GameLaunch.SelectFolderDesc", "Виберіть новий шлях для папки .ClMinecraft"));

        if (string.IsNullOrWhiteSpace(selectedFolder)) return;

        string oldPath = Path.GetFullPath(SettingsManager.Default.PathLacunher);
        string selectedFullPath = Path.GetFullPath(selectedFolder);

        string newPath = Path.GetFileName(selectedFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Equals(".ClMinecraft", StringComparison.OrdinalIgnoreCase)
            ? selectedFullPath
            : Path.Combine(selectedFullPath, ".ClMinecraft");

        if (PlatformPaths.PathsEqual(oldPath, newPath))
        {
            _dialogService.ShowMessage(
                LocalizationManager.GetString("GameLaunch.SameFolderDesc", "Ви вибрали ту саму папку!"),
                LocalizationManager.GetString("GameLaunch.SameFolderTitle", "Увага"),
                MascotEmotion.Confused);
            return;
        }

        string oldWithSep = oldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (newPath.StartsWith(oldWithSep, StringComparison.Ordinal))
        {
            _dialogService.ShowMessage(
                "Неможливо перемістити папку лаунчера всередину самої себе!",
                LocalizationManager.GetString("GameLaunch.FolderMoveErrorTitle", "Помилка переміщення"),
                MascotEmotion.Confused);
            return;
        }

        await MoveLauncherDirectoryInternalAsync(oldPath, newPath, isReset: false);
    }

    [RelayCommand]
    private async Task ResetLauncherPathAsync()
    {
        _soundService.PlayClick();
        string defaultPath = Path.GetFullPath(PlatformPaths.DefaultLauncherPath());
        string currentPath = Path.GetFullPath(SettingsManager.Default.PathLacunher);

        if (PlatformPaths.PathsEqual(currentPath, defaultPath))
        {
            _dialogService.ShowMessage(
                LocalizationManager.GetString("GameLaunch.PathAlreadyDefaultDesc", "Шлях вже встановлено за замовчуванням!"),
                LocalizationManager.GetString("Dialogs.Alert", "Інфо"),
                MascotEmotion.Normal);
            return;
        }

        bool confirm = await _dialogService.AskQuestionAsync(
            string.Format(LocalizationManager.GetString("GameLaunch.PathResetConfirmDesc", "Ви впевнені, що хочете скинути шлях?\n\nВсі файли будуть переміщені з:\n{0}\n\nВ стандартну папку:\n{1}"), currentPath, defaultPath),
            LocalizationManager.GetString("GameLaunch.PathResetConfirmTitle", "Почекайте!"),
            MascotEmotion.Confused);

        if (!confirm) return;

        await MoveLauncherDirectoryInternalAsync(currentPath, defaultPath, isReset: true);
    }

    private async Task MoveLauncherDirectoryInternalAsync(string oldPath, string newPath, bool isReset)
    {
        bool movedInPlace = false;

        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(oldPath))
                {
                    Directory.CreateDirectory(newPath);
                    UpdateModpacksJson(oldPath, newPath);
                    return;
                }

                string? targetParent = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(targetParent))
                {
                    Directory.CreateDirectory(targetParent);
                }

                if (!Directory.Exists(newPath))
                {
                    try
                    {
                        Directory.Move(oldPath, newPath);
                        movedInPlace = true;
                        UpdateModpacksJson(oldPath, newPath);
                        return;
                    }
                    catch (IOException)
                    {

                    }
                }

                long requiredBytes = CalculateDirectorySize(new DirectoryInfo(oldPath));
                long? availableBytes = GetAvailableFreeSpace(targetParent ?? newPath);

                if (availableBytes.HasValue && availableBytes.Value < requiredBytes + (50L * 1024 * 1024))
                {
                    double neededGb = Math.Round(requiredBytes / (1024.0 * 1024.0 * 1024.0), 2);
                    double freeGb = Math.Round(availableBytes.Value / (1024.0 * 1024.0 * 1024.0), 2);
                    throw new IOException($"Недостатньо місця на цільовому диску. Потрібно: {neededGb} ГБ, доступно: {freeGb} ГБ.");
                }

                CopyDirectorySequential(oldPath, newPath);
                UpdateModpacksJson(oldPath, newPath);
            });

            SettingsManager.Default.PathLacunher = newPath;
            LauncherPath = newPath;
            Save();

            if (!movedInPlace && Directory.Exists(oldPath))
            {
                _ = Task.Run(() =>
                {
                    try { Directory.Delete(oldPath, true); } catch { }
                });
            }

            _dialogService.ShowMessage(
                isReset
                    ? LocalizationManager.GetString("GameLaunch.PathResetSuccessDesc", "Шлях скинуто, файли успішно переміщено додому!")
                    : LocalizationManager.GetString("GameLaunch.FolderMovedDesc", "Папку успішно переміщено!"),
                LocalizationManager.GetString("GameLaunch.FolderMovedTitle", "Успіх!"),
                MascotEmotion.Happy);
        }
        catch (Exception ex)
        {
            string actualMessage = ex is AggregateException agg && agg.InnerExceptions.Count > 0
                ? agg.InnerExceptions[0].Message
                : ex.Message;

            _dialogService.ShowMessage(
                string.Format(
                    LocalizationManager.GetString(
                        isReset ? "GameLaunch.PathResetErrorDesc" : "GameLaunch.FolderMoveErrorDesc",
                        "Ой! Не вдалося перемістити папку лаунчера.\nДеталі: {0}"),
                    actualMessage),
                LocalizationManager.GetString(
                    isReset ? "GameLaunch.PathResetErrorTitle" : "GameLaunch.FolderMoveErrorTitle",
                    "Помилка переміщення"),
                MascotEmotion.Sad);
        }
    }

    private static long CalculateDirectorySize(DirectoryInfo dir)
    {
        long size = 0;
        try
        {
            foreach (var fi in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {

                if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                size += fi.Length;
            }
        }
        catch { }
        return size;
    }

    private static long? GetAvailableFreeSpace(string targetPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(targetPath);

            DriveInfo? bestMount = DriveInfo.GetDrives()
                .Where(d => d.IsReady && fullPath.StartsWith(d.RootDirectory.FullName, StringComparison.Ordinal))
                .OrderByDescending(d => d.RootDirectory.FullName.Length)
                .FirstOrDefault();

            return bestMount?.AvailableFreeSpace;
        }
        catch
        {
            return null;
        }
    }

    private static void CopyDirectorySequential(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.EnumerateFiles())
        {
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        foreach (DirectoryInfo subDir in dir.EnumerateDirectories())
        {
            if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectorySequential(subDir.FullName, newDestinationDir);
        }
    }

    private static void UpdateModpacksJson(string oldBasePath, string newBasePath)
    {
        string jsonPath = ModpackPaths.InstalledModpacksJson;
        if (!File.Exists(jsonPath))
        {
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "installed_modpacks.json");
            if (File.Exists(legacyPath)) jsonPath = legacyPath;
        }
        if (!File.Exists(jsonPath)) return;

        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var modpacks = System.Text.Json.JsonSerializer.Deserialize<List<InstalledModpack>>(jsonContent);
            if (modpacks != null)
            {
                string normalizedOld = oldBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedNew = newBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                bool hasChanges = false;

                foreach (var pack in modpacks)
                {
                    if (!string.IsNullOrEmpty(pack.Path) && pack.Path.StartsWith(normalizedOld, StringComparison.OrdinalIgnoreCase))
                    {
                        pack.Path = normalizedNew + pack.Path.Substring(normalizedOld.Length);
                        hasChanges = true;
                    }
                    if (!string.IsNullOrEmpty(pack.PathJson) && pack.PathJson.StartsWith(normalizedOld, StringComparison.OrdinalIgnoreCase))
                    {
                        pack.PathJson = normalizedNew + pack.PathJson.Substring(normalizedOld.Length);
                        hasChanges = true;
                    }
                    if (!string.IsNullOrEmpty(pack.UrlImage) && !pack.UrlImage.StartsWith("http") && pack.UrlImage.StartsWith(normalizedOld, StringComparison.OrdinalIgnoreCase))
                    {
                        pack.UrlImage = normalizedNew + pack.UrlImage.Substring(normalizedOld.Length);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    File.WriteAllText(jsonPath, System.Text.Json.JsonSerializer.Serialize(modpacks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
        catch { }
    }

    #endregion

    #region Auto-Backups Count Commands

    [RelayCommand]
    private void DecreaseBackupCount()
    {
        _soundService.PlayClick();
        if (MaxAutoBackups > 1)
        {
            MaxAutoBackups--;
            SettingsManager.Default.MaxAutoBackups = MaxAutoBackups;
            Save();
        }
    }

    [RelayCommand]
    private void IncreaseBackupCount()
    {
        _soundService.PlayClick();
        if (MaxAutoBackups < 20)
        {
            MaxAutoBackups++;
            SettingsManager.Default.MaxAutoBackups = MaxAutoBackups;
            Save();
        }
    }

    #endregion

    #region Language Commands

    private async Task LoadLanguagesAsync()
    {
        try
        {
            var list = await LocalizationFetcher.GetAvailableLanguagesAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Languages.Clear();
                foreach (var item in list)
                {
                    Languages.Add(item);
                }

                string currentLangCode = SettingsManager.Default.LanguageCode ?? "uk_UA";
                _suppressLanguageChange = true;
                SelectedLanguage = Languages.FirstOrDefault(l => l.Code == currentLangCode) ?? Languages.FirstOrDefault();
                _suppressLanguageChange = false;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load languages: {ex.Message}");
        }
    }

    partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (_suppressLanguageChange || value == null) return;
        _soundService.PlayClick();

        if (LocalizationManager.CurrentLanguage != value.Code)
        {
            _ = SwitchLanguageAsync(value);
        }
    }

    private async Task SwitchLanguageAsync(LanguageItem lang)
    {
        IsLoadingLanguage = true;
        try
        {
            bool downloaded = await LocalizationFetcher.DownloadLanguageAsync(lang);
            bool fileExists = LocalizationManager.IsLanguageFileExists(lang.Code);

            if (downloaded || fileExists)
            {
                SettingsManager.Default.LanguageCode = lang.Code;
                Save();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LocalizationManager.LoadLanguage(lang.Code);
                    UpdateLocalizedThemeOptionNames();
                    UpdateLocalizedUpdateChannels();
                    LanguageConfigurationChanged?.Invoke();
                });
            }
            else
            {
                _dialogService.ShowMessage(
                    LocalizationManager.GetString("Dialogs.Error", "Не вдалося завантажити мовний пакет. Перевірте підключення до мережі."),
                    LocalizationManager.GetString("Dialogs.Oops", "Упс"),
                    MascotEmotion.Sad);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsPageViewModel] SwitchLanguage error: {ex.Message}");
        }
        finally
        {
            IsLoadingLanguage = false;
        }
    }

    private void UpdateLocalizedUpdateChannels()
    {
        string currentKey = SelectedUpdateChannelOption?.Key ?? SettingsManager.Default.UpdateChannel;
        if (string.IsNullOrWhiteSpace(currentKey)) currentKey = "Stable";
        _suppressChannelChange = true;
        AvailableUpdateChannels.Clear();
        AvailableUpdateChannels.Add(new UpdateChannelOption("Stable", LocalizationManager.GetString("Settings.ChannelStable", "Стабільний (Release)")));
        AvailableUpdateChannels.Add(new UpdateChannelOption("Preview", LocalizationManager.GetString("Settings.ChannelPreview", "Тестовий (Pre-release / Beta)")));
        SelectedUpdateChannelOption = AvailableUpdateChannels.FirstOrDefault(c => c.Key.Equals(currentKey, StringComparison.OrdinalIgnoreCase)) ?? AvailableUpdateChannels[0];
        _suppressChannelChange = false;
    }

    private void UpdateLocalizedThemeOptionNames()
    {
        foreach (var theme in AvailableThemes)
        {
            theme.DisplayName = theme.Key switch
            {
                "Dark" => LocalizationManager.GetString("Settings.ThemeDark", "Темна"),
                "Light" => LocalizationManager.GetString("Settings.ThemeLight", "Світла"),
                "Ocean" => LocalizationManager.GetString("Settings.ThemeOcean", "Океан"),
                "Forest" => LocalizationManager.GetString("Settings.ThemeForest", "Ліс"),
                "Sunset" => LocalizationManager.GetString("Settings.ThemeSunset", "Захід Сонця"),
                "Custom" => LocalizationManager.GetString("Settings.ThemeCustom", "Кастомна"),
                _ => theme.DisplayName
            };
        }
    }

    #endregion

    #region UI Scale & Presets Commands

    [RelayCommand]
    private void ResetUiScale()
    {
        _soundService.PlayClick();
        SelectedUiScale = AvailableUiScales.FirstOrDefault(s => Math.Abs(s.Scale - 1.0) < 0.01) ?? AvailableUiScales[0];
    }

    [RelayCommand]
    private void ApplyPreset(string preset)
    {
        _soundService.PlayClick();
        bool isLite = preset == "Lite";
        bool isUltra = preset == "Ultra";

        _suppressModuleNotifications = true;

        EnableModpacks = SettingsManager.Default.EnableModpacks = true;
        EnableMods = SettingsManager.Default.EnableMods = !isLite;
        EnableServers = SettingsManager.Default.EnableServers = !isLite;
        EnableGallery = SettingsManager.Default.EnableGallery = isUltra;
        EnableAIAgent = SettingsManager.Default.EnableAIAgent = isUltra;

        EnableSubFiles = SettingsManager.Default.EnableSubFiles = !isLite;
        EnableSubFilesBackups = SettingsManager.Default.EnableSubFiles_Backups = !isLite;

        EnableSubInfoBug = SettingsManager.Default.EnableSubInfo_Bug = !isLite;
        EnableSubInfoNews = SettingsManager.Default.EnableSubInfo_News = !isLite;
        EnableSubInfoWiki = SettingsManager.Default.EnableSubInfo_Wiki = !isLite;
        EnableSubInfoGithub = SettingsManager.Default.EnableSubInfo_Github = isUltra;
        EnableSubInfoCredits = SettingsManager.Default.EnableSubInfo_Credits = isUltra;
        EnableSubInfoSupport = SettingsManager.Default.EnableSubInfo_Support = !isLite;

        EnableModChangelog = SettingsManager.Default.EnableMod_Changelog = !isLite;
        EnableModLatestActions = SettingsManager.Default.EnableMod_LatestActions = !isLite;
        EnableModDiscordRPC = SettingsManager.Default.EnableMod_DiscordRPC = !isLite;
        EnableModStatistics = SettingsManager.Default.EnableMod_Statistics = !isLite;

        _suppressModuleNotifications = false;
        NotifyModulesChanged();
    }

    #endregion

    #region Theme Commands & Color Pickers

    private void ApplyCurrentCustomTheme()
    {
        SettingsManager.Default.Section_colour = SectionColor;
        SettingsManager.Default.Background_colour = BackgroundColor;
        SettingsManager.Default.Additional_colour = AdditionalColor;
        SettingsManager.Default.Text_colour = TextColor;
        SettingsManager.Default.Button_colour = ButtonColor;
        SettingsManager.Default.bgImage = BackgroundImagePath;
        Save();

        var config = new MainThemeConfig
        {
            SectionColor = SectionColor,
            BackgroundColor = BackgroundColor,
            AdditionalColor = AdditionalColor,
            TextColor = TextColor,
            ButtonColor = ButtonColor,
            BackgroundImage = BackgroundImagePath
        };
        _themeService.ApplyCustomTheme(config);
    }

    private void SwitchToCustomTheme()
    {
        _suppressThemeChange = true;
        SelectedTheme = "Custom";
        SelectedThemeOption = AvailableThemes.FirstOrDefault(t => t.Key == "Custom");
        _suppressThemeChange = false;
        SettingsManager.Default.Them = "Custom";
        Save();
    }

    [RelayCommand]
    private async Task PickSectionColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(SectionColor);
        if (!string.IsNullOrEmpty(hex))
        {
            SectionColor = hex;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private async Task PickBackgroundColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(BackgroundColor);
        if (!string.IsNullOrEmpty(hex))
        {
            BackgroundColor = hex;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private async Task PickAdditionalColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(AdditionalColor);
        if (!string.IsNullOrEmpty(hex))
        {
            AdditionalColor = hex;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private async Task PickTextColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(TextColor);
        if (!string.IsNullOrEmpty(hex))
        {
            TextColor = hex;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private async Task PickButtonColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(ButtonColor);
        if (!string.IsNullOrEmpty(hex))
        {
            ButtonColor = hex;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private async Task PickBackgroundImageAsync()
    {
        _soundService.PlayClick();
        var file = await _dialogService.OpenFileDialogAsync("Виберіть фон", "Image files (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif");
        if (!string.IsNullOrEmpty(file))
        {
            BackgroundImagePath = file;
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    [RelayCommand]
    private void ResetTheme()
    {
        _soundService.PlayClick();
        _suppressThemeChange = true;
        SelectedTheme = "Dark";
        SelectedThemeOption = AvailableThemes.FirstOrDefault(t => t.Key == "Dark");
        _suppressThemeChange = false;

        SettingsManager.Default.Them = "Dark";
        SettingsManager.Default.bgImage = string.Empty;
        Save();

        _themeService.ApplyPredefinedTheme("Dark");
        SyncThemePropertiesFromSettings();
        _ = MemoryCleaner.FlushMemoryAsync(true);
    }

    private static IClipboard? GetClipboard()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktop?.MainWindow?.Clipboard;
    }

    [RelayCommand]
    private async Task CopyThemeAsync()
    {
        _soundService.PlayClick();
        var config = new MainThemeConfig
        {
            SectionColor = SectionColor,
            BackgroundColor = BackgroundColor,
            AdditionalColor = AdditionalColor,
            TextColor = TextColor,
            ButtonColor = ButtonColor,
            BackgroundImage = BackgroundImagePath
        };
        string code = _themeService.ExportMainTheme(config);
        ThemeCode = code;

        var cb = GetClipboard();
        if (cb != null)
        {
            await cb.SetTextAsync(code);
        }
        _dialogService.ShowMessage(
            LocalizationManager.GetString("ThemesCustomization.CopyThemeSuccess", "Код теми скопійовано!"),
            "Експорт",
            MascotEmotion.Happy);
    }

    [RelayCommand]
    private async Task PasteThemeAsync()
    {
        _soundService.PlayClick();
        string? code = null;
        var cb = GetClipboard();
        if (cb != null)
        {
            code = await cb.TryGetTextAsync();
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            code = ThemeCode;
        }
        if (!string.IsNullOrWhiteSpace(code))
        {
            _themeService.ImportMainTheme(code.Trim());
            SyncThemePropertiesFromSettings();
            SwitchToCustomTheme();
            ApplyCurrentCustomTheme();
        }
    }

    #endregion

    #region Load Screen Commands

    [RelayCommand]
    private async Task PickLoadScreenColorAsync()
    {
        _soundService.PlayClick();
        var hex = await _themeService.PickColorAsync(LoadScreenColor);
        if (!string.IsNullOrEmpty(hex))
        {
            LoadScreenColor = hex;
            SettingsManager.Default.LoadScreenBarColor = hex;
            Save();
        }
    }

    [RelayCommand]
    private async Task PickLoadScreenBgAsync()
    {
        _soundService.PlayClick();
        var file = await _dialogService.OpenFileDialogAsync("Виберіть фон для екрану завантаження", "Зображення (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif");
        if (!string.IsNullOrEmpty(file))
        {
            LoadScreenBackground = file;
            SettingsManager.Default.LoadScreenBackground = file;
            Save();
        }
    }

    [RelayCommand]
    private void EditPhrases()
    {
        _soundService.PlayClick();
        _themeService.HandleEditPhrasesClick();
    }

    [RelayCommand]
    private void ResetLoadScreen()
    {
        _soundService.PlayClick();
        LoadScreenColor = "#00BEFF";
        LoadScreenBackground = string.Empty;
        SettingsManager.Default.LoadScreenBarColor = "#00BEFF";
        SettingsManager.Default.LoadScreenBackground = string.Empty;
        Save();
    }

    [RelayCommand]
    private async Task CopyLoadScreenAsync()
    {
        _soundService.PlayClick();
        var config = new LoadScreenConfig
        {
            LoadScreenColor = LoadScreenColor,
            LoadScreenBg = LoadScreenBackground
        };
        string code = _themeService.ExportLoadScreen(config);
        LoadScreenCode = code;

        var cb = GetClipboard();
        if (cb != null)
        {
            await cb.SetTextAsync(code);
        }
        _dialogService.ShowMessage(
            LocalizationManager.GetString("ThemesCustomization.CopyScreenSuccess", "Код LoadScreen скопійовано!"),
            "Експорт",
            MascotEmotion.Happy);
    }

    [RelayCommand]
    private async Task PasteLoadScreenAsync()
    {
        _soundService.PlayClick();
        string? code = null;
        var cb = GetClipboard();
        if (cb != null)
        {
            code = await cb.TryGetTextAsync();
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            code = LoadScreenCode;
        }
        if (!string.IsNullOrWhiteSpace(code))
        {
            _themeService.ImportLoadScreen(code.Trim());
            LoadScreenColor = string.IsNullOrEmpty(SettingsManager.Default.LoadScreenBarColor) ? "#00BEFF" : SettingsManager.Default.LoadScreenBarColor;
            LoadScreenBackground = SettingsManager.Default.LoadScreenBackground ?? string.Empty;
        }
    }

    #endregion

    #region Update Commands

    [RelayCommand]
    public async Task CheckUpdatesManualAsync()
    {
        if (IsCheckingUpdates) return;
        IsCheckingUpdates = true;
        UpdateStatusText = LocalizationManager.GetString("Settings.CheckingUpdates", "Перевірка оновлень...");
        try
        {
            bool isPrerelease = string.Equals(SettingsManager.Default.UpdateChannel, "Preview", StringComparison.OrdinalIgnoreCase);
            var source = new global::Velopack.Sources.GithubSource("https://github.com/WER-CORE/CL-Win-Edition--Update", null, isPrerelease);
            var mgr = new global::Velopack.UpdateManager(source);

            if (mgr.IsInstalled)
            {
                var info = await mgr.CheckForUpdatesAsync();
                if (info != null && info.TargetFullRelease != null)
                {
                    UpdateStatusText = string.Format(LocalizationManager.GetString("Settings.UpdateFound", "Доступно: v{0}"), info.TargetFullRelease.Version);
                    var updater = new UpdaterWindow();
                    updater.Show();
                    return;
                }
                else
                {
                    UpdateStatusText = LocalizationManager.GetString("Settings.LatestVersionInstalled", "У вас найновіша версія");
                }
            }
            else
            {
                var updateService = new UpdateService();
                var info = await updateService.CheckForUpdateAsync(Secrets.updateInfoUrl);
                string localVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                if (info != null && updateService.IsUpdateAvailable(info.Version, localVer))
                {
                    UpdateStatusText = string.Format(LocalizationManager.GetString("Settings.UpdateFound", "Доступно: v{0}"), info.Version);
                    var updater = new UpdaterWindow();
                    updater.Show();
                    return;
                }
                else
                {
                    UpdateStatusText = LocalizationManager.GetString("Settings.LatestVersionInstalled", "У вас найновіша версія");
                }
            }
        }
        catch (Exception)
        {
            UpdateStatusText = LocalizationManager.GetString("Settings.UpdateCheckError", "Помилка перевірки оновлень");
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    #endregion
}
