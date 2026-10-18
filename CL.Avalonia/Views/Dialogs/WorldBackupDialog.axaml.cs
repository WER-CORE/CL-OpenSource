using Avalonia.Controls;
using Avalonia.Input;
using CL_CLegendary_Launcher_.Avalonia.ViewModels.Dialogs;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Dialogs;

public partial class WorldBackupDialog : Window
{
    public WorldBackupDialog()
    {
        InitializeComponent();

        var vm = new WorldBackupViewModel();
        vm.CloseAction = () => this.Close();
        DataContext = vm;
    }

    private void Header_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
}
