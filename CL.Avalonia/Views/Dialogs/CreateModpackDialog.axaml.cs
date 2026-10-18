using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class CreateModpackDialog : Window
{
    public CreateModpackDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CreateModpackViewModel vm)
        {
            vm.CloseAction = Close;
            vm.ScrollToTopRequested += ScrollResultsToTop;
            vm.ApplyLocalization();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is CreateModpackViewModel vm)
        {
            vm.Cleanup();
        }
    }

    private void ScrollResultsToTop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (SearchResultsList != null && SearchResultsList.ItemCount > 0)
            {
                SearchResultsList.ScrollIntoView(0);
            }
            var sv = SearchResultsList?.FindDescendantOfType<ScrollViewer>();
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
}
