using System.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaProgressService : ITaskProgressService
{
    public string Title { get; set; } = string.Empty;
    public CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();
    public bool IsLoaded => _session.IsSessionActive;

    private readonly ProgressUpdateCoalescer _coalescer;
    private readonly ProgressWindowSessionCoordinator _session;
    private DownloadProgressDialog? _window;
    private DownloadProgressViewModel? _viewModel;
    private long _windowGeneration;

    public AvaloniaProgressService()
    {
        _coalescer = new ProgressUpdateCoalescer(
            ApplyProgressSnapshot,
            action => Dispatcher.UIThread.Post(action));
        _session = new ProgressWindowSessionCoordinator(
            action => Dispatcher.UIThread.Post(action),
            OpenWindowOnUiThread,
            CloseWindowOnUiThread,
            _coalescer);
    }

    public void ShowProgressWindow()
    {
        _session.BeginSession(CTS, Title);
    }

    public void CloseProgressWindow()
    {
        _session.CloseSession();
    }

    public void CloseProgressWindow(CancellationTokenSource? ownerCts)
    {
        _session.CloseSession(ownerCts);
    }

    private void OpenWindowOnUiThread(long sessionGen, CancellationTokenSource? capturedCts, string capturedTitle)
    {
        if (!_session.CanOpenWindowOnUiThread(sessionGen))
        {
            return;
        }

        if (_window != null)
        {
            if (_viewModel != null)
            {
                _viewModel.CTS = null;
            }
            _window.Close();
            _window = null;
            _viewModel = null;
            _windowGeneration = 0;
        }

        var vm = new DownloadProgressViewModel
        {
            CTS = capturedCts,
            TitleText = capturedTitle,
            CloseAction = () => _session.CloseSessionByGeneration(sessionGen)
        };

        _viewModel = vm;
        _window = new DownloadProgressDialog { DataContext = _viewModel };
        _windowGeneration = sessionGen;

        _session.NotifyWindowOpenedOnUiThread(sessionGen, snapshot => ApplySnapshotToViewModel(vm, snapshot));

        var desktop = global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner != null && owner.IsVisible)
        {
            _window.ShowDialog(owner);
        }
        else
        {
            _window.Show();
        }
    }

    private void CloseWindowOnUiThread(long closedUpToGen)
    {
        _session.NotifyWindowClosedOnUiThread(closedUpToGen);

        if (_window != null && _windowGeneration <= closedUpToGen)
        {
            if (_viewModel != null)
            {
                _viewModel.CTS = null;
            }
            _window.Close();
            _window = null;
            _viewModel = null;
            _windowGeneration = 0;
        }
    }

    public void AddActiveDownload(ConcurrentDownloadItem item)
    {
        long sessionGen = _session.CurrentGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_session.IsCurrentGeneration(sessionGen) || _viewModel == null) return;
            _viewModel.ActiveDownloads.Add(item);
            _viewModel.IsParallelDownloadsVisible = _viewModel.ActiveDownloads.Count > 0;
        });
    }

    public void RemoveActiveDownload(ConcurrentDownloadItem item)
    {
        long sessionGen = _session.CurrentGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_session.IsCurrentGeneration(sessionGen) || _viewModel == null) return;
            _viewModel.ActiveDownloads.Remove(item);
            _viewModel.IsParallelDownloadsVisible = _viewModel.ActiveDownloads.Count > 0;
        });
    }

    public void UpdateFileProgress(int progress)
    {
        _coalescer.ReportFileProgress(progress);
    }

    public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName)
    {
        _coalescer.ReportFileTaskProgress(totalFiles, downloadedFiles, fileName);
    }

    public void UpdateVersionProgress(int progress, object version)
    {
        _coalescer.ReportVersionProgress(progress, version);
    }

    private void ApplyProgressSnapshot(ProgressSnapshot snapshot)
    {
        _session.RouteSnapshotOnUiThread(snapshot, s =>
        {
            var vm = _viewModel;
            if (vm == null || _windowGeneration != s.SessionGeneration) return;
            ApplySnapshotToViewModel(vm, s);
        });
    }

    private static void ApplySnapshotToViewModel(DownloadProgressViewModel vm, ProgressSnapshot snapshot)
    {
        if (snapshot.HasFileProgress)
        {
            vm.ProgressDowloadFile = snapshot.FileProgress;
            vm.ProgressFileDowloadTXT = $"{snapshot.FileProgress}%";
        }

        if (snapshot.HasFileTaskProgress)
        {
            vm.FileTXTName = snapshot.FileName;
            vm.FileTXT = string.Format(
                LocalizationManager.GetString("DownloadManager.DownloadedFilesCount", "Завантажено {0} з {1} файлів"),
                snapshot.DownloadedFiles,
                snapshot.TotalFiles);
        }

        if (snapshot.HasVersionProgress)
        {
            vm.ProgressDowloadVersion = snapshot.VersionProgress;
            vm.ProgressDowloadTXT = $"{snapshot.VersionProgress}%";
            vm.VersionTXT = string.Format(
                LocalizationManager.GetString("DownloadManager.DownloadingVersion", "Завантажується версія {0}"),
                snapshot.VersionLabel);
        }
    }
}
