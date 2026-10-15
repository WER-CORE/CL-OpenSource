using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using SukiUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    public class AvaloniaDialogService : IDialogService
    {
        private readonly ISukiDialogManager _dialogManager;

        // SukiDialogHost тримає лише один діалог, тож решта чекає черги, а не губиться.
        private readonly SemaphoreSlim _queue = new SemaphoreSlim(1, 1);

        public AvaloniaDialogService(ISukiDialogManager dialogManager)
        {
            _dialogManager = dialogManager;
        }

        public void ShowMessage(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
            => _ = ShowMessageAsync(message, title, emotion);

        public Task ShowMessageAsync(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
            => Dispatcher.UIThread.InvokeAsync(() => ShowAsync(message, title, emotion, false));

        // Ядро зрідка питає синхронно. З фонового потоку достатньо дочекатися відповіді,
        // а на UI-потоці блокувати не можна, тому крутимо вкладений цикл подій.
        public bool AskQuestion(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            var answer = AskQuestionAsync(message, title, emotion);

            if (Dispatcher.UIThread.CheckAccess() && !answer.IsCompleted)
            {
                var frame = new DispatcherFrame();
                answer.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false));
                Dispatcher.UIThread.PushFrame(frame);
            }

            return answer.GetAwaiter().GetResult();
        }

        public Task<bool> AskQuestionAsync(string message, string? title = null, MascotEmotion emotion = MascotEmotion.Normal)
            => Dispatcher.UIThread.InvokeAsync(() => ShowAsync(message, title, emotion, true));

        public async Task<string> OpenFileDialogAsync(string title, string filter)
        {
            var storage = DesktopWindow.Main?.StorageProvider;
            if (storage == null)
                return string.Empty;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = BuildFilters(filter)
            });

            return files.FirstOrDefault()?.TryGetLocalPath() ?? string.Empty;
        }

        public async Task<string> OpenFolderDialogAsync(string title)
        {
            var storage = DesktopWindow.Main?.StorageProvider;
            if (storage == null)
                return string.Empty;

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            return folders.FirstOrDefault()?.TryGetLocalPath() ?? string.Empty;
        }

        private async Task<bool> ShowAsync(string message, string? title, MascotEmotion emotion, bool question)
        {
            await _queue.WaitAsync();
            try
            {
                var builder = _dialogManager.CreateDialog()
                    .WithTitle(title ?? LocalizationManager.GetString("Dialogs.Info", "Інформація"))
                    .WithContent(message)
                    .OfType(ToNotificationType(emotion));

                builder = question
                    ? builder.WithYesNoResult(
                        LocalizationManager.GetString("Dialogs.BtnYesText", "Так"),
                        LocalizationManager.GetString("Dialogs.BtnNoText", "Ні"))
                    : builder.WithOkResult(LocalizationManager.GetString("Dialogs.BtnOkText", "Зрозуміло"));

                return await builder.TryShowAsync(CancellationToken.None);
            }
            finally
            {
                _queue.Release();
            }
        }

        private static NotificationType ToNotificationType(MascotEmotion emotion) => emotion switch
        {
            MascotEmotion.Happy => NotificationType.Success,
            MascotEmotion.Alert or MascotEmotion.Confused => NotificationType.Warning,
            MascotEmotion.Sad or MascotEmotion.Dead => NotificationType.Error,
            _ => NotificationType.Information
        };

        // Переводить WinForms-фільтр ("Опис|*.zip;*.jar|...") у формат Avalonia.
        private static List<FilePickerFileType> BuildFilters(string filter)
        {
            var types = new List<FilePickerFileType>();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var parts = filter.Split('|');
                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    var patterns = parts[i + 1]
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0)
                        .ToList();

                    if (patterns.Count > 0)
                        types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
                }
            }

            types.Add(new FilePickerFileType(LocalizationManager.GetString("Dialogs.AllFiles", "Усі файли")) { Patterns = new[] { "*" } });
            return types;
        }
    }
}
