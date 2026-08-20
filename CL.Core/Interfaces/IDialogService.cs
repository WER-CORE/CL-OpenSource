using System;
using System.Threading.Tasks;
using CL.Core.Models;

namespace CL.Core.Interfaces
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal);
        Task ShowMessageAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal);

        bool AskQuestion(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal);
        Task<bool> AskQuestionAsync(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal);
        
        Task<string> OpenFileDialogAsync(string title, string filter);
        Task<string> OpenFolderDialogAsync(string title);
    }
}
