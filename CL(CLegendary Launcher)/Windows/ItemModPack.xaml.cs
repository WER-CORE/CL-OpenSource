using CL_CLegendary_Launcher_.Class;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class ItemModPack : UserControl
    {
        public ItemModPack()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            SettingTXT.Text = LocalizationManager.GetString("Modpacks.SettingsBtn", "Налаштування");
            PlayTXT.Text = LocalizationManager.GetString("Modpacks.PlayBtn", "Грати");

            DeleteTXT.Text = LocalizationManager.GetString("Modpacks.DeleteBtn", "Видалити");
            FloderPackTXT.Text = LocalizationManager.GetString("Modpacks.FolderBtn", "Тека Збірки");
            ExportTXT.Text = LocalizationManager.GetString("Modpacks.ExportZipBtn", "Експортувати ZIP");
            EditModPackTXT.Text = LocalizationManager.GetString("Modpacks.EditBtn", "Редагувати");
        }
        public void FadeIn(UIElement element, double duration)
        {
            if (element == null) return;

            element.BeginAnimation(UIElement.OpacityProperty, null);

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(duration),
                FillBehavior = FillBehavior.HoldEnd,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            element.Visibility = Visibility.Visible;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            });
        }

        public void FadeOut(UIElement element, double duration)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(duration));
            Storyboard.SetTarget(fadeOut, element);

            fadeOut.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Collapsed;
            };

            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
            Storyboard fadeOutStoryboard = new Storyboard();
            fadeOutStoryboard.Children.Add(fadeOut);
            fadeOutStoryboard.Begin();
        }

        private void SettingTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
            if (GirdFon.Visibility == Visibility.Visible)
            {
                FadeOut(GirdFon, 0.3);
            }
            else
            {
                FadeIn(GirdFon, 0.3);
            }
        }

        private void PlayTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SoundManager.Click();
        }
    }
}