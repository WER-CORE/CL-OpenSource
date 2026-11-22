using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    [Collection("ServiceLocatorAndSettings")]
    public class ProfileCreationFreezeTests
    {
        private sealed class NoOpDialogService : IDialogService
        {
            public int ShowMessageCalls { get; private set; }
            public void ShowMessage(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => ShowMessageCalls++;
            public Task ShowMessageAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal)
            {
                ShowMessageCalls++;
                return Task.CompletedTask;
            }
            public bool AskQuestion(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => false;
            public Task<bool> AskQuestionAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => Task.FromResult(false);
            public Task<string> OpenFileDialogAsync(string title, string filter) => Task.FromResult(string.Empty);
            public Task<string> OpenFolderDialogAsync(string title) => Task.FromResult(string.Empty);
            public Task<string> SaveFileDialogAsync(string title, string defaultFileName, string extension) => Task.FromResult(string.Empty);
        }

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"cl-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public async Task AddOfflineAccountAsync_FirstAndSecondProfile_GeneratesKeyOnceAndPersists()
        {
            string dir = CreateTempDir();
            string prevFolder = SettingsManager.FolderPath;
            var prevDefault = SettingsManager.Default;
            try
            {
                SettingsManager.FolderPath = dir;
                SettingsManager.Default = new AppSettings();
                ServiceLocator.Current.Register<IDialogService, NoOpDialogService>(new NoOpDialogService());

                var profileManager = new ProfileManagerService(dir);
                var accountService = new AccountService(profileManager);

                var first = await accountService.AddOfflineAccountAsync("Steve");
                Assert.NotNull(first);
                Assert.Equal("Steve", first!.NameAccount);
                Assert.False(string.IsNullOrWhiteSpace(SettingsManager.Default.EncryptKey));
                string generatedKey = SettingsManager.Default.EncryptKey;

                var second = await accountService.AddOfflineAccountAsync("Alex");
                Assert.NotNull(second);
                Assert.Equal("Alex", second!.NameAccount);
                Assert.Equal(generatedKey, SettingsManager.Default.EncryptKey);

                // Reopen with a fresh ProfileManagerService instance
                var reopenedManager = new ProfileManagerService(dir);
                var loaded = reopenedManager.LoadProfiles();
                Assert.Equal(2, loaded.Count);
                Assert.Contains(loaded, p => p.NameAccount == "Steve");
                Assert.Contains(loaded, p => p.NameAccount == "Alex");
            }
            finally
            {
                SettingsManager.FolderPath = prevFolder;
                SettingsManager.Default = prevDefault;
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task AddOfflineAccountAsync_DuplicateOrDoubleClick_DoesNotCreateDuplicateProfile()
        {
            string dir = CreateTempDir();
            string prevFolder = SettingsManager.FolderPath;
            var prevDefault = SettingsManager.Default;
            try
            {
                SettingsManager.FolderPath = dir;
                SettingsManager.Default = new AppSettings();
                var dialog = new NoOpDialogService();
                ServiceLocator.Current.Register<IDialogService, NoOpDialogService>(dialog);

                var profileManager = new ProfileManagerService(dir);
                var accountService = new AccountService(profileManager);

                var p1 = await accountService.AddOfflineAccountAsync("PlayerOne");
                var p2 = await accountService.AddOfflineAccountAsync("PlayerOne");

                Assert.NotNull(p1);
                Assert.Null(p2);
                Assert.Equal(1, dialog.ShowMessageCalls);
                Assert.Single(profileManager.LoadProfiles());
            }
            finally
            {
                SettingsManager.FolderPath = prevFolder;
                SettingsManager.Default = prevDefault;
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task AddOfflineAccountAsync_ConcurrentSaves_AreThreadSafeAndLeaveNoTempFiles()
        {
            string dir = CreateTempDir();
            string prevFolder = SettingsManager.FolderPath;
            var prevDefault = SettingsManager.Default;
            try
            {
                SettingsManager.FolderPath = dir;
                SettingsManager.Default = new AppSettings();
                ServiceLocator.Current.Register<IDialogService, NoOpDialogService>(new NoOpDialogService());

                var profileManager = new ProfileManagerService(dir);
                var accountService = new AccountService(profileManager);

                var tasks = Enumerable.Range(1, 8)
                    .Select(i => accountService.AddOfflineAccountAsync($"User{i}"))
                    .ToArray();

                var results = await Task.WhenAll(tasks);

                Assert.All(results, r => Assert.NotNull(r));
                var loaded = profileManager.LoadProfiles();
                Assert.Equal(8, loaded.Count);
                Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            }
            finally
            {
                SettingsManager.FolderPath = prevFolder;
                SettingsManager.Default = prevDefault;
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
    }
}
