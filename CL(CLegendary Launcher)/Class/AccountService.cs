using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using MojangAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.XboxLive;

namespace CL_CLegendary_Launcher_.Class
{
    public class AccountService
    {
        private readonly ProfileManagerService _profileManager;
        private readonly JELoginHandler _loginHandler;

        public MSession CurrentSession { get; private set; }
        public ProfileItem CurrentProfile { get; private set; }

        public AccountService(ProfileManagerService profileManager)
        {
            _profileManager = profileManager;
            _loginHandler = JELoginHandlerBuilder.BuildDefault();
        }

        public List<ProfileItem> GetProfiles()
        {
            return _profileManager.LoadProfiles();
        }

        public async Task<bool> SelectProfileAsync(ProfileItem profile)
        {
            try
            {
                if (profile.TypeAccount == AccountType.Microsoft)
                {
                    bool isTokenValid = false;
                    try
                    {
                        var mojangApi = new Mojang(new HttpClient());
                        isTokenValid = await mojangApi.CheckGameOwnership(profile.AccessToken);
                    }
                    catch { }

                    if (isTokenValid)
                    {
                        CurrentSession = new MSession
                        {
                            Username = profile.NameAccount,
                            UUID = profile.UUID,
                            AccessToken = profile.AccessToken
                        };
                    }
                    else
                    {
                        bool isRefreshed = await RefreshMicrosoftSessionAsync(profile);
                        if (!isRefreshed)
                        {
                            Application.Current.Dispatcher.Invoke(() => {
                                MascotMessageBox.Show(
                                    LocalizationManager.GetString("Accounts.SessionExpired", "Час вашої сесії Microsoft минув.\nБудь ласка, видаліть цей акаунт і увійдіть знову."),
                                    LocalizationManager.GetString("Accounts.NeedAuth", "Потрібна авторизація"),
                                    MascotEmotion.Alert);
                            });
                            return false;
                        }
                    }
                }
                else if (profile.TypeAccount == AccountType.Offline)
                {
                    CurrentSession = new MSession
                    {
                        Username = profile.NameAccount,
                        UUID = profile.UUID,
                        AccessToken = "access_token"
                    };
                }
                else if (profile.TypeAccount == AccountType.LittleSkin)
                {
                    CurrentSession = await _profileManager.CreateSessionForProfileAsync(profile, _loginHandler);
                }

                CurrentProfile = profile;
                return true;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Accounts.ProfileActivationError", "Не вдалося активувати профіль:\n{0}"), ex.Message),
                        LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                        MascotEmotion.Sad);
                });
                return false;
            }
        }

        public void DeleteProfile(ProfileItem profile)
        {
            var profiles = GetProfiles();
            var target = profiles.FirstOrDefault(p => p.UUID == profile.UUID && p.NameAccount == profile.NameAccount);

            if (target != null)
            {
                profiles.Remove(target);
                _profileManager.SaveProfiles(profiles);

                if (CurrentProfile != null && CurrentProfile.UUID == profile.UUID)
                {
                    CurrentSession = null;
                    CurrentProfile = null;
                    if (profile.TypeAccount == AccountType.Microsoft)
                    {
                        _loginHandler.Signout();
                    }
                }
            }
        }
        public async Task<ProfileItem> AddMicrosoftAccountAsync()
        {
            try
            {
                var app = await MsalClientHelper.BuildApplicationWithCache($"{Secrets.CLIENT_ID_AZURE}");
                var loginHandler = new JELoginHandlerBuilder().Build();

                var authenticator = loginHandler.CreateAuthenticatorWithNewAccount();
                authenticator.AddMsalOAuth(app, msal => msal.Interactive());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddForceJEAuthenticator();

                var session = await authenticator.ExecuteForLauncherAsync();

                var mojangApi = new Mojang(new HttpClient());
                bool ownsGame = await mojangApi.CheckGameOwnership(session.AccessToken);

                if (!ownsGame)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        MascotMessageBox.Show(
                            LocalizationManager.GetString("Accounts.NoMinecraftOwned", "На цьому Microsoft акаунті не куплено Minecraft."),
                            LocalizationManager.GetString("Accounts.NoLicenseTitle", "Відсутня ліцензія"),
                            MascotEmotion.Sad);
                    });
                    return null;
                }

                var profile = new ProfileItem
                {
                    NameAccount = session.Username,
                    UUID = session.UUID,
                    ImageUrl = $"https://mc-heads.net/avatar/{session.UUID}",
                    AccessToken = session.AccessToken,
                    TypeAccount = AccountType.Microsoft,
                    LicenseType = "Premium",
                    LastAuthTime = DateTime.UtcNow.ToString("O")
                };

                _profileManager.SaveProfile(profile);

                Application.Current.Dispatcher.Invoke(() => {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Accounts.MicrosoftSuccessDesc", "Вітаємо, {0}! Ліцензію успішно підтверджено."), session.Username),
                        LocalizationManager.GetString("Accounts.LoginSuccessTitle", "Успішний вхід"),
                        MascotEmotion.Happy);
                });

                return profile;
            }
            catch (XboxAuthException ex)
            {
                Debug.WriteLine($"Помилка Xbox: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() => {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Accounts.XboxErrorDesc", "Не вдалося підключитися до Xbox. Переконайтеся, що у вас створено профіль Xbox."),
                        LocalizationManager.GetString("Accounts.XboxErrorTitle", "Помилка Xbox"),
                        MascotEmotion.Sad);
                });
                return null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Debug.WriteLine("Отримано 403 Forbidden.");
                Application.Current.Dispatcher.Invoke(() => {
                    MascotMessageBox.Show(
                        LocalizationManager.GetString("Accounts.ForbiddenErrorDesc", "Доступ заборонено. Можливо, це дитячий акаунт з обмеженнями Family Safety."),
                        LocalizationManager.GetString("Accounts.ForbiddenErrorTitle", "Доступ закрито"),
                        MascotEmotion.Alert);
                });
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка входу: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() => {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Accounts.UnknownErrorDesc", "Щось пішло не так під час авторизації:\n{0}"), ex.Message),
                        LocalizationManager.GetString("Accounts.UnknownErrorTitle", "Невідома помилка"),
                        MascotEmotion.Sad);
                });
                return null;
            }
        }

        private async Task<bool> RefreshMicrosoftSessionAsync(ProfileItem profile)
        {
            try
            {
                var app = await MsalClientHelper.BuildApplicationWithCache($"{Secrets.CLIENT_ID_AZURE}");
                var loginHandler = new JELoginHandlerBuilder().Build();

                var accounts = loginHandler.AccountManager.GetAccounts();

                var cachedAccount = accounts.FirstOrDefault(a => a.Identifier == profile.NameAccount) ?? accounts.FirstOrDefault();

                if (cachedAccount == null)
                {
                    Debug.WriteLine("Локальний кеш порожній, тиха авторизація скасована.");
                    return false;
                }

                var authenticator = loginHandler.CreateAuthenticator(cachedAccount, CancellationToken.None);

                authenticator.AddMsalOAuth(app, msal => msal.Silent());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddForceJEAuthenticator();

                var session = await authenticator.ExecuteForLauncherAsync();

                profile.AccessToken = session.AccessToken;
                profile.LastAuthTime = DateTime.UtcNow.ToString("O");

                _profileManager.UpdateProfile(profile);

                CurrentSession = session;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Тиха авторизація не вдалася: {ex.Message}");
                return false;
            }
        }

        public async Task<ProfileItem> AddOfflineAccountAsync(string nickname)
        {
            string uuid = CreateOfflineUUID(nickname);

            var profile = new ProfileItem
            {
                NameAccount = nickname,
                UUID = uuid,
                AccessToken = "-",
                ImageUrl = "pack://application:,,,/Assets/big-steve-face-2002298922 2.png",
                TypeAccount = AccountType.Offline
            };

            _profileManager.SaveProfile(profile);

            await Task.CompletedTask;

            return profile;
        }

        private string CreateOfflineUUID(string username)
        {
            string input = "OfflinePlayer:" + username;
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

                hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
                hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

                return new Guid(hash).ToString("N");
            }
        }

        public async Task<ProfileItem> AddLittleSkinAccountAsync(string login, string password)
        {
            var session = await _profileManager.LoginLittleSkinAsync(login, password);

            var profile = new ProfileItem
            {
                NameAccount = session.Username,
                UUID = session.UUID,
                AccessToken = session.AccessToken,
                ImageUrl = "pack://application:,,,/Assets/LittleSkinAccount.png",
                TypeAccount = AccountType.LittleSkin
            };

            _profileManager.SaveProfile(profile);
            return profile;
        }
    }
}