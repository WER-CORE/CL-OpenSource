using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaDialogService : IDialogService
{
    public static AvaloniaDialogService? Current { get; private set; }

    public AvaloniaDialogService()
    {
        Current = this;
    }

    public void ShowMessage(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
    {
        _ = ShowMessageAsync(message, title, emotion);
    }

    public async Task ShowMessageAsync(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => ShowMessageAsync(message, title, emotion));
            return;
        }

        var dialog = new MascotDialogWindow(message, title ?? "Message", emotion, false);
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner != null && owner.IsVisible)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    public bool AskQuestion(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return AskQuestionAsync(message, title, emotion).GetAwaiter().GetResult();
        }

        return Dispatcher.UIThread.Invoke(() => AskQuestionAsync(message, title, emotion).GetAwaiter().GetResult());
    }

    public async Task<bool> AskQuestionAsync(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(() => AskQuestionAsync(message, title, emotion));
        }

        var dialog = new MascotDialogWindow(message, title ?? "Confirm", emotion, true);
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;

        if (owner != null && owner.IsVisible)
        {
            return await dialog.ShowDialog<bool>(owner);
        }
        else
        {
            dialog.Show();
            return false;
        }
    }

    public async Task<string?> OpenFileDialogAsync(string title, string filter)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var storage = desktop.MainWindow.StorageProvider;
            var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
            return result?.FirstOrDefault()?.Path.LocalPath;
        }
        return null;
    }

    public async Task<string?> OpenFolderDialogAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var storage = desktop.MainWindow.StorageProvider;
            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return result?.FirstOrDefault()?.Path.LocalPath;
        }
        return null;
    }

    public async Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string extension)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var storage = desktop.MainWindow.StorageProvider;
            var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                DefaultExtension = extension
            });
            return result?.Path.LocalPath;
        }
        return null;
    }

    #region Static Facades

    public static void Show(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
    {
        if (Current != null)
        {
            Current.ShowMessage(message, title, emotion);
            return;
        }

        var app = Application.Current as App;
        var dialogService = app?.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
        dialogService?.ShowMessage(message, title, emotion);
    }

    public static async Task<bool> AskAsync(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Alert)
    {
        if (Current != null)
        {
            return await Current.AskQuestionAsync(message, title, emotion);
        }

        var app = Application.Current as App;
        var dialogService = app?.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService;
        if (dialogService != null)
        {
            return await dialogService.AskQuestionAsync(message, title, emotion);
        }

        return false;
    }

    public static bool Ask(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Alert)
    {
        Show(message, title, emotion);
        return true;
    }

    #endregion
}
