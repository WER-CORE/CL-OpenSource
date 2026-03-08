using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CL_CLegendary_Launcher_.Class
{
    public class TutorialOverlayService
    {
        private readonly Window _window;
        private readonly Grid _overlayGrid;
        private readonly RectangleGeometry _holeRect;
        private readonly RectangleGeometry _screenRect;
        private readonly FrameworkElement _messageContainer;

        private readonly TextBlock _titleText;
        private readonly TextBlock _bodyText;

        public TutorialOverlayService(
            Window window,
            Grid overlayGrid,
            RectangleGeometry holeRect,
            RectangleGeometry screenRect,
            FrameworkElement messageContainer,
            TextBlock titleText, 
            TextBlock bodyText) 
        {
            _window = window;
            _overlayGrid = overlayGrid;
            _holeRect = holeRect;
            _screenRect = screenRect;
            _messageContainer = messageContainer;
            _titleText = titleText;
            _bodyText = bodyText;
        }

        public void ShowTutorial(FrameworkElement targetButton, double? customHeight = null, double verticalOffset = 0)
        {
            if (targetButton == null) return;

            if (customHeight.HasValue)
                _messageContainer.Height = customHeight.Value;
            else
                _messageContainer.Height = double.NaN;

            Point relativePoint = targetButton.TransformToAncestor(_window)
                                              .Transform(new Point(0, 0));

            double padding = 5;

            _holeRect.Rect = new Rect(
                relativePoint.X - padding,
                relativePoint.Y - padding,
                targetButton.ActualWidth + (padding * 2),
                targetButton.ActualHeight + (padding * 2)
            );

            _screenRect.Rect = new Rect(0, 0, _window.ActualWidth, _window.ActualHeight);

            double finalY = relativePoint.Y + verticalOffset;
            double finalX = relativePoint.X - 310; 

            if (relativePoint.X < 320)
            {
                finalX = relativePoint.X + targetButton.ActualWidth + 20;
            }

            _messageContainer.Margin = new Thickness(finalX, finalY, 0, 0);

            _overlayGrid.Visibility = Visibility.Visible;
            _overlayGrid.Opacity = 0;

            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            _overlayGrid.BeginAnimation(Grid.OpacityProperty, fadeIn);
        }
        public void ShowTutorial(FrameworkElement targetButton, string title, string body, double? customHeight = null, double verticalOffset = 0)
        {
            if (_titleText != null) _titleText.Text = title;
            if (_bodyText != null) _bodyText.Text = body;

            ShowTutorial(targetButton, customHeight, verticalOffset);
        }

        public void CloseTutorial(Action onClosed = null)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
            fadeOut.Completed += (s, a) =>
            {
                _overlayGrid.Visibility = Visibility.Collapsed;
                onClosed?.Invoke(); 
            };

            _overlayGrid.BeginAnimation(Grid.OpacityProperty, fadeOut);
        }
    }
}
