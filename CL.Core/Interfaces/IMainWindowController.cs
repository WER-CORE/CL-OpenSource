using System.Diagnostics;

namespace CL.Core.Interfaces
{
    public interface IMainWindowController
    {
        void SetPlayButtonText(string text);
        void SetInstallVersionOnPlay(bool value);
        void Minimize();
        void Restore();
        void Close();
        void ShowGameLog(Process process);
        void ShowGameLogFromFile(string logFilePath);
        void ShowSnackbar(string title, string message, int durationSeconds = 3);
        void UpdateServerMonitoring();
    }
}
