using CL_CLegendary_Launcher_.Class;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class DowloadProgress : Window
    {
        public CancellationTokenSource CTS { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<ConcurrentDownloadItem> ActiveDownloads { get; set; } = new System.Collections.ObjectModel.ObservableCollection<ConcurrentDownloadItem>();

        public DowloadProgress()
        {
            ApplicationThemeManager.Apply(this);
            InitializeComponent();
            ApplyLocalization();
            ParallelDownloadsList.ItemsSource = ActiveDownloads;
        }

        private void ApplyLocalization()
        {
            NameWin.Text = LocalizationManager.GetString("DownloadManager.ProgressTitle", "Менеджер завантаження");
            FileTXTName.Text = LocalizationManager.GetString("DownloadManager.FileCheck", "Перевірка файлів...");
            FileTXT.Text = LocalizationManager.GetString("DownloadManager.Preparation", "Підготовка...");
            VersionTXT.Text = LocalizationManager.GetString("DownloadManager.OverallProgress", "Загальний прогрес");
            DowloadTXT.Text = LocalizationManager.GetString("DownloadManager.CancelBtn", "Скасувати");
        }

        public void AddActiveDownload(ConcurrentDownloadItem item)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ActiveDownloads.Add(item);
                if (ActiveDownloads.Count > 0) ParallelDownloadsBorder.Visibility = Visibility.Visible;
            });
        }

        public void RemoveActiveDownload(ConcurrentDownloadItem item)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ActiveDownloads.Contains(item)) ActiveDownloads.Remove(item);
                if (ActiveDownloads.Count == 0) ParallelDownloadsBorder.Visibility = Visibility.Collapsed;
            });
        }

        public void DowloadProgressBarVersion(int progress, object version)
        {
            VersionTXT.Text = string.Format(LocalizationManager.GetString("DownloadManager.DownloadingVersion", "Завантажується версія {0}"), version);
            AnimationService.AnimateProgressBar(ProgressDowloadVersion, progress);
            ProgressDowloadTXT.Text = progress + "%";
        }

        public void DowloadProgressBarFileTask(int filedowload, int filetotaldowload, string namefile)
        {
            FileTXTName.Text = $"{namefile}";
            FileTXT.Text = string.Format(LocalizationManager.GetString("DownloadManager.DownloadedFilesCount", "Завантажено {0} з {1} файлів"), filetotaldowload, filedowload);
        }

        public void DowloadProgressBarFile(int progress)
        {
            AnimationService.AnimateProgressBar(ProgressDowloadFile, progress);
            ProgressFileDowloadTXT.Text = progress + "%";
        }

        private void StopDownload_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            CTS?.Cancel();
            this.Close();
        }

        private void ExitLauncher_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}