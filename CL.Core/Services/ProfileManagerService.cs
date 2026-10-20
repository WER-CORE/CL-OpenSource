using CL.Core.Interfaces;
using CL.Core.Platform;
using CL.Core.Models;
using CL.Core.Models;
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

namespace CL.Core.Services
{
    public class ProfileManagerService
    {
        private readonly string _profilesManegerPath;

        public ProfileManagerService()
        {
            string directoryPath = PlatformPaths.DataFolder();
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

        private static readonly byte[] MagicHeader = new byte[] { (byte)'C', (byte)'L', (byte)'E', (byte)'N', (byte)'C', 1 };

        public void SaveProfiles(List<ProfileItem> profiles)
        {
            try
            {
                var jsonToWrite = JsonConvert.SerializeObject(profiles, Formatting.Indented);
                byte[] plainBytes = Encoding.UTF8.GetBytes(jsonToWrite);
                byte[] encryptedData = EncryptData(plainBytes);

                File.WriteAllBytes(_profilesManegerPath, encryptedData);
            }
            catch (Exception ex)
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
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
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
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

            if (IsMagicHeader(fileContent))
            {
                try
                {
                    byte[] decryptedBytes = DecryptData(fileContent);
                    string json = Encoding.UTF8.GetString(decryptedBytes);
                    return JsonConvert.DeserializeObject<List<ProfileItem>>(json) ?? new List<ProfileItem>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CLENC decrypt failed: {ex.Message}");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    byte[] decryptedBytes = ProtectedData.Unprotect(fileContent, null, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(decryptedBytes);
                    var profiles = JsonConvert.DeserializeObject<List<ProfileItem>>(json);
                    if (profiles != null && profiles.Count > 0)
                    {

                        SaveProfiles(profiles);
                        return profiles;
                    }
                }
                catch { }
            }

            try
            {
                string json = DecryptLegacyAES(fileContent);
                var profiles = JsonConvert.DeserializeObject<List<ProfileItem>>(json);

                if (profiles != null && profiles.Count > 0)
                {
                    SaveProfiles(profiles);
                    return profiles;
                }
            }
            catch { }

            try
            {
                string text = Encoding.UTF8.GetString(fileContent);
                if (text.TrimStart().StartsWith("["))
                {
                    var profiles = JsonConvert.DeserializeObject<List<ProfileItem>>(text);
                    if (profiles != null && profiles.Count > 0)
                    {
                        SaveProfiles(profiles);
                        return profiles;
                    }
                }
            }
            catch { }

            try
            {
                ServiceLocator.Current.GetService<IDialogService>().ShowMessage(
                    LocalizationManager.GetString("Accounts.ProfileLoadCrashDesc", "Не вдалося завантажити профілі.\nФайл пошкоджено або він з іншого ПК."),
                    LocalizationManager.GetString("Accounts.ProfileLoadCrashTitle", "Збій профілів"),
                    MascotEmotion.Sad);

                File.Move(_profilesManegerPath, _profilesManegerPath + ".corrupted");
            }
            catch { }

            return new List<ProfileItem>();
        }

        public void UpdateProfile(ProfileItem updatedProfile)
        {
            var profiles = LoadProfiles();
            var target = profiles.FirstOrDefault(p => p.NameAccount == updatedProfile.NameAccount && p.TypeAccount == updatedProfile.TypeAccount);

            if (target != null)
            {
                target.AccessToken = updatedProfile.AccessToken;
                target.LastAuthTime = updatedProfile.LastAuthTime;

                SaveProfiles(profiles);
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
            var http = WebHelper.Client;
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
                string errorMsg = body;
                try
                {
                    var errObj = JObject.Parse(body);
                    errorMsg = errObj["errorMessage"]?.ToString() ?? errObj["error"]?.ToString() ?? body;
                }
                catch { }

                ServiceLocator.Current.GetService<IDialogService>()?.ShowMessage(
                    string.Format(LocalizationManager.GetString("Accounts.LoginLittleSkinErrorDesc", "Сервер LittleSkin відмовив у доступі.\nПеревір логін та пароль.\n\nВідповідь сервера: {0}"), errorMsg),
                    LocalizationManager.GetString("Accounts.LoginLittleSkinErrorTitle", "Помилка входу"),
                    MascotEmotion.Sad);
                return null;
            }

            JObject json;
            try
            {
                json = JObject.Parse(body);
            }
            catch
            {
                return null;
            }

            string username = json["selectedProfile"]?["name"]?.ToString() ?? email.Split('@')[0];
            string uuid = json["selectedProfile"]?["id"]?.ToString();
            string token = json["accessToken"]?.ToString();

            if (username == null || uuid == null || token == null)
            {
                ServiceLocator.Current.GetService<IDialogService>()?.ShowMessage(
                    LocalizationManager.GetString("Accounts.LittleSkinIncomplete", "Відповідь від LittleSkin неповна"),
                    LocalizationManager.GetString("Accounts.LoginLittleSkinErrorTitle", "Помилка входу"),
                    MascotEmotion.Sad);
                return null;
            }

            return new MSession
            {
                Username = username,
                UUID = uuid,
                AccessToken = token,
                UserType = "custom"
            };
        }

        private static bool IsMagicHeader(byte[] data)
        {
            if (data == null || data.Length < MagicHeader.Length) return false;
            for (int i = 0; i < MagicHeader.Length; i++)
            {
                if (data[i] != MagicHeader[i]) return false;
            }
            return true;
        }

        private byte[] GetOrCreateEncryptionKey()
        {
            string base64Key = SettingsManager.Default.EncryptKey;
            if (string.IsNullOrWhiteSpace(base64Key))
            {
                byte[] randomBytes = new byte[32];
                RandomNumberGenerator.Fill(randomBytes);
                base64Key = Convert.ToBase64String(randomBytes);
                SettingsManager.Default.EncryptKey = base64Key;
                SettingsManager.Save();
            }

            string salt = Environment.UserName + "@" + PlatformPaths.OsName();
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(base64Key + salt));
        }

        private byte[] EncryptData(byte[] plainBytes)
        {
            byte[] key = GetOrCreateEncryptionKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            ms.Write(MagicHeader, 0, MagicHeader.Length);
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var encryptor = aes.CreateEncryptor())
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
            }

            return ms.ToArray();
        }

        private byte[] DecryptData(byte[] cipherData)
        {
            if (cipherData.Length < MagicHeader.Length + 16)
                throw new InvalidOperationException("Encrypted data is too short.");

            byte[] key = GetOrCreateEncryptionKey();
            byte[] iv = new byte[16];
            Array.Copy(cipherData, MagicHeader.Length, iv, 0, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            int payloadOffset = MagicHeader.Length + 16;
            using var ms = new MemoryStream(cipherData, payloadOffset, cipherData.Length - payloadOffset);
            using var decryptor = aes.CreateDecryptor();
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var outMs = new MemoryStream();
            cs.CopyTo(outMs);
            return outMs.ToArray();
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
