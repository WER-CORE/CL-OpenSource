using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    [Collection("ServiceLocatorAndSettings")]
    public class ProgressCancelRetryRegressionTests
    {
        private sealed class FakeDialogService : IDialogService
        {
            public int AskQuestionSyncCalls { get; private set; }
            public int AskQuestionAsyncCalls { get; private set; }
            public int ShowMessageCalls { get; private set; }
            public bool NextAnswer { get; set; }

            public void ShowMessage(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal)
            {
                ShowMessageCalls++;
            }

            public Task ShowMessageAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal)
            {
                ShowMessageCalls++;
                return Task.CompletedTask;
            }

            public bool AskQuestion(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal)
            {
                AskQuestionSyncCalls++;
                return NextAnswer;
            }

            public Task<bool> AskQuestionAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal)
            {
                AskQuestionAsyncCalls++;
                return Task.FromResult(NextAnswer);
            }

            public Task<string> OpenFileDialogAsync(string title, string filter) => Task.FromResult(string.Empty);
            public Task<string> OpenFolderDialogAsync(string title) => Task.FromResult(string.Empty);
            public Task<string> SaveFileDialogAsync(string title, string defaultFileName, string extension) => Task.FromResult(string.Empty);
        }

        private sealed class FakeProgressService : ITaskProgressService
        {
            public string Title { get; set; } = string.Empty;
            public CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();
            public bool IsLoaded { get; set; } = true;
            public void ShowProgressWindow() => IsLoaded = true;
            public void CloseProgressWindow() => IsLoaded = false;
            public void UpdateVersionProgress(int progress, object version) { }
            public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName) { }
            public void UpdateFileProgress(int progress) { }
            public void AddActiveDownload(ConcurrentDownloadItem item) { }
            public void RemoveActiveDownload(ConcurrentDownloadItem item) { }
        }

        [Fact]
        public void R01_ModpacksPageAxaml_DoesNotContainDuplicateExportProgressOverlay()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string modpacksPageAxaml = Path.Combine(repoRoot, "CL.Avalonia", "Views", "Pages", "ModpacksPage.axaml");
            string mainAxaml = Path.Combine(repoRoot, "CL.Avalonia", "Views", "CL_Main.axaml");

            if (File.Exists(modpacksPageAxaml) && File.Exists(mainAxaml))
            {
                string pageContent = File.ReadAllText(modpacksPageAxaml);
                string mainContent = File.ReadAllText(mainAxaml);

                Assert.DoesNotContain("ExportProgressOverlay", pageContent);
                Assert.Contains("ExportProgressOverlay", mainContent);
            }
        }

        [Fact]
        public async Task R02_DownloadFileWithProgress_WhenCancelled_ThrowsOperationCanceledAndCleansTempFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"cl-r02-cancel-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                string savePath = Path.Combine(tempDir, "mod.jar");
                string tempPath = savePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, "partial");

                var gm = new GameSessionManager();
                var la = new LastActionService();
                var gl = new GameLaunchService(gm, la);
                var md = new ModDownloadService();
                var mp = new ModpackService(gm, gl, md);

                using var cts = new CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    mp.DownloadFileWithProgress("https://example.test/mod.jar", savePath, new FakeProgressService(), cts.Token));

                Assert.False(File.Exists(savePath));
                Assert.False(File.Exists(tempPath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task R02_HandleManualDownloadPrompt_WhenCancelled_DoesNotShowDialog()
        {
            var fakeDialog = new FakeDialogService { NextAnswer = false };
            ServiceLocator.Current.Register<IDialogService, FakeDialogService>(fakeDialog);

            var gm = new GameSessionManager();
            var la = new LastActionService();
            var gl = new GameLaunchService(gm, la);
            var md = new ModDownloadService();
            var mp = new ModpackService(gm, gl, md);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await mp.HandleManualDownloadPrompt("https://example.test/mod.jar", "C:\\temp\\mod.jar", "mod.jar", token: cts.Token);

            Assert.Equal(0, fakeDialog.AskQuestionAsyncCalls);
            Assert.Equal(0, fakeDialog.AskQuestionSyncCalls);
        }

        [Fact]
        public async Task R02_HandleManualDownloadPrompt_OnRealFailure_UsesAsyncDialogAndNotSyncDeadlock()
        {
            var fakeDialog = new FakeDialogService { NextAnswer = false };
            ServiceLocator.Current.Register<IDialogService, FakeDialogService>(fakeDialog);

            var gm = new GameSessionManager();
            var la = new LastActionService();
            var gl = new GameLaunchService(gm, la);
            var md = new ModDownloadService();
            var mp = new ModpackService(gm, gl, md);

            using var cts = new CancellationTokenSource();

            await mp.HandleManualDownloadPrompt("https://example.test/mod.jar", "C:\\temp\\mod.jar", "mod.jar", token: cts.Token);

            Assert.Equal(1, fakeDialog.AskQuestionAsyncCalls);
            Assert.Equal(0, fakeDialog.AskQuestionSyncCalls);
        }

        [Fact]
        public void PlayAfterErrorOrCancellation_CanAcquirePreparationGateAndOpenFreshSession()
        {
            var service = new GameLaunchService(new GameSessionManager(), new LastActionService());
            int openCount = 0;
            int closeCount = 0;

            var coordinator = new ProgressWindowSessionCoordinator(
                dispatch: a => a(),
                onOpenWindow: (_, _, _) => openCount++,
                onCloseWindow: _ => closeCount++);

            // First Play fails or is cancelled -> releases gate and closes its session
            Assert.True(service.TryAcquireLaunchPreparationGate());
            using (var cts1 = new CancellationTokenSource())
            {
                long gen1 = coordinator.BeginSession(cts1, "Install 1");
                cts1.Cancel();
                Assert.True(coordinator.CloseSession(cts1));
                service.ReleaseLaunchPreparationGate(cts1);
                Assert.False(coordinator.IsCurrentGeneration(gen1));
            }

            // Second Play after error/cancellation acquires gate cleanly and starts a fresh session
            Assert.True(service.TryAcquireLaunchPreparationGate());
            using (var cts2 = new CancellationTokenSource())
            {
                long gen2 = coordinator.BeginSession(cts2, "Install 2");
                Assert.True(coordinator.IsCurrentGeneration(gen2));
                Assert.True(coordinator.CloseSession(cts2));
                service.ReleaseLaunchPreparationGate(cts2);
            }

            Assert.Equal(2, openCount);
            Assert.Equal(2, closeCount);
        }

        [Fact]
        public void OldLaunchFinally_DoesNotCloseNewerSessionOrClearNewerGate()
        {
            var coordinator = new ProgressWindowSessionCoordinator(
                dispatch: a => a(),
                onOpenWindow: (_, _, _) => { },
                onCloseWindow: _ => { });

            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            coordinator.BeginSession(cts1, "Run 1");
            coordinator.BeginSession(cts2, "Run 2");

            // Old finally attempting to close with cts1 must not close Run 2
            Assert.False(coordinator.CloseSession(cts1));
            Assert.True(coordinator.IsSessionActive);

            // Closing with cts2 closes Run 2 cleanly
            Assert.True(coordinator.CloseSession(cts2));
            Assert.False(coordinator.IsSessionActive);
        }
    }
}
