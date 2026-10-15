using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CL.Core.Interfaces;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using CL_CLegendary_Launcher_.Avalonia.Views;
using SukiUI.Toasts;
using System;
using System.Diagnostics;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    // Ядро викликає контролер і з фонових потоків, тож кожен метод сам переходить на UI-потік.
    public class AvaloniaWindowController : IMainWindowController
    {
        private readonly HomeViewModel _home;
        private readonly ISukiToastManager _toasts;

        public AvaloniaWindowController(HomeViewModel home, ISukiToastManager toasts)
        {
            _home = home;
            _toasts = toasts;
        }

        public void SetPlayButtonText(string text) => UiThread.Run(() => _home.PlayButtonText = text);

        public void SetInstallVersionOnPlay(bool value) => UiThread.Run(() => _home.IsLaunching = value);

        public void Minimize() => UiThread.Run(() =>
        {
            if (DesktopWindow.Main is { } window)
                window.WindowState = WindowState.Minimized;
        });

        public void Restore() => UiThread.Run(() =>
        {
            if (DesktopWindow.Main is { } window)
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }
        });

        public void Close() => UiThread.Run(() => DesktopWindow.Main?.Close());

        // Процес має стартувати ще до повернення: одразу після виклику ядро чекає на WaitForExitAsync.
        public void ShowGameLog(Process process)
        {
            var log = new GameLogViewModel();
            log.Attach(process);
            UiThread.Run(() => new GameLogWindow { DataContext = log }.Show());
        }

        public void ShowGameLogFromFile(string logFilePath) => UiThread.Run(() =>
        {
            var log = new GameLogViewModel();
            new GameLogWindow { DataContext = log }.Show();
            _ = log.LoadFromFileAsync(logFilePath);
        });

        public void ShowSnackbar(string title, string message, int durationSeconds = 3) => UiThread.Run(() =>
            _toasts.CreateToast()
                .WithTitle(title)
                .WithContent(message)
                .OfType(NotificationType.Information)
                .Dismiss().After(TimeSpan.FromSeconds(durationSeconds))
                .Dismiss().ByClicking()
                .Queue());

        public void UpdateServerMonitoring() { }
    }
}
