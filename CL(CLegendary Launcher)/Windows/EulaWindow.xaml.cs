using CL_CLegendary_Launcher_.Class;
using CL_CLegendary_Launcher_.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class EulaWindow : Window
    {
        private DateTime _versionDate;

        public EulaWindow(EulaConfig config)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

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
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;

            var languages = await LocalizationFetcher.GetAvailableLanguagesAsync();
            LanguageComboBox.ItemsSource = languages;

            string currentLangCode = SettingsManager.Default.LanguageCode ?? "uk_UA";

            var targetLanguage = System.Linq.Enumerable.FirstOrDefault(languages, l => l.Code == currentLangCode)
                              ?? System.Linq.Enumerable.FirstOrDefault(languages);

            if (targetLanguage != null)
            {
                LanguageComboBox.SelectedItem = targetLanguage;

                if (LocalizationManager.CurrentLanguage != targetLanguage.Code ||
                    !LocalizationManager.IsLanguageFileExists(targetLanguage.Code))
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

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || LanguageComboBox.SelectedItem is not LanguageItem selectedLang) return;

            SoundManager.Click();

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

            MascotMessageText.Text = LocalizationManager.GetString("Eula.UpdateMessage", "Привіт! Я оновила правила нашої Спільноти. Перед тим як ми продовжимо, будь ласка, прочитай і підтвердь їх, щоб ми були на одній хвилі! (´• ω •`)");

            if (RunCrashTitle != null)
                RunCrashTitle.Text = LocalizationManager.GetString("Eula.CrashTitle", "Дозволити анонімну відправку звітів про помилки.");
            if (RunCrashDesc != null)
                RunCrashDesc.Text = LocalizationManager.GetString("Eula.CrashDesc", "Це допоможе нам швидше виправляти баги. Жодні особисті дані не збираються.");
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
            else
            {
                MascotMessageBox.Show("Не вдалося завантажити мову.", "Помилка");
            }

            LanguageComboBox.IsEnabled = true;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            SettingsManager.Default.EulaAcceptedDate = _versionDate;
            bool allowCrashReports = ChkAllowCrashReports.IsChecked == true;
            SettingsManager.Default.IsCrashReportingEnabled = allowCrashReports;

            SettingsManager.Save();
            if (SettingsManager.Default.IsCrashReportingEnabled)
            {
                CrashReportManager.Enable();
            }

            this.DialogResult = true;
            this.Close();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            this.DialogResult = false;
            this.Close();
        }
    }
}