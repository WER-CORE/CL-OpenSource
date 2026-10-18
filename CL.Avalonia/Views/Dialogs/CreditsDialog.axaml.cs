using Avalonia.Controls;
using Avalonia.Input;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class CreditsDialog : Window
{
    public CreditsDialog()
    {
        InitializeComponent();

        var vm = new CreditsDialogViewModel();
        vm.CloseAction = () => this.Close();
        DataContext = vm;
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
