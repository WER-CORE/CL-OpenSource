using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Platform;
using CL.Core.Services;
using Newtonsoft.Json;
using Xunit;

namespace CL.Core.Tests;

[Collection("ServiceLocatorAndSettings")]
public class ModsAndModpacksStabilityTests
{
    private sealed class NoOpDialogService : IDialogService
    {
        public void ShowMessage(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) { }
        public Task ShowMessageAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => Task.CompletedTask;
        public bool AskQuestion(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => false;
        public Task<bool> AskQuestionAsync(string message, string title = null!, MascotEmotion emotion = MascotEmotion.Normal) => Task.FromResult(false);
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

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public int RequestCount { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return _responder(request, cancellationToken);
        }
    }

    private sealed class IsolatedTestScope : IDisposable
    {
        private readonly string _prevFolder;
        private readonly AppSettings _prevSettings;
        private readonly string? _prevModpacksJson;

        public string RootDir { get; }
        public string LauncherDir { get; }
        public string DataDir { get; }
        public string InstalledJsonPath { get; }

        public IsolatedTestScope()
        {
            RootDir = Path.Combine(Path.GetTempPath(), $"cl-task02b-{Guid.NewGuid():N}");
            LauncherDir = Path.Combine(RootDir, "launcher");
            DataDir = Path.Combine(RootDir, "data");
            Directory.CreateDirectory(LauncherDir);
            Directory.CreateDirectory(DataDir);
            InstalledJsonPath = Path.Combine(DataDir, "installed_modpacks.json");

            _prevFolder = SettingsManager.FolderPath;
            _prevSettings = SettingsManager.Default;
            _prevModpacksJson = ModpackPaths.CustomInstalledModpacksJsonPath;

            SettingsManager.FolderPath = DataDir;
            SettingsManager.Default = new AppSettings
            {
                PathLacunher = LauncherDir,
                OfflineModLauncher = false
            };
            ModpackPaths.CustomInstalledModpacksJsonPath = InstalledJsonPath;

            ServiceLocator.Current.Register<IDialogService, NoOpDialogService>(new NoOpDialogService());
        }

        public void Dispose()
        {
            SettingsManager.FolderPath = _prevFolder;
            SettingsManager.Default = _prevSettings;
            ModpackPaths.CustomInstalledModpacksJsonPath = _prevModpacksJson;
            if (Directory.Exists(RootDir))
            {
                try { Directory.Delete(RootDir, true); } catch { }
            }
        }
    }

    private static ModpackService CreateModpackService()
    {
        var gm = new GameSessionManager();
        var la = new LastActionService();
        var gl = new GameLaunchService(gm, la);
        var md = new ModDownloadService();
        return new ModpackService(gm, gl, md);
    }

    private static HttpResponseMessage CreateTruncatedResponse(byte[] actualPayload, long declaredLength)
    {
        var content = new ByteArrayContent(actualPayload);
        content.Headers.ContentLength = declaredLength;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Fact]
    public async Task ModDownloadService_TruncatedResponse_RejectsPartialJarAndCleansTempFile()
    {
        using var scope = new IsolatedTestScope();
        var service = new ModDownloadService();
        string targetPath = Path.Combine(scope.LauncherDir, "mods", "example.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        using var handler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(CreateTruncatedResponse(Encoding.UTF8.GetBytes("truncated-jar"), 512)));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<IOException>(() =>
            service.DownloadFileHelperAsync("https://example.test/example.jar", targetPath, CancellationToken.None, null, client));

        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task ModDownloadService_HttpRequestExceptionExhausted_CleansUpTempFile()
    {
        using var scope = new IsolatedTestScope();
        var service = new ModDownloadService();
        string targetPath = Path.Combine(scope.LauncherDir, "mods", "failing.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllTextAsync(targetPath + ".tmp", "stale-tmp");

        using var handler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.DownloadFileHelperAsync("https://example.test/failing.jar", targetPath, CancellationToken.None, null, client));

        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task ModDownloadService_ExistingFile_ReplacesAtomically()
    {
        using var scope = new IsolatedTestScope();
        var service = new ModDownloadService();
        string targetPath = Path.Combine(scope.LauncherDir, "mods", "sodium.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllTextAsync(targetPath, "old-broken-jar");

        byte[] validPayload = Encoding.UTF8.GetBytes("valid-new-mod-jar-contents");
        using var handler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(validPayload)
            }));
        using var client = new HttpClient(handler);

        await service.DownloadFileHelperAsync("https://example.test/sodium.jar", targetPath, CancellationToken.None, null, client);

        Assert.True(File.Exists(targetPath));
        Assert.Equal(validPayload, await File.ReadAllBytesAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task ModpackService_DownloadFileWithProgress_TruncatedStream_ReturnsFalseAndLeavesNoPartialFile()
    {
        using var scope = new IsolatedTestScope();
        var service = CreateModpackService();
        string targetPath = Path.Combine(scope.LauncherDir, "mods", "pack-mod.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        using var handler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(CreateTruncatedResponse(Encoding.UTF8.GetBytes("short-body"), 1024)));
        using var client = new HttpClient(handler);

        bool result = await service.DownloadFileWithProgress(
            "https://example.test/pack-mod.jar",
            targetPath,
            new FakeProgressService(),
            CancellationToken.None,
            httpClient: client);

        Assert.False(result);
        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task ModpackService_DownloadModsFromIndexJson_DisabledAndInstalledMods_DoNotFailWithFileNotFound()
    {
        using var scope = new IsolatedTestScope();
        var service = CreateModpackService();

        string packFolder = Path.Combine(scope.LauncherDir, "CLModpack", "PackWithDisabled");
        string modsDir = Path.Combine(packFolder, "mods");
        Directory.CreateDirectory(modsDir);

        await File.WriteAllTextAsync(Path.Combine(modsDir, "alpha.jar.disabled"), "disabled-mod");
        await File.WriteAllTextAsync(Path.Combine(modsDir, "beta.jar.installed"), "installed-mod");

        string indexPath = Path.Combine(packFolder, "modrinth.index.json");
        var indexPayload = new
        {
            files = new[]
            {
                new
                {
                    path = "mods/alpha.jar",
                    downloads = new[] { "https://example.test/alpha.jar" },
                    hashes = new { sha1 = "0123456789abcdef0123456789abcdef01234567" }
                },
                new
                {
                    path = "mods/beta.jar",
                    downloads = new[] { "https://example.test/beta.jar" },
                    hashes = new { sha1 = "fedcba9876543210fedcba9876543210fedcba98" }
                }
            }
        };
        await File.WriteAllTextAsync(indexPath, JsonConvert.SerializeObject(indexPayload));

        using var handler = new FakeHttpMessageHandler((req, ct) =>
            throw new InvalidOperationException("No HTTP download should occur for already disabled/installed mods."));
        using var client = new HttpClient(handler);

        bool ok = await service.DownloadModsFromIndexJsonAsync(
            indexPath,
            packFolder,
            new FakeProgressService(),
            CancellationToken.None,
            client);

        Assert.True(ok);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ModpackService_ImportModpackFromFile_CorruptedZip_CleansUpExtractDirectory()
    {
        using var scope = new IsolatedTestScope();
        var service = CreateModpackService();

        string corruptZip = Path.Combine(scope.RootDir, "broken-pack.zip");
        await File.WriteAllTextAsync(corruptZip, "this is definitely not a zip archive");
        string expectedExtractDir = Path.Combine(scope.LauncherDir, "CLModpack", "broken-pack");

        await Assert.ThrowsAnyAsync<Exception>(() => service.ImportModpackFromFileAsync(corruptZip));

        Assert.False(Directory.Exists(expectedExtractDir));
        Assert.Empty(service.LoadInstalledModpacks());
    }

    [Fact]
    public async Task ModpackService_AddAndDeleteModpack_ConcurrentCalls_PreserveValidJsonAndLeaveNoTempFiles()
    {
        using var scope = new IsolatedTestScope();
        var service = CreateModpackService();

        var addTasks = Enumerable.Range(1, 8).Select(i => Task.Run(() =>
            service.AddModpack(new InstalledModpack
            {
                Name = $"Pack_{i}",
                TypeSite = "Modrinth",
                MinecraftVersion = "1.20.1",
                LoaderType = "fabric",
                LoaderVersion = "0.15.0",
                Path = Path.Combine(scope.LauncherDir, "CLModpack", $"Pack_{i}"),
                PathJson = string.Empty,
                UrlImage = string.Empty
            })));

        await Task.WhenAll(addTasks);
        Assert.Equal(8, service.LoadInstalledModpacks().Count);

        var deleteTasks = Enumerable.Range(1, 4).Select(i => Task.Run(() =>
            service.DeleteModpack($"Pack_{i}")));

        await Task.WhenAll(deleteTasks);

        var remaining = service.LoadInstalledModpacks();
        Assert.Equal(4, remaining.Count);
        for (int i = 5; i <= 8; i++)
        {
            Assert.Contains(remaining, p => p.Name == $"Pack_{i}");
        }
        Assert.Empty(Directory.GetFiles(scope.DataDir, "*.tmp"));
    }
}
