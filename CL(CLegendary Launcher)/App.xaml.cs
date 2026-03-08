using CL_CLegendary_Launcher_.Class;
using SevenZip.Compression.LZ;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CL_CLegendary_Launcher_
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
