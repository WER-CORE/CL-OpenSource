using CL_CLegendary_Launcher_.Class;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class GameLog : FluentWindow
    {
        private readonly AiAssistantService _aiService;
        private bool _isOfflineMode;

        public GameLog(bool isOfflineMode = false)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            if (SettingsManager.Default.EnableAIAgent)
            {
                _aiService = new AiAssistantService();
                _isOfflineMode = isOfflineMode;
            }
            else { BtnAiAnalyze.Visibility = Visibility.Collapsed; }

            ApplyLocalization();

            if (!string.IsNullOrEmpty(SettingsManager.Default.CustomGeminiKey))
            {
                CustomApiKeyInput.Password = SettingsManager.Default.CustomGeminiKey;
            }
        }

        private void ApplyLocalization()
        {
            TxtConsoleHeader.Text = LocalizationManager.GetString("AI.GameLogTitle", "Консоль розробника");
            TxtLiveOutput.Text = LocalizationManager.GetString("AI.GameLogLiveOutput", "Вихідний журнал в режимі реального часу");

            BtnAiAnalyze.Content = LocalizationManager.GetString("AI.BtnAiAnalyze", "Аналіз Agent C.L.");
            BtnAiAnalyze.ToolTip = LocalizationManager.GetString("AI.BtnAiAnalyzeTooltip", "Виявити причину помилки за допомогою ШІ");

            InputTitleText.Text = LocalizationManager.GetString("AI.AiInputTitle", "Агент C.L. - Налаштування аналізу");
            TxtProviderTitle.Text = LocalizationManager.GetString("AI.AiProviderTitle", "Нейроядро Агента (Провайдер)");

            CboItemGemini.Content = LocalizationManager.GetString("AI.AiProviderGemini", "Google Gemini 2.5 Flash(Хмарне ядро)");
            CboItemOllama.Content = LocalizationManager.GetString("AI.AiProviderOllama", "Ollama (Локальне ядро)");

            ApiKeySectionTitle.Text = LocalizationManager.GetString("AI.AiApiKeyTitle", "Ключ доступу до ШІ (API Key)");
            RadioCustomApi.Content = LocalizationManager.GetString("AI.AiCustomKeyRadio", "Використати власний ключ Gemini (Рекомендовано)");
            BtnOpenTutorial.Content = LocalizationManager.GetString("AI.AiWhatIsThisBtn", "Що це?");
            CustomApiKeyInput.PlaceholderText = LocalizationManager.GetString("AI.AiApiKeyPlaceholder", "Вставте ваш API Key сюди...");
            RadioLauncherApi.Content = LocalizationManager.GetString("AI.AiLauncherKeyRadio", "Використати систему CL Launcher");
            LauncherApiWarning.Text = LocalizationManager.GetString("AI.AiLauncherApiWarning", "Увага! Безкоштовний сервер лаунчера має жорсткі ліміти. Ніхто не гарантує роботу 24/7. Якщо сервер перевантажений, аналіз завершиться помилкою.");

            TxtOllamaModelTitle.Text = LocalizationManager.GetString("AI.OllamaModelTitle", "Назва локальної моделі");
            OllamaModelInput.PlaceholderText = LocalizationManager.GetString("AI.OllamaModelPlaceholder", "Наприклад: llama3, phi3, mistral...");
            TxtOllamaWarning.Text = LocalizationManager.GetString("AI.OllamaModelWarning", "Переконайтеся, що Ollama запущена на вашому ПК і ця модель завантажена.");

            ContextSectionTitle.Text = LocalizationManager.GetString("AI.AiContextTitle", "Що ви робили перед крашем? (Опціонально)");
            ContextSectionDescription.Text = LocalizationManager.GetString("AI.AiContextDesc", "Ця інформація допоможе Агенту дати точнішу відповідь.");
            UserContextBox.PlaceholderText = LocalizationManager.GetString("AI.AiContextPlaceholder", "Наприклад: я зайшов у портал в Незер, і гра зависла...");

            BtnCancelAnalysis.Content = LocalizationManager.GetString("AI.BtnCancelAnalysis", "Скасувати");
            BtnSubmitAnalysis.Content = LocalizationManager.GetString("AI.BtnStartAnalysis", "Почати аналіз");

            TutorialMainTitle.Text = LocalizationManager.GetString("AI.TutorialTitle", "Як отримати безкоштовний API Key?");
            TutorialIntroText.Text = LocalizationManager.GetString("AI.TutorialIntroText", "API Key — це ваш особистий пропуск до новітньої нейромережі Google Gemini 2.5...");
            TutorialStep1Title.Text = LocalizationManager.GetString("AI.TutorialStep1Title", "Крок 1:");
            TutorialStep1Desc.Text = LocalizationManager.GetString("AI.TutorialStep1Desc", "Перейдіть на сайт Google AI Studio (потрібен Google акаунт).");
            TutorialStep2Title.Text = LocalizationManager.GetString("AI.TutorialStep2Title", "Крок 2:");
            TutorialStep2Desc.Text = LocalizationManager.GetString("AI.TutorialStep2Desc", "Натисніть велику синю кнопку «Get API Key».");
            TutorialStep3Title.Text = LocalizationManager.GetString("AI.TutorialStep3Title", "Крок 3:");
            TutorialStep3Desc.Text = LocalizationManager.GetString("AI.TutorialStep3Desc", "Натисніть «Create API Key» -> «Create API key in new project».");
            TutorialStep4Title.Text = LocalizationManager.GetString("AI.TutorialStep4Title", "Крок 4:");
            TutorialStep4Desc.Text = LocalizationManager.GetString("AI.TutorialStep4Desc", "Скопіюйте згенерований довгий код і вставте його в поле в лаунчері.");
            BtnOpenAiStudioLink.Content = LocalizationManager.GetString("AI.BtnOpenAiStudioLink", "Відкрити Google AI Studio");
            BtnBackFromTutorial.Content = LocalizationManager.GetString("AI.BtnBackFromTutorial", "Зрозуміло, назад");

            ThinkingMainText.Text = LocalizationManager.GetString("AI.ThinkingMainText", "Bit-CL обробляє ваші дані...");
            ThinkingSubText.Text = LocalizationManager.GetString("AI.ThinkingSubText", "Встановлення зв'язку з Агентом C.L.");
            ResultTitleText.Text = LocalizationManager.GetString("AI.ResultTitleText", "Звіт Агента C.L.");
        }
        public async Task LoadLogFromFileAsync(string logFilePath)
        {
            try
            {
                if (!System.IO.File.Exists(logFilePath))
                {
                    string notFoundMsg = string.Format(
                        LocalizationManager.GetString("GameLog.FileNotFound", "[Система]: Не вдалося знайти файл логів за шляхом:\n{0}"),
                        logFilePath);

                    AppendTextToLog(notFoundMsg);
                    return;
                }

                AppendTextToLog(LocalizationManager.GetString("GameLog.ReadingCrashLog", "[Система]: Читання посмертного логу (latest.log)...\n"));

                using (var fileStream = new System.IO.FileStream(logFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var reader = new System.IO.StreamReader(fileStream, System.Text.Encoding.UTF8))
                {
                    string allLogs = await reader.ReadToEndAsync();

                    AppendTextToLog(allLogs);

                    AppendTextToLog(LocalizationManager.GetString("GameLog.EndOfLog", "\n[Система]: --- КІНЕЦЬ ЛОГУ ---"));
                }
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(
                    LocalizationManager.GetString("GameLog.ReadError", "\n[Система]: Сталася помилка при спробі прочитати лог-файл:\n{0}"),
                    ex.Message);

                AppendTextToLog(errorMsg);
            }
        }
        public void MinecraftProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                AppendTextToLog(e.Data);
            }
        }

        public void MinecraftProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                AppendTextToLog(e.Data);
            }
        }

        public void AppendTextToLog(string text)
        {
            if (GameLogTXTMincraft.Dispatcher.CheckAccess())
            {
                GameLogTXTMincraft.AppendText(text + Environment.NewLine);
            }
            else
            {
                GameLogTXTMincraft.Dispatcher.BeginInvoke(new Action(() => AppendTextToLog(text)));
            }
        }

        private void BorderTool_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnAiAnalyze_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (string.IsNullOrWhiteSpace(GameLogTXTMincraft.Text))
            {
                MascotMessageBox.Show(LocalizationManager.GetString("AI.EmptyLogAlert", "Лог пустий!"), LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Confused);
                return;
            }

            AiOverlay.Visibility = Visibility.Visible;
            SwitchState(StateInput);
        }

        private void ApiRadio_CheckedChanged(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            if (CustomApiPanel == null || LauncherApiWarning == null) return;

            if (RadioCustomApi.IsChecked == true)
            {
                CustomApiPanel.Visibility = Visibility.Visible;
                LauncherApiWarning.Visibility = Visibility.Collapsed;
            }
            else
            {
                CustomApiPanel.Visibility = Visibility.Collapsed;
                LauncherApiWarning.Visibility = Visibility.Visible;
            }
        }

        private void AiProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SoundManager.Click();

            if (GeminiSettingsPanel == null || OllamaSettingsPanel == null) return;

            if (AiProviderComboBox.SelectedIndex == 0)
            {
                GeminiSettingsPanel.Visibility = Visibility.Visible;
                OllamaSettingsPanel.Visibility = Visibility.Collapsed;
            }
            else if (AiProviderComboBox.SelectedIndex == 1)
            {
                GeminiSettingsPanel.Visibility = Visibility.Collapsed;
                OllamaSettingsPanel.Visibility = Visibility.Visible;
            }
        }

        private void OpenTutorial_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            SwitchState(StateTutorial);
        }

        private void BackToInput_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            SwitchState(StateInput);
        }

        private void OpenAiStudio_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            Process.Start(new ProcessStartInfo("https://aistudio.google.com/app/apikey") { UseShellExecute = true });
        }

        private async void BtnStartAnalysis_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();

            string logContent = GameLogTXTMincraft.Text;
            string userComment = UserContextBox.Text;

            AiProvider selectedProvider = AiProvider.Gemini;
            string apiKeyToUse = null;
            string ollamaModelToUse = "llama3";

            if (AiProviderComboBox.SelectedIndex == 0)
            {
                selectedProvider = AiProvider.Gemini;

                if (RadioCustomApi.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(CustomApiKeyInput.Password))
                    {
                        MascotMessageBox.Show(LocalizationManager.GetString("AI.EmptyApiKeyAlert", "Введіть ваш API Key або оберіть систему лаунчера!"), LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Sad);
                        return;
                    }
                    apiKeyToUse = CustomApiKeyInput.Password;
                    SettingsManager.Default.CustomGeminiKey = apiKeyToUse;
                    SettingsManager.Save();
                }
            }
            else if (AiProviderComboBox.SelectedIndex == 1)
            {
                selectedProvider = AiProvider.Ollama;

                if (string.IsNullOrWhiteSpace(OllamaModelInput.Text))
                {
                    MascotMessageBox.Show(LocalizationManager.GetString("AI.EmptyOllamaAlert", "Введіть назву локальної моделі (наприклад: llama3)."), LocalizationManager.GetString("Dialogs.Alert", "Увага"), MascotEmotion.Sad);
                    return;
                }
                ollamaModelToUse = OllamaModelInput.Text.Trim();
            }
            SwitchState(StateThinking);

            try
            {
                string contextInfo = _isOfflineMode
                    ? LocalizationManager.GetString("AI.AiSystemOfflineMode", "[СИСТЕМА]: Гравець використовує Офлайн-акаунт. ІГНОРУЙ помилки аутентифікації.\n")
                    : "";

                if (!string.IsNullOrWhiteSpace(userComment))
                    contextInfo += string.Format(LocalizationManager.GetString("AI.AiSystemUserComment", "\n[КОМЕНТАР ГРАВЦЯ]: \"{0}\"\n"), userComment);

                string fullQuery = contextInfo + LocalizationManager.GetString("AI.AiSystemCrashLogPrefix", "\n[LOG FILE]\n") + logContent;

                string analysisResult = await _aiService.AnalyzeCrashLogAsync(
                    logContent: fullQuery,
                    provider: selectedProvider,
                    customApiKey: apiKeyToUse,
                    ollamaModel: ollamaModelToUse
                );

                SwitchState(StateResult);
                AiResponseText.Text = analysisResult;
            }
            catch (System.Exception ex)
            {
                AiOverlay.Visibility = Visibility.Collapsed;
                MascotMessageBox.Show(string.Format(LocalizationManager.GetString("AI.AiSystemCrashError", "Збій системи: {0}"), ex.Message), LocalizationManager.GetString("Dialogs.Error", "Помилка"), MascotEmotion.Sad);
            }
        }

        private void SwitchState(UIElement activeState)
        {
            StateInput.Visibility = Visibility.Collapsed;
            StateTutorial.Visibility = Visibility.Collapsed;
            StateThinking.Visibility = Visibility.Collapsed;
            StateResult.Visibility = Visibility.Collapsed;
            activeState.Visibility = Visibility.Visible;
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e)
        {
            SoundManager.Click();
            AiOverlay.Visibility = Visibility.Collapsed;
        }

        private void AiOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == sender)
            {
                SoundManager.Click();
                AiOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}