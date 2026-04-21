using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Windows;
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
            SoundManager.Initialize();

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

            string errorTitle = LocalizationManager.GetString("Crash.Title", "Помилка CL Launcher");
            string errorMessage = LocalizationManager.GetString("Crash.Message",
                "Ой-йой! Лаунчер зіткнувся з критичною помилкою.\nЯкщо ви давали згоду, розробник вже отримав звіт.\n\nДеталі можна знайти в папці logs.");

            MascotMessageBox.Show(errorMessage, errorTitle, MascotEmotion.Sad);

            e.Handled = false;
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