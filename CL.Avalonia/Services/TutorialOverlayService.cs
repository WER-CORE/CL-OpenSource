using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class TutorialOverlayService
{
    private readonly Grid _overlayGrid;
    private readonly Path _overlayPath;
    private readonly Border _messageContainer;
    private readonly TextBlock? _titleText;
    private readonly TextBlock? _bodyText;

    public TutorialOverlayService(
        Grid overlayGrid,
        Path overlayPath,
        Border messageContainer,
        TextBlock? titleText = null,
        TextBlock? bodyText = null)
    {
        _overlayGrid = overlayGrid;
        _overlayPath = overlayPath;
        _messageContainer = messageContainer;
        _titleText = titleText;
        _bodyText = bodyText;
    }

    public void ShowTutorial(Control target, string title, string body, double verticalOffset = 0)
    {
        if (target == null) return;

        var pt = target.TranslatePoint(new Point(0, 0), _overlayGrid);
        if (pt == null || target.Bounds.Width <= 0 || target.Bounds.Height <= 0)
        {
            Dispatcher.UIThread.Post(() => ShowTutorial(target, title, body, verticalOffset), DispatcherPriority.Loaded);
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

        double finalY = y + verticalOffset;
        double finalX = x - 320;

        if (x < 330)
        {
            finalX = x + w + 20;
        }

        double popupWidth = _messageContainer.Bounds.Width > 0 ? _messageContainer.Bounds.Width : 300;
        if (finalX + popupWidth > _overlayGrid.Bounds.Width && _overlayGrid.Bounds.Width > 0)
        {
            finalX = Math.Max(10, _overlayGrid.Bounds.Width - popupWidth - 20);
        }
        if (finalY < 10) finalY = 10;
        if (finalY + 180 > _overlayGrid.Bounds.Height && _overlayGrid.Bounds.Height > 0)
        {
            finalY = Math.Max(10, _overlayGrid.Bounds.Height - 190);
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
