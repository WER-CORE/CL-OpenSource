using CL.Core.Services;
using System;
using System.Threading;

namespace CL.Core.Interfaces
{
    public interface ITaskProgressService
    {
        void ShowProgressWindow();
        void CloseProgressWindow();
        
        void UpdateVersionProgress(int progress, object version);
        void UpdateFileTaskProgress(int totalFiles, int downloadedFiles, string fileName);
        void UpdateFileProgress(int progress);

        void AddActiveDownload(ConcurrentDownloadItem item);
        void RemoveActiveDownload(ConcurrentDownloadItem item);

        string Title { get; set; }
        CancellationTokenSource CTS { get; set; }

        bool IsLoaded { get; }
    }
}
