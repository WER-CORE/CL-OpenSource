using CL_CLegendary_Launcher_.Class;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class PixiColorDialog : FluentWindow
    {
        public Color SelectedColor { get; private set; }

        public PixiColorDialog(Color initialColor)
        {
            InitializeComponent();
            UpdateLocalization();
            ApplicationThemeManager.Apply(this);

            MyColorPicker.SelectedColor = initialColor;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = MyColorPicker.SelectedColor;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateLocalization()
        {
            BtnCancel.Content = LocalizationManager.GetString("ColorPicker.Cancel", "Скасувати");
            BtnApply.Content = LocalizationManager.GetString("ColorPicker.Apply", "Застосувати");
        }
    }
}