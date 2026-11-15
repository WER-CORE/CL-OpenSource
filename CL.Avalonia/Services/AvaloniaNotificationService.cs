using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.ApplicationLifetimes;
using CL.Core.Interfaces;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public static class AvaloniaNotificationService
{
    private static WindowNotificationManager? _notificationManager;
    private static Window? _targetWindow;

    public static void ShowNotification(
        string title,
        string message,
        object? snackbarPresenter = null,
        int sec = 5,
        object? icon = null,
        object? appearance = null)
    {

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (_notificationManager == null || _targetWindow != desktop.MainWindow)
            {
                _targetWindow = desktop.MainWindow;
                _notificationManager = new WindowNotificationManager(desktop.MainWindow)
                {
                    Position = NotificationPosition.BottomRight,
                    MaxItems = 3
                };
            }

            _notificationManager.Show(new Notification(
                title,
                message,
                NotificationType.Information,
                TimeSpan.FromSeconds(sec)));
            return;
        }

        try
        {
            var controller = ((App)Application.Current!).ServiceProvider?
                .GetService(typeof(IMainWindowController)) as IMainWindowController;
            controller?.ShowSnackbar(title, message, sec);
        }
        catch { }
    }
}
