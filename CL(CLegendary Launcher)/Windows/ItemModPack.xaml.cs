using CL_CLegendary_Launcher_.Class;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        void Click()
        {
            Task.Run(() =>
            {
                var Click = new NAudio.Vorbis.VorbisWaveReader(Resource2.click);
                using (var waveOut = new NAudio.Wave.WaveOutEvent())
                {
                    waveOut.Volume = 0.1f;
                    waveOut.Init(Click);
                    waveOut.Play();
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            });
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
            Click();
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
            Click();
        }
    }
}