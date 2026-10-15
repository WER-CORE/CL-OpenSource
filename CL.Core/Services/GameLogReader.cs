using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public static class GameLogReader
    {
        public static void StartWithOutput(Process process, Action<string> onLine)
        {
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public static async Task<string> ReadCrashLogAsync(string logFilePath)
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    return string.Format(
                        LocalizationManager.GetString("GameLog.FileNotFound", "[Система]: Не вдалося знайти файл логів за шляхом:\n{0}"),
                        logFilePath);
                }

                // FileShare.ReadWrite: гра може ще тримати latest.log відкритим.
                using (var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    string allLogs = await reader.ReadToEndAsync();

                    return LocalizationManager.GetString("GameLog.ReadingCrashLog", "[Система]: Читання посмертного логу (latest.log)...\n")
                        + Environment.NewLine + allLogs + Environment.NewLine
                        + LocalizationManager.GetString("GameLog.EndOfLog", "\n[Система]: --- КІНЕЦЬ ЛОГУ ---");
                }
            }
            catch (Exception ex)
            {
                return string.Format(
                    LocalizationManager.GetString("GameLog.ReadError", "\n[Система]: Сталася помилка при спробі прочитати лог-файл:\n{0}"),
                    ex.Message);
            }
        }
    }
}
