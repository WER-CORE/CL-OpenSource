using CL_CLegendary_Launcher_.Class;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Appearance;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class CreditsWindow : Window
    {
        public CreditsWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Credits.Title", "Автори та Спільнота");
            CreditsTitleTxt.Text = LocalizationManager.GetString("Credits.Title", "Автори та Спільнота");
            BtnClose.Content = LocalizationManager.GetString("Credits.CloseBtn", "Закрити");
            CreditsContentText.Text = LocalizationManager.GetString("Credits.Loading", "Сіель шукає список наших героїв...");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCreditsAsync();
        }

        private async Task LoadCreditsAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string urlWithNoCache = $"{Secrets.CreditsUrl}?v={DateTime.Now.Ticks}";

                    string creditsText = await client.GetStringAsync(urlWithNoCache);

                    LoadingRing.Visibility = Visibility.Collapsed;
                    CreditsContentText.HorizontalAlignment = HorizontalAlignment.Left;
                    CreditsContentText.VerticalAlignment = VerticalAlignment.Top;
                    CreditsContentText.Text = creditsText;
                }
            }
            catch (Exception)
            {
                LoadingRing.Visibility = Visibility.Collapsed;
                CreditsContentText.HorizontalAlignment = HorizontalAlignment.Center;
                CreditsContentText.VerticalAlignment = VerticalAlignment.Center;
                CreditsContentText.Text = LocalizationManager.GetString("Credits.LoadError", "Ой, не вдалося завантажити список з інтернету.\nАле ми все одно безмежно вдячні всім, хто нам допомагає! (´• ω •`)");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}