using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Identity.Client;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly AccountService _accountService;
        private readonly IDialogService _dialogs;

        public AccountsViewModel(AccountService accountService, IDialogService dialogs)
        {
            _accountService = accountService;
            _dialogs = dialogs;
        }

        public ObservableCollection<ProfileItem> Profiles { get; } = new ObservableCollection<ProfileItem>();

        [ObservableProperty]
        private ProfileItem? _activeProfile;

        // Виділення у списку: клік по акаунту робить його активним, а невдала спроба повертає виділення назад.
        [ObservableProperty]
        private ProfileItem? _selectedProfile;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddOfflineCommand))]
        private string _offlineNickname = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddLittleSkinCommand))]
        private string _littleSkinLogin = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddLittleSkinCommand))]
        private string _littleSkinPassword = string.Empty;

        [ObservableProperty]
        private string _deviceCodeText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public async Task LoadAsync()
        {
            await ReloadProfilesAsync();

            int savedIndex = SettingsManager.Default.SelectIndexAccount;
            if (savedIndex >= 0 && savedIndex < Profiles.Count)
                await SelectAsync(Profiles[savedIndex]);
        }

        private Task SelectAsync(ProfileItem profile) => RunBusyAsync(() => ActivateAsync(profile));

        partial void OnActiveProfileChanged(ProfileItem? value) => SelectedProfile = value;

        partial void OnSelectedProfileChanged(ProfileItem? value)
        {
            if (value != null && value != ActiveProfile)
                _ = SelectAsync(value);
        }

        [RelayCommand]
        private async Task DeleteAsync(ProfileItem profile)
        {
            bool confirmed = await _dialogs.AskQuestionAsync(
                string.Format(LocalizationManager.GetString("Dialogs.DeleteConfirm", "Видалити {0}?"), profile.NameAccount),
                LocalizationManager.GetString("Dialogs.DeleteConfirmTitle", "Видалення"),
                MascotEmotion.Alert);

            if (!confirmed)
                return;

            _accountService.DeleteProfile(profile);
            Profiles.Remove(profile);

            if (ActiveProfile == profile)
                ActiveProfile = null;

            SaveActiveIndex();
        }

        private bool CanAddOffline() => !string.IsNullOrWhiteSpace(OfflineNickname);

        [RelayCommand(CanExecute = nameof(CanAddOffline))]
        private async Task AddOfflineAsync()
        {
            await AddAsync(() => _accountService.AddOfflineAccountAsync(OfflineNickname.Trim()));
            OfflineNickname = string.Empty;
        }

        [RelayCommand]
        private Task AddMicrosoftAsync() => AddAsync(() => _accountService.AddMicrosoftAccountAsync(OnDeviceCode));

        private bool CanAddLittleSkin() => !string.IsNullOrWhiteSpace(LittleSkinLogin) && !string.IsNullOrWhiteSpace(LittleSkinPassword);

        [RelayCommand(CanExecute = nameof(CanAddLittleSkin))]
        private async Task AddLittleSkinAsync()
        {
            await AddAsync(() => _accountService.AddLittleSkinAccountAsync(LittleSkinLogin.Trim(), LittleSkinPassword));
            LittleSkinPassword = string.Empty;
        }

        // Інтерактивне вікно MSAL є лише на Windows, тому вхід іде через device code:
        // користувач вводить код у браузері, а ми чекаємо завершення.
        private Task OnDeviceCode(DeviceCodeResult result)
        {
            DeviceCodeText = string.Format(
                LocalizationManager.GetString("Accounts.DeviceCodeHint", "Відкрийте {0} та введіть код {1}"),
                result.VerificationUrl, result.UserCode);

            WebHelper.OpenUrl(result.VerificationUrl);
            return Task.CompletedTask;
        }

        private Task AddAsync(Func<Task<ProfileItem>> add) => RunBusyAsync(async () =>
        {
            var added = await add();
            if (added == null)
                return;

            await ReloadProfilesAsync();

            var profile = Profiles.FirstOrDefault(p => p.UUID == added.UUID && p.NameAccount == added.NameAccount);
            if (profile != null)
                await ActivateAsync(profile);
        });

        private async Task ActivateAsync(ProfileItem profile)
        {
            if (!await _accountService.SelectProfileAsync(profile))
            {
                SelectedProfile = ActiveProfile;
                return;
            }

            ActiveProfile = profile;
            SaveActiveIndex();
        }

        private async Task ReloadProfilesAsync()
        {
            var profiles = await Task.Run(() => _accountService.GetProfiles());

            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            if (ActiveProfile != null)
                ActiveProfile = Profiles.FirstOrDefault(p => p.UUID == ActiveProfile.UUID && p.NameAccount == ActiveProfile.NameAccount);
        }

        private void SaveActiveIndex()
        {
            int index = ActiveProfile == null ? -1 : Profiles.IndexOf(ActiveProfile);
            if (index == SettingsManager.Default.SelectIndexAccount)
                return;

            SettingsManager.Default.SelectIndexAccount = index;
            SettingsManager.Save();
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            IsBusy = true;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowMessageAsync(
                    string.Format(LocalizationManager.GetString("Dialogs.ErrorDesc", "Помилка: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
            finally
            {
                IsBusy = false;
                DeviceCodeText = string.Empty;
            }
        }
    }
}
