using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public partial class DownloadProgressViewModel : ObservableObject
{
    public CancellationTokenSource? CTS { get; set; }
    public Action? CloseAction { get; set; }

    [ObservableProperty]
    private string _titleText = string.Empty;

    [ObservableProperty]
    private string _fileTXTName = string.Empty;

    [ObservableProperty]
    private string _progressFileDowloadTXT = "0%";

    [ObservableProperty]
    private int _progressDowloadFile = 0;

    [ObservableProperty]
    private string _fileTXT = string.Empty;

    [ObservableProperty]
    private string _versionTXT = string.Empty;

    [ObservableProperty]
    private string _progressDowloadTXT = "0%";

    [ObservableProperty]
    private int _progressDowloadVersion = 0;

    [ObservableProperty]
    private string _cancelButtonText = string.Empty;

    [ObservableProperty]
    private bool _isParallelDownloadsVisible = false;

    public ObservableCollection<ConcurrentDownloadItem> ActiveDownloads { get; } = new ObservableCollection<ConcurrentDownloadItem>();

    public DownloadProgressViewModel()
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        TitleText = LocalizationManager.GetString("DownloadManager.ProgressTitle", "Менеджер завантаження");
        FileTXTName = LocalizationManager.GetString("DownloadManager.FileCheck", "Перевірка файлів...");
        FileTXT = LocalizationManager.GetString("DownloadManager.Preparation", "Підготовка...");
        VersionTXT = LocalizationManager.GetString("DownloadManager.OverallProgress", "Загальний прогрес");
        CancelButtonText = LocalizationManager.GetString("DownloadManager.CancelBtn", "Скасувати");
    }

    public void AddActiveDownload(ConcurrentDownloadItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveDownloads.Add(item);
            IsParallelDownloadsVisible = ActiveDownloads.Count > 0;
        });
    }

    public void RemoveActiveDownload(ConcurrentDownloadItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ActiveDownloads.Contains(item)) ActiveDownloads.Remove(item);
            IsParallelDownloadsVisible = ActiveDownloads.Count > 0;
        });
    }

    public void DowloadProgressBarVersion(int progress, object version)
    {
        Dispatcher.UIThread.Post(() =>
        {
            VersionTXT = string.Format(LocalizationManager.GetString("DownloadManager.DownloadingVersion", "Завантажується версія {0}"), version);
            ProgressDowloadVersion = progress;
            ProgressDowloadTXT = progress + "%";
        });
    }

    public void DowloadProgressBarFileTask(int filedowload, int filetotaldowload, string namefile)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FileTXTName = $"{namefile}";
            FileTXT = string.Format(LocalizationManager.GetString("DownloadManager.DownloadedFilesCount", "Завантажено {0} з {1} файлів"), filetotaldowload, filedowload);
        });
    }

    public void DowloadProgressBarFile(int progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressDowloadFile = progress;
            ProgressFileDowloadTXT = progress + "%";
        });
    }

    [RelayCommand]
    private void CancelDownload()
    {
        AvaloniaSoundService.Click();
        CTS?.Cancel();
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void ExitLauncher()
    {
        CloseAction?.Invoke();
    }
}
