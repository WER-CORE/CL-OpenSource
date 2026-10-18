using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels;

public partial class CL_MainViewModel : ObservableObject
{
    [ObservableProperty] private ObservableObject? _currentPage;
    [ObservableProperty] private double _uiScale = 1.0;
    [ObservableProperty] private WindowState _windowState = WindowState.Normal;

    public ObservableCollection<ProfileItem> Profiles { get; } = new();
    public ObservableCollection<AccountCardViewModel> AccountCards { get; } = new();
    [ObservableProperty] private AccountCardViewModel? _selectedAccountCard;
    [ObservableProperty] private ProfileItem? _currentProfile;
    [ObservableProperty] private string _currentAccountName = "Відсутній акаунт";
    [ObservableProperty] private Bitmap? _currentAccountAvatar;

    partial void OnSelectedAccountCardChanged(AccountCardViewModel? value)
    {
        if (value?.Profile != null && value.Profile != CurrentProfile)
        {
            _ = SelectProfileAsync(value.Profile);
        }
    }

    public event Action<string>? NavigationRequested;

    public VersionService? VersionService { get; private set; }
    public NewsService? NewsService { get; private set; }
    public ProfileManagerService? ProfileManagerService { get; private set; }
    public AccountService? AccountService { get; private set; }
    public GameSessionManager? GameSessionManager { get; private set; }
    public ServerListService? ServerListService { get; private set; }
    public LastActionService? LastActionService { get; private set; }
    public GameLaunchService? GameLaunchService { get; private set; }
    public ModDownloadService? ModDownloadService { get; private set; }
    public ModpackService? ModpackService { get; private set; }
    public IDialogService? DialogService { get; private set; }

    public CL_MainViewModel()
    {
        EnsureDefaultLauncherPath();
        InitializeServices();
    }

    private void InitializeServices()
    {
        var sp = (Application.Current as App)?.ServiceProvider;

        string launcherPath = SettingsManager.Default.PathLacunher;
        if (string.IsNullOrWhiteSpace(launcherPath))
        {
            launcherPath = PlatformPaths.DefaultLauncherPath();
        }

        ProfileManagerService = Resolve<ProfileManagerService>(sp) ?? new ProfileManagerService();
        AccountService = Resolve<AccountService>(sp) ?? new AccountService(ProfileManagerService);
        GameSessionManager = Resolve<GameSessionManager>(sp) ?? new GameSessionManager();
        LastActionService = Resolve<LastActionService>(sp) ?? new LastActionService();
        GameLaunchService = Resolve<GameLaunchService>(sp) ?? new GameLaunchService(GameSessionManager, LastActionService);

        VersionService = Resolve<VersionService>(sp) ?? new VersionService(launcherPath);
        NewsService = Resolve<NewsService>(sp) ?? new NewsService();
        ServerListService = Resolve<ServerListService>(sp) ?? new ServerListService();
        ModDownloadService = Resolve<ModDownloadService>(sp) ?? new ModDownloadService();
        ModpackService = Resolve<ModpackService>(sp) ?? new ModpackService(GameSessionManager, GameLaunchService, ModDownloadService);
        DialogService = Resolve<IDialogService>(sp);

        if (AccountService != null)
        {
            AccountService.AccountChanged -= OnGlobalAccountChanged;
            AccountService.AccountChanged += OnGlobalAccountChanged;

            if (AccountService.CurrentProfile != null)
            {
                _ = ApplyProfileToUIAsync(AccountService.CurrentProfile);
            }
        }
    }

    private void OnGlobalAccountChanged(ProfileItem? profile, CmlLib.Core.Auth.MSession? session)
    {
        if (profile != null)
        {
            _ = ApplyProfileToUIAsync(profile);
        }
        else
        {
            Dispatcher.UIThread.Post(ResetCurrentAccountState);
        }
    }

    private static T? Resolve<T>(IServiceProvider? sp) where T : class
    {
        try
        {
            var service = sp?.GetService<T>();
            if (service != null) return service;
        }
        catch { }

        try
        {
            return ServiceLocator.Current?.GetService<T>();
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureDefaultLauncherPath()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SettingsManager.Default.PathLacunher))
            {
                string defaultPath = PlatformPaths.DefaultLauncherPath();
                if (!Directory.Exists(defaultPath))
                {
                    Directory.CreateDirectory(defaultPath);
                }
                SettingsManager.Default.PathLacunher = defaultPath;
                SettingsManager.Save();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CL_MainViewModel] Path init error: {ex.Message}");
        }
    }

    #region Управління Профілями Акаунтів

    public async Task<List<ProfileItem>> LoadProfilesAsync()
    {
        if (AccountService == null) return new List<ProfileItem>();

        var loaded = await Task.Run(() => AccountService.GetProfiles()).ConfigureAwait(false);
        var result = loaded ?? new List<ProfileItem>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Profiles.Clear();
            AccountCards.Clear();

            int savedIndex = SettingsManager.Default.SelectIndexAccount;
            if (savedIndex < 0 || savedIndex >= result.Count)
                savedIndex = 0;

            AccountCardViewModel? targetCard = null;

            for (int i = 0; i < result.Count; i++)
            {
                var p = result[i];
                Profiles.Add(p);
                var card = new AccountCardViewModel(
                    p,
                    i,
                    prof => _ = DeleteProfileAsync(prof),
                    prof => _ = SelectProfileAsync(prof));
                AccountCards.Add(card);

                if (i == savedIndex)
                {
                    targetCard = card;
                }
            }

            if (targetCard != null)
            {
                SelectedAccountCard = targetCard;
            }

            if (result.Count == 0)
            {
                ResetCurrentAccountState();
            }
        });

        return result;
    }

    public async Task SelectProfileAsync(ProfileItem profile)
    {
        if (profile == null) return;

        SelectedAccountCard = AccountCards.FirstOrDefault(c => c.Profile == profile || c.Uuid == profile.UUID);

        await ApplyProfileToUIAsync(profile);

        if (AccountService == null) return;

        try
        {
            await AccountService.SelectProfileAsync(profile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SelectProfileAsync] Auth warning: {ex.Message}");
            if (DialogService != null)
            {
                await DialogService.ShowMessageAsync(
                    ex.Message,
                    LocalizationManager.GetString("Accounts.LoginLittleSkinErrorTitle", "Помилка входу"),
                    MascotEmotion.Sad);
            }
        }
    }

    private async Task ApplyProfileToUIAsync(ProfileItem profile)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CurrentProfile = profile;
            CurrentAccountName = profile.NameAccount;
            UpdateSavedIndex(profile);
        });

        if (string.IsNullOrEmpty(profile.ImageUrl))
        {
            await Dispatcher.UIThread.InvokeAsync(() => CurrentAccountAvatar = null);
        }
        else
        {
            try
            {
                var avatar = await AvaloniaImageHelper.LoadOptimizedImageAsync(profile.ImageUrl, 64).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => CurrentAccountAvatar = avatar);
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() => CurrentAccountAvatar = null);
            }
        }
    }

    public async Task<bool> DeleteProfileAsync(ProfileItem profile)
    {
        if (AccountService == null) return false;

        if (DialogService != null)
        {
            bool confirmed = await DialogService.AskQuestionAsync(
                LocalizationManager.GetString("Accounts.DeleteConfirmMsg", $"Ви дійсно бажаєте видалити профіль {profile.NameAccount}?"),
                LocalizationManager.GetString("Dialogs.Confirm", "Підтвердження"),
                MascotEmotion.Confused);

            if (!confirmed) return false;
        }

        try
        {
            AccountService.DeleteProfile(profile);
            Profiles.Remove(profile);

            var cardToRemove = AccountCards.FirstOrDefault(c => c.Profile == profile || c.Uuid == profile.UUID);
            if (cardToRemove != null)
            {
                AccountCards.Remove(cardToRemove);
            }

            if (CurrentProfile?.UUID == profile.UUID)
            {
                if (Profiles.Count > 0)
                {
                    await SelectProfileAsync(Profiles[0]);
                }
                else
                {
                    ResetCurrentAccountState();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            if (DialogService != null)
            {
                await DialogService.ShowMessageAsync(
                    string.Format(LocalizationManager.GetString("Accounts.DeleteFailed", "Не вдалося видалити: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
            return false;
        }
    }

    public async Task<ProfileItem?> AddOfflineAccountAsync(string? username)
    {
        if (AccountService == null || string.IsNullOrWhiteSpace(username)) return null;
        return await AccountService.AddOfflineAccountAsync(username.Trim());
    }

    public async Task<ProfileItem?> AddLittleSkinAccountAsync(string? login, string? password)
    {
        if (AccountService == null || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            return null;
        return await AccountService.AddLittleSkinAccountAsync(login.Trim(), password);
    }

    public async Task<ProfileItem?> AddMicrosoftAccountAsync()
    {
        if (AccountService == null) return null;

        Func<DeviceCodeResult, Task>? deviceCodeCallback = null;
        if (!OperatingSystem.IsWindows())
        {
            deviceCodeCallback = result =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaDialogService.Show(
                        string.Format(LocalizationManager.GetString("Accounts.DeviceCodeHint", "Відкрийте {0} та введіть код {1}"), result.VerificationUrl, result.UserCode),
                        LocalizationManager.GetString("Accounts.NeedAuth", "Авторизація Microsoft"),
                        MascotEmotion.Alert);
                });
                WebHelper.OpenUrl(result.VerificationUrl);
                return Task.CompletedTask;
            };
        }

        var profile = await AccountService.AddMicrosoftAccountAsync(deviceCodeCallback);
        if (profile != null)
        {
            SettingsManager.Default.MicrosoftAccount = true;
            SettingsManager.Save();
        }
        return profile;
    }

    private void UpdateSavedIndex(ProfileItem currentProfile)
    {
        int index = -1;
        for (int i = 0; i < Profiles.Count; i++)
        {
            if (Profiles[i].UUID == currentProfile.UUID)
            {
                index = i;
                break;
            }
        }

        if (index != -1 && index != SettingsManager.Default.SelectIndexAccount)
        {
            SettingsManager.Default.SelectIndexAccount = index;
            SettingsManager.Save();
        }
    }

    private void ResetCurrentAccountState()
    {
        CurrentProfile = null;
        SelectedAccountCard = null;
        CurrentAccountName = LocalizationManager.GetString("Accounts.NoAccount", "Відсутній акаунт");
        CurrentAccountAvatar = null;
        SettingsManager.Default.SelectIndexAccount = -1;
        SettingsManager.Save();
    }

    public string GetFormattedStats() =>
        GameSessionManager?.GetFormattedStats() ?? string.Empty;

    #endregion

    public LaunchConfiguration GetLaunchConfiguration(string? serverIp = null, short serverPort = 0, string? serverName = null)
    {
        var settings = SettingsManager.Default;
        int ram = settings.OP > 0 ? settings.OP : 2048;
        int width = settings.width > 0 ? settings.width : 854;
        int height = settings.height > 0 ? settings.height : 480;

        return new LaunchConfiguration
        {
            IsOffline = settings.OfflineModLauncher || !NetworkInterface.GetIsNetworkAvailable(),
            MinimumRamMb = ram,
            MaximumRamMb = ram,
            ScreenWidth = width,
            ScreenHeight = height,
            IsFullscreen = settings.FullScreen,
            Session = AccountService?.CurrentSession,
            AccountType = AccountService?.CurrentProfile?.TypeAccount ?? AccountType.Offline,
            ServerIp = serverIp,
            ServerPort = serverPort,
            ServerName = serverName
        };
    }

    [RelayCommand]
    private void Navigate(string? pageName)
    {
        if (!string.IsNullOrWhiteSpace(pageName))
        {
            NavigationRequested?.Invoke(pageName);
        }
    }

    [RelayCommand]
    private void ToggleFullScreen()
    {
        if (WindowState == WindowState.Normal)
        {
            WindowState = WindowState.FullScreen;
            double savedScale = SettingsManager.Default.UIScale;
            UiScale = savedScale > 0.1 ? savedScale : 1.0;
        }
        else
        {
            WindowState = WindowState.Normal;
            UiScale = 1.0;
        }
    }

    public Task OnWindowMinimizedAsync()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
        return Task.CompletedTask;
    }
}
