using CL_CLegendary_Launcher_.Models;
using CL_CLegendary_Launcher_.Windows;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CL_CLegendary_Launcher_.Class
{
    public class ProfileManagerService
    {
        private readonly string _profilesManegerPath;

        public ProfileManagerService()
        {
            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            _profilesManegerPath = Path.Combine(directoryPath, "ProfilesManeger.json");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (!File.Exists(_profilesManegerPath))
            {
                File.Create(_profilesManegerPath).Close();
            }
        }


        public void SaveProfiles(List<ProfileItem> profiles)
        {
            try
            {
                var jsonToWrite = JsonConvert.SerializeObject(profiles, Formatting.Indented);

                byte[] plainBytes = Encoding.UTF8.GetBytes(jsonToWrite);
                byte[] encryptedData = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(_profilesManegerPath, encryptedData);
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.ProfileSaveErrorDesc", "Не вдалося зберегти профіль: {0}"), ex.Message),
                    LocalizationManager.GetString("Accounts.ProfileSaveErrorTitle", "Помилка запису"),
                    MascotEmotion.Sad);
            }
        }

        public bool SaveProfile(ProfileItem profileItem)
        {
            var profiles = LoadProfiles();

            if (profiles.Any(p => p.NameAccount == profileItem.NameAccount && p.TypeAccount == profileItem.TypeAccount))
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Accounts.ProfileExistsDesc", "Цей акаунт ({0}) вже є в списку.\nНемає сенсу додавати його двічі."), profileItem.NameAccount),
                    LocalizationManager.GetString("Accounts.ProfileExistsTitle", "Вже існує"),
                    MascotEmotion.Alert);
                return false;
            }

            profiles.Add(profileItem);
            SaveProfiles(profiles);
            return true;
        }
        public List<ProfileItem> LoadProfiles()
        {
            if (!File.Exists(_profilesManegerPath)) return new List<ProfileItem>();

            byte[] fileContent;
            try
            {
                fileContent = File.ReadAllBytes(_profilesManegerPath);
                if (fileContent.Length == 0) return new List<ProfileItem>();
            }
            catch
            {
                return new List<ProfileItem>();
            }

            try
            {
                byte[] decryptedBytes = ProtectedData.Unprotect(fileContent, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonConvert.DeserializeObject<List<ProfileItem>>(json) ?? new List<ProfileItem>();
            }
            catch
            {
                try
                {
                    string json = DecryptLegacyAES(fileContent);
                    var profiles = JsonConvert.DeserializeObject<List<ProfileItem>>(json);

                    if (profiles != null && profiles.Count > 0)
                    {
                        SaveProfiles(profiles);
                    }

                    return profiles ?? new List<ProfileItem>();
                }
                catch (Exception ex)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Accounts.ProfileLoadCrashDesc", "Не вдалося завантажити профілі.\nФайл пошкоджено або він з іншого ПК.\n\nДеталі: {0}"), ex.Message),
                        LocalizationManager.GetString("Accounts.ProfileLoadCrashTitle", "Збій профілів"),
                        MascotEmotion.Sad);

                    File.Move(_profilesManegerPath, _profilesManegerPath + ".corrupted");
                    return new List<ProfileItem>();
                }
            }
        }

        public async Task<MSession> CreateSessionForProfileAsync(ProfileItem profile, JELoginHandler loginHandler)
        {
            switch (profile.TypeAccount)
            {
                case AccountType.Microsoft:
                    return await loginHandler.AuthenticateSilently() ?? await loginHandler.Authenticate();

                case AccountType.LittleSkin:
                    return new MSession(profile.NameAccount, profile.AccessToken, profile.UUID);

                case AccountType.Offline:
                default:
                    return new MSession
                    {
                        Username = profile.NameAccount,
                        UUID = profile.UUID,
                        AccessToken = "access_token",
                        UserType = "offline"
                    };
            }
        }

        public async Task<MSession> LoginLittleSkinAsync(string email, string password)
        {
            using (var http = new HttpClient())
            {
                var payload = new JObject
                {
                    ["agent"] = new JObject { ["name"] = "Minecraft", ["version"] = 1 },
                    ["username"] = email,
                    ["password"] = password,
                    ["requestUser"] = true
                };

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await http.PostAsync(Secrets.AuthUrlLittleSkin, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MascotMessageBox.Show(
                        string.Format(LocalizationManager.GetString("Accounts.LoginLittleSkinErrorDesc", "Сервер LittleSkin відмовив у доступі.\nПеревір логін та пароль.\n\nВідповідь сервера: {0}"), body),
                        LocalizationManager.GetString("Accounts.LoginLittleSkinErrorTitle", "Помилка входу"),
                        MascotEmotion.Sad);
                    throw new Exception(LocalizationManager.GetString("Accounts.LittleSkinAuthFailed", "Помилка авторизації"));
                }

                var json = JObject.Parse(body);
                string username = json["selectedProfile"]?["name"]?.ToString() ?? email.Split('@')[0];
                string uuid = json["selectedProfile"]?["id"]?.ToString();
                string token = json["accessToken"]?.ToString();

                if (username == null || uuid == null || token == null)
                    throw new Exception(LocalizationManager.GetString("Accounts.LittleSkinIncomplete", "Відповідь від LittleSkin неповна"));

                return new MSession
                {
                    Username = username,
                    UUID = uuid,
                    AccessToken = token,
                    UserType = "custom"
                };
            }
        }


        private string DecryptLegacyAES(byte[] cipherData)
        {
            byte[] key = GetLegacyEncryptionKey();
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] iv = new byte[16];
                Array.Copy(cipherData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipherData, 16, cipherData.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private byte[] GetLegacyEncryptionKey()
        {
            string base64Key = SettingsManager.Default.EncryptKey;

            if (string.IsNullOrEmpty(base64Key))
            {
                throw new Exception(LocalizationManager.GetString("Accounts.EncryptionKeyMissing", "Ключ шифрування відсутній у налаштуваннях."));
            }
            return Convert.FromBase64String(base64Key);
        }
    }
}