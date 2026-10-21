using Avalonia.Controls;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Windows
{
    public partial class EulaWindow : Window
    {
        private DateTime _versionDate;

        public EulaWindow()
        {
            InitializeComponent();
            this.EnableAutoMemoryCleanup();
        }

        public EulaWindow(EulaConfig? config)
        {
            InitializeComponent();
            this.EnableAutoMemoryCleanup();
            ApplyLocalization();

            if (config != null)
            {
                MascotMessageText.Text = LocalizationManager.GetString("Eula.UpdateMessage", "Привіт! Я оновила правила нашої Спільноти. Перед тим як ми продовжимо, будь ласка, прочитай і підтвердь їх, щоб ми були на одній хвилі! (´• ω •`)");
                EulaContentText.Text = config.Text;
                _versionDate = config.LastUpdated;
            }
            else
            {
                MascotMessageText.Text = LocalizationManager.GetString("Eula.LoadFailMessage", "Ой, я не змогла завантажити актуальні правила з інтернету. Але ось локальна копія.");
                EulaContentText.Text = LocalizationManager.GetString("Eula.RestartRequired", "Перезавантажіть запускач");
                _versionDate = DateTime.Now;
            }

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;

            var languages = await LocalizationFetcher.GetAvailableLanguagesAsync();
            LanguageComboBox.ItemsSource = languages;

            string currentLangCode = SettingsManager.Default.LanguageCode ?? "uk_UA";
            var targetLanguage = languages?.FirstOrDefault(l => l.Code == currentLangCode) ?? languages?.FirstOrDefault();

            if (targetLanguage != null)
            {
                LanguageComboBox.SelectedItem = targetLanguage;

                if (LocalizationManager.CurrentLanguage != targetLanguage.Code || !LocalizationManager.IsLanguageFileExists(targetLanguage.Code))
                {
                    await LoadAndApplyLanguageAsync(targetLanguage);
                }
                else
                {
                    LocalizationManager.LoadLanguage(targetLanguage.Code);
                    ApplyLocalization();

                    var newConfig = await EulaService.GetEulaAsync(targetLanguage.Code);
                    if (newConfig != null)
                    {
                        EulaContentText.Text = newConfig.Text;
                        MascotMessageText.Text = newConfig.MascotMessage;
                        _versionDate = newConfig.LastUpdated;
                    }
                }
            }

            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            }
            catch (Exception)
            {

            }
        }

        private async void LanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || LanguageComboBox.SelectedItem is not LanguageItem selectedLang) return;

            PlayClickSound();

            if (LocalizationManager.CurrentLanguage != selectedLang.Code)
            {
                await LoadAndApplyLanguageAsync(selectedLang);
            }
        }

        private void ApplyLocalization()
        {
            this.Title = LocalizationManager.GetString("Eula.WindowTitle", "Угода");
            TxtMascotName.Text = LocalizationManager.GetString("Eula.MascotName", "Сіель");
            TxtMascotRole.Text = LocalizationManager.GetString("Eula.MascotRole", "Головний адміністратор");
            BtnDecline.Content = LocalizationManager.GetString("Eula.DeclineBtn", "Я не згоден (Вихід)");
            BtnAccept.Content = LocalizationManager.GetString("Eula.AcceptBtn", "Згода!");
            MascotMessageText.Text = LocalizationManager.GetString("Eula.UpdateMessage", "Привіт! Я оновила правила нашої Спільноти...");

            if (RunCrashTitle != null) RunCrashTitle.Text = LocalizationManager.GetString("Eula.CrashTitle", "Дозволити анонімну відправку звітів про помилки.");
            if (RunCrashDesc != null) RunCrashDesc.Text = LocalizationManager.GetString("Eula.CrashDesc", "Це допоможе нам швидше виправляти баги. Жодні особисті дані не збираються.");
        }

        private async Task LoadAndApplyLanguageAsync(LanguageItem selectedLang)
        {
            if (selectedLang == null) return;
            LanguageComboBox.IsEnabled = false;

            bool success = await LocalizationFetcher.DownloadLanguageAsync(selectedLang);
            if (success)
            {
                LocalizationManager.LoadLanguage(selectedLang.Code);
                SettingsManager.Default.LanguageCode = selectedLang.Code;
                SettingsManager.Save();

                ApplyLocalization();

                var newConfig = await EulaService.GetEulaAsync(selectedLang.Code);
                if (newConfig != null)
                {
                    EulaContentText.Text = newConfig.Text;
                    MascotMessageText.Text = newConfig.MascotMessage;
                    _versionDate = newConfig.LastUpdated;
                }
            }
            LanguageComboBox.IsEnabled = true;
        }

        private void Accept_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            PlayClickSound();
            SettingsManager.Default.EulaAcceptedDate = _versionDate;
            SettingsManager.Default.IsCrashReportingEnabled = ChkAllowCrashReports.IsChecked == true;
            SettingsManager.Save();

            if (SettingsManager.Default.IsCrashReportingEnabled) CrashReportManager.Enable();

            this.Close(true);
        }

        private void Decline_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            PlayClickSound();
            this.Close(false);
        }

        private void PlayClickSound()
        {
            AvaloniaSoundService.Click();
        }

        private void Header_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }
    }
}
