using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs
{
    public partial class MascotDialogWindow : Window
    {
        public MascotDialogWindow()
        {
            InitializeComponent();
            this.EnableAutoMemoryCleanup();
        }

        public MascotDialogWindow(string message, string title, MascotEmotion emotion, bool isQuestion)
        {
            InitializeComponent();
            this.EnableAutoMemoryCleanup();

            TitleTxt.Text = title;
            MessageTxt.Text = message;

            ApplyLocalization(isQuestion);

            if (isQuestion)
            {
                BtnCancel.IsVisible = true;
            }
            else
            {
                BtnCancel.IsVisible = false;
            }

            SetMascotImage(emotion);
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
            MascotImage.IsVisible = false;
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(0);
        }

        private void BtnOK_Click(object? sender, RoutedEventArgs e)
        {
            PlayClickSound();
            this.Close(true);
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            PlayClickSound();
            this.Close(false);
        }

        private void MainBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void PlayClickSound()
        {
            Services.AvaloniaSoundService.Click();
        }
    }
}
