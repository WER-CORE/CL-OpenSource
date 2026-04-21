using CL_CLegendary_Launcher_.Class;
using System.Windows;
using System.Windows.Input;

namespace CL_CLegendary_Launcher_.Windows
{
    public enum MascotEmotion
    {
        Normal,
        Happy,
        Sad,
        Confused,
        Alert,
        Dead
    }

    public partial class MascotDialogWindow : Window
    {
        public MascotDialogWindow(string message, string title, MascotEmotion emotion, bool isQuestion)
        {
            InitializeComponent();

            TitleTxt.Text = title;
            MessageTxt.Text = message;

            ApplyLocalization(isQuestion);

            if (isQuestion)
            {
                BtnCancel.Visibility = Visibility.Visible;
            }
            else
            {
                BtnCancel.Visibility = Visibility.Collapsed;
            }

            SetMascotImage(emotion);
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void ApplyLocalization(bool isQuestion)
        {
            if (isQuestion)
            {
                BtnOK.Content = LocalizationManager.GetString("Dialogs.BtnYesText", "Так");
                BtnCancel.Content = LocalizationManager.GetString("Dialogs.BtnNoText", "Ні, чекай");
            }
            else
            {
                BtnOK.Content = LocalizationManager.GetString("Dialogs.BtnOkText", "Зрозуміло");
            }
        }

        private void SetMascotImage(MascotEmotion emotion)
        {
            try
            {
                HideMascotColumn();
            }
            catch
            {
                HideMascotColumn();
            }
        }

        private void HideMascotColumn()
        {
            MascotImage.Visibility = Visibility.Collapsed;
            MascotColumn.Width = new GridLength(0);
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            DialogResult = false;
            Close();
        }

        private void Close_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();

            DialogResult = false;
            Close();
        }

        private void MainBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}