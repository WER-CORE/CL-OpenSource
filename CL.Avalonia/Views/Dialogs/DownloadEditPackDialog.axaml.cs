using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class DownloadEditPackDialog : Window
{
    public DownloadEditPackDialog()
    {
        InitializeComponent();
        this.EnableAutoMemoryCleanup();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DownloadEditPackViewModel vm)
        {
            vm.CloseAction = Close;
            vm.ScrollToTopRequested += ScrollModsToTop;
        }
    }

    private void ScrollModsToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ModsListBox != null && ModsListBox.ItemCount > 0)
            {
                ModsListBox.ScrollIntoView(0);
            }
            var sv = ModsListBox?.FindDescendantOfType<ScrollViewer>();
            if (sv != null)
            {
                sv.Offset = new Vector(0, 0);
            }
        }, DispatcherPriority.Loaded);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Header_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
}
