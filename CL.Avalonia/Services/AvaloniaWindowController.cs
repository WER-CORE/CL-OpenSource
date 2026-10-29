using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CL.Core.Interfaces;
using FluentAvalonia.UI.Controls;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaWindowController : IMainWindowController
{
    public void Minimize()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Minimized;
            if (OperatingSystem.IsLinux())
            {
                desktop.MainWindow.Hide();
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                AvaloniaImageHelper.ClearCache();
                await CL.Core.Services.MemoryCleaner.FlushMemoryAsync(trimWorkingSet: true);
            });
        }
    }

    public void Maximize()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            desktop.MainWindow.WindowState = desktop.MainWindow.WindowState == global::Avalonia.Controls.WindowState.Maximized
                ? global::Avalonia.Controls.WindowState.Normal
                : global::Avalonia.Controls.WindowState.Maximized;
        }
    }

    public void Close()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            desktop.MainWindow.Close();
    }

    public void Restore()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (!desktop.MainWindow.IsVisible)
            {
                desktop.MainWindow.Show();
            }
            desktop.MainWindow.WindowState = global::Avalonia.Controls.WindowState.Normal;
            desktop.MainWindow.Activate();
        }
    }

    public void ShowSnackbar(string title, string message, int durationSeconds = 3)
    {
        _ = ShowToastAsync(title, message, durationSeconds);
    }

    private async Task ShowToastAsync(string title, string message, int duration)
    {
        var dialog = new FAContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    public void SetPlayButtonText(string text) { }
    public void SetInstallVersionOnPlay(bool isInstall) { }
    public void ShowGameLog(Process process)
    {
        var gameLog = new Views.Dialogs.GameLogWindow();
        gameLog.Show();

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.OutputDataReceived += gameLog.MinecraftProcess_OutputDataReceived;
        process.ErrorDataReceived += gameLog.MinecraftProcess_ErrorDataReceived;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public void ShowGameLogFromFile(string path)
    {
        var gameLog = new Views.Dialogs.GameLogWindow();
        gameLog.Show();
        _ = gameLog.LoadLogFromFileAsync(path);
    }
    public void UpdateServerMonitoring() { }
}
