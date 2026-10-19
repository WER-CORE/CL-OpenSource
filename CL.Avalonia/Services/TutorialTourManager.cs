using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CL.Core.Services;
using CL_CLegendary_Launcher_.Avalonia.Views;
using CL_CLegendary_Launcher_.Avalonia.Views.Pages;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public enum TourPageType
{
    Home,
    ModPacks,
    Mods,
    Servers,
    Settings
}

public class TourStep
{
    public TourPageType PageType { get; set; }
    public string TargetControlName { get; set; } = string.Empty;
    public string? FallbackControlName { get; set; }
    public string TitleKey { get; set; } = string.Empty;
    public string DefaultTitle { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string DefaultDescription { get; set; } = string.Empty;
    public CardPlacement Placement { get; set; } = CardPlacement.Auto;
    public double VerticalOffset { get; set; }
}

public class TutorialTourManager
{
    private readonly CL_Main _main;
    private readonly TutorialOverlayService _tutorialService;
    private readonly List<TourStep> _steps;
    private int _currentStepIndex = -1;
    private bool _isTransitioning;

    public bool IsTourActive { get; private set; }

    public TutorialTourManager(CL_Main main, TutorialOverlayService tutorialService)
    {
        _main = main;
        _tutorialService = tutorialService;
        _steps = new List<TourStep>
        {
            new TourStep
            {
                PageType = TourPageType.Home,
                TargetControlName = "BorderButton",
                FallbackControlName = "PlayTXT",
                TitleKey = "TutorialTour.Step1Title",
                DefaultTitle = "Запуск та вибір версії",
                DescriptionKey = "TutorialTour.Step1Desc",
                DefaultDescription = "Тут ти запускаєш гру та вибираєш потрібну версію Minecraft. Можна переглянути свіжі релізи, снапшоти або старі версії для швидкого встановлення.",
                Placement = CardPlacement.Above,
                VerticalOffset = 60
            },
            new TourStep
            {
                PageType = TourPageType.Home,
                TargetControlName = "SelectPanelAccount",
                FallbackControlName = "SelectGirdAccount",
                TitleKey = "TutorialTour.Step2Title",
                DefaultTitle = "Профілі та акаунти",
                DescriptionKey = "TutorialTour.Step2Desc",
                DefaultDescription = "Тут налаштовуються твої акаунти. Можна грати з простим офлайн-ніком, підключити свій скін через LittleSkin або безпечно увійти через офіційний Microsoft.",
                Placement = CardPlacement.Below,
                VerticalOffset = 15
            },
            new TourStep
            {
                PageType = TourPageType.Home,
                TargetControlName = "FolderPanel",
                FallbackControlName = "FilesMenuGrid",
                TitleKey = "TutorialTour.Step3Title",
                DefaultTitle = "Папка гри .ClMinecraft",
                DescriptionKey = "TutorialTour.Step3Desc",
                DefaultDescription = "Ця кнопка відразу відкриває робочу папку лаунчера .ClMinecraft. Тут лежать твої моди, ресурспаки, шейдери, збережені світи та логи.",
                Placement = CardPlacement.Above,
                VerticalOffset = 60
            },
            new TourStep
            {
                PageType = TourPageType.ModPacks,
                TargetControlName = "DowloadModPacks",
                FallbackControlName = "CreateModPacksTXT",
                TitleKey = "TutorialTour.Step4Title",
                DefaultTitle = "Створення збірок",
                DescriptionKey = "TutorialTour.Step4Desc",
                DefaultDescription = "Тут створюються окремі збірки під будь-яке ядро (Forge, Fabric, NeoForge чи Quilt). Кожна збірка живе у власній папці, тому моди не перемішуються між собою і гра працює стабільно.",
                Placement = CardPlacement.Below,
                VerticalOffset = 20
            },
            new TourStep
            {
                PageType = TourPageType.ModPacks,
                TargetControlName = "ImportModPacks",
                FallbackControlName = "ImportTXT",
                TitleKey = "TutorialTour.Step5Title",
                DefaultTitle = "Імпорт збірок",
                DescriptionKey = "TutorialTour.Step5Desc",
                DefaultDescription = "Якщо у тебе є завантажений модпак у форматі .mrpack або .zip, просто додай його сюди. Лаунчер сам розпакує файли і підготує все до запуску.",
                Placement = CardPlacement.Below,
                VerticalOffset = 20
            },
            new TourStep
            {
                PageType = TourPageType.Mods,
                TargetControlName = "GirdModsType",
                FallbackControlName = "ModsTypeBorder",
                TitleKey = "TutorialTour.Step6Title",
                DefaultTitle = "Каталог модів",
                DescriptionKey = "TutorialTour.Step6Desc",
                DefaultDescription = "Тут є два зручні режими встановлення. У режимі «У збірку» ти обираєш потрібну збірку, а лаунчер показує сумісні релізи модів. У режимі «Окремо» відкривається список версій гри для ручного вибору.",
                Placement = CardPlacement.Right,
                VerticalOffset = 30
            },
            new TourStep
            {
                PageType = TourPageType.Servers,
                TargetControlName = "ServerList",
                FallbackControlName = "SearchSystemTXT",
                TitleKey = "TutorialTour.Step7Title",
                DefaultTitle = "Список серверів",
                DescriptionKey = "TutorialTour.Step7Desc",
                DefaultDescription = "Тут можна дивитися онлайн, пінг та гравців на перевірених серверах і заходити на них в один клік. Додати свій сервер вручну не можна, заявки на додавання подають лише власники проєктів.",
                Placement = CardPlacement.TopScreen,
                VerticalOffset = 25
            },
            new TourStep
            {
                PageType = TourPageType.Settings,
                TargetControlName = "OPSlider",
                FallbackControlName = "SettingPanelMinecraft",
                TitleKey = "TutorialTour.Step8Title",
                DefaultTitle = "Пам'ять та налаштування",
                DescriptionKey = "TutorialTour.Step8Desc",
                DefaultDescription = "Тут виділяється оперативна пам'ять для гри, хоча для кожної збірки її можна налаштувати окремо. Також тут вибираються теми оформлення, ефект скла та автобекапи світів.",
                Placement = CardPlacement.Below,
                VerticalOffset = 25
            },
            new TourStep
            {
                PageType = TourPageType.Home,
                TargetControlName = "InfoLauncherPanel",
                FallbackControlName = "Loc_InfoMenu",
                TitleKey = "TutorialTour.Step9Title",
                DefaultTitle = "Довідка та інформація",
                DescriptionKey = "TutorialTour.Step9Desc",
                DefaultDescription = "Тут зібрані наша документація, новини та контакти для зв'язку. А якщо тобі знадобиться переглянути консоль логів або розібрати краш через ШІ, просто увімкни показ логів у налаштуваннях.",
                Placement = CardPlacement.Above,
                VerticalOffset = 60
            }
        };
    }

    public void StartTour()
    {
        if (_isTransitioning) return;
        IsTourActive = true;
        _currentStepIndex = 0;
        Dispatcher.UIThread.Post(async () => await ShowCurrentStepAsync());
    }

    public void NextStep()
    {
        if (!IsTourActive || _isTransitioning) return;

        if (_currentStepIndex < _steps.Count - 1)
        {
            _currentStepIndex++;
            Dispatcher.UIThread.Post(async () => await ShowCurrentStepAsync());
        }
        else
        {
            FinishTour();
        }
    }

    public void PreviousStep()
    {
        if (!IsTourActive || _isTransitioning) return;

        if (_currentStepIndex > 0)
        {
            _currentStepIndex--;
            Dispatcher.UIThread.Post(async () => await ShowCurrentStepAsync());
        }
    }

    public void FinishTour()
    {
        IsTourActive = false;
        _currentStepIndex = -1;
        _isTransitioning = false;
        SettingsManager.Default.OnboardingTourCompleted = true;
        SettingsManager.Default.TutorialComplete = true;
        SettingsManager.Save();
        _tutorialService.SetButtonsEnabled(true);
        _tutorialService.CloseTutorial();
    }

    public void SkipTour()
    {
        IsTourActive = false;
        _currentStepIndex = -1;
        _isTransitioning = false;
        SettingsManager.Default.OnboardingTourCompleted = true;
        SettingsManager.Default.TutorialComplete = true;
        SettingsManager.Save();
        _tutorialService.SetButtonsEnabled(true);
        _tutorialService.CloseTutorial();
    }

    private async Task ShowCurrentStepAsync()
    {
        if (!IsTourActive || _currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

        _isTransitioning = true;
        _tutorialService.SetButtonsEnabled(false);

        try
        {
            var step = _steps[_currentStepIndex];

            bool navigationOccurred = EnsurePage(step.PageType);
            if (navigationOccurred)
            {
                await Task.Delay(400);
            }

            if (!IsTourActive) return;

            Control? target = FindTargetControl(step);
            if (target == null)
            {
                await Task.Delay(250);
                if (!IsTourActive) return;
                target = FindTargetControl(step);
            }

            if (target == null)
            {
                if (_currentStepIndex < _steps.Count - 1)
                {
                    _currentStepIndex++;
                    await ShowCurrentStepAsync();
                }
                else
                {
                    FinishTour();
                }
                return;
            }

            target.BringIntoView();

            string stepCounter = string.Format(
                LocalizationManager.GetString("TutorialTour.StepCounter", "Крок {0} з {1}"),
                _currentStepIndex + 1,
                _steps.Count);

            string title = LocalizationManager.GetString(step.TitleKey, step.DefaultTitle);
            string desc = LocalizationManager.GetString(step.DescriptionKey, step.DefaultDescription);

            _tutorialService.ShowTutorial(
                target,
                title,
                desc,
                step.VerticalOffset,
                stepCounter,
                isTourMode: true,
                isFirstStep: _currentStepIndex == 0,
                isLastStep: _currentStepIndex == _steps.Count - 1,
                placement: step.Placement);
        }
        finally
        {
            _isTransitioning = false;
            _tutorialService.SetButtonsEnabled(true);
        }
    }

    private bool EnsurePage(TourPageType pageType)
    {
        switch (pageType)
        {
            case TourPageType.Home:
                if (_main.PageHost.Content is not HomePage)
                {
                    _main.NavigationService.NavigateToHome();
                    return true;
                }
                break;
            case TourPageType.ModPacks:
                if (_main.PageHost.Content is not ModPacksPage)
                {
                    _main.NavigationService.NavigateToModPacks();
                    return true;
                }
                break;
            case TourPageType.Mods:
                if (_main.PageHost.Content is not ModsPage)
                {
                    _main.NavigationService.NavigateToMods();
                    return true;
                }
                break;
            case TourPageType.Servers:
                if (_main.PageHost.Content is not ServerPage)
                {
                    _main.NavigationService.NavigateToServers();
                    return true;
                }
                break;
            case TourPageType.Settings:
                if (_main.PageHost.Content is not SettingsPage)
                {
                    _main.NavigationService.NavigateToSettings();
                    return true;
                }
                break;
        }
        return false;
    }

    private Control? FindTargetControl(TourStep step)
    {
        var ctrl = _main.FindControl<Control>(step.TargetControlName);
        if (ctrl != null) return ctrl;

        if (_main.PageHost.Content is Control pageCtrl)
        {
            ctrl = pageCtrl.FindControl<Control>(step.TargetControlName);
            if (ctrl != null) return ctrl;

            if (!string.IsNullOrEmpty(step.FallbackControlName))
            {
                ctrl = pageCtrl.FindControl<Control>(step.FallbackControlName);
                if (ctrl != null) return ctrl;
            }
        }

        if (!string.IsNullOrEmpty(step.FallbackControlName))
        {
            ctrl = _main.FindControl<Control>(step.FallbackControlName);
            if (ctrl != null) return ctrl;
        }

        return null;
    }
}
