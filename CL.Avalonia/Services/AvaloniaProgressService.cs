using System.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using CL.Core.Interfaces;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaProgressService : ITaskProgressService
{
    public string Title { get; set; } = string.Empty;
    public CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();
    public bool IsLoaded { get; private set; }

    private DownloadProgressDialog? _window;
    private DownloadProgressViewModel? _viewModel;

    public void ShowProgressWindow()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_window != null) return;
            IsLoaded = true;
            _viewModel = new DownloadProgressViewModel
            {
                CTS = CTS,
                TitleText = Title,
                CloseAction = CloseProgressWindow
            };
            _window = new DownloadProgressDialog { DataContext = _viewModel };

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
        });
    }

    public void CloseProgressWindow()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsLoaded = false;
            if (_window != null)
            {
                _window.Close();
                _window = null;
            }
            _viewModel = null;
        });
    }

    public void AddActiveDownload(ConcurrentDownloadItem item)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel != null)
            {
                _viewModel.ActiveDownloads.Add(item);
                _viewModel.IsParallelDownloadsVisible = _viewModel.ActiveDownloads.Count > 0;
            }
        });
    }

    public void RemoveActiveDownload(ConcurrentDownloadItem item)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel != null)
            {
                _viewModel.ActiveDownloads.Remove(item);
                _viewModel.IsParallelDownloadsVisible = _viewModel.ActiveDownloads.Count > 0;
            }
        });
    }

    public void UpdateFileProgress(int progress)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel != null)
            {
                _viewModel.ProgressDowloadFile = progress;
                _viewModel.ProgressFileDowloadTXT = $"{progress}%";
            }
        });
    }

    public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel != null)
            {
                _viewModel.FileTXTName = fileName ?? "";
                _viewModel.FileTXT = string.Format(CL.Core.Services.LocalizationManager.GetString("DownloadManager.DownloadedFilesCount", "Завантажено {0} з {1} файлів"), downloadedFiles, totalFiles);
            }
        });
    }

    public void UpdateVersionProgress(int progress, object version)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel != null)
            {
                _viewModel.ProgressDowloadVersion = progress;
                _viewModel.ProgressDowloadTXT = $"{progress}%";
                _viewModel.VersionTXT = string.Format(CL.Core.Services.LocalizationManager.GetString("DownloadManager.DownloadingVersion", "Завантажується версія {0}"), version);
            }
        });
    }
}
