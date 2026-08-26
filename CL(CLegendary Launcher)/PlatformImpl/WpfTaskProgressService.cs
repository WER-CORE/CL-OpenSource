using System;
using CL.Core.Interfaces;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Windows;
using System.Threading;
using System.Windows;

namespace CL_CLegendary_Launcher_.PlatformImpl
{
    public class WpfTaskProgressService : ITaskProgressService
    {
        private DowloadProgress _progressWindow;
        private string _pendingTitle;
        private CancellationTokenSource _pendingCts;

        public bool IsLoaded 
        {
            get
            {
                if (_progressWindow == null) return false;
                return Application.Current.Dispatcher.Invoke(() => _progressWindow.IsLoaded);
            }
        }

        public string Title 
        { 
            get => _progressWindow != null ? Application.Current.Dispatcher.Invoke(() => _progressWindow.Title) : _pendingTitle;
            set 
            {
                if (_progressWindow != null) Application.Current.Dispatcher.Invoke(() => _progressWindow.Title = value);
                else _pendingTitle = value;
            }
        }

        public CancellationTokenSource CTS 
        { 
            get => _progressWindow != null ? _progressWindow.CTS : _pendingCts;
            set 
            {
                if (_progressWindow != null) _progressWindow.CTS = value;
                else _pendingCts = value;
            }
        }

        public void ShowProgressWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_progressWindow == null || !_progressWindow.IsLoaded)
                {
                    _progressWindow = new DowloadProgress();
                    if (_pendingTitle != null) _progressWindow.Title = _pendingTitle;
                    if (_pendingCts != null) _progressWindow.CTS = _pendingCts;
                    _progressWindow.Show();
                }
            });
        }

        public void CloseProgressWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                {
                    _progressWindow.Close();
                    _progressWindow = null;
                }
            });
        }

        public void UpdateVersionProgress(int progress, object version)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                    _progressWindow.DowloadProgressBarVersion(progress, version);
            }));
        }

        public void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                    _progressWindow.DowloadProgressBarFileTask(downloadedFiles, totalFiles, fileName);
            }));
        }

        public void UpdateFileProgress(int progress)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                    _progressWindow.DowloadProgressBarFile(progress);
            }));
        }

        public void AddActiveDownload(ConcurrentDownloadItem item)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                    _progressWindow.AddActiveDownload(item);
            }));
        }

        public void RemoveActiveDownload(ConcurrentDownloadItem item)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_progressWindow != null && _progressWindow.IsLoaded)
                    _progressWindow.RemoveActiveDownload(item);
            }));
        }
    }
}
