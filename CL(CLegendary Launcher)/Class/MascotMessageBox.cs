using CL_CLegendary_Launcher_.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public static class MascotMessageBox
    {
        public static void Show(string message, string title = null, MascotEmotion emotion = MascotEmotion.Normal)
        {
            string actualTitle = title ?? LocalizationManager.GetString("Dialogs.InfoTitle", "Інформація");
            var dialog = new MascotDialogWindow(message, actualTitle, emotion, isQuestion: false);
            dialog.ShowDialog();
        }

        public static bool Ask(string message, string title = null, MascotEmotion emotion = MascotEmotion.Alert)
        {
            string actualTitle = title ?? LocalizationManager.GetString("Dialogs.QuestionTitle", "Питання");
            var dialog = new MascotDialogWindow(message, actualTitle, emotion, isQuestion: true);
            var result = dialog.ShowDialog();
            return result == true;
        }
    }
}