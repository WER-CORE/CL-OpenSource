using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows;

namespace CL_CLegendary_Launcher_.Class
{
    public static class AnimationService
    {
        public static void AnimateProgressBar(System.Windows.Controls.Primitives.RangeBase progressBar, double targetValue, double durationMs = 250)
        {
            if (progressBar == null) return;

            var animation = new DoubleAnimation
            {
                To = targetValue,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            progressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
        }
        public static void AnimateVerticalIndicator(UIElement indicator, double targetY)
        {
            if (indicator == null) return;

            if (indicator.RenderTransform == null || indicator.RenderTransform is not TranslateTransform)
            {
                indicator.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)indicator.RenderTransform;

            var moveAnim = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };

            transform.BeginAnimation(TranslateTransform.YProperty, moveAnim);
        }
        public static void AnimateTabIndicator(FrameworkElement indicator, TranslateTransform transform, double targetX, double targetWidth, bool animate)
        {
            if (indicator == null || transform == null) return;

            if (animate)
            {
                var duration = TimeSpan.FromMilliseconds(300);
                var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };

                var moveAnim = new DoubleAnimation
                {
                    To = targetX,
                    Duration = duration,
                    EasingFunction = easing
                };
                transform.BeginAnimation(TranslateTransform.XProperty, moveAnim);

                var widthAnim = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = duration,
                    EasingFunction = easing
                };
                indicator.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }
            else
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                indicator.BeginAnimation(FrameworkElement.WidthProperty, null);

                transform.X = targetX;
                indicator.Width = targetWidth;
            }
        }
        public static void AnimateMenuSelector(FrameworkElement targetButton, UIElement containerGrid, FrameworkElement movingPanel, TranslateTransform translateTransform)
        {
            if (targetButton == null || targetButton.Visibility != Visibility.Visible) return;

            var transform = targetButton.TransformToAncestor(containerGrid);
            var targetPosition = transform.Transform(new Point(0, 0));

            movingPanel.Width = targetButton.ActualWidth;

            var animation = new DoubleAnimation
            {
                To = targetPosition.X,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };

            translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
        public static void AnimateRotation(UIElement element, double targetAngle, double durationSeconds = 0.2)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not RotateTransform)
            {
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = new RotateTransform(0);
            }

            var transform = (RotateTransform)element.RenderTransform;
            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation rotateAnim = new DoubleAnimation
            {
                To = targetAngle,
                Duration = duration,
                EasingFunction = easing
            };

            transform.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        }
        public static void AnimateBorderColor(Border border, Color targetColor, double durationSeconds = 0.15)
        {
            if (border == null || border.BorderBrush is not SolidColorBrush currentBrush) return;

            SolidColorBrush brush = new SolidColorBrush(currentBrush.Color);
            border.BorderBrush = brush;

            ColorAnimation colorAnim = new ColorAnimation
            {
                To = targetColor,
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }
        public static void AnimateScale(UIElement element, double targetScale, double durationSeconds = 0.15)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not ScaleTransform)
            {
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                element.RenderTransform = new ScaleTransform(1, 1);
            }

            var transform = (ScaleTransform)element.RenderTransform;
            var anim = new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
        public static void AnimatePageTransition(UIElement element, double startOffset = 40, double durationSeconds = 0.4)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)element.RenderTransform;

            element.Visibility = Visibility.Visible;
            element.Opacity = 0;
            transform.Y = startOffset; 

            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new CircleEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = easing
            };

            DoubleAnimation slideAnim = new DoubleAnimation
            {
                From = startOffset, 
                To = 0,             
                Duration = duration,
                EasingFunction = easing
            };

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }
        public static void AnimatePageTransitionExit(UIElement element, double endOffset = 40, double durationSeconds = 0.3)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)element.RenderTransform;
            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new CircleEase { EasingMode = EasingMode.EaseIn }; 

            DoubleAnimation opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = duration,
                EasingFunction = easing
            };

            DoubleAnimation slideAnim = new DoubleAnimation
            {
                From = 0,
                To = endOffset,
                Duration = duration,
                EasingFunction = easing
            };

            opacityAnim.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Hidden;
            };

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }
        public static void FadeIn(UIElement element, double duration)
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
        public static void FadeOut(UIElement element, double duration)
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
        public static void AnimateBorder(double targetX, double targetY, UIElement border)
        {
            DoubleAnimation moveXAnimation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase()
            };

            DoubleAnimation moveYAnimation = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase()
            };

            border.RenderTransform.BeginAnimation(TranslateTransform.XProperty, moveXAnimation);
            border.RenderTransform.BeginAnimation(TranslateTransform.YProperty, moveYAnimation);
        }

        public static void AnimateBorderObject(double targetX, double targetY, Border border, bool visibly)
        {
            if (border.RenderTransform == null || border.RenderTransform is not TranslateTransform)
                border.RenderTransform = new TranslateTransform();

            if (visibly) border.Visibility = Visibility.Visible;

            var moveXAnimation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase()
            };

            var moveYAnimation = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase()
            };

            moveXAnimation.Completed += (s, e) =>
            {
                if (!visibly) border.Visibility = Visibility.Hidden;
            };

            var transform = (TranslateTransform)border.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, moveXAnimation);
            transform.BeginAnimation(TranslateTransform.YProperty, moveYAnimation);
        }
    }
}
