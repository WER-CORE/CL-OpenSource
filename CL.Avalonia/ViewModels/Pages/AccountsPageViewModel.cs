using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Identity.Client;
using CL_CLegendary_Launcher_.Avalonia.Services;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;

public partial class AccountsPageViewModel : ObservableObject
{
    private readonly AccountService? _accountService;
    private readonly GameSessionManager? _gameSessionManager;

    public ObservableCollection<ProfileItem> Profiles { get; } = new();

    [ObservableProperty] private ProfileItem? _selectedProfile;

    [ObservableProperty] private string _offlineUsername = string.Empty;
    [ObservableProperty] private string _littleSkinUsername = string.Empty;
    [ObservableProperty] private string _littleSkinPassword = string.Empty;

    [ObservableProperty] private bool _isAddAccountDialogVisible;
    [ObservableProperty] private int _activeAddTab;

    [ObservableProperty] private string _formattedStats = string.Empty;

    public AccountsPageViewModel() : this(
        ResolveService<AccountService>(),
        ResolveService<GameSessionManager>())
    {
    }

    public AccountsPageViewModel(AccountService? accountService, GameSessionManager? gameSessionManager)
    {
        _accountService = accountService;
        _gameSessionManager = gameSessionManager;

        _ = LoadProfilesAsync();
    }

    private static T? ResolveService<T>() where T : class
    {
        var app = Application.Current as App;
        return app?.ServiceProvider?.GetService(typeof(T)) as T
               ?? ServiceLocator.Current?.GetService<T>();
    }

    [RelayCommand]
    public async Task LoadProfilesAsync()
    {
        if (_accountService == null) return;

        var profiles = await Task.Run(() => _accountService.GetProfiles()).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Profiles.Clear();
            if (profiles == null) return;

            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            SelectedProfile = _accountService.CurrentProfile;
        });
    }

    [RelayCommand]
    private async Task SelectProfileAsync(ProfileItem? profile)
    {
        if (profile == null || _accountService == null) return;

        try
        {
            await _accountService.SelectProfileAsync(profile);
            SelectedProfile = profile;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Помилка входу: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileItem? profile)
    {
        if (profile == null || _accountService == null) return;

        var dialogService = ResolveService<IDialogService>();
        if (dialogService != null)
        {
            bool confirm = await dialogService.AskQuestionAsync(
                LocalizationManager.GetString("Accounts.DeleteConfirmMsg", $"Ви дійсно бажаєте видалити профіль {profile.NameAccount}?"),
                LocalizationManager.GetString("Dialogs.Confirm", "Підтвердження"),
                MascotEmotion.Confused);

            if (!confirm) return;
        }

        try
        {
            _accountService.DeleteProfile(profile);
            Profiles.Remove(profile);

            if (SelectedProfile?.UUID == profile.UUID)
            {
                SelectedProfile = _accountService.CurrentProfile;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Не вдалося видалити: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddOfflineAccountAsync()
    {
        if (_accountService == null || string.IsNullOrWhiteSpace(OfflineUsername)) return;

        var newProfile = await _accountService.AddOfflineAccountAsync(OfflineUsername.Trim());
        if (newProfile != null)
        {
            OfflineUsername = string.Empty;
            IsAddAccountDialogVisible = false;
            await LoadProfilesAsync();
        }
    }

    [RelayCommand]
    private async Task AddMicrosoftAccountAsync()
    {
        if (_accountService == null) return;

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

        var newProfile = await _accountService.AddMicrosoftAccountAsync(deviceCodeCallback);
        if (newProfile != null)
        {
            IsAddAccountDialogVisible = false;
            await LoadProfilesAsync();
        }
    }

    [RelayCommand]
    private async Task AddLittleSkinAccountAsync()
    {
        if (_accountService == null ||
            string.IsNullOrWhiteSpace(LittleSkinUsername) ||
            string.IsNullOrWhiteSpace(LittleSkinPassword))
            return;

        var newProfile = await _accountService.AddLittleSkinAccountAsync(LittleSkinUsername.Trim(), LittleSkinPassword);
        if (newProfile != null)
        {
            LittleSkinUsername = string.Empty;
            LittleSkinPassword = string.Empty;
            IsAddAccountDialogVisible = false;
            await LoadProfilesAsync();
        }
    }

    [RelayCommand]
    private void ShowAddAccountDialog(string? tabIndex)
    {
        if (int.TryParse(tabIndex, out int index))
        {
            ActiveAddTab = index;
            IsAddAccountDialogVisible = true;
        }
    }

    [RelayCommand]
    private void LoadStats()
    {
        if (_gameSessionManager != null)
        {
            FormattedStats = _gameSessionManager.GetFormattedStats();
        }
    }
}
