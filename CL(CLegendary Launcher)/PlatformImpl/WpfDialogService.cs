using CL.Core.Interfaces;
using CL.Core.Models;
using CL.Core.Services;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.PlatformImpl
{
    public class WpfDialogService : IDialogService
    {
        public void ShowMessage(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            MascotMessageBox.Show(message, title, emotion);
        }

        public Task ShowMessageAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            MascotMessageBox.Show(message, title, emotion);
            return Task.CompletedTask;
        }

        public bool AskQuestion(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            return MascotMessageBox.Ask(message, title, emotion);
        }

        public Task<bool> AskQuestionAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            return Task.FromResult(MascotMessageBox.Ask(message, title, emotion));
        }

        public Task<string> OpenFileDialogAsync(string title, string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult(dialog.FileName);
            }
            return Task.FromResult<string>(null);
        }

        public Task<string> OpenFolderDialogAsync(string title)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return Task.FromResult(dialog.SelectedPath);
            }
            return Task.FromResult<string>(null);
        }
    }
}
