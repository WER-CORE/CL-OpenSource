using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CL.Core.Models;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

public enum AiOverlayState
{
    Input,
    Tutorial,
    Thinking,
    Result
}

public partial class GameLogViewModel : ObservableObject
{
    private readonly AiAssistantService? _aiService;
    private readonly bool _isOfflineMode;

    [ObservableProperty]
    private string _windowTitle = "CL - Консоль розробника";

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _isAiAgentEnabled;

    [ObservableProperty]
    private bool _isAiOverlayVisible;

    [ObservableProperty]
    private AiOverlayState _currentState = AiOverlayState.Input;

    public bool IsInputState => CurrentState == AiOverlayState.Input;
    public bool IsTutorialState => CurrentState == AiOverlayState.Tutorial;
    public bool IsThinkingState => CurrentState == AiOverlayState.Thinking;
    public bool IsResultState => CurrentState == AiOverlayState.Result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeminiProvider))]
    [NotifyPropertyChangedFor(nameof(IsOllamaProvider))]
    private int _selectedProviderIndex = 0;

    public bool IsGeminiProvider => SelectedProviderIndex == 0;
    public bool IsOllamaProvider => SelectedProviderIndex == 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLauncherApiSelected))]
    private bool _isCustomApiKeySelected = true;

    public bool IsLauncherApiSelected => !IsCustomApiKeySelected;

    [ObservableProperty]
    private string _customApiKey = string.Empty;

    [ObservableProperty]
    private string _ollamaModel = "llama3";

    [ObservableProperty]
    private string _userContext = string.Empty;

    [ObservableProperty]
    private string _aiResponseText = "Аналіз завершено...";

    [ObservableProperty] private string _liveOutputText = "Вихідний журнал в режимі реального часу";
    [ObservableProperty] private string _btnAiAnalyzeText = "Аналіз Agent C.L.";
    [ObservableProperty] private string _btnAiAnalyzeTooltip = "Виявити причину помилки за допомогою ШІ";
    [ObservableProperty] private string _aiInputTitle = "Агент C.L. - Налаштування аналізу";
    [ObservableProperty] private string _aiProviderTitle = "Нейроядро Агента (Провайдер)";
    [ObservableProperty] private string _aiProviderGemini = "Google Gemini 2.5 Flash(Хмарне ядро)";
    [ObservableProperty] private string _aiProviderOllama = "Ollama (Локальне ядро)";
    [ObservableProperty] private string _aiApiKeyTitle = "Ключ доступу до ШІ (API Key)";
    [ObservableProperty] private string _aiCustomKeyRadio = "Використати власний ключ Gemini (Рекомендовано)";
    [ObservableProperty] private string _aiLauncherKeyRadio = "Використати систему CL Launcher";
    [ObservableProperty] private string _aiWhatIsThisBtn = "Що це?";
    [ObservableProperty] private string _aiApiKeyPlaceholder = "Вставте ваш API Key сюди...";
    [ObservableProperty] private string _aiLauncherApiWarning = "Увага! Безкоштовний сервер лаунчера має жорсткі ліміти. Ніхто не гарантує роботу 24/7. Якщо сервер перевантажений, аналіз завершиться помилкою.";
    [ObservableProperty] private string _ollamaModelTitle = "Назва локальної моделі";
    [ObservableProperty] private string _ollamaModelPlaceholder = "Наприклад: llama3, phi3, mistral...";
    [ObservableProperty] private string _ollamaModelWarning = "Переконайтеся, що Ollama запущена на вашому ПК і ця модель завантажена.";
    [ObservableProperty] private string _aiContextTitle = "Що ви робили перед крашем? (Опціонально)";
    [ObservableProperty] private string _aiContextDesc = "Ця інформація допоможе Агенту дати точнішу відповідь.";
    [ObservableProperty] private string _aiContextPlaceholder = "Наприклад: я зайшов у портал в Незер, і гра зависла...";
    [ObservableProperty] private string _btnCancelAnalysis = "Скасувати";
    [ObservableProperty] private string _btnStartAnalysis = "Почати аналіз";
    [ObservableProperty] private string _tutorialTitle = "Як отримати безкоштовний API Key?";
    [ObservableProperty] private string _tutorialIntroText = "API Key — це ваш особистий пропуск до новітньої нейромережі Google Gemini 2.5. Він абсолютно безкоштовний і дає вам гарантований особистий ліміт — 20 детальних аналізів на день (чого з головою вистачить для вирішення будь-яких проблем зі збіркою!).";
    [ObservableProperty] private string _tutorialStep1Title = "Крок 1:";
    [ObservableProperty] private string _tutorialStep1Desc = "Перейдіть на сайт Google AI Studio (потрібен Google акаунт).";
    [ObservableProperty] private string _tutorialStep2Title = "Крок 2:";
    [ObservableProperty] private string _tutorialStep2Desc = "Натисніть велику синю кнопку «Get API Key».";
    [ObservableProperty] private string _tutorialStep3Title = "Крок 3:";
    [ObservableProperty] private string _tutorialStep3Desc = "Натисніть «Create API Key» -> «Create API key in new project».";
    [ObservableProperty] private string _tutorialStep4Title = "Крок 4:";
    [ObservableProperty] private string _tutorialStep4Desc = "Скопіюйте згенерований довгий код і вставте його в поле в лаунчері.";
    [ObservableProperty] private string _btnOpenAiStudioLink = "Відкрити Google AI Studio";
    [ObservableProperty] private string _btnBackFromTutorial = "Зрозуміло, назад";
    [ObservableProperty] private string _thinkingMainText = "Bit-CL обробляє ваші дані...";
    [ObservableProperty] private string _thinkingSubText = "Встановлення зв'язку з Агентом C.L.";
    [ObservableProperty] private string _resultTitleText = "Звіт Агента C.L.";

    public GameLogViewModel() : this(false)
    {
    }

    public GameLogViewModel(bool isOfflineMode = false)
    {
        _isOfflineMode = isOfflineMode;
        IsAiAgentEnabled = SettingsManager.Default.EnableAIAgent;

        if (IsAiAgentEnabled)
        {
            _aiService = new AiAssistantService();
        }

        if (!string.IsNullOrEmpty(SettingsManager.Default.CustomGeminiKey))
        {
            CustomApiKey = SettingsManager.Default.CustomGeminiKey;
        }

        ApplyLocalization();
        SettingsPageViewModel.LanguageConfigurationChanged += () =>
        {
            Dispatcher.UIThread.Post(ApplyLocalization);
        };
    }

    public void ApplyLocalization()
    {
        WindowTitle = "CL - " + LocalizationManager.GetString("AI.GameLogTitle", "Консоль розробника");
        LiveOutputText = LocalizationManager.GetString("AI.GameLogLiveOutput", "Вихідний журнал в режимі реального часу");
        BtnAiAnalyzeText = LocalizationManager.GetString("AI.BtnAiAnalyze", "Аналіз Agent C.L.");
        BtnAiAnalyzeTooltip = LocalizationManager.GetString("AI.BtnAiAnalyzeTooltip", "Виявити причину помилки за допомогою ШІ");
        AiInputTitle = LocalizationManager.GetString("AI.AiInputTitle", "Агент C.L. - Налаштування аналізу");
        AiProviderTitle = LocalizationManager.GetString("AI.AiProviderTitle", "Нейроядро Агента (Провайдер)");
        AiProviderGemini = LocalizationManager.GetString("AI.AiProviderGemini", "Google Gemini 2.5 Flash(Хмарне ядро)");
        AiProviderOllama = LocalizationManager.GetString("AI.AiProviderOllama", "Ollama (Локальне ядро)");
        AiApiKeyTitle = LocalizationManager.GetString("AI.AiApiKeyTitle", "Ключ доступу до ШІ (API Key)");
        AiCustomKeyRadio = LocalizationManager.GetString("AI.AiCustomKeyRadio", "Використати власний ключ Gemini (Рекомендовано)");
        AiLauncherKeyRadio = LocalizationManager.GetString("AI.AiLauncherKeyRadio", "Використати систему CL Launcher");
        AiWhatIsThisBtn = LocalizationManager.GetString("AI.AiWhatIsThisBtn", "Що це?");
        AiApiKeyPlaceholder = LocalizationManager.GetString("AI.AiApiKeyPlaceholder", "Вставте ваш API Key сюди...");
        AiLauncherApiWarning = LocalizationManager.GetString("AI.AiLauncherApiWarning", "Увага! Безкоштовний сервер лаунчера має жорсткі ліміти. Ніхто не гарантує роботу 24/7. Якщо сервер перевантажений, аналіз завершиться помилкою.");
        OllamaModelTitle = LocalizationManager.GetString("AI.OllamaModelTitle", "Назва локальної моделі");
        OllamaModelPlaceholder = LocalizationManager.GetString("AI.OllamaModelPlaceholder", "Наприклад: llama3, phi3, mistral...");
        OllamaModelWarning = LocalizationManager.GetString("AI.OllamaModelWarning", "Переконайтеся, що Ollama запущена на вашому ПК і ця модель завантажена.");
        AiContextTitle = LocalizationManager.GetString("AI.AiContextTitle", "Що ви робили перед крашем? (Опціонально)");
        AiContextDesc = LocalizationManager.GetString("AI.AiContextDesc", "Ця інформація допоможе Агенту дати точнішу відповідь.");
        AiContextPlaceholder = LocalizationManager.GetString("AI.AiContextPlaceholder", "Наприклад: я зайшов у портал в Незер, і гра зависла...");
        BtnCancelAnalysis = LocalizationManager.GetString("AI.BtnCancelAnalysis", "Скасувати");
        BtnStartAnalysis = LocalizationManager.GetString("AI.BtnStartAnalysis", "Почати аналіз");
        TutorialTitle = LocalizationManager.GetString("AI.TutorialTitle", "Як отримати безкоштовний API Key?");
        TutorialIntroText = LocalizationManager.GetString("AI.TutorialIntroText", "API Key — це ваш особистий пропуск до новітньої нейромережі Google Gemini 2.5. Він абсолютно безкоштовний і дає вам гарантований особистий ліміт — 20 детальних аналізів на день (чого з головою вистачить для вирішення будь-яких проблем зі збіркою!).");
        TutorialStep1Title = LocalizationManager.GetString("AI.TutorialStep1Title", "Крок 1:");
        TutorialStep1Desc = LocalizationManager.GetString("AI.TutorialStep1Desc", "Перейдіть на сайт Google AI Studio (потрібен Google акаунт).");
        TutorialStep2Title = LocalizationManager.GetString("AI.TutorialStep2Title", "Крок 2:");
        TutorialStep2Desc = LocalizationManager.GetString("AI.TutorialStep2Desc", "Натисніть велику синю кнопку «Get API Key».");
        TutorialStep3Title = LocalizationManager.GetString("AI.TutorialStep3Title", "Крок 3:");
        TutorialStep3Desc = LocalizationManager.GetString("AI.TutorialStep3Desc", "Натисніть «Create API Key» -> «Create API key in new project».");
        TutorialStep4Title = LocalizationManager.GetString("AI.TutorialStep4Title", "Крок 4:");
        TutorialStep4Desc = LocalizationManager.GetString("AI.TutorialStep4Desc", "Скопіюйте згенерований довгий код і вставте його в поле в лаунчері.");
        BtnOpenAiStudioLink = LocalizationManager.GetString("AI.BtnOpenAiStudioLink", "Відкрити Google AI Studio");
        BtnBackFromTutorial = LocalizationManager.GetString("AI.BtnBackFromTutorial", "Зрозуміло, назад");
        ThinkingMainText = LocalizationManager.GetString("AI.ThinkingMainText", "Bit-CL обробляє ваші дані...");
        ThinkingSubText = LocalizationManager.GetString("AI.ThinkingSubText", "Встановлення зв'язку з Агентом C.L.");
        ResultTitleText = LocalizationManager.GetString("AI.ResultTitleText", "Звіт Агента C.L.");
    }

    partial void OnCurrentStateChanged(AiOverlayState value)
    {
        OnPropertyChanged(nameof(IsInputState));
        OnPropertyChanged(nameof(IsTutorialState));
        OnPropertyChanged(nameof(IsThinkingState));
        OnPropertyChanged(nameof(IsResultState));
    }

    private readonly StringBuilder _logBuffer = new StringBuilder();
    private bool _isFlushScheduled = false;

    public void AppendLog(string text)
    {
        lock (_logBuffer)
        {
            _logBuffer.AppendLine(text);
            if (!_isFlushScheduled)
            {
                _isFlushScheduled = true;
                Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(100);
                    FlushLogs();
                });
            }
        }
    }

    private void FlushLogs()
    {
        string newLogs;
        lock (_logBuffer)
        {
            newLogs = _logBuffer.ToString();
            _logBuffer.Clear();
            _isFlushScheduled = false;
        }

        if (!string.IsNullOrEmpty(newLogs))
        {
            LogText += newLogs;
        }
    }

    public async Task LoadLogFromFileAsync(string logFilePath)
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                string notFoundMsg = string.Format(
                    LocalizationManager.GetString("GameLog.FileNotFound", "[Система]: Не вдалося знайти файл логів за шляхом:\n{0}"),
                    logFilePath);
                AppendLog(notFoundMsg);
                return;
            }

            AppendLog(LocalizationManager.GetString("GameLog.ReadingCrashLog", "[Система]: Читання посмертного логу (latest.log)...\n"));

            using var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream, Encoding.UTF8);

            string allLogs = await reader.ReadToEndAsync();
            AppendLog(allLogs);
            AppendLog(LocalizationManager.GetString("GameLog.EndOfLog", "\n[Система]: --- КІНЕЦЬ ЛОГУ ---"));
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(
                LocalizationManager.GetString("GameLog.ReadError", "\n[Система]: Сталася помилка при спробі прочитати лог-файл:\n{0}"),
                ex.Message);
            AppendLog(errorMsg);
        }
    }

    [RelayCommand]
    private void OpenAiOverlay()
    {
        AvaloniaSoundService.Click();

        if (string.IsNullOrWhiteSpace(LogText))
        {
            AvaloniaDialogService.Show(
                LocalizationManager.GetString("AI.EmptyLogAlert", "Лог пустий!"),
                LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                MascotEmotion.Confused);
            return;
        }

        CurrentState = AiOverlayState.Input;
        IsAiOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseAiOverlay()
    {
        AvaloniaSoundService.Click();
        IsAiOverlayVisible = false;
    }

    [RelayCommand]
    private void SelectCustomApi()
    {
        AvaloniaSoundService.Click();
        IsCustomApiKeySelected = true;
    }

    [RelayCommand]
    private void SelectLauncherApi()
    {
        AvaloniaSoundService.Click();
        IsCustomApiKeySelected = false;
    }

    [RelayCommand]
    private void OpenTutorial()
    {
        AvaloniaSoundService.Click();
        CurrentState = AiOverlayState.Tutorial;
    }

    [RelayCommand]
    private void BackToInput()
    {
        AvaloniaSoundService.Click();
        CurrentState = AiOverlayState.Input;
    }

    [RelayCommand]
    private void OpenAiStudio()
    {
        AvaloniaSoundService.Click();
        UrlOpener.Open("https://aistudio.google.com/app/apikey");
    }

    [RelayCommand]
    private async Task StartAnalysisAsync()
    {
        AvaloniaSoundService.Click();

        if (_aiService == null)
        {
            AvaloniaDialogService.Show("ШІ Асистент вимкнено у налаштуваннях!", "Увага", MascotEmotion.Sad);
            return;
        }

        AiProvider selectedProvider = SelectedProviderIndex == 0 ? AiProvider.Gemini : AiProvider.Ollama;
        string? apiKeyToUse = null;
        string ollamaModelToUse = "llama3";

        if (selectedProvider == AiProvider.Gemini)
        {
            if (IsCustomApiKeySelected)
            {
                if (string.IsNullOrWhiteSpace(CustomApiKey))
                {
                    AvaloniaDialogService.Show(
                        LocalizationManager.GetString("AI.EmptyApiKeyAlert", "Введіть ваш API Key або оберіть систему лаунчера!"),
                        LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                        MascotEmotion.Sad);
                    return;
                }

                apiKeyToUse = CustomApiKey.Trim();
                SettingsManager.Default.CustomGeminiKey = apiKeyToUse;
                SettingsManager.Save();
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(OllamaModel))
            {
                AvaloniaDialogService.Show(
                    LocalizationManager.GetString("AI.EmptyOllamaAlert", "Введіть назву локальної моделі (наприклад: llama3)."),
                    LocalizationManager.GetString("Dialogs.Alert", "Увага"),
                    MascotEmotion.Sad);
                return;
            }
            ollamaModelToUse = OllamaModel.Trim();
        }

        CurrentState = AiOverlayState.Thinking;

        try
        {
            string contextInfo = _isOfflineMode
                ? LocalizationManager.GetString("AI.AiSystemOfflineMode", "[СИСТЕМА]: Гравець використовує Офлайн-акаунт. ІГНОРУЙ помилки аутентифікації.\n")
                : "";

            if (!string.IsNullOrWhiteSpace(UserContext))
            {
                contextInfo += string.Format(
                    LocalizationManager.GetString("AI.AiSystemUserComment", "\n[КОМЕНТАР ГРАВЦЯ]: \"{0}\"\n"),
                    UserContext.Trim());
            }

            string fullQuery = contextInfo + LocalizationManager.GetString("AI.AiSystemCrashLogPrefix", "\n[LOG FILE]\n") + LogText;

            string analysisResult = await _aiService.AnalyzeCrashLogAsync(
                logContent: fullQuery,
                provider: selectedProvider,
                customApiKey: apiKeyToUse,
                ollamaModel: ollamaModelToUse
            );

            AiResponseText = analysisResult;
            CurrentState = AiOverlayState.Result;
        }
        catch (Exception ex)
        {
            IsAiOverlayVisible = false;
            AvaloniaDialogService.Show(
                string.Format(LocalizationManager.GetString("AI.AiSystemCrashError", "Збій системи: {0}"), ex.Message),
                LocalizationManager.GetString("Dialogs.Error", "Помилка"),
                MascotEmotion.Sad);
        }
    }
}
