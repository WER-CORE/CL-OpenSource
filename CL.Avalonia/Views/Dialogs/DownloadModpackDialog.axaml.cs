using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CL_CLegendary_Launcher_.Avalonia.Services;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class DownloadModpackDialog : Window
{
    public DownloadModpackDialog()
    {
        InitializeComponent();
        this.EnableAutoMemoryCleanup();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DownloadModpackDialogViewModel vm)
        {
            vm.CloseAction = Close;
            vm.ScrollToTopRequested += ScrollModpacksToTop;
        }
    }

    private void ScrollModpacksToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ModpacksList != null && ModpacksList.ItemCount > 0)
            {
                ModpacksList.ScrollIntoView(0);
            }
            var sv = ModpacksList?.FindDescendantOfType<ScrollViewer>();
            if (sv != null)
            {
                sv.Offset = new Vector(0, 0);
            }
        }, DispatcherPriority.Loaded);
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void ExitButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
