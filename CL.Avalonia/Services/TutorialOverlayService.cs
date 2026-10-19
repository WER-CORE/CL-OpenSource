using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using CL.Core.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public enum CardPlacement
{
    Auto,
    Above,
    Below,
    Right,
    TopScreen
}

public class TutorialOverlayService
{
    private readonly Grid _overlayGrid;
    private readonly Path _overlayPath;
    private readonly Border _messageContainer;
    private readonly TextBlock? _titleText;
    private readonly TextBlock? _bodyText;
    private readonly TextBlock? _stepCounterText;
    private readonly Panel? _tourButtonsPanel;
    private readonly Button? _backBtn;
    private readonly Button? _nextBtn;
    private readonly Button? _finishBtn;
    private readonly Button? _skipBtn;
    private readonly Button? _understoodBtn;

    public TutorialOverlayService(
        Grid overlayGrid,
        Path overlayPath,
        Border messageContainer,
        TextBlock? titleText = null,
        TextBlock? bodyText = null,
        TextBlock? stepCounterText = null,
        Panel? tourButtonsPanel = null,
        Button? backBtn = null,
        Button? nextBtn = null,
        Button? finishBtn = null,
        Button? skipBtn = null,
        Button? understoodBtn = null)
    {
        _overlayGrid = overlayGrid;
        _overlayPath = overlayPath;
        _messageContainer = messageContainer;
        _titleText = titleText;
        _bodyText = bodyText;
        _stepCounterText = stepCounterText;
        _tourButtonsPanel = tourButtonsPanel;
        _backBtn = backBtn;
        _nextBtn = nextBtn;
        _finishBtn = finishBtn;
        _skipBtn = skipBtn;
        _understoodBtn = understoodBtn;
    }

    public void SetButtonsEnabled(bool isEnabled)
    {
        if (_backBtn != null) _backBtn.IsEnabled = isEnabled;
        if (_nextBtn != null) _nextBtn.IsEnabled = isEnabled;
        if (_finishBtn != null) _finishBtn.IsEnabled = isEnabled;
    }

    public void ShowTutorial(Control target, string title, string body, double verticalOffset = 0)
    {
        ShowTutorial(target, title, body, verticalOffset, null, false, false, false, CardPlacement.Auto);
    }

    public void ShowTutorial(
        Control target,
        string title,
        string body,
        double verticalOffset,
        string? stepCounter,
        bool isTourMode,
        bool isFirstStep,
        bool isLastStep,
        CardPlacement placement = CardPlacement.Auto)
    {
        if (target == null) return;

        var pt = target.TranslatePoint(new Point(0, 0), _overlayGrid);
        if (pt == null || target.Bounds.Width <= 0 || target.Bounds.Height <= 0)
        {
            Dispatcher.UIThread.Post(() => ShowTutorial(target, title, body, verticalOffset, stepCounter, isTourMode, isFirstStep, isLastStep, placement), DispatcherPriority.Loaded);
            return;
        }

        double padding = 6;
        double x = pt.Value.X - padding;
        double y = pt.Value.Y - padding;
        double w = target.Bounds.Width + (padding * 2);
        double h = target.Bounds.Height + (padding * 2);

        if (_overlayPath.Data is CombinedGeometry cg)
        {
            double screenW = Math.Max(4000, _overlayGrid.Bounds.Width + 1000);
            double screenH = Math.Max(4000, _overlayGrid.Bounds.Height + 1000);
            cg.Geometry1 = new RectangleGeometry(new Rect(0, 0, screenW, screenH));
            cg.Geometry2 = new RectangleGeometry(new Rect(x, y, w, h), 8, 8);
        }

        if (_titleText != null) _titleText.Text = title;
        if (_bodyText != null) _bodyText.Text = body;

        if (_stepCounterText != null)
        {
            _stepCounterText.Text = stepCounter ?? string.Empty;
            _stepCounterText.IsVisible = isTourMode && !string.IsNullOrEmpty(stepCounter);
        }

        if (_tourButtonsPanel != null)
        {
            _tourButtonsPanel.IsVisible = isTourMode;
        }
        if (_backBtn != null)
        {
            _backBtn.Content = LocalizationManager.GetString("TutorialTour.BtnBack", "Назад");
            _backBtn.IsVisible = isTourMode && !isFirstStep;
        }
        if (_nextBtn != null)
        {
            _nextBtn.Content = LocalizationManager.GetString("TutorialTour.BtnNext", "Далі");
            _nextBtn.IsVisible = isTourMode && !isLastStep;
        }
        if (_finishBtn != null)
        {
            _finishBtn.Content = LocalizationManager.GetString("TutorialTour.BtnFinish", "Завершити");
            _finishBtn.IsVisible = isTourMode && isLastStep;
        }
        if (_skipBtn != null)
        {
            _skipBtn.Content = LocalizationManager.GetString("TutorialTour.BtnSkip", "Пропустити");
            _skipBtn.IsVisible = isTourMode;
        }
        if (_understoodBtn != null)
        {
            _understoodBtn.Content = LocalizationManager.GetString("Tutorial.UnderstoodBtn", "Зрозуміло");
            _understoodBtn.IsVisible = !isTourMode;
        }

        _messageContainer.Measure(new Size(340, double.PositiveInfinity));
        double popupWidth = Math.Max(340, _messageContainer.DesiredSize.Width);
        double popupHeight = Math.Max(180, _messageContainer.DesiredSize.Height);
        if (_messageContainer.Bounds.Width > 0) popupWidth = Math.Max(popupWidth, _messageContainer.Bounds.Width);
        if (_messageContainer.Bounds.Height > 0) popupHeight = Math.Max(popupHeight, _messageContainer.Bounds.Height);

        if (placement == CardPlacement.Auto)
        {
            if (verticalOffset < 0)
            {
                placement = CardPlacement.Above;
            }
            else if (verticalOffset > 0)
            {
                placement = CardPlacement.Below;
            }
            else
            {
                placement = (y > 350) ? CardPlacement.Above : CardPlacement.Below;
            }
        }

        double finalX;
        double finalY;

        switch (placement)
        {
            case CardPlacement.Above:
                finalY = y - popupHeight - Math.Abs(verticalOffset);
                finalX = x + (w / 2.0) - (popupWidth / 2.0);
                break;
            case CardPlacement.Below:
                finalY = y + h + Math.Abs(verticalOffset);
                finalX = x + (w / 2.0) - (popupWidth / 2.0);
                break;
            case CardPlacement.Right:
                finalX = x + w + Math.Abs(verticalOffset);
                finalY = y + (h / 2.0) - (popupHeight / 2.0);
                break;
            case CardPlacement.TopScreen:
                finalY = Math.Max(20, Math.Abs(verticalOffset));
                finalX = (_overlayGrid.Bounds.Width / 2.0) - (popupWidth / 2.0);
                break;
            default:
                finalY = y;
                finalX = x;
                break;
        }

        if (_overlayGrid.Bounds.Width > 0)
        {
            finalX = Math.Clamp(finalX, 15, Math.Max(15, _overlayGrid.Bounds.Width - popupWidth - 15));
        }
        if (_overlayGrid.Bounds.Height > 0)
        {
            finalY = Math.Clamp(finalY, 15, Math.Max(15, _overlayGrid.Bounds.Height - popupHeight - 15));
        }

        _messageContainer.Margin = new Thickness(finalX, finalY, 0, 0);
        _overlayGrid.IsVisible = true;
    }

    public void CloseTutorial(Action? onClosed = null)
    {
        _overlayGrid.IsVisible = false;
        onClosed?.Invoke();
    }
}
