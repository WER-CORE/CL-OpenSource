using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public record VersionEntry(string Name, bool IsInstalled);

    public partial class HomeViewModel : ObservableObject
    {
        // Значення LastSelectedType, які пише й WPF-клієнт.
        private const int VanillaType = 1;
        private const int OptifineType = 5;

        private readonly GameLaunchService _gameLaunchService;
        private readonly AccountService _accountService;
        private readonly IDialogService _dialogs;

        private List<VersionEntry> _allVersions = new List<VersionEntry>();
        private int _loadGeneration;

        public HomeViewModel(GameLaunchService gameLaunchService, AccountService accountService, AccountsViewModel accounts, IDialogService dialogs)
        {
            _gameLaunchService = gameLaunchService;
            _accountService = accountService;
            _dialogs = dialogs;
            Accounts = accounts;

            _isOptifine = SettingsManager.Default.LastSelectedType == OptifineType;
            _playButtonText = LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ");
        }

        public AccountsViewModel Accounts { get; }

        public ObservableCollection<VersionEntry> Versions { get; } = new ObservableCollection<VersionEntry>();

        public ObservableCollection<string> OptifineVersions { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private bool _isOptifine;

        [ObservableProperty]
        private bool _showSnapshots;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isLoadingVersions;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private VersionEntry? _selectedVersion;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private string? _selectedOptifineVersion;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        private bool _isLaunching;

        [ObservableProperty]
        private string _playButtonText;

        partial void OnIsOptifineChanged(bool value) => _ = LoadVersionsAsync();

        partial void OnShowSnapshotsChanged(bool value) => _ = LoadVersionsAsync();

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        partial void OnSelectedVersionChanged(VersionEntry? value)
        {
            UpdatePlayButtonText();
            _ = LoadOptifineVersionsAsync(value);
        }

        partial void OnSelectedOptifineVersionChanged(string? value) => UpdatePlayButtonText();

        [RelayCommand]
        public async Task LoadVersionsAsync()
        {
            int generation = ++_loadGeneration;
            bool optifine = IsOptifine;
            IsLoadingVersions = true;

            try
            {
                var service = new VersionService(SettingsManager.Default.PathLacunher);
                var installed = new HashSet<string>(service.GetLocalVersions(), StringComparer.OrdinalIgnoreCase);
                List<string> names;

                if (WebHelper.IsOffline())
                {
                    names = installed
                        .Where(v => optifine
                            ? v.Contains("optifine", StringComparison.OrdinalIgnoreCase)
                            : !VersionService.IsModdedVersionName(v))
                        .ToList();
                }
                else
                {
                    names = await service.GetFilteredVersionsAsync(string.Empty, true, ShowSnapshots && !optifine, false, false);

                    if (optifine)
                    {
                        var supported = await service.GetOptifineSupportedVersionsAsync();
                        names = names.Where(supported.Contains).ToList();
                    }
                }

                if (generation != _loadGeneration)
                    return;

                _allVersions = names.Select(n => new VersionEntry(n, installed.Contains(n))).ToList();
                ApplyFilter();

                string last = SettingsManager.Default.LastSelectedVersion;
                SelectedVersion = Versions.FirstOrDefault(v => v.Name == last) ?? Versions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowMessageAsync(
                    string.Format(LocalizationManager.GetString("Dialogs.ErrorDesc", "Помилка: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка версій"),
                    MascotEmotion.Sad);
            }
            finally
            {
                if (generation == _loadGeneration)
                    IsLoadingVersions = false;
            }
        }

        private bool CanPlay() =>
            !IsLaunching && SelectedVersion != null && (!IsOptifine || IsPrepackedOptifine(SelectedVersion) || SelectedOptifineVersion != null);

        [RelayCommand(CanExecute = nameof(CanPlay))]
        private async Task PlayAsync()
        {
            var session = _accountService.CurrentSession;
            var profile = _accountService.CurrentProfile;

            if (session == null || profile == null)
            {
                await _dialogs.ShowMessageAsync(
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedDesc", "Агов! Будь ласка, оберіть акаунт перед початком гри!"),
                    LocalizationManager.GetString("GameLaunch.NoAccountSelectedTitle", "Акаунт не обраний"),
                    MascotEmotion.Alert);
                return;
            }

            string version = SelectedVersion!.Name;
            string optifineVersion = IsOptifine ? SelectedOptifineVersion ?? string.Empty : string.Empty;

            SettingsManager.Default.LastSelectedVersion = version;
            SettingsManager.Default.LastSelectedModVersion = optifineVersion;
            SettingsManager.Default.LastSelectedType = IsOptifine ? OptifineType : VanillaType;
            SettingsManager.Save();

            var config = LaunchConfiguration.FromSettings();
            config.Session = session;
            config.AccountType = profile.TypeAccount;

            await _gameLaunchService.LaunchGameAsync(
                IsOptifine ? LoaderType.Optifine : LoaderType.Vanilla,
                version,
                optifineVersion,
                config);

            await LoadVersionsAsync();
        }

        private async Task LoadOptifineVersionsAsync(VersionEntry? version)
        {
            OptifineVersions.Clear();
            SelectedOptifineVersion = null;

            if (!IsOptifine || version == null || IsPrepackedOptifine(version) || WebHelper.IsOffline())
                return;

            try
            {
                var service = new VersionService(SettingsManager.Default.PathLacunher);
                var list = await service.GetOptifineVersionsAsync(version.Name);

                if (SelectedVersion != version)
                    return;

                foreach (var item in list)
                    OptifineVersions.Add(item);

                string last = SettingsManager.Default.LastSelectedModVersion;
                SelectedOptifineVersion = OptifineVersions.FirstOrDefault(v => v == last) ?? OptifineVersions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowMessageAsync(
                    string.Format(LocalizationManager.GetString("Dialogs.ErrorDesc", "Помилка: {0}"), ex.Message),
                    LocalizationManager.GetString("GameLaunch.OptifineErrorTitle", "Помилка Optifine"),
                    MascotEmotion.Sad);
            }
        }

        // Встановлена збірка OptiFine вже містить версію в назві і не потребує вибору.
        private static bool IsPrepackedOptifine(VersionEntry version)
            => version.Name.Contains("optifine", StringComparison.OrdinalIgnoreCase);

        private void ApplyFilter()
        {
            var selected = SelectedVersion;
            string search = SearchText.Trim();

            Versions.Clear();
            foreach (var entry in _allVersions.Where(v => search.Length == 0 || v.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                Versions.Add(entry);

            if (selected != null && Versions.Contains(selected))
                SelectedVersion = selected;
        }

        private void UpdatePlayButtonText()
        {
            if (IsLaunching)
                return;

            string? label = IsOptifine && !string.IsNullOrEmpty(SelectedOptifineVersion)
                ? SelectedOptifineVersion
                : SelectedVersion?.Name;

            PlayButtonText = label == null
                ? LocalizationManager.GetString("GameLaunch.PlayBtnSelect", "ОБЕРІТЬ ВЕРСІЮ")
                : string.Format(LocalizationManager.GetString("GameLaunch.PlayBtnPlayIn", "ГРАТИ В ({0})"), label);
        }
    }
}
