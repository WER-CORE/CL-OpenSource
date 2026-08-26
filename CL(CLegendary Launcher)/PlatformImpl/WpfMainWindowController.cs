using CL.Core.Interfaces;
using CL_CLegendary_Launcher_.Windows;
using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_.PlatformImpl
{
    public class WpfMainWindowController : IMainWindowController
    {
        private readonly CL_Main_ _main;

        public WpfMainWindowController(CL_Main_ main)
        {
            _main = main;
        }

        public void SetPlayButtonText(string text)
        {
            _main.Dispatcher.Invoke(() => _main.PlayTXT.Text = text);
        }

        public void SetInstallVersionOnPlay(bool value)
        {
            _main.Dispatcher.Invoke(() => _main.InstallVersionOnPlay = value);
        }

        public void Minimize()
        {
            _main.Dispatcher.Invoke(() => _main.WindowState = WindowState.Minimized);
        }

        public void Restore()
        {
            _main.Dispatcher.Invoke(() => 
            {
                _main.Show();
                _main.WindowState = WindowState.Normal;
            });
        }

        public void Close()
        {
            _main.Dispatcher.Invoke(() => _main.Close());
        }

        public void ShowGameLog(Process process)
        {
            _main.Dispatcher.Invoke(() => _main.ShowGameLog(process));
        }

        public void ShowGameLogFromFile(string logFilePath)
        {
            _main.Dispatcher.Invoke(() => _main.ShowGameLogFromFile(logFilePath));
        }

        public void ShowSnackbar(string title, string message, int durationSeconds = 3)
        {
            _main.Dispatcher.Invoke(() => 
            {
                var snackbar = new Wpf.Ui.Controls.Snackbar(_main.SnackbarPresenter)
                {
                    Title = title,
                    Content = message,
                    Timeout = TimeSpan.FromSeconds(durationSeconds),
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary
                };
                snackbar.Show();
            });
        }

        public void UpdateServerMonitoring()
        {
        }
    }
}
