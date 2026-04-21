using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_
{
    public partial class CL_Main_
    {
        public async Task LoadProfilesAsync()
        {
            ListAccount.Items.Clear();

            var profiles = await Task.Run(() => _accountService.GetProfiles());

            if (profiles == null || profiles.Count == 0)
            {
                SettingsManager.Default.SelectIndexAccount = -1;
                SettingsManager.Save();
                return;
            }

            foreach (var profile in profiles)
            {
                int index = profiles.IndexOf(profile);

                var uiItem = LauncherUIFactory.CreateAccountControl(
                    profile,
                    index,
                    OnDeleteProfileClicked,
                    OnSelectProfileClicked
                );

                uiItem.Tag = profile;
                ListAccount.Items.Add(uiItem);
            }

            int savedIndex = SettingsManager.Default.SelectIndexAccount;
            if (savedIndex >= 0 && savedIndex < profiles.Count)
            {
                OnSelectProfileClicked(profiles[savedIndex]);
            }
        }

        private void IconAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                SoundManager.Click();

                if (PanelManegerAccount.Visibility == Visibility.Visible)
                {
                    AnimationService.AnimateRotation(CheckMarkAccount, 0);
                    AnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20);

                    ListAccount.Items?.Clear();
                }
                else
                {
                    AnimationService.AnimateRotation(CheckMarkAccount, 180);
                    AnimationService.AnimatePageTransition(PanelManegerAccount, -20);

                    if (PanelListStats.Visibility == Visibility.Visible)
                    {
                        AnimationService.FadeOut(PanelListStats, 0.3);
                    }

                    _ = LoadProfilesAsync();
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.MenuError", "Помилка меню акаунтів: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        private void OnDeleteProfileClicked(ProfileItem profile)
        {
            var result = MascotMessageBox.Ask(
                string.Format(LocalizationManager.GetString("Dialogs.DeleteConfirm", "Видалити {0}?"), profile.NameAccount),
                LocalizationManager.GetString("Dialogs.DeleteConfirmTitle", "Видалення"), MascotEmotion.Alert);

            if (result != true) return;

            try
            {
                _accountService.DeleteProfile(profile);

                var itemToRemove = ListAccount.Items.OfType<FrameworkElement>()
                                                    .FirstOrDefault(x => (x.Tag as ProfileItem)?.UUID == profile.UUID);

                if (itemToRemove != null)
                {
                    ListAccount.Items.Remove(itemToRemove);
                }

                if (NameNik.Text == profile.NameAccount)
                {
                    ResetCurrentAccountUI();
                }
                else
                {
                    UpdateSavedIndex();
                }

                NotificationService.ShowNotification(
                    LocalizationManager.GetString("Dialogs.Success", "Успіх!"),
                    LocalizationManager.GetString("Accounts.ProfileDeleted", "Профіль стерто."), SnackbarPresenter, 3);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.DeleteFailed", "Не вдалося видалити: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        private void ResetCurrentAccountUI()
        {
            NameNik.Text = LocalizationManager.GetString("Accounts.NoAccount", "Відсутній акаунт");
            IconAccount.Source = null;
            selectAccountNow = 0;

            SettingsManager.Default.SelectIndexAccount = -1;
            SettingsManager.Save();
        }

        private async void OnSelectProfileClicked(ProfileItem profile)
        {
            try
            {
                SoundManager.Click();
                await _accountService.SelectProfileAsync(profile);

                NameNik.Text = profile.NameAccount;
                selectAccountNow = profile.TypeAccount;

                SetAccountImage(profile.ImageUrl);

                UpdateSavedIndex(profile);

                session = _accountService.CurrentSession;
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    ex.Message,
                    LocalizationManager.GetString("Accounts.LoginLittleSkinErrorTitle", "Помилка входу"),
                    MascotEmotion.Sad);
            }
        }

        private void SetAccountImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                IconAccount.Source = null;
                return;
            }

            try
            {
                IconAccount.Source = ImageHelper.LoadOptimizedImage(imageUrl, 64);
            }
            catch
            {
                IconAccount.Source = null;
            }
        }

        private void UpdateSavedIndex(ProfileItem currentProfile = null)
        {
            var allProfiles = _accountService.GetProfiles();

            if (currentProfile == null)
            {
                return;
            }

            int index = allProfiles.FindIndex(p => p.UUID == currentProfile.UUID);
            if (index != SettingsManager.Default.SelectIndexAccount)
            {
                SettingsManager.Default.SelectIndexAccount = index;
                SettingsManager.Save();
            }
        }

        private async void CreateAccount_Offline_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            if (string.IsNullOrWhiteSpace(NameNikManeger.Text)) return;

            try
            {
                await _accountService.AddOfflineAccountAsync(NameNikManeger.Text);
                NameNikManeger.Text = null;

                CloseAccountSelectionUI();

                if (PanelManegerAccount.Visibility == Visibility.Visible)
                    await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.ProfileCreateError", "Не вдалося створити профіль: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        private async void MicrosoftLoginButton_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            try
            {
                CloseAccountSelectionUI();

                await _accountService.AddMicrosoftAccountAsync();

                SettingsManager.Default.MicrosoftAccount = true;
                SettingsManager.Save();

                await LoadProfilesAsync();
                MascotMessageBox.Show(
                    LocalizationManager.GetString("Accounts.LoginSuccess", "Вхід успішний!"),
                    LocalizationManager.GetString("Accounts.LoginSuccessTitle", "Ура"),
                    MascotEmotion.Happy);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    ex.Message,
                    LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                    MascotEmotion.Sad);
            }
        }

        private async void LoginAccountLittleSkin_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            try
            {
                await _accountService.AddLittleSkinAccountAsync(Login_LittleSkin.Text, PasswordLittleSkin.Password);

                Login_LittleSkin.Text = null;
                PasswordLittleSkin.Password = null;

                CloseAccountSelectionUI();
                await LoadProfilesAsync();
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.LittleSkinError", "Ех, біда! Не вдалося підключитися до LittleSkin.\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Accounts.LittleSkinErrorTitle", "Помилка LittleSkin"),
                    MascotEmotion.Sad
                );
            }
        }

        private void CloseAccountSelectionUI()
        {
            AnimationService.FadeOut(GridOfflineMode, 0.2);
            AnimationService.FadeOut(GridOnlineMode, 0.2);
            AnimationService.FadeOut(GridFormAccountAdd, 0.2);
            AnimationService.FadeOut(GridSelectAccountType, 0.2);
            AnimationService.FadeOut(GridLittleSkinMode, 0.2);
        }

        private void AddProfile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            AnimationService.AnimatePageTransition(GridFormAccountAdd);
            AnimationService.AnimatePageTransition(GridOfflineMode);
            AnimationService.AnimatePageTransition(GridSelectAccountType);
        }

        private void GirdFormAccountAdd_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            CloseAccountSelectionUI();

            if (SelectCreatePackMinecraft.Visibility == Visibility.Visible)
            {
                AnimationService.AnimatePageTransitionExit(SelectCreatePackMinecraft);
                AnimationService.AnimatePageTransitionExit(GridFormAccountAdd);
            }
        }

        private void MicrosoftAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            SwitchAccountAddTab(GridOnlineMode);
        }

        private void OfflineAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            SwitchAccountAddTab(GridOfflineMode);
        }

        private void LittleSkinAccount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            SwitchAccountAddTab(GridLittleSkinMode);
        }

        private async void SwitchAccountAddTab(UIElement targetGrid)
        {
            AnimationService.AnimatePageTransitionExit(GridLittleSkinMode, default, 0.2);
            AnimationService.AnimatePageTransitionExit(GridOfflineMode, default, 0.2);
            AnimationService.AnimatePageTransitionExit(GridOnlineMode, default, 0.2);

            await Task.Delay(300);

            AnimationService.AnimatePageTransition(targetGrid);
        }

        private void StatsTextOpen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            TextStatsGameMinecraft.Text = _gameSessionManager.GetFormattedStats();

            AnimationService.AnimatePageTransitionExit(PanelManegerAccount, -20, 0.2);
            IconRotateTransform.Angle = 0;

            AnimationService.AnimatePageTransition(PanelListStats, 20);
        }
    }
}