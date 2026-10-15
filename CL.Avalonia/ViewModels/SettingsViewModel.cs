using Avalonia;
using Avalonia.Styling;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const int MinRamMb = 1024;

        public SettingsViewModel()
        {
            var settings = SettingsManager.Default;

            MaxRamMb = Math.Max(MinRamMb, (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)));
            _ramMb = Math.Clamp(settings.OP, MinRamMb, MaxRamMb);
            _screenWidth = settings.width;
            _screenHeight = settings.height;
            _fullScreen = settings.FullScreen;
            _closeLauncher = settings.CloseLaucnher;
            _showGameLog = settings.EnableLog;
            _offlineMode = settings.OfflineModLauncher;
            _isLightTheme = settings.Them == "Light";
        }

        public int MaxRamMb { get; }

        public string GamePath => SettingsManager.Default.PathLacunher;

        [ObservableProperty]
        private int _ramMb;

        [ObservableProperty]
        private int _screenWidth;

        [ObservableProperty]
        private int _screenHeight;

        [ObservableProperty]
        private bool _fullScreen;

        [ObservableProperty]
        private bool _closeLauncher;

        [ObservableProperty]
        private bool _showGameLog;

        [ObservableProperty]
        private bool _offlineMode;

        [ObservableProperty]
        private bool _isLightTheme;

        partial void OnRamMbChanged(int value) => Save(s => s.OP = value);

        partial void OnScreenWidthChanged(int value) => Save(s => s.width = value);

        partial void OnScreenHeightChanged(int value) => Save(s => s.height = value);

        partial void OnFullScreenChanged(bool value) => Save(s => s.FullScreen = value);

        partial void OnCloseLauncherChanged(bool value) => Save(s => s.CloseLaucnher = value);

        partial void OnShowGameLogChanged(bool value) => Save(s => s.EnableLog = value);

        partial void OnOfflineModeChanged(bool value) => Save(s => s.OfflineModLauncher = value);

        partial void OnIsLightThemeChanged(bool value)
        {
            Save(s => s.Them = value ? "Light" : "Dark");
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = IsLightTheme ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        [RelayCommand]
        private void OpenGameFolder()
        {
            Directory.CreateDirectory(GamePath);
            UrlOpener.Open(GamePath);
        }

        private static void Save(Action<CL.Core.Models.AppSettings> change)
        {
            change(SettingsManager.Default);
            SettingsManager.Save();
        }
    }
}
