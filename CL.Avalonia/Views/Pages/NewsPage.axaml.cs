using System;
using Avalonia;
using Avalonia.Controls;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Pages;

public partial class NewsPage : UserControl
{
    public static readonly StyledProperty<Control?> BackgroundVisualProperty =
        AvaloniaProperty.Register<NewsPage, Control?>(nameof(BackgroundVisual));

    public Control? BackgroundVisual
    {
        get => GetValue(BackgroundVisualProperty);
        set => SetValue(BackgroundVisualProperty, value);
    }

    public NewsPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext == null && Application.Current is App app && app.ServiceProvider != null)
        {
            DataContext = app.ServiceProvider.GetService<NewsPageViewModel>();
        }

        if (DataContext is NewsPageViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (DataContext is NewsPageViewModel vm)
        {
            vm.Cleanup();
        }
    }
}
