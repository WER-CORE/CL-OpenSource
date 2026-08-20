using CL.Core.Services;
using CL_CLegendary_Launcher_.Windows;
using CL.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace CL_CLegendary_Launcher_
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            CL.Core.Platform.ServiceLocator.Current.Register<CL.Core.Interfaces.IDispatcherService, CL_CLegendary_Launcher_.PlatformImpl.WpfDispatcherService>(new CL_CLegendary_Launcher_.PlatformImpl.WpfDispatcherService());
            CL.Core.Platform.ServiceLocator.Current.Register<CL.Core.Interfaces.IDialogService, CL_CLegendary_Launcher_.PlatformImpl.WpfDialogService>(new CL_CLegendary_Launcher_.PlatformImpl.WpfDialogService());
            CL.Core.Platform.ServiceLocator.Current.Register<CL.Core.Interfaces.ISoundService, CL_CLegendary_Launcher_.PlatformImpl.WpfSoundService>(new CL_CLegendary_Launcher_.PlatformImpl.WpfSoundService());
            CL.Core.Platform.ServiceLocator.Current.Register<CL.Core.Interfaces.ITaskProgressService, CL_CLegendary_Launcher_.PlatformImpl.WpfTaskProgressService>(new CL_CLegendary_Launcher_.PlatformImpl.WpfTaskProgressService());

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            SoundManager.Initialize();

            UrlOpener.ErrorHandler = (ex, url) => MascotMessageBox.Show(
                string.Format(LocalizationManager.GetString("Dialogs.UrlOpenErrorDesc", "Не вдалося відкрити посилання.\nДеталі: {0}"), ex.Message),
                LocalizationManager.GetString("Dialogs.UrlOpenErrorTitle", "Помилка браузера"),
                MascotEmotion.Sad);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }
        private void LogException(string source, Exception ex)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                string text =
                    $"[{DateTime.Now}] {source}\n" +
                    $"Message: {ex.Message}\n" +
                    $"StackTrace:\n{ex.StackTrace}\n";

                if (ex.InnerException != null)
                {
                    text += $"\n--- Inner Exception ---\n" +
                            $"Message: {ex.InnerException.Message}\n" +
                            $"StackTrace:\n{ex.InnerException.StackTrace}\n";
                }

                File.WriteAllText(logPath, text);
            }
            catch { }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("Dispatcher", e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("AppDomain", e.ExceptionObject as Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception realError = e.Exception;
            if (e.Exception is AggregateException aggEx && aggEx.InnerException != null)
            {
                realError = aggEx.GetBaseException();
            }

            LogException("TaskScheduler", realError);

            e.SetObserved();
        }
    }
}