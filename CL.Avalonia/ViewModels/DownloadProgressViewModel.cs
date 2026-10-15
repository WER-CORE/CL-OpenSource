using CL.Core.Interfaces;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels
{
    public partial class DownloadProgressViewModel : ObservableObject, ITaskProgressService
    {
        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _versionText = string.Empty;

        [ObservableProperty]
        private int _versionProgress;

        [ObservableProperty]
        private string _fileText = string.Empty;

        [ObservableProperty]
        private int _fileProgress;

        public ObservableCollection<ConcurrentDownloadItem> ActiveDownloads { get; } = new ObservableCollection<ConcurrentDownloadItem>();

        public CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();

        public bool IsLoaded => IsVisible;

        public void ShowProgressWindow() => UiThread.Run(() =>
        {
            VersionText = FileText = string.Empty;
            VersionProgress = FileProgress = 0;
            IsVisible = true;
        });

        public void CloseProgressWindow() => UiThread.Run(() =>
        {
            IsVisible = false;
            ActiveDownloads.Clear();
        });

        public void UpdateVersionProgress(int progress, object version) => UiThread.Run(() =>
        {
            VersionProgress = progress;
            VersionText = version?.ToString() ?? string.Empty;
        });

        public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName)
            => UiThread.Run(() => FileText = $"{downloadedFiles}/{totalFiles}  {fileName}");

        public void UpdateFileProgress(int progress) => UiThread.Run(() => FileProgress = progress);

        public void AddActiveDownload(ConcurrentDownloadItem item) => UiThread.Run(() => ActiveDownloads.Add(item));

        public void RemoveActiveDownload(ConcurrentDownloadItem item) => UiThread.Run(() => ActiveDownloads.Remove(item));

        [RelayCommand]
        private void Cancel() => CTS?.Cancel();
    }
}
