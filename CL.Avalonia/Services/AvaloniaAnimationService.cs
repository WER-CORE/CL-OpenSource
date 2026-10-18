using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace CL_CLegendary_Launcher_.Avalonia.Services
{
    public static class AvaloniaAnimationService
    {
        public static void AnimateProgressBar(RangeBase progressBar, double targetValue, double durationMs = 250)
        {
            if (progressBar == null) return;

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(durationMs),
                FillMode = FillMode.Forward,
                Easing = new QuadraticEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = RangeBase.ValueProperty, Value = targetValue } }
                    }
                }
            };

            _ = animation.RunAsync(progressBar);
        }

        public static void AnimateVerticalIndicator(Control indicator, double targetY)
        {
            if (indicator == null) return;

            if (indicator.RenderTransform == null || indicator.RenderTransform is not TranslateTransform)
            {
                indicator.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)indicator.RenderTransform;

            var moveAnim = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                FillMode = FillMode.Forward,
                Easing = new BackEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = TranslateTransform.YProperty, Value = targetY } }
                    }
                }
            };

            _ = moveAnim.RunAsync(indicator);
        }

        public static void AnimateTabIndicator(Control indicator, TranslateTransform transform, double targetX, double targetWidth, bool animate)
        {
            if (indicator == null || transform == null) return;

            if (animate)
            {
                var duration = TimeSpan.FromMilliseconds(300);
                var easing = new BackEaseOut();

                var moveAnim = new Animation
                {
                    Duration = duration,
                    FillMode = FillMode.Forward,
                    Easing = easing,
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(1d),
                            Setters = { new Setter { Property = TranslateTransform.XProperty, Value = targetX } }
                        }
                    }
                };
                _ = moveAnim.RunAsync(indicator);

                var widthAnim = new Animation
                {
                    Duration = duration,
                    FillMode = FillMode.Forward,
                    Easing = easing,
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(1d),

                            Setters = { new Setter { Property = Layoutable.WidthProperty, Value = targetWidth } }
                        }
                    }
                };
                _ = widthAnim.RunAsync(indicator);
            }
            else
            {
                transform.X = targetX;
                indicator.Width = targetWidth;
            }
        }

        public static void AnimateMenuSelector(Control targetButton, Control containerGrid, Control movingPanel, TranslateTransform translateTransform)
        {
            if (targetButton == null || !targetButton.IsVisible) return;

            var targetPosition = targetButton.TranslatePoint(new Point(0, 0), containerGrid);
            if (targetPosition == null) return;

            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new BackEaseOut();

            var animationX = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = TranslateTransform.XProperty, Value = targetPosition.Value.X } }
                    }
                }
            };
            _ = animationX.RunAsync(movingPanel);

            var animationWidth = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = Layoutable.WidthProperty, Value = targetButton.Bounds.Width } }
                    }
                }
            };
            _ = animationWidth.RunAsync(movingPanel);
        }

        public static void AnimateRotation(Control element, double targetAngle, double durationSeconds = 0.2)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not RotateTransform)
            {
                element.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                element.RenderTransform = new RotateTransform(0);
            }

            var transform = (RotateTransform)element.RenderTransform;
            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new QuadraticEaseOut();

            var rotateAnim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = RotateTransform.AngleProperty, Value = targetAngle } }
                    }
                }
            };

            _ = rotateAnim.RunAsync(element);
        }

        public static void AnimateBorderColor(Border border, Color targetColor, double durationSeconds = 0.15)
        {
            if (border == null || border.BorderBrush is not SolidColorBrush currentBrush) return;

            var colorAnim = new Animation
            {
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter { Property = Border.BorderBrushProperty, Value = new SolidColorBrush(targetColor) } }
                    }
                }
            };

            _ = colorAnim.RunAsync(border);
        }

        public static void AnimateScale(Control element, double targetScale, double durationSeconds = 0.15)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not ScaleTransform)
            {
                element.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                element.RenderTransform = new ScaleTransform(1, 1);
            }

            var transform = (ScaleTransform)element.RenderTransform;
            var anim = new Animation
            {
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FillMode = FillMode.Forward,
                Easing = new QuadraticEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter { Property = ScaleTransform.ScaleXProperty, Value = targetScale },
                            new Setter { Property = ScaleTransform.ScaleYProperty, Value = targetScale }
                        }
                    }
                }
            };

            _ = anim.RunAsync(element);
        }

        public static void AnimatePageTransition(Control element, double startOffset = 40, double durationSeconds = 0.4)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)element.RenderTransform;

            element.IsVisible = true;
            element.Opacity = 0;
            transform.Y = startOffset;

            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new CircularEaseOut();

            var opacityAnim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 0d } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 1d } } }
                }
            };

            var slideAnim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = TranslateTransform.YProperty, Value = startOffset } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = TranslateTransform.YProperty, Value = 0d } } }
                }
            };

            _ = opacityAnim.RunAsync(element).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() => element.Opacity = 1d);
            });
            _ = slideAnim.RunAsync(element).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() => transform.Y = 0d);
            });
        }

        public static async void AnimatePageTransitionExit(Control element, double endOffset = 40, double durationSeconds = 0.3)
        {
            if (element == null) return;

            if (element.RenderTransform == null || element.RenderTransform is not TranslateTransform)
            {
                element.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)element.RenderTransform;
            var duration = TimeSpan.FromSeconds(durationSeconds);
            var easing = new CircularEaseIn();

            var opacityAnim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 1d } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 0d } } }
                }
            };

            var slideAnim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = TranslateTransform.YProperty, Value = 0d } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = TranslateTransform.YProperty, Value = endOffset } } }
                }
            };

            _ = slideAnim.RunAsync(element);
            await opacityAnim.RunAsync(element);

            element.IsVisible = false;
        }

        public static void FadeIn(Control element, double duration)
        {
            if (element == null) return;

            element.IsVisible = true;

            var fadeIn = new Animation
            {
                Duration = TimeSpan.FromSeconds(duration),
                FillMode = FillMode.Forward,
                Easing = new QuadraticEaseInOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 0d } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 1d } } }
                }
            };

            _ = fadeIn.RunAsync(element);
        }

        public static async void FadeOut(Control element, double duration)
        {
            if (element == null) return;

            var fadeOut = new Animation
            {
                Duration = TimeSpan.FromSeconds(duration),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 1d } } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter { Property = Visual.OpacityProperty, Value = 0d } } }
                }
            };

            await fadeOut.RunAsync(element);
            element.IsVisible = false;
        }

        public static void AnimateBorder(double targetX, double targetY, Control border)
        {
            if (border?.RenderTransform is not TranslateTransform transform) return;

            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new QuarticEaseOut();

            var anim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter { Property = TranslateTransform.XProperty, Value = targetX },
                            new Setter { Property = TranslateTransform.YProperty, Value = targetY }
                        }
                    }
                }
            };

            _ = anim.RunAsync(border);
        }

        public static async void AnimateBorderObject(double targetX, double targetY, Border border, bool visibly)
        {
            if (border == null) return;

            if (border.RenderTransform == null || border.RenderTransform is not TranslateTransform)
                border.RenderTransform = new TranslateTransform();

            if (visibly) border.IsVisible = true;

            var transform = (TranslateTransform)border.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new QuarticEaseOut();

            var anim = new Animation
            {
                Duration = duration,
                FillMode = FillMode.Forward,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter { Property = TranslateTransform.XProperty, Value = targetX },
                            new Setter { Property = TranslateTransform.YProperty, Value = targetY }
                        }
                    }
                }
            };

            await anim.RunAsync(border);

            if (!visibly) border.IsVisible = false;
        }
    }
}
